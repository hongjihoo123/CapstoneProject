using UnityEngine;

namespace RobotWeapons
{
    // 소드오프 샷건 공용 로직 (펠릿 판정 + 탄약 2발 + 장전 + 총구 좌우 번갈아 발사).
    // 판정은 즉시 레이캐스트, 시각화만 DumbBulletVisual(가짜 총알)로 따로 날림.
    // MeleeSawedOffWeapon/SniperSawedOffWeapon이 이 클래스를 들고 조합해서 씀 (상속 아님).
    public class SawedOffShotgunModule
    {
        private readonly int pelletCount;
        private readonly float spreadAngle;
        private readonly float damagePerPellet;
        private readonly float range;
        private readonly float reloadDuration;
        private readonly float damageFalloffAtMaxRange;
        private readonly GameObject bulletPrefab;
        private readonly float bulletSpeed;

        public int CurrentAmmo { get; private set; }
        public int MaxAmmo { get; }
        public bool IsReloading { get; private set; }
        private float reloadTimer;
        private bool useLeftBarrel = true;

        public SawedOffShotgunModule(int pelletCount, float spreadAngle, float damagePerPellet,
            float range, int maxAmmo, float reloadDuration, float damageFalloffAtMaxRange,
            GameObject bulletPrefab, float bulletSpeed)
        {
            this.pelletCount = pelletCount;
            this.spreadAngle = spreadAngle;
            this.damagePerPellet = damagePerPellet;
            this.range = range;
            this.reloadDuration = reloadDuration;
            this.damageFalloffAtMaxRange = damageFalloffAtMaxRange;
            this.bulletPrefab = bulletPrefab;
            this.bulletSpeed = bulletSpeed;
            MaxAmmo = maxAmmo;
            CurrentAmmo = maxAmmo;
        }

        public void Tick(float dt)
        {
            if (!IsReloading) return;
            reloadTimer -= dt;
            if (reloadTimer <= 0f)
            {
                CurrentAmmo = MaxAmmo;
                IsReloading = false;
            }
        }

        public void Reload()
        {
            if (IsReloading || CurrentAmmo >= MaxAmmo) return;
            IsReloading = true;
            reloadTimer = reloadDuration;
        }

        public bool CanFire => !IsReloading && CurrentAmmo > 0;

        public void Fire(IWeaponOwner owner, float bonusDamage, System.Action<string> raiseAttackTriggered,
            GameObject defaultHitEffectPrefab = null)
        {
            if (!CanFire || owner == null) return;
            CurrentAmmo--;

            Vector3 origin = owner.AimOrigin.position;
            Vector3 forward = owner.AimOrigin.forward;

            for (int i = 0; i < pelletCount; i++)
            {
                Vector3 dir = AimUtility.GetSpreadDirection(forward, spreadAngle);
                Vector3 targetPoint;

                if (Physics.Raycast(origin, dir, out RaycastHit hit, range))
                {
                    targetPoint = hit.point;

                    float t = Mathf.Clamp01(hit.distance / range);
                    float falloff = Mathf.Lerp(1f, damageFalloffAtMaxRange, t);
                    float finalDamage = damagePerPellet * falloff + bonusDamage;

                    var target = hit.collider.GetComponentInParent<IDamageable>();
                    if (target != null && target.IsAlive)
                        owner.ApplyDamageTo(target, finalDamage);

                    SpawnHitEffect(hit, defaultHitEffectPrefab);
                }
                else
                {
                    targetPoint = origin + dir * range;
                }

                SpawnBulletVisual(owner, targetPoint);
            }

            raiseAttackTriggered(useLeftBarrel ? "SawedOff_FireLeft" : "SawedOff_FireRight");
            useLeftBarrel = !useLeftBarrel;
        }

        private void SpawnBulletVisual(IWeaponOwner owner, Vector3 targetPoint)
        {
            if (bulletPrefab == null) return;

            GameObject bullet = GameObject.Instantiate(bulletPrefab, owner.MuzzleOrigin.position, Quaternion.identity);
            if (bullet.TryGetComponent<DumbBulletVisual>(out var visual))
                visual.Launch(targetPoint, bulletSpeed);
        }

        private static void SpawnHitEffect(RaycastHit hit, GameObject defaultPrefab)
        {
            GameObject prefab = hit.collider.GetComponentInParent<IHitEffectSource>()?.HitEffectPrefab;
            if (prefab == null) prefab = defaultPrefab;
            if (prefab == null) return;

            GameObject.Instantiate(prefab, hit.point, Quaternion.LookRotation(hit.normal));
        }
    }
}
