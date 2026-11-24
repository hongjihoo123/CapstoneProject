using RobotWeapons;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies
{
    public class MeleeEnemy : AbstractEnemy
    {
        [Header("Normal Attack")]
        [SerializeField] private float attackDamage = 10f;
        [SerializeField] private LayerMask targetLayer;
        [Tooltip("사각형 크기 (x: 가로, y: 높이, z: 앞쪽 길이)")]
        [SerializeField] private Vector3 attackBoxSize = new Vector3(2f, 2f, 2.5f);

        public override void NormalAttack()
        {
            GetAttackBox(out Vector3 center, out Quaternion rotation);

            Collider[] hits = Physics.OverlapBox(center, attackBoxSize * 0.5f, rotation, targetLayer);

            foreach (Collider hit in hits)
            {
                if (hit.transform.IsChildOf(transform))
                    continue;

                if (hit.TryGetComponent<IDamageable>(out var damageable) ||
                    hit.GetComponentInParent<IDamageable>() is IDamageable parentDamageable &&
                    (damageable = parentDamageable) != null)
                {
                    if (damageable.IsAlive)
                    {
                        damageable.TakeDamage(attackDamage, gameObject);
                        return;
                    }
                }
            }

            Debug.LogWarning("[NormalAttack] 공격 범위 안에서 플레이어를 찾지 못했습니다.");
        }

        private void GetAttackBox(out Vector3 center, out Quaternion rotation)
        {
            rotation = transform.rotation;
            center = transform.position + transform.forward * (attackBoxSize.z * 0.5f)
                                + Vector3.up * (attackBoxSize.y * 0.5f);
        }

#if UNITY_EDITOR

        private void OnDrawGizmosSelected()
        {
            GetAttackBox(out Vector3 center, out Quaternion rotation);

            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.9f);

            Matrix4x4 matrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);

            Gizmos.DrawWireCube(Vector3.zero, attackBoxSize);

            Gizmos.matrix = matrix;
        }

#endif
    }
}
