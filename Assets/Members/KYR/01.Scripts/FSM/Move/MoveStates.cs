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

        public abstract float PlanarSpeed { get; }

        public virtual void Enter() { }

        public virtual void Exit() { }

        public abstract void Tick(float deltaTime);
    }

    public sealed class IdleMoveState : MoveState
    {
        public IdleMoveState(MoveStateModule module) : base(module) { }

        public override float PlanarSpeed => 0f;

        public override void Tick(float deltaTime)
        {
            Module.ResolveGrounded();
        }
    }

    public sealed class WalkMoveState : MoveState
    {
        public WalkMoveState(MoveStateModule module) : base(module) { }

        public override float PlanarSpeed => Module.Player.Mover.WalkSpeed;

        public override void Tick(float deltaTime)
        {
            Module.ResolveGrounded();
        }
    }

    public sealed class RunMoveState : MoveState
    {
        public RunMoveState(MoveStateModule module) : base(module) { }

        public override float PlanarSpeed => Module.Player.Mover.RunSpeed;

        public override void Tick(float deltaTime)
        {
            Module.ResolveGrounded();
        }
    }
}
