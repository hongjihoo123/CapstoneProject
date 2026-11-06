using UnityEngine;

namespace Members.KYR._01.Scripts
{
    public class MinimapIcon : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer icon;
        [SerializeField] private bool rotateWithTarget;
        [SerializeField] private float height = 3f;

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
                    transform.rotation = Quaternion.LookRotation(Vector3.up, forward);
                    return;
                }
            }

            transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        }
    }
}
