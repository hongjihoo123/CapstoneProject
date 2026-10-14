using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01_Scripts.FSM.Core;
using UnityEngine;

namespace Members.KYR._01_Scripts.FSM.Weapon
{
    public class WeaponStateModule : Module, IAfterInitModule
    {
        private static readonly ReadyWeaponFallback Fallback = new();

        public PlayerAgent Player { get; private set; }
        public StateMachine Machine { get; private set; }

        public IWeaponCapabilities Capabilities => Fallback;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            Player = owner as PlayerAgent;
            Debug.Assert(Player != null, $"{owner.name}의 WeaponStateModule은 PlayerAgent 아래여야 합니다.");
        }

        public void AfterInitalize()
        {
            Machine = new StateMachine();
            Machine.Register(new IdleWeaponState());
            Machine.ChangeState<IdleWeaponState>();
        }

        public void Tick(float deltaTime)
        {
            Machine.Tick(deltaTime);
            TryFire();
        }

        public void ChangeState<T>() where T : IState
        {
            Machine.ChangeState<T>();
        }

        public void ForceIdle()
        {
            ChangeState<IdleWeaponState>();
        }

        private void TryFire()
        {
            if (Player.SkillFsm == null || Player.MoveFsm == null)
                return;

            if (!Capabilities.AllowsFire || !Player.SkillFsm.Capabilities.AllowsFire
                || Player.SkillFsm.IsAimingSkill)
                return;

            Player.Weapon.TryFire(Player.Input.FireHeld, Player.Input.FirePressed);
        }

        private sealed class ReadyWeaponFallback : IWeaponCapabilities
        {
            public bool AllowsFire => true;
            public bool AllowsAim => true;
            public bool LocksSprint => false;
            public float MoveSpeedMultiplier => 1f;
        }
    }

    public sealed class IdleWeaponState : IState
    {
        public void Enter() { }

        public void Exit() { }

        public void Tick(float deltaTime) { }
    }
}
