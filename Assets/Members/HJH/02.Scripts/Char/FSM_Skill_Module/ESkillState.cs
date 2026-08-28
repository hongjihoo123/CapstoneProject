using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // 검 공격 (정커퀸 E 스타일)
    public class ESkillState : SkillStateBase
    {
        private const float Duration = 0.6f;

        public override float Cooldown => 6f;
        public override bool AllowsMove => false;
        public override bool AllowsFire => false;
        public override bool IsFinished => Time.time - EnterTime >= Duration;

        public ESkillState(SkillStateModule owner) : base(owner) { }

        public override void Enter()
        {
            base.Enter();
            // TODO: 애니메이션 트리거 + 타이밍 맞춰 히트 판정
        }
    }
}