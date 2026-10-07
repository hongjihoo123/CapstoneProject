namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // Lets another system borrow skill keys for a moment (e.g. picking the slot to swap a skill into).
    // While Intercepts(slot) is true that key never casts; its press goes to OnInterceptedPress instead.
    public interface ISkillInputInterceptor
    {
        bool Intercepts(SkillSlotId slot);
        void OnInterceptedPress(SkillSlotId slot);
    }
}
