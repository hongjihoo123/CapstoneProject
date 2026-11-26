using System;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public enum SkillSlotId
    {
        Dash = 0,
        Weapon = 1,
        Ultimate = 2,
        Basic1 = 3,
        Basic2 = 4
    }

    public static class SkillSlots
    {
        public static readonly SkillSlotId[] All = (SkillSlotId[])Enum.GetValues(typeof(SkillSlotId));

        public static int Count => All.Length;

        public static bool IsKitSlot(SkillSlotId slot) =>
            slot == SkillSlotId.Dash || slot == SkillSlotId.Weapon || slot == SkillSlotId.Ultimate;

        public static bool IsSwappable(SkillSlotId slot) => !IsKitSlot(slot);

        public static bool CastsOnPress(SkillSlotId slot) => slot == SkillSlotId.Dash;
    }
}
