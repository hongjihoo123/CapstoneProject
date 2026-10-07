using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Element;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // Ties the combo chain to the skill bar so players read combos where they press:
    //  - every skill that would cast the NEXT element pulses in that element's color (proc glow),
    //  - when a combo lands, the skills that formed it burst gold.
    // Re-evaluated every frame, so kit / skill swaps are picked up without extra wiring.
    public class ComboSkillBarLink : MonoBehaviour
    {
        [SerializeField] private ElementComboChain chain;
        [SerializeField] private SkillBarView skillBar;
        [SerializeField] private ElementPalette palette;
        [SerializeField] private Color burstColor = new Color(1f, 0.83f, 0.3f);

        private void OnEnable() => chain.ComboLanded += HandleComboLanded;

        private void OnDisable() => chain.ComboLanded -= HandleComboLanded;

        private void LateUpdate()
        {
            SkillStateModule skills = chain.Skills;
            if (skills == null)
                return;

            bool onRoute = chain.TryGetNext(out ElementType next, out _, out _);
            Color color = onRoute && palette.TryGet(next, out ElementPalette.Entry entry) ? entry.color : Color.white;

            foreach (SkillBarView.SlotBinding binding in skillBar.Bindings)
            {
                if (binding.view == null)
                    continue;

                SkillData data = skills.GetSkill(binding.slot);
                bool hinted = onRoute && data != null && data.TryGetElement(out ElementType element) && element == next;
                binding.view.SetComboHint(hinted, color);
            }
        }

        private void HandleComboLanded(ElementComboChain.LandedCombo landed)
        {
            for (int i = landed.StartIndex; i < landed.StartIndex + landed.Combo.Length; i++)
            {
                if (i >= 0 && i < chain.Inputs.Count && skillBar.TryGetView(chain.Inputs[i].Slot, out SkillSlotView view))
                    view.ComboBurst(burstColor);
            }
        }
    }
}
