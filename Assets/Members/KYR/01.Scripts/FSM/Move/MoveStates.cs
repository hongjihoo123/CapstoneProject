using Members.KYR._01_Scripts.FSM.Core;

namespace Members.KYR._01_Scripts.FSM.Move
{
    public abstract class MoveState : IState, IMoveCapabilities
    {
        protected readonly MoveStateModule Module;

        protected MoveState(MoveStateModule module)
        {
            Module = module;
        }

        public abstract bool CanAim { get; }
        public abstract bool CanFire { get; }
        public virtual bool IsCrouching => false;
        public virtual bool IsAirborne => false;
        public abstract float PlanarSpeed { get; }

        public virtual void Enter() { }

        public virtual void Exit() { }

        public abstract void Tick(float deltaTime);
    }

    public sealed class IdleMoveState : MoveState
    {
        public IdleMoveState(MoveStateModule module) : base(module) { }

        public override bool CanAim => true;
        public override bool CanFire => true;
        public override float PlanarSpeed => 0f;

        public override void Tick(float deltaTime)
        {
            Module.ResolveGrounded();
        }
    }

    public sealed class WalkMoveState : MoveState
    {
        public WalkMoveState(MoveStateModule module) : base(module) { }

        public override bool CanAim => true;
        public override bool CanFire => true;
        public override float PlanarSpeed => Module.Player.Mover.WalkSpeed;

        public override void Tick(float deltaTime)
        {
            Module.ResolveGrounded();
        }
    }

    public sealed class RunMoveState : MoveState
    {
        public RunMoveState(MoveStateModule module) : base(module) { }

        public override bool CanAim => false;
        public override bool CanFire => true;
        public override float PlanarSpeed => Module.Player.Mover.RunSpeed;

        public override void Tick(float deltaTime)
        {
            Module.ResolveGrounded();
        }
    }

    public sealed class CrouchMoveState : MoveState
    {
        public CrouchMoveState(MoveStateModule module) : base(module) { }

        public override bool CanAim => true;
        public override bool CanFire => true;
        public override bool IsCrouching => true;
        public override float PlanarSpeed => Module.Player.Mover.CrouchSpeed;

        public override void Tick(float deltaTime)
        {
            Module.ResolveGrounded();
        }
    }

    public sealed class JumpMoveState : MoveState
    {
        private float _ignoreGroundedTime;

        public JumpMoveState(MoveStateModule module) : base(module) { }

        public override bool CanAim => true;
        public override bool CanFire => true;
        public override bool IsAirborne => true;

        public override float PlanarSpeed
        {
            get
            {
                var input = Module.Player.Input;
                float baseSpeed = input.RunHeld ? Module.Player.Mover.RunSpeed : Module.Player.Mover.WalkSpeed;
                return baseSpeed * Module.Player.Mover.AirControl;
            }
        }

        public override void Enter()
        {
            _ignoreGroundedTime = 0.12f;
            Module.Player.Mover.Jump();
        }

        public override void Tick(float deltaTime)
        {
            _ignoreGroundedTime -= deltaTime;
            if (_ignoreGroundedTime > 0f) return;

            if (Module.Player.Mover.IsGrounded)
                Module.ResolveGrounded();
        }
    }
}
