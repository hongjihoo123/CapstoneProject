using UnityEngine;

namespace Members.KYR._01.Scripts
{
    public class MinimapIcon : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer icon;
        [SerializeField] private bool rotateWithTarget;
        [SerializeField] private float height = 3f;
        [SerializeField, Tooltip("아이콘 그림 자체의 방향 보정(도). 화살표가 오른쪽을 향한 그림이면 90 또는 -90")]
        private float angleOffset;

        private void Awake()
        {
            if (icon == null)
                icon = GetComponent<SpriteRenderer>();

            int layer = LayerMask.NameToLayer("MiniMap");
            if (layer >= 0)
                gameObject.layer = layer;
        }

        private void LateUpdate()
        {
            Transform follow = transform.parent;
            if (follow != null)
            {
                Vector3 position = follow.position;
                transform.position = new Vector3(position.x, position.y + height, position.z);
            }

            if (rotateWithTarget && follow != null)
            {
                Vector3 forward = follow.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(Vector3.up, forward) * Quaternion.Euler(0f, 0f, angleOffset);
                    return;
                }
            }

            transform.rotation = Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(0f, 0f, angleOffset);
        }
    }
}
