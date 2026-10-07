using System;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // Int values are serialized in scenes/assets; keep them stable when renaming.
    public enum SkillSlotId
    {
        Dash = 0,       // Space: casts on press (no aim), cancels aiming instead while another skill is held
        Weapon = 1,     // Right click: weapon-specific skill (from WeaponKitData), no element
        Ultimate = 2,   // R: no element; releases the combo finisher
        Basic1 = 3,     // Q: element skill, equipped separately from the kit; casts on press
        Basic2 = 4      // E: element skill; casts on press
    }

    public static class SkillSlots
    {
        public static readonly SkillSlotId[] All = (SkillSlotId[])Enum.GetValues(typeof(SkillSlotId));

        public static int Count => All.Length;

        // Slots filled by WeaponKitData; the others are equipped independently.
        public static bool IsKitSlot(SkillSlotId slot) =>
            slot == SkillSlotId.Dash || slot == SkillSlotId.Weapon || slot == SkillSlotId.Ultimate;

        // Element skills that can be swapped for skills found in the stage (Q/E).
        public static bool IsSwappable(SkillSlotId slot) => !IsKitSlot(slot);

        // Cast immediately on press, without aiming or a range preview.
        public static bool CastsOnPress(SkillSlotId slot) =>
            slot == SkillSlotId.Dash || slot == SkillSlotId.Basic1 || slot == SkillSlotId.Basic2;
    }
}
