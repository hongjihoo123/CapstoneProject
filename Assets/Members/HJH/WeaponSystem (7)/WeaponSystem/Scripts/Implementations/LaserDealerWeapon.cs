using UnityEngine;

namespace RobotWeapons
{
    public class LaserDealerWeapon : WeaponBase
    {
        private readonly LaserDealerData data;
        private bool isFiring;
        private bool isAiming;
        private IDamageable currentTarget;
        private float rampTime;

        public bool IsFiring => isFiring;
        public Vector3 BeamEndPoint { get; private set; }

        public LaserDealerWeapon(LaserDealerData d) : base(d) { data = d; }

        public override void PrimaryAttack()
        {
            isFiring = !isFiring;
            if (!isFiring)
            {
                currentTarget = null;
                rampTime = 0f;
            }
            RaiseAttackTriggered(isFiring ? "Laser_Start" : "Laser_End");
        }

        public override void SecondaryAction()
        {
            isAiming = !isAiming;
            RaiseAttackTriggered(isAiming ? "Laser_AimStart" : "Laser_AimEnd");
        }

        public override void Tick(float dt)
        {
            if (owner == null) return;

            if (!isFiring)
            {
                BeamEndPoint = owner.AttackOrigin.position;
                return;
            }

            CurrentResource -= data.resourceDrainPerSecond * dt;
            if (CurrentResource <= 0f)
            {
                CurrentResource = 0f;
                isFiring = false;
                RaiseAttackTriggered("Laser_End");
                return;
            }

            Vector3 origin = owner.AttackOrigin.position;
            Vector3 dir = owner.AttackOrigin.forward;

            if (Physics.Raycast(origin, dir, out var hit, data.range))
            {
                BeamEndPoint = hit.point;

                if (hit.collider.TryGetComponent<IDamageable>(out var target) && target.IsAlive)
                {
                    rampTime = (target == currentTarget) ? rampTime + dt : 0f;
                    currentTarget = target;

                    float multiplier = Mathf.Min(1f + data.rampUpRate * rampTime, data.maxRampMultiplier);
                    float dmg = (data.damagePerSecond + bonusDamage) * multiplier * dt;
                    owner.ApplyDamageTo(target, dmg);
                    RaiseDamage(dmg);
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
    }
}
