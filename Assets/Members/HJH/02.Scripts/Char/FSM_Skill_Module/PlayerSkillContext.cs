using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using Members.KYR._01_Scripts;
using Members.KYR._01_Scripts.Stats;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public sealed class PlayerSkillContext : ISkillContext
    {
        private static readonly IReadOnlyList<IDamageable> NoTargets = new List<IDamageable>();
        private static readonly Collider[] OverlapBuffer = new Collider[64];

        private readonly PlayerAgent _player;
        private readonly LayerMask _targetMask;
        private readonly HashSet<IDamageable> _hitThisActivation = new();

        public PlayerSkillContext(PlayerAgent player, LayerMask targetMask)
        {
            _player = player;
            _targetMask = targetMask;
        }

        public Transform Transform => _player.transform;
        public IWeapon Weapon => _player.Weapon.Weapon;

        public Vector3 MoveInputDirection
        {
            get
            {
                PlayerInputState input = _player.Input;
                return input.HasMoveInput ? _player.Mover.ToWorldMove(input.Move) : Vector3.zero;
            }
        }

        public Vector3 AimDirection
        {
            get
            {
                Vector3 direction = _player.AimDirection;
                direction.y = 0f;
                return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            }
        }

        public Vector3 AimPoint => _player.AimPoint;

        public bool IsDashing => _player.Mover.IsDashing;

        public bool StatsReady => _player.Stats != null && _player.Stats.Tree != null;

        public void BeginActivation() => _hitThisActivation.Clear();

        public void Dash(Vector3 direction, float speed, float duration, float endSlowdown = 0f) =>
            _player.Mover.Dash(direction, speed, duration, endSlowdown);

        public void SetSpinEffect(GameObject prefab, bool active, float radius)
        {
            SkillSpinEffect effect = _player.GetComponent<SkillSpinEffect>();
            if (effect == null)
            {
                if (!active)
                    return;

                effect = _player.gameObject.AddComponent<SkillSpinEffect>();
            }

            if (active)
                effect.Play(prefab, radius);
            else
                effect.Stop();
        }

        public void FlashRange(float radius, float duration) =>
            AttackAreaBus.Raise(AttackArea.Circle(AttackAreaKind.Skill, _player.transform.position + Vector3.up, radius, duration));

        public void SetScriptedMotion(Vector3 velocity) => _player.Mover.SetScriptedMotion(velocity);

        public void CancelDash() => _player.Mover.CancelDash();

        public bool TryRegisterHit(IDamageable target) => _hitThisActivation.Add(target);

        public IReadOnlyList<IDamageable> OverlapMelee()
        {
            SkillOverlapHitbox hitbox = _player.Weapon.SkillOverlapHitbox;
            if (hitbox == null)
            {
                Debug.LogWarning($"{_player.name}의 PlayerWeapon에 Skill Overlap Hitbox가 연결돼있지 않습니다.");
                return NoTargets;
            }

            return hitbox.Overlap();
        }

        public IReadOnlyList<IDamageable> OverlapSphere(Vector3 center, float radius)
        {
            var results = new List<IDamageable>();
            int count = Physics.OverlapSphereNonAlloc(center, radius, OverlapBuffer, _targetMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                IDamageable target = OverlapBuffer[i].GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive && !results.Contains(target))
                    results.Add(target);
            }

            return results;
        }

        public void DealDamage(IDamageable target, float amount, bool isWeakpoint = false) =>
            _player.ApplyDamageTo(target, amount, isWeakpoint);

        public void ApplyDamageOverTime(IDamageable target, float damagePerTick, float tickInterval, float duration)
        {
            StatusEffectModule status = _player.GetModule<StatusEffectModule>();
            status?.ApplyDamageOverTime(target, damagePerTick, tickInterval, duration, _player.gameObject);
        }

        public void AddStatModifier(PlayerStatId id, StatModifier modifier) => _player.Stats.AddModifier(id, modifier);

        public void RemoveStatModifiers(object source) => _player.Stats.RemoveModifiers(source);
    }
}
