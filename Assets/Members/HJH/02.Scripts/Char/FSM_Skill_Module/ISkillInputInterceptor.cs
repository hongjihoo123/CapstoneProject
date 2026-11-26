namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public interface ISkillInputInterceptor
    {
        bool Intercepts(SkillSlotId slot);
        void OnInterceptedPress(SkillSlotId slot);
    }
}
