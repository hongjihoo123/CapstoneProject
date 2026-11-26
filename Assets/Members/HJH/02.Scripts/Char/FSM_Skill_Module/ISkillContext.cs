using System.Collections.Generic;
using Members.KYR._01.Scripts.Stats;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public interface ISkillMover
    {
        Vector3 MoveInputDirection { get; }
        Vector3 AimDirection { get; }
        Vector3 AimPoint { get; }
        bool IsDashing { get; }
        void Dash(Vector3 direction, float speed, float duration, float endSlowdown = 0f, bool faceDirection = true);
        bool TryGetDashFacing(out Vector3 direction);
        void LockAim(Vector3 direction, bool lockMove);
        void ReleaseAim();
        bool TryGetAimLock(out Vector3 direction);
        void SetScriptedMotion(Vector3 velocity);
        void SetScriptedPosition(Vector3 position);
        void CancelDash();
        bool SweepEnvironment(Vector3 from, Vector3 to);
        bool TryFindGround(Vector3 from, out Vector3 ground);
        bool LineBlocked(Vector3 from, Vector3 to, out Vector3 hitPoint);
    }

    public interface ISkillEffects
    {
        void SetSpinEffect(GameObject prefab, bool active, float radius);
        void FlashRange(float radius, float duration);
        void Run(ISkillTimedEffect effect);
        void PlayHitFeel(float hitStop, float shake);
        void PlayShake(float amplitude, float duration);
        void PlayCameraKick(Vector3 offset, float duration = 0.22f);
    }

    public interface ISkillCombatant
    {
        bool TryRegisterHit(IDamageable target);
        IReadOnlyList<IDamageable> OverlapMelee();
        IReadOnlyList<IDamageable> OverlapSphere(Vector3 center, float radius);
        IReadOnlyList<IDamageable> OverlapCapsule(Vector3 from, Vector3 to, float radius);
        void PushAway(Vector3 center, float radius);
        void PullToward(Vector3 center, float radius, float maxDistance);
        void Knockback(IDamageable target, Vector3 direction, float distance);
        void DealDamage(IDamageable target, float amount, bool isWeakpoint = false);
        void DealDamage(IDamageable target, float amount, SkillData skill);
        void ApplyDamageOverTime(IDamageable target, float damagePerTick, float tickInterval, float duration);
        void BoostSkillDamage(SkillData skill, float scale, float seconds);
    }

    public interface ISkillStats
    {
        bool StatsReady { get; }
        void AddStatModifier(PlayerStatId id, StatModifier modifier);
        void RemoveStatModifiers(object source);
    }

    public interface ISkillVitals
    {
        void Heal(float amount);
        void AddShield(float amount);
        event System.Action<float, GameObject> DamageTaken;
    }

    public interface ISkillCooldowns
    {
        void ReduceCooldown(SkillSlotId slot, float seconds);
        float GetCooldownRemaining(SkillSlotId slot);
    }

    public interface ISkillWeapon
    {
        IWeapon Weapon { get; }
    }

    public interface ISkillContext : ISkillMover, ISkillCombatant, ISkillStats, ISkillVitals, ISkillWeapon, ISkillEffects, ISkillCooldowns
    {
        Transform Transform { get; }
        SkillData ActiveSkill { get; set; }
        IPassive Passive { get; }
    }

    public interface ISkillTimedEffect
    {
        bool Tick(float deltaTime);
    }
}
