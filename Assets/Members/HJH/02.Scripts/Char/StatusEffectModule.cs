using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Members.JJH._02_Scripts.Systems.ModuleSystem;

namespace Assets.Members.HJH._02.Scripts.Char
{
    public class StatusEffectModule : Module
    {
        private readonly Dictionary<BuffType, float> _multipliers = new();
        private readonly List<(BuffType type, float expireAt)> _active = new();

        public void Apply(BuffType type, float multiplier, float duration)
        {
            _multipliers[type] = multiplier;
            _active.Add((type, Time.time + duration));
        }

        public float Get(BuffType type) => _multipliers.TryGetValue(type, out var m) ? m : 1f;

        public void Tick(float deltaTime)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (Time.time < _active[i].expireAt) continue;
                _multipliers[_active[i].type] = 1f;
                _active.RemoveAt(i);
            }
        }
    }
}
