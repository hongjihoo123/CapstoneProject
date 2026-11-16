using System.Collections;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using Assets.Members.HJH._02.Scripts.UI;
using DG.Tweening;
using Members.JJH._02_Scripts.Map;
using Members.KYR._01.Scripts;
using Members.KYR._01.Scripts.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Members.HJH._02.Scripts.Quest
{
    // 적이 있는 방의 포탈은 처음부터 "꺼진 상태"로 서 있음:
    //   - 모양: 중앙의 에너지 소용돌이(Quad)가 꺼지고 미니맵 아이콘도 어둡게
    //   - 동작: 포탈 트리거(F 로 맵 열기)가 멈춤. 가까이 가거나 F 를 누르면 "포탈이 아직 활성화되지 않았습니다" 안내
    // 방을 다 클리어하면(RoomClearTracker.Cleared) 에너지가 솟아오르며 켜지고 트리거가 다시 작동함.
    // 적이 하나도 없는 방(상점·업그레이드 등)은 포탈을 건드리지 않음.
    public class RoomPortalGate : MonoBehaviour
    {
        private const string EnergyName = "Quad";
        private static readonly Color DimIcon = new(0.45f, 0.5f, 0.6f, 0.55f);

        [SerializeField, Min(0f)] private float openDelay = 0.3f;
        [SerializeField, Min(0.05f)] private float popDuration = 0.6f;
        [SerializeField, Min(0.5f)] private float noticeRadius = 6f;
        [SerializeField] private Color burstColor = new(0.45f, 0.9f, 1f, 1f);
        [SerializeField] private string lockedMessage = "포탈이 아직 활성화되지 않았습니다";
        [SerializeField] private string openedMessage = "포탈이 활성화되었습니다";

        private sealed class Portal
        {
            public MapPortalTrigger trigger;
            public Collider collider;
            public readonly List<(Renderer renderer, Vector3 scale)> energy = new();
            public SpriteRenderer mapIcon;
        }

        private RoomClearTracker _tracker;
        private readonly List<Portal> _portals = new();
        private bool _locked;
        private Transform _player;

        private CanvasGroup _noticeGroup;
        private TMP_Text _noticeText;
        private float _noticeUntil;
        private bool _noticeIsOpened;

        public void Bind(RoomClearTracker tracker)
        {
            _tracker = tracker;
            _tracker.Cleared += OnCleared;
            BuildNotice();
            StartCoroutine(LockWhenEnemiesExist());
        }

        private void OnDestroy()
        {
            if (_tracker != null)
                _tracker.Cleared -= OnCleared;
        }

        // 화면 아래쪽 가운데 안내 문구
        private void BuildNotice()
        {
            Transform canvas = transform.parent != null ? transform.parent : transform;
            RectTransform rect = HudUiKit.NewRect("PortalNotice", canvas);
            HudUiKit.Place(rect, new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(700f, 48f));
            _noticeGroup = rect.gameObject.AddComponent<CanvasGroup>();
            _noticeGroup.alpha = 0f;
            _noticeGroup.blocksRaycasts = false;
            _noticeText = HudUiKit.NewText("Text", rect, lockedMessage, 26f, HudUiKit.Title, TextAlignmentOptions.Center);
            HudUiKit.Stretch(_noticeText.rectTransform);
        }

        // 한 프레임 + 잠깐 기다려서 적 수가 잡힌 뒤에 판단
        private IEnumerator LockWhenEnemiesExist()
        {
            yield return null;
            yield return new WaitForSeconds(0.3f);

            if (_tracker == null || _tracker.Total <= 0 || _tracker.IsCleared)
                yield break;

            foreach (MapPortalTrigger trigger in FindObjectsByType<MapPortalTrigger>(FindObjectsSortMode.None))
            {
                var portal = new Portal { trigger = trigger, collider = trigger.GetComponent<Collider>() };
                foreach (Renderer r in trigger.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.gameObject.name == EnergyName)
                    {
                        portal.energy.Add((r, r.transform.localScale));
                        r.enabled = false;
                    }
                }

                MinimapIcon icon = trigger.GetComponentInChildren<MinimapIcon>(true);
                portal.mapIcon = icon != null ? icon.GetComponent<SpriteRenderer>() : null;

                trigger.enabled = false;
                _portals.Add(portal);
            }

            _locked = _portals.Count > 0;

            // 미니맵 아이콘 모양 입히는 부품이 먼저 돈 뒤에 어둡게
            yield return new WaitForSeconds(1f);
            foreach (Portal portal in _portals)
            {
                if (_locked && portal.mapIcon != null)
                    portal.mapIcon.color = DimIcon;
            }
        }

        private void Update()
        {
            bool showing = false;

            if (_locked)
            {
                Transform player = FindPlayer();
                if (player != null && NearAnyPortal(player.position))
                {
                    showing = true;
                    if (_noticeIsOpened)
                    {
                        _noticeIsOpened = false;
                        _noticeText.color = HudUiKit.Title;
                    }

                    int left = _tracker != null ? Mathf.Max(0, _tracker.Total - _tracker.Kills) : 0;
                    _noticeText.text = left > 0 ? $"{lockedMessage}\n<size=20><color=#9FB4C8>남은 적 {left}마리</color></size>" : lockedMessage;
                    _noticeUntil = Time.unscaledTime + 0.15f;

                    // F 를 누르면 안내가 한 번 튐
                    if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                    {
                        _noticeGroup.transform.DOKill(true);
                        _noticeGroup.transform.DOPunchScale(Vector3.one * 0.1f, 0.25f, 6, 0.6f).SetUpdate(true);
                    }
                }
            }

            bool visible = showing || Time.unscaledTime < _noticeUntil;
            float target = visible ? 1f : 0f;
            _noticeGroup.alpha = Mathf.MoveTowards(_noticeGroup.alpha, target, Time.unscaledDeltaTime * (visible ? 6f : 2.5f));
        }

        private Transform FindPlayer()
        {
            if (_player == null)
            {
                PlayerAgent agent = FindFirstObjectByType<PlayerAgent>();
                if (agent != null)
                    _player = agent.transform;
            }
            return _player;
        }

        private bool NearAnyPortal(Vector3 playerPosition)
        {
            foreach (Portal portal in _portals)
            {
                if (portal.trigger == null)
                    continue;

                Vector3 closest = portal.collider != null ? portal.collider.ClosestPoint(playerPosition) : portal.trigger.transform.position;
                if (Vector3.Distance(playerPosition, closest) <= noticeRadius)
                    return true;
            }
            return false;
        }

        private void OnCleared() => StartCoroutine(Unlock());

        private IEnumerator Unlock()
        {
            yield return new WaitForSeconds(openDelay);

            _locked = false;
            foreach (Portal portal in _portals)
            {
                if (portal.trigger == null)
                    continue;

                portal.trigger.enabled = true;
                foreach ((Renderer renderer, Vector3 scale) in portal.energy)
                {
                    if (renderer == null)
                        continue;

                    renderer.transform.localScale = Vector3.zero;
                    renderer.enabled = true;
                    renderer.transform.DOScale(scale, popDuration).SetEase(Ease.OutBack).SetUpdate(true).SetLink(renderer.gameObject);
                }

                if (portal.mapIcon != null)
                    portal.mapIcon.color = Color.white;

                Fx.Shockwave(portal.trigger.transform.position, burstColor, 3.5f);
            }

            // "활성화되었습니다" 안내를 잠깐 금색으로
            _noticeIsOpened = true;
            _noticeText.text = openedMessage;
            _noticeText.color = HudUiKit.Gold;
            _noticeUntil = Time.unscaledTime + 2.2f;

            Debug.Log($"[RoomPortalGate] 포탈 {_portals.Count}개 활성화");
            _portals.Clear();
        }
    }
}
