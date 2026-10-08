using Assets.Members.HJH._02.Scripts.Element;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using DG.Tweening;
using Members.JJH._02_Scripts.ElementsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // One skill icon: element-colored frame, element badge (bottom-left), key (bottom-right) and a
    // LoL-style cooldown:
    //   cooling  - icon darkened, dark wedge unwinding from 12 o'clock with a bright sweep edge, seconds left
    //   ready    - white flash over the icon + a light ring expanding off the frame (the slot itself never moves)
    //   denied   - content shakes sideways, wedge and number flash red
    // Combo glow (proc-style): pulses while this skill is the next combo input, bursts when a combo lands.
    //
    // Continuous, state-driven values (wedge, sweep edge, hint pulse) are set every frame.
    // One-shot reactions (ready, denied, burst, swap punch) are DOTween tweens linked to this object.
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

        [Header("Parts (built by HJH > UI > Upgrade Skill Slots)")]
        [SerializeField, Tooltip("Everything that shakes. The slot root stays where the layout put it.")]
        private RectTransform content;
        [SerializeField, Tooltip("Thin bright line on the moving edge of the cooldown wedge.")]
        private Image cooldownEdge;
        [SerializeField, Tooltip("White quad over the icon, flashed when the skill is ready.")]
        private Image readyFlash;
        [SerializeField, Tooltip("Outline ring that expands off the frame when the skill is ready.")]
        private Image readyRing;
        [SerializeField] private Image comboGlow;

        [Header("Cooldown")]
        [SerializeField] private Color cooldownIconTint = new Color(0.42f, 0.42f, 0.46f, 1f);

        [Header("Ready")]
        [SerializeField] private Color readyColor = new Color(1f, 0.96f, 0.82f, 1f);
        [SerializeField] private float flashAlpha = 0.85f;
        [SerializeField] private float flashDuration = 0.3f;
        [SerializeField] private float ringDuration = 0.45f;
        [SerializeField] private float ringScale = 1.35f;

        [Header("Denied")]
        [SerializeField] private Color deniedColor = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private float deniedDuration = 0.3f;
        [SerializeField] private float deniedShake = 7f;
        [SerializeField] private int deniedVibrato = 18;

        [Header("Combo")]
        [SerializeField] private float glowPulseSpeed = 6f;
        [SerializeField] private float burstDuration = 0.5f;
        [SerializeField] private float burstScale = 1.25f;
        [SerializeField, Tooltip("Icon pop when a swapped-in skill lands in the slot.")]
        private float swapPunch = 0.18f;

        private SkillData _bound;
        private bool _hasBound;
        private bool _hinted;
        private Color _hintColor;
        private bool _blocked;
        private Color _iconColor = Color.white;
        private Color _overlayColor;
        private Color _textColor;

        private Sequence _readyTween;
        private Sequence _deniedTween;
        private Sequence _burstTween;
        private Tween _punchTween;

        private void Awake()
        {
            if (cooldownOverlay != null)
                _overlayColor = cooldownOverlay.color;
            if (cooldownText != null)
                _textColor = cooldownText.color;
            if (readyFlash != null)
                readyFlash.enabled = false;
            if (readyRing != null)
                readyRing.enabled = false;
            if (cooldownEdge != null)
                cooldownEdge.enabled = false;
        }

        public void SetComboHint(bool on, Color color)
        {
            _hinted = on;
            _hintColor = color;
        }

        public void ComboBurst(Color color)
        {
            if (comboGlow == null)
                return;

            _burstTween?.Kill();
            RectTransform rect = comboGlow.rectTransform;
            comboGlow.enabled = true;
            comboGlow.color = new Color(color.r, color.g, color.b, 1f);
            rect.localScale = Vector3.one * burstScale;
            _burstTween = DOTween.Sequence()
                .Join(rect.DOScale(1f, burstDuration).SetEase(Ease.OutQuad))
                .Join(comboGlow.DOFade(0f, burstDuration).SetEase(Ease.InQuad))
                .OnKill(() => _burstTween = null)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        // Skill came off cooldown (SkillStateModule.SkillReady).
        public void PlayReady() => PlayReady(readyColor);

        // Key pressed while the skill cannot be cast (SkillStateModule.SkillDenied).
        public void Deny()
        {
            _deniedTween?.Kill(true);
            RectTransform target = content != null ? content : (RectTransform)transform;
            Color overlayRed = new Color(deniedColor.r * 0.6f, deniedColor.g * 0.6f, deniedColor.b * 0.6f, _overlayColor.a);

            _deniedTween = DOTween.Sequence()
                .Join(target.DOShakeAnchorPos(deniedDuration, new Vector2(deniedShake, 0f), deniedVibrato, 0f, false, true));
            if (cooldownOverlay != null)
            {
                cooldownOverlay.color = overlayRed;
                _deniedTween.Join(cooldownOverlay.DOColor(_overlayColor, deniedDuration).SetEase(Ease.InQuad));
            }
            if (cooldownText != null)
            {
                cooldownText.color = deniedColor;
                _deniedTween.Join(cooldownText.DOColor(_textColor, deniedDuration).SetEase(Ease.InQuad));
            }

            _deniedTween.OnKill(() => _deniedTween = null).SetUpdate(true).SetLink(gameObject);
        }

        // A new skill landed in this slot (skill swap): ready flash in its element color + icon pop.
        public void Punch(Color color)
        {
            PlayReady(color);
            ComboBurst(color);
            if (icon == null)
                return;

            _punchTween?.Kill(true);
            _punchTween = icon.rectTransform.DOPunchScale(Vector3.one * swapPunch, 0.35f, 6, 0.6f)
                .OnKill(() => _punchTween = null)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void PlayReady(Color color)
        {
            _readyTween?.Kill(true);
            _readyTween = DOTween.Sequence();

            if (readyFlash != null)
            {
                readyFlash.enabled = true;
                readyFlash.color = new Color(color.r, color.g, color.b, flashAlpha);
                _readyTween.Join(readyFlash.DOFade(0f, flashDuration).SetEase(Ease.OutQuad));
            }

            if (readyRing != null)
            {
                RectTransform ring = readyRing.rectTransform;
                readyRing.enabled = true;
                readyRing.color = new Color(color.r, color.g, color.b, 1f);
                ring.localScale = Vector3.one;
                _readyTween.Join(ring.DOScale(ringScale, ringDuration).SetEase(Ease.OutCubic));
                _readyTween.Join(readyRing.DOFade(0f, ringDuration).SetEase(Ease.InQuad));
            }

            _readyTween.OnKill(() =>
                {
                    _readyTween = null;
                    if (readyFlash != null)
                        readyFlash.enabled = false;
                    if (readyRing != null)
                        readyRing.enabled = false;
                })
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void LateUpdate()
        {
            // A one-shot burst owns the glow until it finishes; the hint pulse resumes afterwards.
            if (comboGlow == null || _burstTween != null)
                return;

            if (!_hinted)
            {
                comboGlow.enabled = false;
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * glowPulseSpeed);
            comboGlow.enabled = true;
            comboGlow.color = new Color(_hintColor.r, _hintColor.g, _hintColor.b, Mathf.Lerp(0.35f, 1f, pulse));
            comboGlow.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.06f, pulse);
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
            _iconColor = data != null && data.Icon != null ? Color.white : emptyIconColor;
            ApplyIconTint();

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
            float fill = blocked ? Mathf.Clamp01(remaining / duration) : 0f;

            if (blocked != _blocked)
            {
                _blocked = blocked;
                ApplyIconTint();
            }

            cooldownOverlay.enabled = blocked;
            if (blocked)
                cooldownOverlay.fillAmount = fill;
            UpdateSweepEdge(blocked, fill);

            if (cooldownText == null)
                return;

            cooldownText.enabled = blocked || showCharges;
            if (blocked)
                cooldownText.text = remaining < 1f ? remaining.ToString("0.0") : Mathf.CeilToInt(remaining).ToString();
            else if (showCharges)
                cooldownText.text = charges.ToString();
        }

        private void ApplyIconTint()
        {
            if (icon != null)
                icon.color = _blocked ? _iconColor * cooldownIconTint : _iconColor;
        }

        // The line sits on the wedge boundary: pivot at the icon center, rotated with the fill, and
        // stretched so it always reaches the square's edge (longer toward the corners).
        private void UpdateSweepEdge(bool blocked, float fill)
        {
            if (cooldownEdge == null)
                return;

            cooldownEdge.enabled = blocked && fill > 0.001f && fill < 0.999f;
            if (!cooldownEdge.enabled)
                return;

            bool clockwise = cooldownOverlay.fillClockwise;
            float degrees = fill * 360f * (clockwise ? -1f : 1f);
            float radians = degrees * Mathf.Deg2Rad;
            float toEdge = 1f / Mathf.Max(Mathf.Abs(Mathf.Sin(radians)), Mathf.Abs(Mathf.Cos(radians)));

            RectTransform edge = cooldownEdge.rectTransform;
            edge.localRotation = Quaternion.Euler(0f, 0f, degrees);
            float half = cooldownOverlay.rectTransform.rect.height * 0.5f;
            edge.sizeDelta = new Vector2(edge.sizeDelta.x, half * toEdge);
        }
    }
}
