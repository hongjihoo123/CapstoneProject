using UnityEngine;

namespace RobotWeapons
{
    public interface IWeaponOwner
    {
        Transform AimOrigin { get; }
        Transform MuzzleOrigin { get; }
        void ApplyDamageTo(IDamageable target, float amount, bool isWeakpoint = false);
        void SetWeaponHitboxActive(bool active);
    }

    public interface IHealCapable
    {
        void ApplyHealTo(IHealable target, float amount);
        void Heal(float amount);
    }

    public interface IRecoilCapable
    {
        void ApplyRecoil(float pitchDelta, float yawDelta, float dutchImpulse = 0f);
        void SetMoveSpeedMultiplier(float multiplier);
    }
}