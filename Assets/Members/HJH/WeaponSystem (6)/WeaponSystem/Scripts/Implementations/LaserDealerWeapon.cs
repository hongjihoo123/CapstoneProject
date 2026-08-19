using UnityEngine;

namespace RobotWeapons
{
    public class LaserDealerWeapon : WeaponBase
    {
        private readonly LaserDealerData data;
        private bool isFiring;
        private bool firedThisFrame;
        private IDamageable currentTarget;
        private float rampTime;
        private float gauge;
        private float energyBallCooldownTimer;

        public override bool PrimaryIsHeld => true;

        public bool IsFiring => isFiring;
        public Vector3 BeamEndPoint { get; private set; }
        public float GaugeRatio => data.maxGauge <= 0f ? 0f : Mathf.Clamp01(gauge / data.maxGauge);
        public float LastFireChargeRatio { get; private set; }

        public LaserDealerWeapon(LaserDealerData d) : base(d) { data = d; }

        // 누르고 있는 동안 매 프레임 호출됨 (PrimaryIsHeld = true)
        public override void PrimaryAttack()
        {
            if (IsReloading || CurrentResource <= 0f) return;

            firedThisFrame = true;

            if (!isFiring)
            {
                isFiring = true;
                RaiseAttackTriggered("Laser_Start");
            }

            FireBeamTick(Time.deltaTime);
        }

        public override void SecondaryAction()
        {
            if (energyBallCooldownTimer > 0f || gauge <= 0f) return;

            float chargeRatio = GaugeRatio;
            float dmg = data.energyBallDamage * chargeRatio + bonusDamage;
            gauge = 0f;
            energyBallCooldownTimer = data.energyBallCooldown;

            FireEnergyBall(dmg, chargeRatio);
        }

        // 버튼을 뗀 프레임엔 PrimaryAttack이 안 불리므로, 여기서 그걸 감지해서 빔을 끔
        public override void Tick(float dt)
        {
            if (owner == null) return;

            if (!firedThisFrame && isFiring)
            {
                isFiring = false;
                currentTarget = null;
                rampTime = 0f;
                RaiseAttackTriggered("Laser_End");
            }

            if (!isFiring)
                BeamEndPoint = owner.AimOrigin.position;

            firedThisFrame = false;
            TickReload(dt);

            if (energyBallCooldownTimer > 0f)
                energyBallCooldownTimer -= dt;
        }

        private void FireBeamTick(float dt)
        {
            CurrentResource -= data.resourceDrainPerSecond * dt;
            if (CurrentResource <= 0f)
            {
                CurrentResource = 0f;
                isFiring = false;
                firedThisFrame = false;
                RaiseAttackTriggered("Laser_End");
                return;
            }

            Vector3 origin = owner.AimOrigin.position;
            Vector3 dir = owner.AimOrigin.forward;

            if (Physics.Raycast(origin, dir, out var hit, data.range))
            {
                BeamEndPoint = hit.point;

                if (hit.collider.GetComponentInParent<IDamageable>() is { IsAlive: true } target)
                {
                    bool isWeakpoint = hit.collider.gameObject.layer == LayerMask.NameToLayer("Weakpoint");
                    rampTime = (target == currentTarget) ? rampTime + dt : 0f;
                    currentTarget = target;

                    float multiplier = Mathf.Min(1f + data.rampUpRate * rampTime, data.maxRampMultiplier);
                    float dmg = (data.damagePerSecond + bonusDamage) * multiplier * dt;
                    owner.ApplyDamageTo(target, dmg, isWeakpoint);
                    RaiseDamage(dmg);

                    ChargeGauge(dt);
                    return;
                }
            }
            else
            {
                BeamEndPoint = origin + dir * data.range;
            }

            currentTarget = null;
            rampTime = 0f;
        }

        private void ChargeGauge(float dt)
        {
            gauge = Mathf.Min(gauge + data.gaugeChargePerSecond * dt, data.maxGauge);
        }

        private void FireEnergyBall(float damage, float chargeRatio)
        {
            if (data.energyBallPrefab == null) return;

            Quaternion muzzleRot = AimUtility.GetConvergedMuzzleRotation(owner, owner.AimOrigin.forward, data.energyBallAimRange);
            GameObject ball = GameObject.Instantiate(data.energyBallPrefab, owner.MuzzleOrigin.position, muzzleRot);
            if (ball.TryGetComponent<Projectile>(out var p))
                p.Init(damage, data.energyBallSpeed, owner);

            LastFireChargeRatio = chargeRatio;
            RaiseAttackTriggered("Laser_EnergyBall");
        }
    }
}
