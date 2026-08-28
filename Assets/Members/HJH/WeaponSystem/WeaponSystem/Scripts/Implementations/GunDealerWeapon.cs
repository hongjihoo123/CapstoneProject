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

        public float AttackSpeedMultiplier = 1f;
        public bool IsBursting => burstShotsRemaining > 0;

        // Auto만 누르고 있는 동안 계속 발사. Semi/Burst는 클릭(프레스)마다 한 번씩만 반응.
        public override bool PrimaryIsHeld => data.fireMode == GunDealerData.FireMode.Auto;

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
                        FireOneShot(raiseFireEvent: false);
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

            if (data.fireMode == GunDealerData.FireMode.Burst)
            {
                burstShotsRemaining = data.burstCount;
                burstSafetyTimer = data.burstSafetyDuration;
                // 킥오프 신호 딱 한 번만 - 이걸로 IsBursting이 버스트 끝날 때까지 발사상태를 계속 유지해줌.
                // 개별 발들은 더 이상 이 신호를 재발행할 필요가 없음 (마지막 발에서 중복 재생되던 원인이었음).
                RaiseAttackTriggered("Gun_Fire");
                return;
            }

            // Semi/Auto 둘 다 한 발만 - 매 발이 자기 몫의 신호가 필요함(버스트처럼 유지해주는 게 없어서)
            FireOneShot(raiseFireEvent: true);
        }

        public override void ExecuteHit()
        {
            if (burstShotsRemaining <= 0) return;
            FireOneShot(raiseFireEvent: false); // 버스트 개별 발은 이벤트 재발행 안 함
            burstShotsRemaining--;
            burstSafetyTimer = data.burstSafetyDuration;
        }

        private void FireOneShot(bool raiseFireEvent)
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

            if (raiseFireEvent)
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