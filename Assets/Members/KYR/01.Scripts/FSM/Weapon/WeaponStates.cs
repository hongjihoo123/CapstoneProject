using Members.KYR._01_Scripts.FSM.Core;
using static UnityEngine.UI.GridLayoutGroup;

namespace Members.KYR._01_Scripts.FSM.Weapon
{
    public abstract class WeaponState : IState, IWeaponCapabilities
    {
        protected readonly WeaponStateModule Module;

        protected WeaponState(WeaponStateModule module)
        {
            Module = module;
        }

        public abstract bool AllowsFire { get; }
        public abstract bool AllowsAim { get; }
        public abstract bool LocksSprint { get; }
        public virtual float MoveSpeedMultiplier => 1f;

        public virtual void Enter() { }

        public virtual void Exit() { }

        public abstract void Tick(float deltaTime);
    }

    public sealed class IdleWeaponState : WeaponState
    {
        public IdleWeaponState(WeaponStateModule module) : base(module) { }

        public override bool AllowsFire => true;
        public override bool AllowsAim => true;
        public override bool LocksSprint => false;

        public override void Tick(float deltaTime)
        {
            Module.ResolveReady();
        }
    }

    public sealed class AimWeaponState : WeaponState
    {
        public AimWeaponState(WeaponStateModule module) : base(module) { }

        public override bool AllowsFire => true;
        public override bool AllowsAim => true;
        public override bool LocksSprint => true;

        public override void Enter()
        {
            Module.Player.Weapon.SetAiming(true);
            //Module.Player.OnAimStateChanged?.Invoke(true);
        }

        public override void Exit()
        {
            Module.Player.Weapon.SetAiming(false);
            //Module.Player.OnAimStateChanged?.Invoke(false);
        }

        public override void Tick(float deltaTime)
        {
            if (!Module.Player.MoveFsm.Capabilities.CanAim)
            {
                Module.ChangeState<IdleWeaponState>();
                return;
            }

            Module.ResolveReady();
        }
    }

    public sealed class ReloadWeaponState : WeaponState
    {
        public ReloadWeaponState(WeaponStateModule module) : base(module) { }

        public override bool AllowsFire => false;
        public override bool AllowsAim => false;
        public override bool LocksSprint => false;
        public override float MoveSpeedMultiplier => 0.7f;

        public override void Enter()
        {
            Module.Player.Weapon.RequestReload();
        }

        public override void Tick(float deltaTime)
        {
            if (Module.Player.Weapon.Weapon == null || !Module.Player.Weapon.Weapon.IsReloading)
                Module.ResolveReady();
        }
    }
}
