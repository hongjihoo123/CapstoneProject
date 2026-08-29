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

        // 스킬처럼 "지금 장착된 무기"와 무관하게 히트 판정이 필요할 때 임시로 설정.
        // 설정돼있으면 owningWeapon.OnHitboxTouch 대신 이쪽으로 감.
        private Action<IDamageable, bool> overrideHandler;

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