using UnityEngine;

namespace RobotWeapons
{
    public class GunDealerWeapon : WeaponBase, IAmmoDisplay, IAttackFeedbackSource, IBurstWeapon, IAttackSpeedScalable, IUltimateWeapon
    {
        private readonly GunDealerData data;
        private float fireCooldown;
        private float currentSpread;
        private bool isAiming;

        private int burstShotsRemaining;
        private float burstSafetyTimer;
        private float ultimateTimer;

        public float AttackSpeedMultiplier { get; set; } = 1f;
        public bool IsBursting => burstShotsRemaining > 0;
        public bool IsUltimateActive => ultimateTimer > 0f;

        public override bool PrimaryIsHeld => data.fireMode == GunDealerData.FireMode.Auto || IsUltimateActive;

        public GunDealerWeapon(GunDealerData d) : base(d) { data = d; currentSpread = d.baseSpreadAngle; }

        public void ActivateUltimate(float duration)
        {
            ultimateTimer = duration;
            burstShotsRemaining = 0;
        }

        public bool TryGetAmmoText(out string text)
        {
            text = AmmoText.ForResource(this);
            return true;
        }

        public AttackFeedback DescribeAttack(string animId) =>
            animId == "Gun_Fire" ? new AttackFeedback { IsFire = true } : default;

        public override void Tick(float dt)
        {
            fireCooldown -= dt;
            currentSpread = Mathf.Max(data.baseSpreadAngle, currentSpread - data.spreadRecoverPerSecond * dt);
            TickReload(dt);

            if (ultimateTimer > 0f)
                ultimateTimer -= dt;

            if (burstShotsRemaining > 0)
            {
                burstSafetyTimer -= dt;
                if (burstSafetyTimer <= 0f)
                {
                    while (burstShotsRemaining > 0)
                    {
                        FireOneShot(raiseFireEvent: false);
                        burstShotsRemaining--;
                    }
                }
            }
        }

        public override void PrimaryAttack()
        {
            if (IsReloading) return;
            if (!IsUltimateActive && CurrentResource <= 0f) return;
            if (fireCooldown > 0f || owner == null || data.projectilePrefab == null) return;
            if (burstShotsRemaining > 0) return;

            fireCooldown = (1f / (IsUltimateActive ? data.ultimateFireRate : data.fireRate)) / AttackSpeedMultiplier;

            // 궁극기 중에는 Burst 모드여도 점사 없이 연사로 처리
            if (!IsUltimateActive && data.fireMode == GunDealerData.FireMode.Burst)
            {
                burstShotsRemaining = data.burstCount;
                burstSafetyTimer = data.burstSafetyDuration;
                RaiseAttackTriggered("Gun_Fire");
                return;
            }

            FireOneShot(raiseFireEvent: true);
        }

        public override void ExecuteHit()
        {
            if (burstShotsRemaining <= 0) return;
            FireOneShot(raiseFireEvent: false);
            burstShotsRemaining--;
            burstSafetyTimer = data.burstSafetyDuration;
        }

        private void FireOneShot(bool raiseFireEvent)
        {
            if (!IsUltimateActive)
            {
                if (CurrentResource <= 0f) return;
                CurrentResource -= 1f;
            }

            float spread = isAiming ? currentSpread * data.aimSpreadMultiplier : currentSpread;
            Vector3 aimDir = GetSpreadDirection(owner.AimOrigin.forward, spread);
            Quaternion muzzleRot = AimUtility.GetConvergedMuzzleRotation(owner, aimDir, data.aimRange);

            GameObject proj = GameObject.Instantiate(data.projectilePrefab, owner.MuzzleOrigin.position, muzzleRot);
            if (proj.TryGetComponent<Projectile>(out var p))
                p.Init((data.damagePerBullet + bonusDamage) * DamageMultiplier, data.projectileSpeed, owner);

            currentSpread = Mathf.Min(data.maxSpreadAngle, currentSpread + data.spreadGrowthPerShot);

            if (raiseFireEvent)
                RaiseAttackTriggered("Gun_Fire");

            float horizontalKick = Random.Range(data.recoilPerShotHorizontalMin, data.recoilPerShotHorizontalMax);
            float dutchKick = Random.Range(data.dutchKickMin, data.dutchKickMax);

            if (owner is IRecoilCapable recoilOwner)
                recoilOwner.ApplyRecoil(data.recoilPerShotVertical, horizontalKick, dutchKick);
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
