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
        private bool suppressNextFireEvent; // 점사 킥오프 신호랑 첫 발 신호가 겹치는 것 방지용

        public float AttackSpeedMultiplier = 1f; // 업그레이드/버프로 공격속도 오르면 이 값만 조절
        public bool IsBursting => burstShotsRemaining > 0; // 애니메이터가 발사 상태를 계속 유지해야 할지 판단용

        // 점사 무기는 클릭 한 번에 애니메이션이 알아서 여러 발 트리거하므로 홀드 연사가 아님
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
                    // 애니메이션 이벤트가 안 불렸을 때를 대비한 안전장치 - 강제로 나머지 발사
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
            if (burstShotsRemaining > 0) return; // 점사 진행 중엔 새 입력 무시

            fireCooldown = (1f / data.fireRate) / AttackSpeedMultiplier;

            if (data.isBurstFire)
            {
                // 실제 발사는 여기서 안 하고, 애니메이션 이벤트가 ExecuteHit()을 부를 때마다 한 발씩
                burstShotsRemaining = data.burstCount;
                burstSafetyTimer = data.burstSafetyDuration;
                suppressNextFireEvent = true; // 첫 발이 나갈 때 이 킥오프 신호랑 중복으로 안 겹치게
                RaiseAttackTriggered("Gun_Fire");
                return;
            }

            FireOneShot();
        }

        // 애니메이션 이벤트가 직접 호출 - 점사 중일 때만 의미 있고, 아니면 그냥 무시됨
        public override void ExecuteHit()
        {
            if (burstShotsRemaining <= 0) return;
            FireOneShot();
            burstShotsRemaining--;
            burstSafetyTimer = data.burstSafetyDuration; // 다음 발까지 다시 유예 - 총 길이랑 무관해짐
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

            if (suppressNextFireEvent)
                suppressNextFireEvent = false; // 킥오프 때 이미 신호 나갔으니 이번만 건너뜀
            else
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