using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public abstract class SkillStateBase : ISkillState
    {
        protected readonly SkillStateModule Owner;
        protected float EnterTime;
        private float _lastExitTime = float.NegativeInfinity;

        protected SkillStateBase(SkillStateModule owner) => Owner = owner;

        public virtual bool AllowsMove => true;
        public virtual bool AllowsFire => true;
        public virtual bool AllowsReload => false;
        public virtual float MoveSpeedMultiplier => 1f;
        public virtual bool IsFinished => true;
        public abstract float Cooldown { get; }
        public bool IsReady => Time.time - _lastExitTime >= Cooldown;

        public virtual void Enter()
        {
            EnterTime = Time.time;
            Owner.ResetHitTracking();
        }
        public virtual void Exit() => _lastExitTime = Time.time;
        public virtual void Tick(float deltaTime) { }

        public virtual void OnAnimationHitEvent() { }

        // 이거 임시로 둔거 ㅇㅇ, 버그 디버깅 때문에
        public float DebugElapsed => Time.time - EnterTime;
        public virtual float DebugDuration => 0f;
    }
}