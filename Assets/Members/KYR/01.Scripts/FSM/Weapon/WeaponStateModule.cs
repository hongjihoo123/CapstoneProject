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

        public IWeaponCapabilities Capabilities => Machine?.Current as IWeaponCapabilities ?? Fallback;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            Player = owner as PlayerAgent;
            Debug.Assert(Player != null, $"{owner.name}의 WeaponStateModule은 PlayerAgent 아래여야 합니다.");
        }

        public void AfterInit()
        {
            Machine = new StateMachine();
            Machine.Register(new IdleWeaponState(this));
            Machine.Register(new AimWeaponState(this));
            Machine.Register(new ReloadWeaponState(this));
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

        public void ResolveReady()
        {
            PlayerInputState input = Player.Input;

            if (Player.Weapon.Weapon != null && Player.Weapon.Weapon.IsReloading)
            {
                ChangeState<ReloadWeaponState>();
                return;
            }

            if (input.ReloadPressed && Player.Weapon.CanStartReload)
            {
                ChangeState<ReloadWeaponState>();
                return;
            }

            bool wantAim = input.AimHeld && Player.MoveFsm.Capabilities.CanAim;
            if (wantAim)
            {
                ChangeState<AimWeaponState>();
                return;
            }

            ChangeState<IdleWeaponState>();
        }

        private void TryFire()
        {
            if (!Capabilities.AllowsFire || !Player.MoveFsm.Capabilities.CanFire)
                return;

            Player.Weapon.TryFire(Player.Input.FireHeld, Player.Input.FirePressed);
        }

        private sealed class ReadyWeaponFallback : IWeaponCapabilities
        {
            public bool AllowsFire => false;
            public bool AllowsAim => true;
            public bool LocksSprint => false;
            public float MoveSpeedMultiplier => 1f;
        }
    }
}
