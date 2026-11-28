using UnityEngine;

namespace HJH.Fx
{
    public class EnemyFxVariant : MonoBehaviour
    {
        [SerializeField] private GameObject[] variants;
        [SerializeField] private float scale = 1f;
        [SerializeField] private LayerMask groundLayers = 1;

        public void Setup(GameObject[] list, float size, LayerMask ground)
        {
            variants = list;
            scale = size;
            groundLayers = ground;
        }

        private void Start()
        {
            if (variants == null || variants.Length == 0) return;

            GameObject visual = Instantiate(variants[Random.Range(0, variants.Length)], transform);
            visual.transform.localScale *= scale;

            Vector3 position = transform.position;
            if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 20f, groundLayers,
                    QueryTriggerInteraction.Ignore))
                position.y = hit.point.y;
            visual.transform.position = position;
        }
    }
}
