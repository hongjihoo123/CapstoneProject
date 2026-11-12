
using RobotWeapons;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.DamageProjectiles
{
    public class BossPoisonArea : MonoBehaviour
    {
        [Header("Poison Settings")]
        [SerializeField] private Vector3 boxSize = new Vector3(4f, 1f, 4f);
        [SerializeField] private float damageInterval = 0.5f;
        [SerializeField] private LayerMask targetLayer;

        private GameObject _owner;
        private float _damage;
        private float _nextDamageTime;

        public void Initialize(GameObject owner, float radius, float damage,
                               float duration, float interval)
        {
            _owner = owner;
            _damage = damage;
            damageInterval = interval;

            _nextDamageTime = Time.time;

            Destroy(gameObject, duration);
        }

        private void Update()
        {
            if (Time.time < _nextDamageTime)
                return;

            Collider[] colliders = Physics.OverlapBox(transform.position, boxSize / 2,
                transform.rotation, targetLayer, QueryTriggerInteraction.Collide);

            foreach (Collider other in colliders)
            {
                if (other.transform.IsChildOf(_owner.transform))
                    continue;

                if (!other.TryGetComponent<IDamageable>(out var damageable))
                    damageable = other.GetComponentInParent<IDamageable>();

                if (damageable == null || !damageable.IsAlive)
                    continue;

                damageable.TakeDamage(_damage, _owner);
                _nextDamageTime = Time.time + damageInterval;
                break;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

            Gizmos.DrawWireCube(Vector3.zero, boxSize);
        }
    }
}
