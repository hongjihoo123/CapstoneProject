using Assets.Members.HJH._02.Scripts.Element;
using System;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using DG.Tweening;
using Members.JJH._02_Scripts.ElementsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // One skill card in the swap panel: element frame, icon with cooldown wedge, key tag, name.
    // Grows and glows while hovered; the panel listens to Hovered to show the tooltip.
    public class SkillCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image frame;
        [SerializeField] private Image icon;
        [SerializeField] private Image elementBadge;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private TMP_Text cooldownText;
        [SerializeField] private TMP_Text keyText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text elementText;
        [SerializeField] private Image hoverGlow;
        [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.12f);
        [SerializeField] private float hoverScale = 1.07f;
        [SerializeField] private float hoverDuration = 0.14f;

        private float _hover;
        private Tween _hoverTween;

        public SkillData Data { get; private set; }
        public bool IsHovered { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        public event Action<SkillCardView, bool> Hovered;

        public void SetKey(string key)
        {
            if (keyText != null)
                keyText.text = key;
        }

        public void Bind(SkillData data, ElementPalette palette, float cooldownRemaining, float cooldownDuration)
        {
            Data = data;

            ElementPalette.Entry entry = default;
            bool hasElement = data != null && palette != null
                              && data.TryGetElement(out ElementType element) && palette.TryGet(element, out entry);
            Color color = hasElement ? entry.color : palette != null ? palette.NeutralColor : Color.gray;

            frame.color = color;
            if (hoverGlow != null)
                hoverGlow.color = new Color(color.r, color.g, color.b, hoverGlow.color.a);

            icon.sprite = data != null ? data.Icon : null;
            icon.color = data != null && data.Icon != null ? Color.white : emptyColor;

            if (elementBadge != null)
            {
                elementBadge.enabled = hasElement && entry.badge != null;
                elementBadge.sprite = entry.badge;
            }

            if (nameText != null)
                nameText.text = data != null ? data.DisplayName : "빈 슬롯";
            if (elementText != null)
            {
                elementText.text = data != null ? SkillTooltipView.ElementLine(data) : string.Empty;
                elementText.color = color;
            }

            bool cooling = data != null && cooldownRemaining > 0f && cooldownDuration > 0f;
            if (cooldownOverlay != null)
            {
                cooldownOverlay.enabled = cooling;
                if (cooling)
                    cooldownOverlay.fillAmount = Mathf.Clamp01(cooldownRemaining / cooldownDuration);
            }

            if (cooldownText != null)
            {
                cooldownText.enabled = cooling;
                if (cooling)
                    cooldownText.text = cooldownRemaining < 1f ? cooldownRemaining.ToString("0.0") : Mathf.CeilToInt(cooldownRemaining).ToString();
            }
        }

        public void OnPointerEnter(PointerEventData eventData) => SetHovered(true);

        public void OnPointerExit(PointerEventData eventData) => SetHovered(false);

        private void OnDisable() => SetHovered(false);

        private void SetHovered(bool hovered)
        {
            if (IsHovered == hovered)
                return;

            IsHovered = hovered;
            Hovered?.Invoke(this, hovered);

            // Enter / exit tween the hover weight; Update turns the weight into scale and a pulsing glow.
            _hoverTween?.Kill();
            _hoverTween = DOTween.To(() => _hover, value => _hover = value, hovered ? 1f : 0f, hoverDuration)
                .SetEase(hovered ? Ease.OutBack : Ease.OutQuad)
                .OnKill(() => _hoverTween = null)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void Update()
        {
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(1f, hoverScale, _hover);

            if (hoverGlow != null)
            {
                Color c = hoverGlow.color;
                c.a = Mathf.Clamp01(_hover) * (0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f));
                hoverGlow.color = c;
                hoverGlow.enabled = c.a > 0.01f;
            }
        }
    }
}
