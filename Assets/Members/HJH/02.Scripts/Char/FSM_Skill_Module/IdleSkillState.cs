namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // No skill running: everything allowed, no cooldown.
    public class IdleSkillState : SkillStateBase
    {
        public override float Cooldown => 0f;
        public override bool AllowsReload => true;
        public IdleSkillState(ISkillHost host) : base(host) { }
    }
}
