namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // What a skill state needs from its owner (SkillStateModule): the context, cooldown reduction, an enter hook.
    public interface ISkillHost
    {
        ISkillContext Context { get; }
        float CooldownReduction { get; }
        void OnSkillEntered();
    }
}
