using UnityEngine;

namespace RobotWeapons
{
    public class SniperSawedOffWeapon : WeaponBase
    {
        private enum Mode { Sniper, Shotgun }

        private readonly SniperSawedOffData data;
        private readonly SawedOffShotgunModule shotgun;
        private Mode mode = Mode.Sniper;

        private float sniperFireCooldown;
        private float postFireZoomLockTimer;
        private int sniperAmmo;
        private bool sniperIsReloading;
        private float sniperReloadTimer;
        private bool isZoomed;

        public bool IsShotgunMode => mode == Mode.Shotgun;
        public bool IsZoomed => isZoomed && mode == Mode.Sniper;
        public float ZoomFOV => data.zoomFOV;
        public float DefaultFOV => data.defaultFOV;
        public float ZoomTransitionSpeed => data.zoomTransitionSpeed;
        public float ZoomSwayAmount => data.zoomSwayAmount;
        public float ZoomSwaySpeed => data.zoomSwaySpeed;
        public Vector3 LastShotStart { get; private set; }
        public Vector3 LastShotEnd { get; private set; }

        public int SniperCurrentAmmo => sniperAmmo;
        public int SniperMaxAmmo => data.sniperMaxAmmo;
        public bool SniperIsReloading => sniperIsReloading;
        public int ShotgunCurrentAmmo => shotgun.CurrentAmmo;
        public int ShotgunMaxAmmo => shotgun.MaxAmmo;
        public bool ShotgunIsReloading => shotgun.IsReloading;

        public SniperSawedOffWeapon(SniperSawedOffData d) : base(d)
        {
            data = d;
            sniperAmmo = d.sniperMaxAmmo;
            shotgun = new SawedOffShotgunModule(d.pelletCount, d.spreadAngle, d.damagePerPellet,
                d.shotgunRange, d.shotgunMaxAmmo, d.shotgunReloadDuration, d.damageFalloffAtMaxRange,
                d.bulletPrefab, d.bulletSpeed);
        }

        public override void SwapMode()
        {
            mode = mode == Mode.Sniper ? Mode.Shotgun : Mode.Sniper;
            isZoomed = false;
            RaiseAttackTriggered(mode == Mode.Sniper ? "Swap_Sniper" : "Swap_Shotgun");
        }

        public override void PrimaryAttack()
        {
            if (mode == Mode.Sniper) FireSniper();
            else shotgun.Fire(owner, bonusDamage, RaiseAttackTriggered, data.defaultHitEffectPrefab);
        }

        public override void SecondaryAction()
        {
            if (mode != Mode.Sniper || postFireZoomLockTimer > 0f) return;
            isZoomed = !isZoomed;
            RaiseAttackTriggered(isZoomed ? "Sniper_ZoomIn" : "Sniper_ZoomOut");
        }

        public override void Reload()
        {
            if (mode == Mode.Sniper)
            {
                if (sniperIsReloading || sniperAmmo >= data.sniperMaxAmmo) return;
                sniperIsReloading = true;
                sniperReloadTimer = data.sniperReloadDuration;
            }
            else
            {
                shotgun.Reload();
            }
        }

        public override void Tick(float dt)
        {
            shotgun.Tick(dt);

            if (sniperFireCooldown > 0f) sniperFireCooldown -= dt;
            if (postFireZoomLockTimer > 0f) postFireZoomLockTimer -= dt;

            if (sniperIsReloading)
            {
                sniperReloadTimer -= dt;
                if (sniperReloadTimer <= 0f)
                {
                    sniperAmmo = data.sniperMaxAmmo;
                    sniperIsReloading = false;
                }
            }
        }

        private void FireSniper()
        {
            if (sniperIsReloading || sniperAmmo <= 0 || sniperFireCooldown > 0f || postFireZoomLockTimer > 0f || owner == null) return;

            sniperFireCooldown = 1f / data.sniperFireRate;
            sniperAmmo--;

            float spread = isZoomed ? data.zoomedSpreadAngle : data.hipFireSpreadAngle;
            Vector3 dir = AimUtility.GetSpreadDirection(owner.AimOrigin.forward, spread);
            Vector3 aimPos = owner.AimOrigin.position;
            Vector3 hitPoint;

            if (Physics.Raycast(aimPos, dir, out RaycastHit hit, data.sniperRange))
            {
                hitPoint = hit.point;
                var target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive)
                {
                    bool isWeakpoint = hit.collider.gameObject.layer == LayerMask.NameToLayer("Weakpoint");
                    float dmg = data.sniperDamage + bonusDamage;
                    owner.ApplyDamageTo(target, dmg, isWeakpoint);
                    RaiseDamage(dmg);
                }
            }
            else
            {
                hitPoint = aimPos + dir * data.sniperRange;
            }

            // 트레이서 시각화용: 총구에서 조준점이 맞춘 지점까지
            LastShotStart = owner.MuzzleOrigin.position;
            LastShotEnd = hitPoint;

            // 쏘면 자동으로 줌 풀리고, 잠깐(postFireZoomLockDuration) 재줌/재발사 못하게 잠금
            isZoomed = false;
            postFireZoomLockTimer = data.postFireZoomLockDuration;

            RaiseAttackTriggered("Sniper_Fire");
        }
    }
}
