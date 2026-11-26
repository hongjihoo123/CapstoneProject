using System;

namespace RobotWeapons
{
    public interface IWeapon
    {
        WeaponType Type { get; }
        float CurrentResource { get; }
        float MaxResource { get; }
        bool IsReloading { get; }

        void Equip(IWeaponOwner owner);
        void Unequip();

        void PrimaryAttack();
        void SecondaryAction();
        void Tick(float dt);
        void Reload();
        void SwapMode();

        void OnPrimaryPressed();
        void OnPrimaryHeld(float dt);
        void OnPrimaryReleased();

        void ApplyUpgrade(UpgradeData upgrade);

        bool PrimaryIsHeld { get; }

        void ExecuteHit();

        void StartHitWindow();
        void EndHitWindow();
        void OnHitboxTouch(IDamageable target, bool isWeakpoint);
        void InstantReload();

        event Action<string> OnAttackTriggered;
        event Action<float> OnDamageDealt;
        event Action<float> OnHealApplied;
    }
}
