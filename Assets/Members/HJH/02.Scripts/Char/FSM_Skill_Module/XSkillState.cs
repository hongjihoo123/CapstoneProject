namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // 즉발 버프 (공속/데미지 등). 상태를 점유하지 않고 바로 Idle 복귀.
    public class XSkillState : SkillStateBase
    {
        public override float Cooldown => 10f;
        public override bool IsFinished => true;

        public XSkillState(SkillStateModule owner) : base(owner) { }

        public override void Enter()
        {
            base.Enter();
            // TODO: Owner.Player 쪽 StatusEffect/버프 시스템 호출
        }
    }
}