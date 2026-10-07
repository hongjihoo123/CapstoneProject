using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01_Scripts;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // Which key casts which element with the kit equipped right now ("Q", or "Q/E" when two skills share it).
    // Combos are written in elements; players press keys, so the combo UI shows keys.
    public static class ComboKeyMap
    {
        private static readonly SkillSlotId[] SlotOrder =
        {
            SkillSlotId.Dash, SkillSlotId.Weapon, SkillSlotId.Basic1, SkillSlotId.Basic2, SkillSlotId.Ultimate,
        };

        public static Dictionary<ElementType, string> Build(PlayerAgent player)
        {
            var keys = new Dictionary<ElementType, string>();
            SkillStateModule skills = player != null ? player.SkillFsm : null;
            if (skills == null)
                return keys;

            foreach (SkillSlotId slot in SlotOrder)
            {
                SkillData data = skills.GetSkill(slot);
                if (data == null || !data.TryGetElement(out ElementType element))
                    continue;

                string key = Key(player, slot);
                keys[element] = keys.TryGetValue(element, out string existing) ? $"{existing}/{key}" : key;
            }

            return keys;
        }

        public static string Key(PlayerAgent player, SkillSlotId slot)
        {
            string label = player != null ? player.GetSkillKeyLabel(slot) : string.Empty;
            return string.IsNullOrEmpty(label) ? slot.ToString() : label;
        }

        public static string KeyFor(Dictionary<ElementType, string> keys, ElementType element) =>
            keys.TryGetValue(element, out string key) ? key : "-";
    }
}
