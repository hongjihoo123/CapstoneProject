using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01_Scripts.FSM.Core;
using RobotWeapons;
using UnityEngine;

namespace Members.KYR._01_Scripts.FSM.Control
{
    public class ControlStateModule : Module, IAfterInitModule
    {
        public PlayerAgent Player { get; private set; }
        public StateMachine Machine { get; private set; }

        public bool IsGameplayAlive => Machine != null && Machine.IsCurrent<AliveControlState>();

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            Player = owner as PlayerAgent;
            Debug.Assert(Player != null, $"{owner.name}의 ControlStateModule은 PlayerAgent 아래여야 합니다.");
        }

        public void AfterInit()
        {
            Machine = new StateMachine();
            Machine.Register(new AliveControlState(this));
            Machine.Register(new StunnedControlState(this));
            Machine.Register(new DeadControlState(this));
            Machine.ChangeState<AliveControlState>();
        }

        public void Tick(float deltaTime)
        {
            Machine.Tick(deltaTime);
        }

        public void ChangeState<T>() where T : IState
        {
            Machine.ChangeState<T>();
        }
    }
}
