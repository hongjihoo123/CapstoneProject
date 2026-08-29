using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public class ESkillState : SkillStateBase
    {
        private readonly SkillData _data;

        public override float Cooldown => _data.Cooldown;
        public override bool AllowsMove => _data.AllowsMove;
        public override bool AllowsFire => _data.AllowsFire;
        public override float MoveSpeedMultiplier => _data.MoveSpeedMultiplier;
        public override bool IsFinished => Time.time - EnterTime >= _data.Duration;
        public override float DebugDuration => _data.Duration;

        public ESkillState(SkillStateModule owner, SkillData data) : base(owner)
        {
            _data = data;
        }

        public override void Enter()
        {
            base.Enter();
            _data.Execute(Owner);
        }

        public override void OnAnimationHitEvent()
        {
            _data.OnAnimationHitEvent(Owner);
        }
    }
}