using System.Collections.Generic;
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

        public bool StatsReady => _player.Stats != null && _player.Stats.Tree != null;

        public void BeginActivation() => _hitThisActivation.Clear();

        public void Dash(Vector3 direction, float speed, float duration) =>
            _player.Mover.Dash(direction, speed, duration);

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
