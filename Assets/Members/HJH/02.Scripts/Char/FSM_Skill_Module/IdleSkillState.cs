namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public class IdleSkillState : SkillStateBase
    {
        public override float Cooldown => 0f;
        public override bool AllowsReload => true;
        public IdleSkillState(SkillStateModule owner) : base(owner) { }
    }
}