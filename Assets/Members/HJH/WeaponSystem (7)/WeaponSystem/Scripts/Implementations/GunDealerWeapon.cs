using UnityEngine;

namespace RobotWeapons
{
    public class GunDealerWeapon : WeaponBase
    {
        private readonly GunDealerData data;
        private float fireCooldown;
        private float currentSpread;
        private bool isAiming;

        public override bool PrimaryIsHeld => true;

        public GunDealerWeapon(GunDealerData d) : base(d) { data = d; currentSpread = d.baseSpreadAngle; }

        public override void Tick(float dt)
        {
            fireCooldown -= dt;
            currentSpread = Mathf.Max(data.baseSpreadAngle, currentSpread - data.spreadRecoverPerSecond * dt);
        }

        public override void PrimaryAttack()
        {
            if (fireCooldown > 0f || owner == null || data.projectilePrefab == null) return;
            fireCooldown = 1f / data.fireRate;

            float spread = (isAiming ? currentSpread * data.aimSpreadMultiplier : currentSpread);
            Quaternion rot = GetSpreadRotation(owner.AttackOrigin.forward, spread);

            GameObject proj = GameObject.Instantiate(data.projectilePrefab, owner.AttackOrigin.position, rot);
            if (proj.TryGetComponent<Projectile>(out var p))
                p.Init(data.damagePerBullet + bonusDamage, data.projectileSpeed, owner);

            currentSpread = Mathf.Min(data.maxSpreadAngle, currentSpread + data.spreadGrowthPerShot);
            RaiseAttackTriggered("Gun_Fire");
        }

        public override void SecondaryAction()
        {
            isAiming = !isAiming;
            RaiseAttackTriggered(isAiming ? "Gun_AimStart" : "Gun_AimEnd");
        }

        private Quaternion GetSpreadRotation(Vector3 forward, float spreadDeg)
        {
            if (spreadDeg <= 0f) return Quaternion.LookRotation(forward);

            float angle = Random.Range(0f, spreadDeg);
            float spin = Random.Range(0f, 360f);

            Vector3 perpendicular = Vector3.Cross(forward, Vector3.up).normalized;
            if (perpendicular.sqrMagnitude < 0.001f)
                perpendicular = Vector3.Cross(forward, Vector3.right).normalized;

            Vector3 dir = Quaternion.AngleAxis(spin, forward) * Quaternion.AngleAxis(angle, perpendicular) * forward;
            return Quaternion.LookRotation(dir);
        }
    }
}
