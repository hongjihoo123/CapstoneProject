using RobotWeapons;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.DamageProjectiles
{
    [RequireComponent(typeof(Rigidbody))]
    public class MovingRootProjectile : MonoBehaviour
    {
        [SerializeField] private float lifeTime = 5f;

        private Rigidbody _rigidbody;
        private GameObject _owner;
        private Vector3 _direction;
        private float _speed;
        private float _maxSpeed;
        private float _acceleration;
        private float _damage;
        private bool _initialized;
        private bool _hasHit;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.useGravity = false;
        }

        private void FixedUpdate()
        {
            if (!_initialized)
                return;

            _speed = Mathf.MoveTowards(_speed, _maxSpeed, _acceleration * Time.fixedDeltaTime);
            _rigidbody.linearVelocity = _direction * _speed;
        }

        public void Initialize(Vector3 direction, float speed, float maxSpeed, float acceleration,
            float damage, GameObject owner)
        {
            _owner = owner;
            _direction = direction.normalized;
            _speed = speed;
            _maxSpeed = maxSpeed;
            _acceleration = acceleration;
            _damage = damage;
            _initialized = true;

            _rigidbody.linearVelocity = _direction * _speed;

            Destroy(gameObject, lifeTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            Hit(other);
        }

        private void Hit(Collider other)
        {
            if (!_initialized || _hasHit || other.transform.IsChildOf(_owner.transform))
                return;

            if (!other.TryGetComponent<IDamageable>(out var damageable))
                damageable = other.GetComponentInParent<IDamageable>();

            if (damageable == null || !damageable.IsAlive)
                return;

            _hasHit = true;
            damageable.TakeDamage(_damage, _owner);
            Destroy(gameObject);
        }
    }
}