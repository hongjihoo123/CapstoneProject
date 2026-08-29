using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // Q 슬롯. 실제 동작은 SkillData가 결정 (대쉬든 뭐든 캐릭터별로 교체 가능).
    public class QSkillState : SkillStateBase
    {
        private readonly SkillData _data;

        public override float Cooldown => _data.Cooldown;
        public override bool AllowsMove => _data.AllowsMove;
        public override bool AllowsFire => _data.AllowsFire;
        public override float MoveSpeedMultiplier => _data.MoveSpeedMultiplier;
        public override bool IsFinished => Time.time - EnterTime >= _data.Duration;

        public QSkillState(SkillStateModule owner, SkillData data) : base(owner)
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