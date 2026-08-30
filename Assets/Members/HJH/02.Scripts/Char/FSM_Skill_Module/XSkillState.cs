using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // 즉발 버프 (공속/데미지 등). 상태를 점유하지 않고 바로 Idle 복귀.
    public class XSkillState : SkillStateBase
    {
        private readonly SkillData _data;

        public override float Cooldown => _data.Cooldown;
        public override bool AllowsMove => _data.AllowsMove;
        public override bool AllowsFire => _data.AllowsFire;
        public override float MoveSpeedMultiplier => _data.MoveSpeedMultiplier;
        public override bool IsFinished => Time.time - EnterTime >= _data.Duration;

        public XSkillState(SkillStateModule owner, SkillData data) : base(owner)
        {
            _data = data;
        }

        public override void Enter()
        {
            base.Enter();
            _data.Execute(Owner);
        }
    }
}