using System.Collections.Generic;
using Members.KYR._01_Scripts.Stats;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // Everything a skill may do to the world, split by concern. Skills only see these interfaces,
    // never PlayerAgent, so a skill asset works for any owner that implements ISkillContext.
    public interface ISkillMover
    {
        Vector3 MoveInputDirection { get; }
        Vector3 AimDirection { get; }
        Vector3 AimPoint { get; }
        bool IsDashing { get; }
        void Dash(Vector3 direction, float speed, float duration, float endSlowdown = 0f);
        void SetScriptedMotion(Vector3 velocity);
        // Moves through enemies/props; ends with CancelDash.
        void SetScriptedPosition(Vector3 position);
        void CancelDash();
        bool SweepEnvironment(Vector3 from, Vector3 to);
        bool TryFindGround(Vector3 from, out Vector3 ground);
        // Thin ray against ground/walls only (for projectiles).
        bool LineBlocked(Vector3 from, Vector3 to, out Vector3 hitPoint);
    }

    public interface ISkillEffects
    {
        void SetSpinEffect(GameObject prefab, bool active, float radius);
        void FlashRange(float radius, float duration);
        // Keeps running after the skill state ends (projectiles, delayed blasts, ...).
        void Run(ISkillTimedEffect effect);
        void PlayHitFeel(float hitStop, float shake);
        // Camera only, no hit stop: shake for a set time (charge rumbles, cast feel).
        void PlayShake(float amplitude, float duration);
        // Camera snaps by offset (world) and eases back: recoil = opposite the shot, slam = down.
        void PlayCameraKick(Vector3 offset, float duration = 0.22f);
    }

    public interface ISkillCombatant
    {
        bool TryRegisterHit(IDamageable target);
        IReadOnlyList<IDamageable> OverlapMelee();
        IReadOnlyList<IDamageable> OverlapSphere(Vector3 center, float radius);
        // Everything touching the swept sphere from -> to, nearest to "from" first.
        IReadOnlyList<IDamageable> OverlapCapsule(Vector3 from, Vector3 to, float radius);
        void PushAway(Vector3 center, float radius);
        void PullToward(Vector3 center, float radius, float maxDistance);
        void DealDamage(IDamageable target, float amount, bool isWeakpoint = false);
        void ApplyDamageOverTime(IDamageable target, float damagePerTick, float tickInterval, float duration);
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
    }

    public interface ISkillWeapon
    {
        IWeapon Weapon { get; }
    }

    public interface ISkillContext : ISkillMover, ISkillCombatant, ISkillStats, ISkillVitals, ISkillWeapon, ISkillEffects
    {
        Transform Transform { get; }
        // The equipped kit's passive runtime. Skills that share a resource with it (e.g. runes)
        // cast this to a small interface instead of knowing the concrete passive.
        IPassive Passive { get; }
    }

    public interface ISkillTimedEffect
    {
        // Return false when finished.
        bool Tick(float deltaTime);
    }
}
