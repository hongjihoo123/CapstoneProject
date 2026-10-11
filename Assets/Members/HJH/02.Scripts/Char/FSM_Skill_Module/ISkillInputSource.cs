namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // Per-frame skill input as the skill system sees it (KYR's PlayerInputState implements it).
    public interface ISkillInputSource
    {
        bool WasSkillPressed(SkillSlotId slot);
        bool IsSkillHeld(SkillSlotId slot);
        bool WasSkillReleased(SkillSlotId slot);
        bool WasCancelPressed();
        bool WasJumpPressed();
    }
}
