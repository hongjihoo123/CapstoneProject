using System.Collections.Generic;
using System.Linq;
using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.KYR._01_Scripts;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Members.JJH._02_Scripts.Augments
{
    public class AugmentManager : MonoBehaviour
    {
        [Header("증강 목록 (트랙 단위)")]
        [SerializeField] private List<AugmentTrackSO> allTracks = new List<AugmentTrackSO>();

        [Header("이벤트 채널")]
        [Tooltip("증강 선택 요청 / 증강 적용 / 증강 초기화 이벤트를 주고받는 채널")]
        [SerializeField] private EventChannelSO augmentEventChannel;

        [Header("UI")]
        [Tooltip("증강 선택 UI 전체를 켜고 끌 루트(선택 사항). 비워두면 항상 켜져 있다고 가정.")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private AugmentUI augmentUIPrefab;
        [SerializeField] private RectTransform augmentParent;
        [SerializeField] private RectTransform[] slotAnchors = new RectTransform[3];

        [Header("연출 설정")]
        [SerializeField] private float appearDuration = 0.5f;
        [SerializeField] private float appearStagger = 0.1f;
        [SerializeField] private float selectMoveDuration = 0.4f;
        [SerializeField] private float selectedScale = 1.4f;
        [SerializeField] private float selectedFadeDuration = 0.3f;
        [SerializeField] private float dismissDuration = 0.4f;
        [SerializeField] private float screenOffsetY = 1200f;

        [Header("테스트")]
        [Tooltip("테스트용: 라운드 종료 없이도 이 키를 누르면 로컬 플레이어에게 증강 선택 이벤트가 발생한다.")]
        [SerializeField] private bool enableTestKey = true;
        [SerializeField] private Key testKey = Key.L;
        [Tooltip("싱글 테스트용으로 특정 플레이어를 강제 지정하고 싶으면 여기에 드래그 (비워두면 NGO Owner로 로컬 플레이어를 자동으로 찾음)")]
        [SerializeField] private PlayerAgent overridePlayer;

        // player -> (trackId -> 현재 단계). 플레이어별로 독립적으로 진행됨.
        private readonly Dictionary<PlayerAgent, Dictionary<string, int>> _tiersByPlayer = new();
        private readonly List<AugmentUI> _currentAugmentUIs = new();
        private PlayerAgent _activePlayer;

        private void Awake()
        {
            if (rootPanel != null)
                rootPanel.SetActive(false);
        }

        private void OnEnable()
        {
            if (augmentEventChannel != null)
            {
                augmentEventChannel.AddListener<AugmentSelectionRequestedEvent>(HandleSelectionRequested);
                augmentEventChannel.AddListener<AugmentsResetEvent>(HandleResetRequested);
            }
        }

        private void OnDisable()
        {
            if (augmentEventChannel != null)
            {
                augmentEventChannel.RemoveListener<AugmentSelectionRequestedEvent>(HandleSelectionRequested);
                augmentEventChannel.RemoveListener<AugmentsResetEvent>(HandleResetRequested);
            }
        }

        private void Update()
        {
            if (!enableTestKey) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[testKey].wasPressedThisFrame)
                RequestAugmentSelection(ResolvePlayer(null));
        }

        // 라운드 매니저 등 다른 시스템에서 "이 플레이어" 라운드가 끝났을 때 호출.
        // player를 안 넘기면(null) 로컬 플레이어 기준으로 처리됨.
        public void RequestAugmentSelection(PlayerAgent player = null)
        {
            if (augmentEventChannel != null)
                augmentEventChannel.RaiseEvent(new AugmentSelectionRequestedEvent(player));
            else
                ShowRandomAugments(ResolvePlayer(player));
        }

        // 기지로 복귀했을 때 호출 - 그 플레이어가 이번 탐사에서 고른 증강을 전부 초기화.
        public void ResetPlayer(PlayerAgent player)
        {
            player = ResolvePlayer(player);
            if (player == null) return;

            _tiersByPlayer.Remove(player);

            if (augmentEventChannel != null)
                augmentEventChannel.RaiseEvent(new AugmentsResetEvent(player));
        }

        public void ResetAllPlayers()
        {
            List<PlayerAgent> players = _tiersByPlayer.Keys.ToList();
            foreach (PlayerAgent player in players)
                ResetPlayer(player);
        }

        private void HandleSelectionRequested(AugmentSelectionRequestedEvent evt)
        {
            ShowRandomAugments(ResolvePlayer(evt.Player));
        }

        private void HandleResetRequested(AugmentsResetEvent evt)
        {
            // 다른 시스템(스탯 등)이 이 이벤트를 구독해서 자기 쪽 정리를 하면 됨.
            // AugmentManager 쪽 단계 기록은 ResetPlayer에서 이미 지웠으므로 여기선 할 일 없음.
        }

        public int GetTier(PlayerAgent player, string trackId)
        {
            if (player == null) return 0;
            if (!_tiersByPlayer.TryGetValue(player, out var tiers)) return 0;
            return tiers.TryGetValue(trackId, out int t) ? t : 0;
        }

        // NGO 기준 "내 화면에 붙은 플레이어"를 찾는다. player가 이미 지정돼 있으면 그대로 씀.
        private PlayerAgent ResolvePlayer(PlayerAgent player)
        {
            if (player != null) return player;
            if (overridePlayer != null) return overridePlayer;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
            {
                NetworkObject localObject = NetworkManager.Singleton.LocalClient.PlayerObject;
                if (localObject != null)
                {
                    PlayerAgent found = localObject.GetComponent<PlayerAgent>();
                    if (found != null) return found;
                }
            }

            // 네트워크가 아직 안 붙은 싱글 테스트 상황 등의 최후 수단.
            return FindAnyObjectByType<PlayerAgent>();
        }

        private void ShowRandomAugments(PlayerAgent player)
        {
            if (player == null)
            {
                Debug.LogWarning("[AugmentManager] 증강을 적용할 플레이어를 찾지 못했습니다.");
                return;
            }

            _activePlayer = player;
            ClearCurrentAugments();

            if (!_tiersByPlayer.ContainsKey(player))
                _tiersByPlayer[player] = new Dictionary<string, int>();

            List<(AugmentTrackSO track, int nextTier)> picked = PickRandomAvailableTracks(player, 3);

            if (picked.Count == 0)
            {
                Debug.Log("[AugmentManager] 선택 가능한 증강이 없습니다 (모든 트랙이 최대 단계).");
                return;
            }

            if (rootPanel != null)
                rootPanel.SetActive(true);
            UiFocusService.Acquire(this);

            for (int i = 0; i < picked.Count; i++)
            {
                AugmentUI ui = Instantiate(augmentUIPrefab, augmentParent);
                ui.Setup(picked[i].track, picked[i].nextTier, OnAugmentClicked);

                RectTransform targetRect = slotAnchors[i];
                Vector2 targetPos = targetRect.anchoredPosition;

                Vector2 startPos = targetPos + Vector2.down * screenOffsetY;
                ui.RectTransform.anchoredPosition = startPos;
                ui.RectTransform.localScale = Vector3.one;

                ui.PlayAppear(targetPos, appearDuration, i * appearStagger);

                _currentAugmentUIs.Add(ui);
            }
        }

        // 아직 최대 단계에 도달하지 않은 트랙들 중에서 최대 count개를 무작위로 고른다.
        // 트랙마다 "다음에 먹을 단계(nextTier)"는 현재 단계 + 1로 고정 -> 1->2->3 순서 보장.
        private List<(AugmentTrackSO track, int nextTier)> PickRandomAvailableTracks(PlayerAgent player, int count)
        {
            var available = new List<(AugmentTrackSO, int)>();
            foreach (AugmentTrackSO track in allTracks)
            {
                if (track == null) continue;
                int current = GetTier(player, track.trackId);
                if (current < track.MaxTier)
                    available.Add((track, current + 1));
            }

            return available
                .OrderBy(_ => Random.value)
                .Take(Mathf.Min(count, available.Count))
                .ToList();
        }

        private void OnAugmentClicked(AugmentUI clicked)
        {
            foreach (AugmentUI ui in _currentAugmentUIs)
                ui.SetInteractable(false);

            Vector2 centerPos = Vector2.zero;

            foreach (AugmentUI ui in _currentAugmentUIs)
            {
                if (ui == clicked)
                {
                    AugmentTrackSO track = clicked.Track;
                    int chosenTier = clicked.NextTier;

                    ui.PlaySelected(centerPos, selectMoveDuration, selectedScale, selectedFadeDuration, () =>
                    {
                        ApplyChoice(track, chosenTier);
                    });
                }
                else
                {
                    Vector2 currentPos = ui.RectTransform.anchoredPosition;
                    Vector2 offScreenPos = currentPos + Vector2.down * screenOffsetY;
                    ui.PlayDismiss(offScreenPos, dismissDuration);
                }
            }

            _currentAugmentUIs.Clear();
        }

        private void ApplyChoice(AugmentTrackSO track, int newTier)
        {
            PlayerAgent player = _activePlayer;
            if (player == null) return;

            _tiersByPlayer[player][track.trackId] = newTier;

            if (rootPanel != null)
                rootPanel.SetActive(false);
            UiFocusService.Release(this);

            if (augmentEventChannel != null)
                augmentEventChannel.RaiseEvent(new AugmentAppliedEvent(player, track, newTier));

            Debug.Log($"[AugmentManager] {player.name} : {track.trackName} -> {newTier}단계 적용");
        }

        private void ClearCurrentAugments()
        {
            foreach (AugmentUI ui in _currentAugmentUIs)
            {
                if (ui != null) Destroy(ui.gameObject);
            }
            _currentAugmentUIs.Clear();
        }
    }
}
