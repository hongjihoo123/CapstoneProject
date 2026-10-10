namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // Payload of SkillStateModule.SkillUsed: which slot cast which skill.
    public readonly struct SkillUsedInfo
    {
        public readonly SkillSlotId Slot;
        public readonly SkillData Data;

        public SkillUsedInfo(SkillSlotId slot, SkillData data)
        {
            Slot = slot;
            Data = data;
        }
    }
}
