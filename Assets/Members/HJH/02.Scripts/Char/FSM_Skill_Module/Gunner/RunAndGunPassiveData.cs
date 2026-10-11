using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Gunner
{
    // Character 1 passive: dashing loads empowered shots; landing one shortens the dash recharge.
    // Hitting keeps you moving, missing slows the kiting loop down.
    [CreateAssetMenu(menuName = "Skill/Passive/Run And Gun")]
    public class RunAndGunPassiveData : PassiveData
    {
        [SerializeField, Min(1)] private int empoweredShots = 2;
        [SerializeField] private float empoweredDamageMultiplier = 1.6f;
        [SerializeField, Tooltip("Seconds taken off the dash recharge per empowered hit.")]
        private float dashCooldownRefund = 0.6f;

        public override IPassive CreateRuntime(IPassiveHost host) => new Runtime(this, host);

        private sealed class Runtime : PassiveRuntime
        {
            private readonly RunAndGunPassiveData _data;
            private IShotHitSource _shots;

            public Runtime(RunAndGunPassiveData data, IPassiveHost host) : base(host)
            {
                _data = data;
                Rebind();
            }

            public override void OnSkillUsed(in SkillUsedInfo info)
            {
                if (info.Slot != SkillSlotId.Dash)
                    return;

                Rebind();
                (Context.Weapon as IEmpowerableShots)?.Empower(_data.empoweredShots, _data.empoweredDamageMultiplier);
            }

            public override void Tick(float deltaTime) => Rebind();

            public override void Dispose() => Bind(null);

            // The weapon instance changes when a kit or weapon is re-equipped.
            private void Rebind()
            {
                IShotHitSource current = Context.Weapon as IShotHitSource;
                if (!ReferenceEquals(current, _shots))
                    Bind(current);
            }

            private void Bind(IShotHitSource source)
            {
                if (_shots != null)
                    _shots.ShotHit -= HandleShotHit;

                _shots = source;

                if (_shots != null)
                    _shots.ShotHit += HandleShotHit;
            }

            private void HandleShotHit(IDamageable target, bool empowered)
            {
                if (empowered)
                    Host.ReduceCooldown(SkillSlotId.Dash, _data.dashCooldownRefund);
            }
        }
    }
}
