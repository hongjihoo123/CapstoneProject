using RobotWeapons;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.DamageProjectiles
{
    [RequireComponent(typeof(Rigidbody))]
    public class BossDamageProjectile : MonoBehaviour
    {
        [SerializeField] private float lifeTime = 5f;

        private Rigidbody _rigidbody;
        private GameObject _owner;
        private float _damage;
        private bool _initialized;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        public void Initialize(Vector3 direction, float speed, float damage, GameObject owner)
        {
            _owner = owner;
            _damage = damage;
            _initialized = true;

            _rigidbody.linearVelocity = direction.normalized * speed;

            Destroy(gameObject, lifeTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_initialized || other.transform.IsChildOf(_owner.transform))
                return;

            if (!other.TryGetComponent<IDamageable>(out var damageable))
                damageable = other.GetComponentInParent<IDamageable>();

            if (damageable != null && damageable.IsAlive)
                damageable.TakeDamage(_damage, _owner);

            Destroy(gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_initialized || collision.transform.IsChildOf(_owner.transform))
                return;

            if (!collision.transform.TryGetComponent<IDamageable>(out var damageable))
                damageable = collision.transform.GetComponentInParent<IDamageable>();

            if (damageable != null && damageable.IsAlive)
                damageable.TakeDamage(_damage, _owner);

            Destroy(gameObject);
        }
    }
}