using Members.KYR._01_Scripts.Spawner;
using UnityEngine;

namespace Members.KYR._01.Scripts.Spawner
{
    [DisallowMultipleComponent]
    public class SpawnPoint : MonoBehaviour
    {
        [SerializeField]
        private SpawnPointId id = SpawnPointId.None;

        [SerializeField, Min(0f)]
        [Tooltip("포인트 가중치")]
        private float weight = 1f;

        public SpawnPointId Id => id;
        public float Weight => weight;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Vector3 position = transform.position;
            Gizmos.DrawWireSphere(position, 0.35f);
            Gizmos.DrawLine(position, position + Vector3.up * 1.2f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(position + Vector3.up * 1.4f, id == SpawnPointId.None ? "Pool" : id.ToString());
#endif
        }
    }
}
