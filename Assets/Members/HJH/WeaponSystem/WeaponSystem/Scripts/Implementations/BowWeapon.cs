using UnityEngine;

namespace RobotWeapons
{
    public class BowWeapon : WeaponBase
    {
        private readonly BowData data;
        private float chargeTime;
        private bool isDrawing;
        private float shotCooldown;

        public float ChargeRatio => data.maxChargeTime <= 0f ? 0f : Mathf.Clamp01(chargeTime / data.maxChargeTime);
        public bool IsFullyDrawn => isDrawing && ChargeRatio >= 1f;
        public float FullDrawShakeAmount => data.fullDrawShakeAmount;
        public float FullDrawShakeSpeed => data.fullDrawShakeSpeed;

        public BowWeapon(BowData d) : base(d) { data = d; }

        public override void PrimaryAttack() { } // 발사는 OnPrimaryReleased에서 처리

        public override void Tick(float dt)
        {
            if (shotCooldown > 0f) shotCooldown -= dt;
        }

        public override void OnPrimaryPressed()
        {
            if (shotCooldown > 0f) return;
            isDrawing = true;
            chargeTime = 0f;
            RaiseAttackTriggered("Bow_DrawStart");
        }

        public override void OnPrimaryHeld(float dt)
        {
            if (!isDrawing) return;
            chargeTime = Mathf.Min(chargeTime + dt, data.maxChargeTime);
        }

        public override void OnPrimaryReleased()
        {
            if (!isDrawing) return;
            isDrawing = false;

            if (shotCooldown <= 0f)
                FireArrow();

            chargeTime = 0f;
            RaiseAttackTriggered("Bow_Release");
        }

        private void FireArrow()
        {
            if (owner == null || data.arrowPrefab == null) return;

            shotCooldown = data.minFireInterval;

            float t = ChargeRatio;
            float damage = Mathf.Lerp(data.minDamage, data.maxDamage, t) + bonusDamage;
            float speed = Mathf.Lerp(data.minLaunchSpeed, data.maxLaunchSpeed, t);

            Vector3 velocity = owner.AimOrigin.forward * speed;
            GameObject arrow = GameObject.Instantiate(data.arrowPrefab, owner.MuzzleOrigin.position, Quaternion.LookRotation(velocity));
            if (arrow.TryGetComponent<ArrowProjectile>(out var proj))
                proj.Init(damage, velocity, owner, data.defaultHitEffectPrefab);
        }
    }
}
