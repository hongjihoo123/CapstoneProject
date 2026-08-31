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

        // 누르고 있다가 뗄 때 발사하는 방식(활 등)을 위한 훅. 기본은 no-op.
        void OnPrimaryPressed();
        void OnPrimaryHeld(float dt);
        void OnPrimaryReleased();

        void ApplyUpgrade(UpgradeData upgrade);

        // true면 버튼을 누르고 있는 동안 매 프레임 PrimaryAttack 호출 (연사 무기용)
        bool PrimaryIsHeld { get; }

        // 단발 판정 (원거리 등 즉시성 낮은 무기용)
        void ExecuteHit();

        // 궤적 판정 (근접 스윙용) - StartHitWindow~EndHitWindow 구간에 OnHitboxTouch 호출됨
        void StartHitWindow();
        void EndHitWindow();
        void OnHitboxTouch(IDamageable target, bool isWeakpoint);
        void InstantReload();

        event Action<string> OnAttackTriggered;
        event Action<float> OnDamageDealt;
        event Action<float> OnHealApplied;
    }
}
