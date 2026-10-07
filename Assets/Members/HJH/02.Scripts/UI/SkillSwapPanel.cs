using System;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.SkillSwap;
using DG.Tweening;
using Members.JJH._02_Scripts.ElementsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("Swap flight")]
        [SerializeField, Tooltip("Found in the scene when empty. The swapped-in icon flies into its slot.")]
        private SkillBarView skillBar;
        [SerializeField, Tooltip("Hidden Image on the HUD canvas root that carries the icon. Without it the slot just pops.")]
        private Image flightIcon;
        [SerializeField] private float flightDuration = 0.38f;
        [SerializeField] private Ease flightEase = Ease.InOutCubic;
        [SerializeField, Tooltip("Upward bulge of the flight path, in canvas units.")]
        private float flightArc = 90f;
        [SerializeField, Tooltip("Size relative to the card / slot it flies between.")]
        private float flightIconScale = 0.8f;
        [SerializeField, Tooltip("Extra size at the top of the arc.")]
        private float flightSwell = 0.25f;
        [SerializeField, Tooltip("Z tilt at the top of the arc, degrees.")]
        private float flightTilt = -12f;

        private Tween _flight;

        private SkillPickup _pickup;
        private Transform _follow;
        private SkillCardView _hovered;
        private float _open;

        private void Start()
        {
            if (interactor == null)
                interactor = FindFirstObjectByType<SkillSwapInteractor>();
            if (interactor != null)
            {
                interactor.ChoosingChanged += HandleChoosingChanged;
                interactor.Swapped += HandleSwapped;
            }
            if (skillBar == null)
                skillBar = FindFirstObjectByType<SkillBarView>();

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
            {
                interactor.ChoosingChanged -= HandleChoosingChanged;
                interactor.Swapped -= HandleSwapped;
            }
        }

        // The ground card's icon flies into the skill bar slot it was swapped into, then the slot pops.
        private void HandleSwapped(SkillSlotId slot, SkillData outgoing, SkillData incoming)
        {
            if (incoming == null || skillBar == null || !skillBar.TryGetView(slot, out SkillSlotView view))
                return;

            Color color = palette != null ? palette.NeutralColor : Color.white;
            if (palette != null && incoming.TryGetElement(out ElementType element) && palette.TryGet(element, out ElementPalette.Entry entry))
                color = entry.color;

            if (incoming.Icon == null || flightIcon == null)
            {
                view.Punch(color);
                return;
            }

            FlyIcon(incoming.Icon, groundCard.Rect, (RectTransform)view.transform, view, color);
        }

        // One reused Image on the HUD canvas (outside this panel, so the panel fading out does not hide it).
        // The tween is linked to that Image: if anything disables it mid-flight the tween is killed and
        // OnKill hides it, so no icon is ever left hanging on screen.
        private void FlyIcon(Sprite sprite, RectTransform from, RectTransform to, SkillSlotView view, Color color)
        {
            _flight?.Kill(true);

            RectTransform rect = flightIcon.rectTransform;
            Canvas canvas = flightIcon.canvas;
            float unit = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            flightIcon.sprite = sprite;
            flightIcon.gameObject.SetActive(true);
            rect.SetAsLastSibling();

            Vector3 start = from.TransformPoint(from.rect.center);
            Vector2 startSize = from.rect.size * flightIconScale;
            Vector2 endSize = to.rect.size * flightIconScale;

            _flight = DOVirtual.Float(0f, 1f, flightDuration, t =>
                {
                    float eased = DOVirtual.EasedValue(0f, 1f, t, flightEase);
                    float bulge = Mathf.Sin(t * Mathf.PI);
                    Vector3 end = to.TransformPoint(to.rect.center);
                    rect.position = Vector3.LerpUnclamped(start, end, eased) + Vector3.up * (bulge * flightArc * unit);
                    rect.sizeDelta = Vector2.LerpUnclamped(startSize, endSize, eased) * (1f + flightSwell * bulge);
                    rect.localRotation = Quaternion.Euler(0f, 0f, bulge * flightTilt);
                })
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .SetLink(flightIcon.gameObject, LinkBehaviour.KillOnDisable)
                .OnComplete(() => { if (view != null) view.Punch(color); })
                .OnKill(() =>
                {
                    _flight = null;
                    if (flightIcon != null)
                        flightIcon.gameObject.SetActive(false);
                });
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
