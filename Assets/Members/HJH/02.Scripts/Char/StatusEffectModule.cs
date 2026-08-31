using System;
using System.Collections.Generic;
using UnityEngine;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using RobotWeapons;

namespace Assets.Members.HJH._02.Scripts.Char
{
    public class StatusEffectModule : Module
    {
        private readonly Dictionary<BuffType, float> _multipliers = new();
        private readonly List<(BuffType type, float expireAt)> _active = new();

        private class DamageOverTime
        {
            public IDamageable Target;
            public float DamagePerTick;
            public float TickInterval;
            public float NextTickTime;
            public float ExpireAt;
            public GameObject Source;
        }
        private readonly List<DamageOverTime> _dots = new();

        public void Apply(BuffType type, float multiplier, float duration)
        {
            _multipliers[type] = multiplier;
            _active.Add((type, Time.time + duration));
        }

        public float Get(BuffType type) => _multipliers.TryGetValue(type, out var m) ? m : 1f;
        public void ApplyDamageOverTime(IDamageable target, float damagePerTick, float tickInterval, float duration, GameObject source)
        {
            if (target == null) return;
            _dots.Add(new DamageOverTime
            {
                Target = target,
                DamagePerTick = damagePerTick,
                TickInterval = tickInterval,
                NextTickTime = Time.time + tickInterval,
                ExpireAt = Time.time + duration,
                Source = source
            });
        }

        public void Tick(float deltaTime)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (Time.time < _active[i].expireAt) continue;
                _multipliers[_active[i].type] = 1f;
                _active.RemoveAt(i);
            }

            for (int i = _dots.Count - 1; i >= 0; i--)
            {
                var dot = _dots[i];
                if (dot.Target == null || !dot.Target.IsAlive || Time.time >= dot.ExpireAt)
                {
                    _dots.RemoveAt(i);
                    continue;
                }
                if (Time.time >= dot.NextTickTime)
                {
                    dot.Target.TakeDamage(dot.DamagePerTick, dot.Source);
                    dot.NextTickTime += dot.TickInterval;
                }
            }
        }
    }
}