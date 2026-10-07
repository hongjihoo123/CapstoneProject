using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.JJH._02_Scripts.ElementsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // One skill icon: element-colored frame, element badge (bottom-left), key (bottom-right),
    // and a LoL-style cooldown (dark wedge that unwinds clockwise from 12 o'clock + seconds left).
    // Optional combo glow (proc-style): a pulsing outer frame while this skill is the next combo input,
    // and a short burst when it was part of a combo that just landed.
    public class SkillSlotView : MonoBehaviour
    {
        [SerializeField] private Image frame;
        [SerializeField] private Image icon;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private TMP_Text cooldownText;
        [SerializeField] private Image elementBadge;
        [SerializeField] private TMP_Text keyText;
        [SerializeField] private Image keyIcon;
        [SerializeField] private Color emptyIconColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Image comboGlow;
        [SerializeField] private float glowPulseSpeed = 6f;
        [SerializeField] private float burstDuration = 0.5f;
        [SerializeField] private float burstScale = 1.25f;

        private SkillData _bound;
        private bool _hasBound;
        private bool _hinted;
        private Color _hintColor;
        private float _burstTime = -1f;
        private Color _burstColor;

        public void SetComboHint(bool on, Color color)
        {
            _hinted = on;
            _hintColor = color;
        }

        public void ComboBurst(Color color)
        {
            _burstColor = color;
            _burstTime = 0f;
        }

        private void LateUpdate()
        {
            if (comboGlow == null)
                return;

            float scale = 1f;
            Color color;
            if (_burstTime >= 0f)
            {
                _burstTime += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_burstTime / burstDuration);
                scale = Mathf.Lerp(burstScale, 1f, t);
                color = new Color(_burstColor.r, _burstColor.g, _burstColor.b, 1f - t);
                if (t >= 1f)
                    _burstTime = -1f;
            }
            else if (_hinted)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * glowPulseSpeed);
                scale = Mathf.Lerp(1f, 1.06f, pulse);
                color = new Color(_hintColor.r, _hintColor.g, _hintColor.b, Mathf.Lerp(0.35f, 1f, pulse));
            }
            else
            {
                comboGlow.enabled = false;
                return;
            }

            comboGlow.enabled = true;
            comboGlow.color = color;
            comboGlow.rectTransform.localScale = Vector3.one * scale;
        }

        // Labels like "RMB" must stay on one line: shrink to fit instead of wrapping letter by letter.
        public void SetKeyLabel(string label)
        {
            if (keyText == null)
                return;

            keyText.textWrappingMode = TextWrappingModes.NoWrap;
            keyText.enableAutoSizing = true;
            keyText.fontSizeMin = 14f;
            keyText.fontSizeMax = Mathf.Max(keyText.fontSizeMax, 30f);
            keyText.text = label;
        }

        // Keys too wide for a text label (Space) are shown as an icon instead.
        public void SetKeyIcon(Sprite sprite)
        {
            if (keyText != null)
                keyText.text = string.Empty;
            if (keyIcon != null)
            {
                keyIcon.sprite = sprite;
                keyIcon.enabled = sprite != null;
            }
        }

        public void Bind(SkillData data, ElementPalette palette)
        {
            if (_hasBound && _bound == data)
                return;

            _bound = data;
            _hasBound = true;

            icon.sprite = data != null ? data.Icon : null;
            icon.color = data != null && data.Icon != null ? Color.white : emptyIconColor;

            ElementPalette.Entry entry = default;
            bool hasElement = data != null && palette != null
                              && data.TryGetElement(out ElementType element) && palette.TryGet(element, out entry);

            frame.color = hasElement ? entry.color : palette != null ? palette.NeutralColor : Color.gray;

            if (elementBadge != null)
            {
                elementBadge.enabled = hasElement && entry.badge != null;
                elementBadge.sprite = entry.badge;
            }
        }

        // Multi-charge skills stay usable while recharging: the wedge only covers the icon when
        // no charge is left, and the text shows the stored charge count instead of seconds.
        public void SetCooldown(float remaining, float duration, int charges = 0, int maxCharges = 1)
        {
            bool cooling = remaining > 0f && duration > 0f;
            bool blocked = cooling && charges <= 0;
            bool showCharges = maxCharges > 1 && charges > 0;

            cooldownOverlay.enabled = blocked;
            if (blocked)
                cooldownOverlay.fillAmount = Mathf.Clamp01(remaining / duration);

            if (cooldownText == null)
                return;

            cooldownText.enabled = blocked || showCharges;
            if (blocked)
                cooldownText.text = remaining < 1f ? remaining.ToString("0.0") : Mathf.CeilToInt(remaining).ToString();
            else if (showCharges)
                cooldownText.text = charges.ToString();
        }
    }
}
