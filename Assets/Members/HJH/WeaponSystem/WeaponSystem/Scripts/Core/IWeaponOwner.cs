using UnityEngine;

namespace RobotWeapons
{
    public interface IWeaponOwner
    {
        Transform AimOrigin { get; }
        Transform MuzzleOrigin { get; }

        void ApplyDamageTo(IDamageable target, float amount, bool isWeakpoint = false);
        void ApplyHealTo(IHealable target, float amount);
        void Heal(float amount);
        void SetMoveSpeedMultiplier(float multiplier);
        void ApplyRecoil(float pitchDelta, float yawDelta, float dutchImpulse = 0f);
        void SetWeaponHitboxActive(bool active);
    }
}
