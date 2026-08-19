using UnityEngine;

namespace RobotWeapons
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class HealShotProjectile : MonoBehaviour
    {
        private float healAmount, speed;
        private IWeaponOwner caster;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        public void Init(float heal, float projectileSpeed, IWeaponOwner owner)
        {
            healAmount = heal;
            speed = projectileSpeed;
            caster = owner;
            Destroy(gameObject, 5f);
        }

        private void Update()
        {
            transform.position += transform.forward * speed * Time.deltaTime;
        }

        private void OnTriggerEnter(Collider other)
        {
            var target = other.GetComponentInParent<IHealable>();
            if (target != null)
            {
                caster?.ApplyHealTo(target, healAmount);
                Destroy(gameObject);
            }
        }
    }
}
