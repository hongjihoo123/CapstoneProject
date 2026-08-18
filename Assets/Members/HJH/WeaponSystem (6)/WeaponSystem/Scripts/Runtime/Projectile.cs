using UnityEngine;

namespace RobotWeapons
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float lifeTime = 5f;

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
            if (other.TryGetComponent<IDamageable>(out var target))
            {
                shooter?.ApplyDamageTo(target, damage);
                Destroy(gameObject);
            }
        }
    }
}
