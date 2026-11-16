using System.Collections.Generic;
using Members.KYR._01.Scripts;
using Members.KYR._01.Scripts.Player;
using Members.JJH._02_Scripts.Map;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 미니맵에 뜨는 아이콘(MinimapIcon)의 모양을 새 디자인으로 바꿈. 다른 사람 프리팹은 건드리지 않고 실행 중에 입힌다.
    //  플레이어: 바라보는 방향을 가리키는 청록 화살표 / 적: 붉은 점(보스는 크게) / 포탈: 금색 고리
    // 0.5초마다 새로 생긴 아이콘을 찾아서 한 번만 바꿈. 플레이어 방향은 매 프레임 따라감.
    [DefaultExecutionOrder(100)]
    public class MinimapIconSkin : MonoBehaviour
    {
        [SerializeField] private float playerSize = 3.6f;
        [SerializeField] private float enemySize = 2.4f;
        [SerializeField] private float bossSize = 4.2f;
        [SerializeField] private float portalSize = 4f;

        private readonly HashSet<int> _done = new();
        private readonly List<Transform> _players = new();
        private Sprite _player, _enemy, _portal;
        private float _next;

        private void Awake()
        {
            _player = HudUiKit.Load("minimap_player");
            _enemy = HudUiKit.Load("minimap_enemy");
            _portal = HudUiKit.Load("minimap_portal");
        }

        private void Update()
        {
            if (Time.unscaledTime < _next)
                return;

            _next = Time.unscaledTime + 0.5f;
            foreach (MinimapIcon icon in FindObjectsByType<MinimapIcon>(FindObjectsSortMode.None))
            {
                if (_done.Add(icon.GetInstanceID()))
                    Skin(icon);
            }
        }

        // 플레이어 화살표가 바라보는 쪽으로 (MinimapIcon 이 먼저 돌려놓은 걸 덮어씀)
        private void LateUpdate()
        {
            for (int i = _players.Count - 1; i >= 0; i--)
            {
                Transform icon = _players[i];
                if (icon == null)
                {
                    _players.RemoveAt(i);
                    continue;
                }

                Transform owner = icon.parent;
                if (owner == null)
                    continue;

                Vector3 forward = owner.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                    icon.rotation = Quaternion.LookRotation(Vector3.up, forward);
            }
        }

        private void Skin(MinimapIcon icon)
        {
            if (!icon.TryGetComponent(out SpriteRenderer sr))
                return;

            Transform owner = icon.transform.parent;
            Sprite sprite;
            float size;
            int order;

            if (owner != null && owner.GetComponentInParent<PlayerAgent>() != null)
            {
                sprite = _player; size = playerSize; order = 20;
                _players.Add(icon.transform);
            }
            else if (owner != null && owner.GetComponentInParent<MapPortalTrigger>() != null)
            {
                sprite = _portal; size = portalSize; order = 10;
            }
            else
            {
                bool boss = owner != null && owner.name.Contains("Boss");
                sprite = _enemy; size = boss ? bossSize : enemySize; order = 5;
            }

            if (sprite == null)
                return;

            sr.sprite = sprite;
            sr.color = Color.white;
            sr.sortingOrder = order;

            // 부모 크기에 끌려가지 않게 월드 기준 크기로 맞춤
            float parentScale = owner != null ? Mathf.Max(0.01f, owner.lossyScale.x) : 1f;
            float spriteWorld = sprite.bounds.size.x;
            float scale = size / Mathf.Max(0.01f, spriteWorld) / parentScale;
            icon.transform.localScale = Vector3.one * scale;
        }
    }
}
