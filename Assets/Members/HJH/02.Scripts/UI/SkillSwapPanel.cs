using System;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.SkillSwap;
using TMPro;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // Opens while SkillSwapInteractor is choosing (after F): the skill on the ground on the left,
    // the player's swappable skills (Q/E) on the right. Hovering any card shows its tooltip.
    public class SkillSwapPanel : MonoBehaviour
    {
        [Serializable]
        public struct SlotCard
        {
            public SkillSlotId slot;
            public SkillCardView card;
        }

        [SerializeField] private SkillSwapInteractor interactor;
        [SerializeField] private ElementPalette palette;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform body;
        [SerializeField] private SkillCardView groundCard;
        [SerializeField] private SlotCard[] slotCards;
        [SerializeField] private SkillTooltipView tooltip;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private float openSpeed = 9f;
        [SerializeField, Tooltip("The box sits on the screen point this high above the orb (pivot bottom-center).")]
        private float worldHeight = 2.2f;

        private SkillPickup _pickup;
        private Transform _follow;
        private SkillCardView _hovered;
        private float _open;

        private void Start()
        {
            if (interactor == null)
                interactor = FindFirstObjectByType<SkillSwapInteractor>();
            if (interactor != null)
                interactor.ChoosingChanged += HandleChoosingChanged;

            groundCard.SetKey("바닥");
            groundCard.Hovered += HandleHovered;
            foreach (SlotCard binding in slotCards)
            {
                binding.card.SetKey(interactor != null ? interactor.KeyLabel(binding.slot) : binding.slot.ToString());
                binding.card.Hovered += HandleHovered;
            }

            if (hintText != null && interactor != null)
            {
                var keys = new System.Text.StringBuilder();
                foreach (SlotCard binding in slotCards)
                    keys.Append($"<color=#FFD36B>[{interactor.KeyLabel(binding.slot)}]</color> ");
                hintText.text = $"{keys}교체     <color=#FFD36B>[{interactor.InteractKeyLabel}]</color> 취소";
            }

            Apply(0f);
        }

        private void OnDestroy()
        {
            if (interactor != null)
                interactor.ChoosingChanged -= HandleChoosingChanged;
        }

        private void HandleChoosingChanged(SkillPickup pickup)
        {
            _pickup = pickup;
            if (pickup != null)
                _follow = pickup.transform;
            else
            {
                _hovered = null;
                tooltip.Hide();
            }
        }

        private void HandleHovered(SkillCardView card, bool hovered)
        {
            if (hovered)
                _hovered = card;
            else if (_hovered == card)
                _hovered = null;
        }

        private void LateUpdate()
        {
            bool open = _pickup != null;
            _open = Mathf.MoveTowards(_open, open ? 1f : 0f, Time.unscaledDeltaTime * openSpeed);
            Apply(_open);
            FollowOrb();

            if (!open)
                return;

            SkillData ground = _pickup.Skill;
            groundCard.Bind(ground, palette, _pickup.Cooldown.Remaining, ground != null ? ground.Cooldown : 0f);

            SkillStateModule skills = interactor.Skills;
            foreach (SlotCard binding in slotCards)
            {
                SkillSlotId slot = binding.slot;
                bool blocked = skills.GetCharges(slot) <= 0;
                binding.card.Bind(skills.GetSkill(slot), palette,
                    blocked ? skills.GetCooldownRemaining(slot) : 0f, skills.GetCooldownDuration(slot));
            }

            if (_hovered != null && _hovered.Data != null)
                tooltip.Show(_hovered.Data, palette, _hovered.Rect, Footer(_hovered));
            else
                tooltip.Hide();
        }

        private string Footer(SkillCardView card)
        {
            float remaining;
            string where;
            if (card == groundCard)
            {
                remaining = _pickup.Cooldown.Remaining;
                where = "바닥에 놓인 스킬";
            }
            else
            {
                SkillSlotId slot = SlotOf(card);
                SkillStateModule skills = interactor.Skills;
                remaining = skills.GetCharges(slot) <= 0 ? skills.GetCooldownRemaining(slot) : 0f;
                where = $"현재 [{interactor.KeyLabel(slot)}] 슬롯";
            }

            return remaining > 0f ? $"{where}   <color=#FF8A6B>재사용까지 {remaining:0.0}초</color>" : where;
        }

        private SkillSlotId SlotOf(SkillCardView card)
        {
            foreach (SlotCard binding in slotCards)
            {
                if (binding.card == card)
                    return binding.slot;
            }

            return default;
        }

        // Keeps the box over the orb (screen-space overlay canvas), inside the screen.
        private void FollowOrb()
        {
            Camera cam = Camera.main;
            if (_follow == null || cam == null || _open <= 0f)
                return;

            Vector3 screen = cam.WorldToScreenPoint(_follow.position + Vector3.up * worldHeight);
            if (screen.z < 0f)
                return;

            var rect = (RectTransform)transform;
            Vector3 scale = rect.lossyScale;
            float halfWidth = rect.rect.width * 0.5f * scale.x;
            float height = rect.rect.height * scale.y;
            screen.x = Mathf.Clamp(screen.x, halfWidth + 8f, Screen.width - halfWidth - 8f);
            screen.y = Mathf.Clamp(screen.y, 8f, Screen.height - height - 8f);
            screen.z = 0f;
            rect.position = screen;
        }

        // 0 = closed, 1 = open: fade plus a small pop and rise.
        private void Apply(float t)
        {
            float eased = 1f - (1f - t) * (1f - t);
            group.alpha = eased;
            group.blocksRaycasts = t > 0.5f;
            group.interactable = t > 0.5f;
            if (body != null)
            {
                body.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, eased);
                body.anchoredPosition = new Vector2(0f, Mathf.Lerp(-18f, 0f, eased));
            }
        }
    }
}
