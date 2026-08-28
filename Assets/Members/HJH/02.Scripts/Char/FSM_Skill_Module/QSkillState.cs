using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public class QSkillState : SkillStateBase
    {
        private const float Duration = 0.25f;

        public override float Cooldown => 3f;
        public override bool AllowsMove => false;
        public override bool AllowsFire => false;
        public override bool IsFinished => Time.time - EnterTime >= Duration;

        public QSkillState(SkillStateModule owner) : base(owner) { }

        public override void Enter()
        {
            base.Enter();
            // TODO: Owner.Player.Mover 쪽 대쉬 임펄스 API 연결
        }
    }
}