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
        // faceDirection: 대쉬하는 동안 몸을 날아가는 방향으로 돌림. 반동(뒤로 밀리기)처럼 조준 방향을 유지해야 하면 false
        void Dash(Vector3 direction, float speed, float duration, float endSlowdown = 0f, bool faceDirection = true);
        // 지금 대쉬 중이고 날아가는 방향을 봐야 하면 true (조준 컨트롤러가 이걸 보고 몸을 돌림)
        bool TryGetDashFacing(out Vector3 direction);
        // 조준 잠금 (충전 사격 등): 몸과 조준을 direction 으로 고정해서 마우스로 안 돌아감.
        // lockMove 면 걷기도 막음 (대쉬/반동은 그대로). 끝나면 꼭 ReleaseAim
        void LockAim(Vector3 direction, bool lockMove);
        void ReleaseAim();
        bool TryGetAimLock(out Vector3 direction);
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
        // Pushes one target along direction (flattened) by distance.
        void Knockback(IDamageable target, Vector3 direction, float distance);
        void DealDamage(IDamageable target, float amount, bool isWeakpoint = false);
        void DealDamage(IDamageable target, float amount, SkillData skill);
        void ApplyDamageOverTime(IDamageable target, float damagePerTick, float tickInterval, float duration);
        // 이 스킬이 주는 피해를 seconds 동안 scale 배 (피니시 강화). 늦게 터지는 투사체/폭발도 포함되게 시간으로 줌
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
        // 나를 때린 피해량 + 때린 오브젝트 (모르면 null)
        event System.Action<float, GameObject> DamageTaken;
    }

    // 다른 칸 스킬 쿨타임 (과충전, 순풍)
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
