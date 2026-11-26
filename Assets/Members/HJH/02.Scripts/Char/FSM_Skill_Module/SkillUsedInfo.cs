namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
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
