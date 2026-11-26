using System;
using UnityEngine;
namespace RobotWeapons
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class WeaponHitbox : MonoBehaviour
    {
        private Collider hitCollider;
        private IWeapon owningWeapon;

        private Action<IDamageable, bool> overrideHandler;

        private void Awake()
        {
            hitCollider = GetComponent<Collider>();
            hitCollider.isTrigger = true;
            hitCollider.enabled = false;
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

        public void SetOverrideHandler(Action<IDamageable, bool> handler) => overrideHandler = handler;
        public void ClearOverrideHandler() => overrideHandler = null;

        private void OnTriggerEnter(Collider other)
        {
            var target = other.GetComponentInParent<IDamageable>();
            if (target == null) return;
            bool isWeakpoint = other.gameObject.layer == LayerMask.NameToLayer("Weakpoint");

            if (overrideHandler != null)
            {
                overrideHandler(target, isWeakpoint);
                return;
            }

            if (owningWeapon == null) return;
            owningWeapon.OnHitboxTouch(target, isWeakpoint);
        }
    }
}