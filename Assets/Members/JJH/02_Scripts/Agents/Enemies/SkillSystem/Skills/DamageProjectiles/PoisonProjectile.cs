using Members.JJH._02_Scripts.Systems.ObjectPoolSystem.Runtime;
using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Effects
{
    [RequireComponent(typeof(Rigidbody))]
    public class PoisonProjectile : PoolableMono
    {
        [Header("Projectile Settings")]
        [SerializeField] private float speed = 15f;
        [SerializeField] private float lifeTime = 5f;

        private Rigidbody _rigidbody;
        private PoolInitializer _poolInitializer;
        private GameObject _owner;

        private Coroutine _lifeCoroutine;
        private int _targetLayer;
        private int _obstacleLayer;
        private float _damage;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        public override void ResetItem()
        {
            if (_lifeCoroutine != null)
            {
                StopCoroutine(_lifeCoroutine);
                _lifeCoroutine = null;
            }

            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            _owner = null;
            _damage = 0f;
        }

        public void Init(Vector3 position, Vector3 direction, float damage, GameObject owner,
                                        LayerMask targetLayer, LayerMask obstacleLayer)
        {
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));

            _owner = owner;
            _damage = damage;
            _targetLayer = Mathf.RoundToInt(Mathf.Log(targetLayer.value, 2));
            _obstacleLayer = Mathf.RoundToInt(Mathf.Log(obstacleLayer.value, 2));

            _rigidbody.linearVelocity = direction.normalized * speed;

            if (_poolInitializer == null)
                _poolInitializer = FindFirstObjectByType<PoolInitializer>();

            _lifeCoroutine = StartCoroutine(LifeCoroutine());

            _rigidbody.WakeUp();
        }

        private void OnTriggerEnter(Collider collision)
        {
            if (collision.gameObject.layer == _obstacleLayer)
            {
                ReturnToPool();
                return;
            }

            if (collision.gameObject.layer != _targetLayer)
                return;

            if (!collision.TryGetComponent<IDamageable>(out IDamageable damageable))
                damageable = collision.GetComponentInParent<IDamageable>();

            if (damageable != null && damageable.IsAlive)
                damageable.TakeDamage(_damage, _owner);

            ReturnToPool();
        }

        private IEnumerator LifeCoroutine()
        {
            yield return new WaitForSeconds(lifeTime);
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (_poolInitializer == null)
                return;

            _poolInitializer.Push(this);
        }
    }
}