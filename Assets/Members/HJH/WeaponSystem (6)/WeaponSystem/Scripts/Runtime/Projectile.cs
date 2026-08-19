using UnityEngine;

namespace RobotWeapons
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float lifeTime = 5f;
        [SerializeField] private GameObject defaultHitEffectPrefab;

        private float damage;
        private float speed;
        private IWeaponOwner shooter;

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
            Destroy(gameObject, lifeTime);
        }

        private void Update()
        {
            transform.position += transform.forward * speed * Time.deltaTime;
        }

        private void OnTriggerEnter(Collider other)
        {
            var target = other.GetComponentInParent<IDamageable>();
            if (target == null) return;

            bool isWeakpoint = other.gameObject.layer == LayerMask.NameToLayer("Weakpoint");

            SpawnHitEffect(other);
            shooter?.ApplyDamageTo(target, damage, isWeakpoint);
            Destroy(gameObject);
        }

        private void SpawnHitEffect(Collider hitCollider)
        {
            GameObject prefab = hitCollider.GetComponentInParent<IHitEffectSource>()?.HitEffectPrefab;
            if (prefab == null) prefab = defaultHitEffectPrefab;
            if (prefab == null) return;

            Vector3 point = hitCollider.ClosestPoint(transform.position);
            Vector3 normal = (transform.position - point).normalized;
            if (normal.sqrMagnitude < 0.001f) normal = -transform.forward;

            GameObject.Instantiate(prefab, point, Quaternion.LookRotation(normal));
        }
    }
}
