using UnityEngine;
namespace RobotWeapons
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float lifeTime = 5f;
        [SerializeField] private GameObject defaultHitEffectPrefab;

        [SerializeField] private LayerMask hitMask = ~0;

        private float damage;
        private float speed;
        private IWeaponOwner shooter;
        private Vector3 _previousPosition;
        private bool _destroyed;

        private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        public void Init(float dmg, float projectileSpeed, IWeaponOwner owner)
        {
            damage = dmg;
            speed = projectileSpeed;
            shooter = owner;
            _previousPosition = transform.position;
            Destroy(gameObject, lifeTime);
        }

        private void Update()
        {
            if (_destroyed) return;

            Vector3 nextPosition = transform.position + transform.forward * speed * Time.deltaTime;
            Vector3 delta = nextPosition - _previousPosition;
            float distance = delta.magnitude;

            if (distance > 0f && TryFindClosestHit(delta.normalized, distance, out RaycastHit hit))
            {
                HandleHit(hit.collider, hit.point, hit.normal);
                return;
            }

            transform.position = nextPosition;
            _previousPosition = nextPosition;
        }
        
        private bool TryFindClosestHit(Vector3 direction, float distance, out RaycastHit result)
        {
            int count = Physics.RaycastNonAlloc(_previousPosition, direction, HitBuffer, distance, hitMask, QueryTriggerInteraction.Collide);
            if (count == 0)
            {
                result = default;
                return false;
            }

            if (count > 1)
                System.Array.Sort(HitBuffer, 0, count, DistanceComparer);

            result = HitBuffer[0];
            return true;
        }

        private static readonly System.Collections.Generic.IComparer<RaycastHit> DistanceComparer =
            System.Collections.Generic.Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        private void HandleHit(Collider hitCollider, Vector3 point, Vector3 normal)
        {
            _destroyed = true;

            var target = hitCollider.GetComponentInParent<IDamageable>();
            bool isWeakpoint = hitCollider.gameObject.layer == LayerMask.NameToLayer("Weakpoint");

            SpawnHitEffect(hitCollider, point, normal);
            shooter?.ApplyDamageTo(target, damage, isWeakpoint);

            Destroy(gameObject);
        }

        private void SpawnHitEffect(Collider hitCollider, Vector3 point, Vector3 normal)
        {
            GameObject prefab = hitCollider.GetComponentInParent<IHitEffectSource>()?.HitEffectPrefab;
            if (prefab == null) prefab = defaultHitEffectPrefab;
            if (prefab == null) return;

            if (normal.sqrMagnitude < 0.001f) normal = -transform.forward;
            GameObject.Instantiate(prefab, point, Quaternion.LookRotation(normal));
        }
    }
}