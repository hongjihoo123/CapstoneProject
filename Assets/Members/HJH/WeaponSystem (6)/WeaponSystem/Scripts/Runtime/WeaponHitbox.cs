using UnityEngine;

namespace RobotWeapons
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class WeaponHitbox : MonoBehaviour
    {
        private Collider hitCollider;
        private IWeapon owningWeapon;

        private void Awake()
        {
            hitCollider = GetComponent<Collider>();
            hitCollider.isTrigger = true;
            hitCollider.enabled = false;

            // 트리거 판정이 확실히 발생하려면 한쪽에 Rigidbody 필요
            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        public void Init(IWeapon weapon) => owningWeapon = weapon;

        public void SetActive(bool active)
        {
            if (hitCollider != null)
                hitCollider.enabled = active;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (owningWeapon == null) return;
            if (other.TryGetComponent<IDamageable>(out var target))
                owningWeapon.OnHitboxTouch(target);
        }
    }
}
