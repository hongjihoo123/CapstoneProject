using UnityEngine;

namespace RobotWeapons
{
    public class EnemyGunWeapon : WeaponBase
    {
        private readonly EnemyGunData data;
        private float fireCooldown;
        private float currentSpread;

        private int burstShotsRemaining;
        private float burstSafetyTimer;

        public bool IsBursting => burstShotsRemaining > 0;
        public override bool PrimaryIsHeld => data.fireMode == EnemyGunData.FireMode.Auto;

        public EnemyGunWeapon(EnemyGunData d) : base(d)
        {
            data = d;
            currentSpread = d.baseSpreadAngle;
        }

        public override void Tick(float dt)
        {
            fireCooldown -= dt;
            currentSpread = Mathf.Max(data.baseSpreadAngle, currentSpread - data.spreadRecoverPerSecond * dt);

            if (burstShotsRemaining > 0)
            {
                burstSafetyTimer -= dt;
                if (burstSafetyTimer <= 0f)
                {
                    while (burstShotsRemaining > 0)
                    {
                        FireOneShot();
                        burstShotsRemaining--;
                    }
                }
            }
        }

        public override void PrimaryAttack()
        {
            if (fireCooldown > 0f || owner == null || data.projectilePrefab == null) return;
            if (burstShotsRemaining > 0) return;

            fireCooldown = 1f / data.fireRate;

            if (data.fireMode == EnemyGunData.FireMode.Burst)
            {
                burstShotsRemaining = data.burstCount;
                burstSafetyTimer = data.burstSafetyDuration;
                RaiseAttackTriggered("Enemy_Gun_Fire");
                return;
            }

            FireOneShot();
        }

        public override void ExecuteHit()
        {
            if (burstShotsRemaining <= 0) return;
            FireOneShot();
            burstShotsRemaining--;
            burstSafetyTimer = data.burstSafetyDuration;
        }

        private void FireOneShot()
        {
            Vector3 aimDir = GetSpreadDirection(owner.AimOrigin.forward, currentSpread);
            Quaternion muzzleRot = Quaternion.LookRotation(aimDir);

            GameObject proj = GameObject.Instantiate(data.projectilePrefab, owner.MuzzleOrigin.position, muzzleRot);
            if (proj.TryGetComponent<Projectile>(out var p))
                p.Init(data.damagePerBullet * DamageMultiplier, data.projectileSpeed, owner);

            currentSpread = Mathf.Min(data.maxSpreadAngle, currentSpread + data.spreadGrowthPerShot);
            RaiseAttackTriggered("Enemy_Gun_Fire");
        }

        private Vector3 GetSpreadDirection(Vector3 forward, float spreadDeg)
        {
            if (spreadDeg <= 0f) return forward;
            float angle = Random.Range(0f, spreadDeg);
            float spin = Random.Range(0f, 360f);
            Vector3 perpendicular = Vector3.Cross(forward, Vector3.up).normalized;
            if (perpendicular.sqrMagnitude < 0.001f)
                perpendicular = Vector3.Cross(forward, Vector3.right).normalized;
            return Quaternion.AngleAxis(spin, forward) * Quaternion.AngleAxis(angle, perpendicular) * forward;
        }
    }
}