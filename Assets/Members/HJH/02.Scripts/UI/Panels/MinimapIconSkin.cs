using System.Collections.Generic;
using Members.KYR._01.Scripts;
using Members.KYR._01.Scripts.Player;
using Members.JJH._02_Scripts.Map;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.UI
{
    [DefaultExecutionOrder(100)]
    public class MinimapIconSkin : MonoBehaviour, IUiBake
    {
        [SerializeField] private float playerSize = 3.6f;
        [SerializeField] private float enemySize = 2.4f;
        [SerializeField] private float bossSize = 4.2f;
        [SerializeField] private float portalSize = 4f;

        private readonly HashSet<int> _done = new();
        private readonly List<Transform> _players = new();
        [SerializeField, Tooltip("미니맵 아이콘 그림 (Resources/HJH/UI 의 minimap_*)")] private Sprite playerSprite;
        [SerializeField] private Sprite enemySprite;
        [SerializeField] private Sprite portalSprite;
        private float _next;

        [ContextMenu("그림 연결")]
        public void BakeLayout()
        {
#if UNITY_EDITOR
            playerSprite = HudUiKit.Load("minimap_player");
            enemySprite = HudUiKit.Load("minimap_enemy");
            portalSprite = HudUiKit.Load("minimap_portal");
#endif
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
                sprite = playerSprite; size = playerSize; order = 20;
                _players.Add(icon.transform);
            }
            else if (owner != null && owner.GetComponentInParent<MapPortalTrigger>() != null)
            {
                sprite = portalSprite; size = portalSize; order = 10;
            }
            else
            {
                bool boss = owner != null && owner.name.Contains("Boss");
                sprite = enemySprite; size = boss ? bossSize : enemySize; order = 5;
            }

            if (sprite == null)
                return;

            sr.sprite = sprite;
            sr.color = Color.white;
            sr.sortingOrder = order;

            float parentScale = owner != null ? Mathf.Max(0.01f, owner.lossyScale.x) : 1f;
            float spriteWorld = sprite.bounds.size.x;
            float scale = size / Mathf.Max(0.01f, spriteWorld) / parentScale;
            icon.transform.localScale = Vector3.one * scale;
        }
    }
}
