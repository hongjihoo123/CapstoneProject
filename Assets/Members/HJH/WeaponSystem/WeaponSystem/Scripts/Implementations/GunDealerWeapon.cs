using RobotWeapons;
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;
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

        public float AttackSpeedMultiplier = 1f;
        public bool IsBursting => burstShotsRemaining > 0;

        [Header("궁 (연사 모드)")]
        public float ultimateFireRate = 10f;
        private float ultimateTimer;
        public bool IsUltimateActive => ultimateTimer > 0f;

        public void ActivateUltimate(float duration)
        {
            ultimateTimer = duration;
            burstShotsRemaining = 0; // 진행 중이던 점사 취소하고 즉시 연사로 전환
        }

        // Auto거나 궁 활성 중이면 계속 발사
        public override bool PrimaryIsHeld => data.fireMode == GunDealerData.FireMode.Auto || IsUltimateActive;

        public GunDealerWeapon(GunDealerData d) : base(d) { data = d; currentSpread = d.baseSpreadAngle; }

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

            fireCooldown = (1f / data.fireRate) / AttackSpeedMultiplier;

            fireCooldown = (1f / (IsUltimateActive ? data.ultimateFireRate : data.fireRate)) / AttackSpeedMultiplier;

            // 궁 활성 중엔 Burst든 뭐든 전부 단발 연사로 처리
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