using System;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public enum SkillSlotId
    {
        Dash = 0,
        Basic = 1,
        Ultimate = 2,
        Free1 = 3,
        Free2 = 4
    }

    public static class SkillSlots
    {
        public static readonly SkillSlotId[] All = (SkillSlotId[])Enum.GetValues(typeof(SkillSlotId));

        public static int Count => All.Length;

        public static bool IsFree(SkillSlotId slot) => slot == SkillSlotId.Free1 || slot == SkillSlotId.Free2;
    }
}
