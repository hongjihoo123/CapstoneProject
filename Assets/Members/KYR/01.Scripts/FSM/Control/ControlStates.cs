using Members.KYR._01_Scripts.FSM.Core;
using UnityEngine;

namespace Members.KYR._01_Scripts.FSM.Control
{
    public abstract class ControlState : IState
    {
        protected readonly ControlStateModule Module;

        protected ControlState(ControlStateModule module)
        {
            Module = module;
        }

        public virtual void Enter() { }

        public virtual void Exit() { }

        public abstract void Tick(float deltaTime);

        protected void FreezeLocomotionAndWeapon()
        {
            Module.Player.MoveFsm.ForceIdle();
            Module.Player.WeaponFsm.ForceIdle();
            Module.Player.Mover.SetPlanarInput(Vector2.zero, 0f);
        }
    }

    public sealed class AliveControlState : ControlState
    {
        public AliveControlState(ControlStateModule module) : base(module) { }

        public override void Tick(float deltaTime)
        {
            if (Module.Player.Health.IsDead)
            {
                Module.ChangeState<DeadControlState>();
                return;
            }

            if (Module.Player.Health.IsStunned)
                Module.ChangeState<StunnedControlState>();
        }
    }

    public sealed class StunnedControlState : ControlState
    {
        public StunnedControlState(ControlStateModule module) : base(module) { }

        public override void Enter()
        {
            FreezeLocomotionAndWeapon();
        }

        public override void Tick(float deltaTime)
        {
            if (Module.Player.Health.IsDead)
            {
                Module.ChangeState<DeadControlState>();
                return;
            }

            if (!Module.Player.Health.IsStunned)
                Module.ChangeState<AliveControlState>();
        }
    }

    public sealed class DeadControlState : ControlState
    {
        public DeadControlState(ControlStateModule module) : base(module) { }

        public override void Enter()
        {
            FreezeLocomotionAndWeapon();
        }

        public override void Tick(float deltaTime) { }
    }
}
