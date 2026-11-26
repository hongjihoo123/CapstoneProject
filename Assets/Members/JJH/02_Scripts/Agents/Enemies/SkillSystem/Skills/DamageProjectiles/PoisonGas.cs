using Members.JJH._02_Scripts.Systems.ObjectPoolSystem.Runtime;
using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.DamageProjectiles
{
    public class PoisonGas : PoolableMono
    {
        [Header("Poison Settings")]
        [SerializeField] private Vector3 boxSize = new Vector3(4f, 1f, 4f);
        [SerializeField] private LayerMask targetLayer;

        private PoolInitializer _poolInitializer;
        private GameObject _owner;
        private float _damage;
        private float _damageInterval;
        private Coroutine _lifeCoroutine;

        public void Initialize(GameObject owner, float radius, float damage,
                               float duration, float interval)
        {
            _owner = owner;
            _damage = damage;
            _damageInterval = interval;

            boxSize = new Vector3(radius * 2f, boxSize.y, radius * 2f);

            if (_poolInitializer == null)
                _poolInitializer = FindFirstObjectByType<PoolInitializer>();

            _lifeCoroutine = StartCoroutine(LifeCoroutine(duration));
        }

        public override void ResetItem()
        {
            if (_lifeCoroutine != null)
            {
                StopCoroutine(_lifeCoroutine);
                _lifeCoroutine = null;
            }

            _owner = null;
            _damage = 0f;
            _damageInterval = 0f;
            _nextDamageTime = 0f;
        }

        private void Update()
        {
            if (_owner == null)
                return;

            Collider[] colliders = Physics.OverlapBox(
                transform.position,
                boxSize / 2f,
                transform.rotation,
                targetLayer,
                QueryTriggerInteraction.Collide);

            if (colliders.Length == 0)
                return;

            if (_nextDamageTime > Time.time)
                return;

            foreach (Collider other in colliders)
            {
                if (other.transform.IsChildOf(_owner.transform))
                    continue;

                if (!other.TryGetComponent<IDamageable>(out var damageable))
                    damageable = other.GetComponentInParent<IDamageable>();

                if (damageable == null || !damageable.IsAlive)
                    continue;

                damageable.TakeDamage(_damage, _owner);
            }

            _nextDamageTime = Time.time + _damageInterval;
        }

        private float _nextDamageTime;

        private IEnumerator LifeCoroutine(float duration)
        {
            yield return new WaitForSeconds(duration);

            _poolInitializer.Push(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, boxSize);
        }
    }
}