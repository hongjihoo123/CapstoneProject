using UnityEngine;

namespace RobotWeapons
{
    public interface IWeaponOwner
    {
        Transform AttackOrigin { get; }

        void ApplyDamageTo(IDamageable target, float amount);
        void ApplyHealTo(IHealable target, float amount);
        void Heal(float amount);
        void SetMoveSpeedMultiplier(float multiplier);
        void SetWeaponHitboxActive(bool active);
    }
}
