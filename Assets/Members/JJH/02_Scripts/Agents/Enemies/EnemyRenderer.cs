using System;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies
{
    public class EnemyRenderer : AgentRenderer
    {
        public event Action OnAttackEvent;
        public event Action OnAnimationEndEvent;

        public void AnimationAttackEvent()
            => OnAttackEvent?.Invoke();

        public void AnimationEndEvent()
            => OnAnimationEndEvent?.Invoke();
    }
}