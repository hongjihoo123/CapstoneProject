using System.Collections.Generic;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public class SkillEffectRunner : MonoBehaviour
    {
        private readonly List<ISkillTimedEffect> _effects = new();
        private readonly List<ISkillTimedEffect> _pending = new();

        public void Add(ISkillTimedEffect effect) => _pending.Add(effect);

        private void Update()
        {
            _effects.AddRange(_pending);
            _pending.Clear();

            float deltaTime = Time.deltaTime;
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                if (!_effects[i].Tick(deltaTime))
                    _effects.RemoveAt(i);
            }
        }
    }
}
