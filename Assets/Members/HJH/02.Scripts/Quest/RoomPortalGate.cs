using System.Collections;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using Assets.Members.HJH._02.Scripts.UI;
using DG.Tweening;
using Members.JJH._02_Scripts.Map;
using Members.KYR._01.Scripts;
using Members.KYR._01.Scripts.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Members.HJH._02.Scripts.Quest
{
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

        [SerializeField, Tooltip("포탈 안내 프리팹 (05.Data/Prefab/UI/PortalNotice). 모양은 프리팹에서 수정")]
        private ScreenNotice noticePrefab;
        private ScreenNotice _notice;

        public void Bind(RoomClearTracker tracker)
        {
            _tracker = tracker;
            _tracker.Cleared += OnCleared;
            if (noticePrefab != null)
                _notice = Instantiate(noticePrefab);
            StartCoroutine(LockWhenEnemiesExist());
        }

        private void OnDestroy()
        {
            if (_tracker != null)
                _tracker.Cleared -= OnCleared;
        }

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

            yield return new WaitForSeconds(1f);
            foreach (Portal portal in _portals)
            {
                if (_locked && portal.mapIcon != null)
                    portal.mapIcon.color = DimIcon;
            }
        }

        private void Update()
        {
            if (!_locked || _notice == null)
                return;

            Transform player = FindPlayer();
            if (player == null || !NearAnyPortal(player.position))
                return;

            int left = _tracker != null ? Mathf.Max(0, _tracker.Total - _tracker.Kills) : 0;
            _notice.Hold(left > 0 ? $"{lockedMessage}\n<size=20><color=#9FB4C8>남은 적 {left}마리</color></size>" : lockedMessage, HudUiKit.Title);

            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                _notice.Punch();
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

            if (_notice != null)
                _notice.ShowText(openedMessage, HudUiKit.Gold, 2.2f);

            Debug.Log($"[RoomPortalGate] 포탈 {_portals.Count}개 활성화");
            _portals.Clear();
        }
    }
}
