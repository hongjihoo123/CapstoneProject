using System.Collections.Generic;
using Members.KYR._01_Scripts.Stats;
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
        void Dash(Vector3 direction, float speed, float duration, float endSlowdown = 0f);
        void SetScriptedMotion(Vector3 velocity);
        void CancelDash();
    }

    public interface ISkillEffects
    {
        void SetSpinEffect(GameObject prefab, bool active, float radius);
        void FlashRange(float radius, float duration);
    }

    public interface ISkillCombatant
    {
        bool TryRegisterHit(IDamageable target);
        IReadOnlyList<IDamageable> OverlapMelee();
        IReadOnlyList<IDamageable> OverlapSphere(Vector3 center, float radius);
        void DealDamage(IDamageable target, float amount, bool isWeakpoint = false);
        void ApplyDamageOverTime(IDamageable target, float damagePerTick, float tickInterval, float duration);
    }

    public interface ISkillStats
    {
        bool StatsReady { get; }
        void AddStatModifier(PlayerStatId id, StatModifier modifier);
        void RemoveStatModifiers(object source);
    }

    public interface ISkillWeapon
    {
        IWeapon Weapon { get; }
    }

    public interface ISkillContext : ISkillMover, ISkillCombatant, ISkillStats, ISkillWeapon, ISkillEffects
    {
        Transform Transform { get; }
    }
}
