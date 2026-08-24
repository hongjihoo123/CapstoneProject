using UnityEngine;

namespace RobotWeapons
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class ArrowProjectile : MonoBehaviour
    {
        [SerializeField] private float flightLifeTime = 8f;

        private float damage;
        private IWeaponOwner shooter;
        private GameObject defaultHitEffectPrefab;
        private bool hasHit;
        private float flightTimer;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = false;
            var rb = GetComponent<Rigidbody>();
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        public void Init(float dmg, Vector3 velocity, IWeaponOwner owner, GameObject hitEffectPrefab)
        {
            damage = dmg;
            shooter = owner;
            defaultHitEffectPrefab = hitEffectPrefab;

            var rb = GetComponent<Rigidbody>();
            rb.linearVelocity = velocity;
            transform.rotation = Quaternion.LookRotation(velocity);
        }

        private void Update()
        {
            if (hasHit) return;

            flightTimer += Time.deltaTime;
            if (flightTimer >= flightLifeTime)
            {
                Destroy(gameObject);
                return;
            }

            var rb = GetComponent<Rigidbody>();
            if (rb.linearVelocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (hasHit) return;
            hasHit = true;

            var target = collision.collider.GetComponentInParent<IDamageable>();
            if (target != null && target.IsAlive)
            {
                bool isWeakpoint = collision.collider.gameObject.layer == LayerMask.NameToLayer("Weakpoint");
                shooter?.ApplyDamageTo(target, damage, isWeakpoint);
            }

            GameObject prefab = collision.collider.GetComponentInParent<IHitEffectSource>()?.HitEffectPrefab ?? defaultHitEffectPrefab;
            if (prefab != null)
            {
                var contact = collision.GetContact(0);
                GameObject.Instantiate(prefab, contact.point, Quaternion.LookRotation(contact.normal));
            }

            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            GetComponent<Collider>().enabled = false;

            transform.SetParent(collision.transform, true);
        }
    }
}