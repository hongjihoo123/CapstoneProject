using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.DamageProjectiles
{
    [RequireComponent(typeof(Rigidbody))]
    public class BossPoisonProjectile : MonoBehaviour
    {
        [SerializeField] private BossPoisonProjectile projectilePrefab;
        [SerializeField] private float lifeTime = 5f;

        private Rigidbody _rigidbody;
        private BossPoisonArea _areaPrefab;

        private GameObject _owner;

        private float _damage;
        private float _areaRadius;
        private float _areaDuration;
        private float _damageInterval;

        private int _splashCount;
        private float _splashSpeed;

        private bool _initialized;
        private bool _hasHit;

        private bool _isArc;
        private Vector3 _arcStart;
        private Vector3 _arcTarget;
        private float _arcHeight;
        private float _arcDuration;
        private float _arcElapsed;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        public void InitializeArc(Vector3 target, float flightTime, float arcHeight, GameObject owner,
                                                BossPoisonArea areaPrefab, float damage, float areaRadius, float areaDuration,
                                                float damageInterval, int splashCount, float splashSpeed)
        {
            SetCommon(owner, areaPrefab, damage, areaRadius, areaDuration,
                damageInterval, splashCount, splashSpeed);

            _isArc = true;
            _arcStart = transform.position;
            _arcTarget = target;
            _arcHeight = arcHeight;

            _arcDuration = Mathf.Max(0.01f, flightTime);
            _arcElapsed = 0f;

            _rigidbody.isKinematic = true;
            _rigidbody.linearVelocity = Vector3.zero;
        }


        public void Initialize(Vector3 direction, float speed, GameObject owner, BossPoisonArea areaPrefab,
            float damage, float areaRadius, float areaDuration, float damageInterval, int splashCount, float splashSpeed)
        {
            SetCommon(owner, areaPrefab, damage, areaRadius, areaDuration,
                damageInterval, splashCount, splashSpeed);

            _isArc = false;
            _rigidbody.linearVelocity = direction.normalized * speed;
        }

        private void SetCommon(GameObject owner, BossPoisonArea areaPrefab, float damage,
            float areaRadius, float areaDuration, float damageInterval, int splashCount, float splashSpeed)
        {
            _owner = owner;
            _areaPrefab = areaPrefab;
            _damage = damage;
            _areaRadius = areaRadius;
            _areaDuration = areaDuration;
            _damageInterval = damageInterval;
            _splashCount = splashCount;
            _splashSpeed = splashSpeed;

            _initialized = true;
            _hasHit = false;

            Destroy(gameObject, lifeTime);
        }

        private void Update()
        {
            if (!_initialized || !_isArc || _hasHit)
                return;

            _arcElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_arcElapsed / _arcDuration);

            Vector3 pos = Vector3.Lerp(_arcStart, _arcTarget, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * _arcHeight;

            transform.position = pos;

            if (t >= 1f)
                Hit(_arcTarget);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isArc || !_initialized || _hasHit ||
                other.transform.IsChildOf(_owner.transform))
                return;

            Hit(other.ClosestPoint(transform.position));
        }

        private void Hit(Vector3 position)
        {
            if (_hasHit)
                return;

            _hasHit = true;

            SpawnArea(position, _areaRadius);

            if (_splashCount > 0)
                SpawnSplashes(position);

            Destroy(gameObject);
        }

        private void SpawnArea(Vector3 position, float radius)
        {
            if (_areaPrefab == null)
                return;

            BossPoisonArea area = Instantiate(_areaPrefab, position, Quaternion.identity);
            area.Initialize(_owner, radius, _damage, _areaDuration, _damageInterval);
        }

        private void SpawnSplashes(Vector3 position)
        {
            if (projectilePrefab == null || _splashCount <= 0)
                return;

            for (int i = 0; i < _splashCount; i++)
            {
                float angle = 360f / _splashCount * i;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

                BossPoisonProjectile projectile = Instantiate(projectilePrefab,
                    position + Vector3.up * 0.3f, Quaternion.identity);

                projectile.Initialize(direction, _splashSpeed, _owner, _areaPrefab,
                    _damage, _areaRadius * 0.65f, _areaDuration, _damageInterval, 0, 0f);
            }
        }
    }
}