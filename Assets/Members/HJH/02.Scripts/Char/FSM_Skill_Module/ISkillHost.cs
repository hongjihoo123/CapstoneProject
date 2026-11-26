namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public interface ISkillHost
    {
        ISkillContext Context { get; }
        float CooldownReduction { get; }
        void OnSkillEntered();
    }
}
