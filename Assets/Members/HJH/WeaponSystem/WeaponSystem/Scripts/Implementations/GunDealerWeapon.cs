using UnityEngine;

namespace RobotWeapons
{
    public class GunDealerWeapon : WeaponBase
    {
        private readonly GunDealerData data;
        private float fireCooldown;
        private float currentSpread;
        private bool isAiming;

        private int burstShotsRemaining;
        private float burstSafetyTimer;

        public float AttackSpeedMultiplier = 1f; // 업그레이드/버프로 공격속도 오르면 이 값만 조절
        public bool IsBursting => burstShotsRemaining > 0;

        public override bool PrimaryIsHeld => !data.isBurstFire;

        public GunDealerWeapon(GunDealerData d) : base(d) { data = d; currentSpread = d.baseSpreadAngle; }

        public override void Tick(float dt)
        {
            fireCooldown -= dt;
            currentSpread = Mathf.Max(data.baseSpreadAngle, currentSpread - data.spreadRecoverPerSecond * dt);
            TickReload(dt);

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
            if (IsReloading || CurrentResource <= 0f) return;
            if (fireCooldown > 0f || owner == null || data.projectilePrefab == null) return;
            if (burstShotsRemaining > 0) return;

            fireCooldown = (1f / data.fireRate) / AttackSpeedMultiplier;

            if (data.isBurstFire)
            {
                burstShotsRemaining = data.burstCount;
                burstSafetyTimer = data.burstSafetyDuration;
                RaiseAttackTriggered("Gun_Fire");
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
            if (CurrentResource <= 0f) return;
            CurrentResource -= 1f;

            float spread = isAiming ? currentSpread * data.aimSpreadMultiplier : currentSpread;
            Vector3 aimDir = GetSpreadDirection(owner.AimOrigin.forward, spread);
            Quaternion muzzleRot = AimUtility.GetConvergedMuzzleRotation(owner, aimDir, data.aimRange);

            GameObject proj = GameObject.Instantiate(data.projectilePrefab, owner.MuzzleOrigin.position, muzzleRot);
            if (proj.TryGetComponent<Projectile>(out var p))
                p.Init(data.damagePerBullet + bonusDamage, data.projectileSpeed, owner);

            currentSpread = Mathf.Min(data.maxSpreadAngle, currentSpread + data.spreadGrowthPerShot);
            RaiseAttackTriggered("Gun_Fire");

            float horizontalKick = Random.Range(data.recoilPerShotHorizontalMin, data.recoilPerShotHorizontalMax);
            float dutchKick = Random.Range(data.dutchKickMin, data.dutchKickMax);
            owner.ApplyRecoil(data.recoilPerShotVertical, horizontalKick, dutchKick);
        }

        public override void SecondaryAction()
        {
            isAiming = !isAiming;
            RaiseAttackTriggered(isAiming ? "Gun_AimStart" : "Gun_AimEnd");
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