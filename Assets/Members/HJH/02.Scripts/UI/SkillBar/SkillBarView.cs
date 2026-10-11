using Assets.Members.HJH._02.Scripts.Element;
using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01_Scripts;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 아래 스킬바임. 패시브 + 슬롯마다 SkillSlotView 하나씩
    // 매 프레임 스킬 상태 읽어서 그려서, 스킬이나 캐릭터 바뀌어도 알아서 따라감
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

        private SkillStateModule _subscribed;

        private void Start()
        {
            if (player == null)
                return;

            _subscribed = player.SkillFsm;
            if (_subscribed != null)
            {
                _subscribed.SkillDenied += HandleSkillDenied;
                _subscribed.SkillReady += HandleSkillReady;
            }

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

        private void OnDestroy()
        {
            if (_subscribed != null)
            {
                _subscribed.SkillDenied -= HandleSkillDenied;
                _subscribed.SkillReady -= HandleSkillReady;
            }
        }

        private void HandleSkillDenied(SkillSlotId slot)
        {
            if (TryGetView(slot, out SkillSlotView view))
                view.Deny();
        }

        private void HandleSkillReady(SkillSlotId slot)
        {
            if (TryGetView(slot, out SkillSlotView view))
                view.PlayReady();
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

                float remaining = skills.GetCooldownRemaining(binding.slot);
                int charges = skills.GetCharges(binding.slot);
                float lockLeft = skills.GetLockRemaining(binding.slot);
                // 교체 직후 잠금이 실제로 막고 있을 땐 그것도 쿨타임처럼 보여줌
                if (lockLeft > 0f && (charges > 0 || lockLeft >= remaining))
                    binding.view.SetCooldown(lockLeft, skills.GetLockDuration(binding.slot), 0, 1);
                else
                    binding.view.SetCooldown(remaining, skills.GetCooldownDuration(binding.slot), charges, skills.GetMaxCharges(binding.slot));
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
