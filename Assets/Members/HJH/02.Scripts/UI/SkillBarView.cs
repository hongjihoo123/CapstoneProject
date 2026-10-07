using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01_Scripts;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // Bottom skill bar: passive + one SkillSlotView per bound slot. Reads the player's
    // SkillStateModule every frame, so re-equipping a kit or skill updates the bar by itself.
    public class SkillBarView : MonoBehaviour
    {
        [Serializable]
        public struct SlotBinding
        {
            public SkillSlotId slot;
            public SkillSlotView view;
            [Tooltip("Drawn instead of the key text (e.g. a Space bar cap). Optional.")]
            public Sprite keyIcon;
        }

        [SerializeField] private PlayerAgent player;
        [SerializeField] private ElementPalette palette;
        [SerializeField] private SlotBinding[] slots;
        [SerializeField] private Image passiveIcon;

        public bool TryGetView(SkillSlotId slot, out SkillSlotView view)
        {
            foreach (SlotBinding binding in slots)
            {
                if (binding.slot == slot && binding.view != null)
                {
                    view = binding.view;
                    return true;
                }
            }

            view = null;
            return false;
        }

        public IEnumerable<SlotBinding> Bindings => slots;

        private void Start()
        {
            if (player == null)
                return;

            foreach (SlotBinding binding in slots)
            {
                if (binding.view != null)
                {
                    if (binding.keyIcon != null)
                        binding.view.SetKeyIcon(binding.keyIcon);
                    else
                        binding.view.SetKeyLabel(player.GetSkillKeyLabel(binding.slot));
                }
            }
        }

        private void LateUpdate()
        {
            SkillStateModule skills = player != null ? player.SkillFsm : null;
            if (skills == null)
                return;

            foreach (SlotBinding binding in slots)
            {
                if (binding.view == null)
                    continue;

                binding.view.Bind(skills.GetSkill(binding.slot), palette);
                binding.view.SetCooldown(skills.GetCooldownRemaining(binding.slot), skills.GetCooldownDuration(binding.slot),
                    skills.GetCharges(binding.slot), skills.GetMaxCharges(binding.slot));
            }

            if (passiveIcon != null)
            {
                Sprite sprite = skills.Passive != null ? skills.Passive.Icon : null;
                passiveIcon.sprite = sprite;
                passiveIcon.enabled = sprite != null;
            }
        }
    }
}
