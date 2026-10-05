using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01_Scripts.FSM.Core;
using UnityEngine;

namespace Members.KYR._01_Scripts.FSM.Move
{
    public class MoveStateModule : Module, IAfterInitModule
    {
        private static readonly AllowAllMoveFallback Fallback = new();

        public PlayerAgent Player { get; private set; }
        public StateMachine Machine { get; private set; }

        public IMoveCapabilities Capabilities => Machine?.Current as IMoveCapabilities ?? Fallback;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            Player = owner as PlayerAgent;
            Debug.Assert(Player != null, $"{owner.name}의 MoveStateModule은 PlayerAgent 아래여야 합니다.");
        }

        public void AfterInit()
        {
            Machine = new StateMachine();
            Machine.Register(new IdleMoveState(this));
            Machine.Register(new WalkMoveState(this));
            Machine.Register(new RunMoveState(this));
            Machine.ChangeState<IdleMoveState>();
        }

        public void Tick(float deltaTime)
        {
            Machine.Tick(deltaTime);
            ApplyMovement();
        }

        public void ChangeState<T>() where T : IState
        {
            Machine.ChangeState<T>();
        }

        public void ForceIdle()
        {
            ChangeState<IdleMoveState>();
            Player.Mover.SetPlanarInput(Vector2.zero, 0f);
        }

        public void ResolveGrounded()
        {
            PlayerInputState input = Player.Input;

            if (input.HasMoveInput && input.RunHeld)
            {
                ChangeState<RunMoveState>();
                return;
            }

            if (input.HasMoveInput)
            {
                ChangeState<WalkMoveState>();
                return;
            }

            ChangeState<IdleMoveState>();
        }

        private void ApplyMovement()
        {
            float speed = Capabilities.PlanarSpeed * Player.SkillFsm.Capabilities.MoveSpeedMultiplier;

            if (!Player.SkillFsm.Capabilities.AllowsMove)
                speed = 0f;

            Player.Mover.SetPlanarInput(Player.Input.Move, speed);
        }

        private sealed class AllowAllMoveFallback : IMoveCapabilities
        {
            public float PlanarSpeed => 0f;
        }
    }
}
