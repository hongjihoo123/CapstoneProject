using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public abstract class SkillStateBase : ISkillState
    {
        protected readonly ISkillHost Host;
        protected float EnterTime;
        private float _lastExitTime = float.NegativeInfinity;

        protected SkillStateBase(ISkillHost host) => Host = host;

        public virtual bool AllowsMove => true;
        public virtual bool AllowsFire => true;
        public virtual bool AllowsReload => false;
        public virtual float MoveSpeedMultiplier => 1f;
        public virtual bool IsFinished => true;
        public abstract float Cooldown { get; }
        public bool IsReady => Time.time - _lastExitTime >= EffectiveCooldown;
        public float Elapsed => Time.time - EnterTime;
        public float CooldownRemaining => Mathf.Max(0f, EffectiveCooldown - (Time.time - _lastExitTime));

        protected float EffectiveCooldown
        {
            get
            {
                float cooldown = Cooldown;
                return cooldown <= 0f ? 0f : cooldown * (1f - Host.CooldownReduction);
            }
        }

        public virtual void Enter()
        {
            EnterTime = Time.time;
            Host.OnSkillEntered();
        }

        public virtual void Exit() => _lastExitTime = Time.time;
        public virtual void Tick(float deltaTime) { }
        public virtual void OnAnimationHitEvent() { }
    }
}
