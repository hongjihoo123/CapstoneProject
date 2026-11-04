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

            IDamageable closest = null;
            float closestSqr = float.MaxValue;

            foreach (Collider hit in hits)
            {
                if (hit.transform.IsChildOf(transform))
                    continue;

                if (!hit.TryGetComponent<IDamageable>(out var damageable))
                    damageable = hit.GetComponentInParent<IDamageable>();

                if (damageable == null || !damageable.IsAlive)
                    continue;

                float sqr = (hit.transform.position - transform.position).sqrMagnitude;
                if (sqr < closestSqr)
                {
                    closestSqr = sqr;
                    closest = damageable;
                }
            }

            closest?.TakeDamage(attackDamage, gameObject);
        }

        private void GetAttackBox(out Vector3 center, out Quaternion rotation)
        {
            rotation = transform.rotation;
            center = transform.position + transform.forward * (attackBoxSize.z * 0.5f)
                                + Vector3.up * (attackBoxSize.y * 0.5f);
        }
    }
}
