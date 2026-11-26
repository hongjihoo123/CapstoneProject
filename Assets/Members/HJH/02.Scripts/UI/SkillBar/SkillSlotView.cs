using Assets.Members.HJH._02.Scripts.Element;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using DG.Tweening;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.JJH._02_Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    public class SkillSlotView : MonoBehaviour, IUiBake
    {
        [SerializeField] private Image frame;
        [SerializeField] private Image icon;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private TMP_Text cooldownText;
        [SerializeField] private Image elementBadge;
        [SerializeField] private TMP_Text keyText;
        [SerializeField] private Image keyIcon;
        [SerializeField] private Color emptyIconColor = new Color(1f, 1f, 1f, 0.15f);

        [Header("부품")]
        [SerializeField, Tooltip("Everything that shakes. The slot root stays where the layout put it.")]
        private RectTransform content;
        [SerializeField, Tooltip("Thin bright line on the moving edge of the cooldown wedge.")]
        private Image cooldownEdge;
        [SerializeField, Tooltip("White quad over the icon, flashed when the skill is ready.")]
        private Image readyFlash;
        [SerializeField, Tooltip("Outline ring that expands off the frame when the skill is ready.")]
        private Image readyRing;
        [SerializeField] private Image comboGlow;

        [Header("속성 링 (셰이더 HJH/UI/ElementRing)")]
        [SerializeField, Tooltip("Empty = created at runtime behind the frame.")]
        private ElementRingView ring;
        [SerializeField, Tooltip("Ring quad size as a multiple of the frame size.")]
        private float ringSize = 1.6f;
        [SerializeField, Range(0.5f, 1f), Tooltip("How much of the frame the round slot fills (the circle art is 0.84).")]
        private float circleFraction = 0.84f;
        [SerializeField, Min(0.1f)] private float emphasisFollow = 18f;

        [Header("쿨타임")]
        [SerializeField] private Color cooldownIconTint = new Color(0.42f, 0.42f, 0.46f, 1f);
        [SerializeField, Tooltip("쿨 도는 부채꼴 색 (롤: 아이콘을 덮는 진한 파랑). 알파 0 이면 프리팹에 구워 둔 색 그대로")]
        private Color cooldownBlue = new Color(0.04f, 0.3f, 0.7f, 0.82f);

        [Header("쿨 끝남")]
        [SerializeField] private Color readyColor = new Color(1f, 0.96f, 0.82f, 1f);
        [SerializeField] private float flashAlpha = 0.85f;
        [SerializeField] private float flashDuration = 0.3f;
        [SerializeField] private float ringDuration = 0.45f;
        [SerializeField] private float ringScale = 1.35f;

        [Header("못 쓸 때")]
        [SerializeField] private Color deniedColor = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private float deniedDuration = 0.3f;
        [SerializeField] private float deniedShake = 7f;
        [SerializeField] private int deniedVibrato = 18;

        [Header("번쩍 (스킬 교체 때)")]
        [SerializeField] private float burstDuration = 0.5f;
        [SerializeField] private float burstScale = 1.25f;
        [SerializeField, Tooltip("Icon pop when a swapped-in skill lands in the slot.")]
        private float swapPunch = 0.18f;

        [Header("쿨 줄어듦 (롤 나보리처럼)")]
        [SerializeField, Tooltip("쿨이 완전히 초기화될 때 색")] private Color resetColor = new Color(1f, 0.82f, 0.3f, 1f);
        [SerializeField, Range(0f, 1f)] private float refundFlashAlpha = 0.5f;
        [SerializeField] private float refundPunch = 0.14f;
        [SerializeField] private float resetPunch = 0.3f;
        [SerializeField, Tooltip("깎인 만큼의 쿨 막대가 쭉 당겨지고 깜빡이는 시간 (롤 처럼)")] private float refundPullTime = 0.36f;
        [SerializeField, Tooltip("깜빡일 때의 밝은 회색 (막대와 아이콘 위 번쩍 둘 다). 롤은 연한 회청색")] private Color refundBarColor = new Color(0.8f, 0.84f, 0.9f, 0.92f);
        [SerializeField, Range(1, 4), Tooltip("딱딱 깜빡이는 횟수")] private int refundBlinks = 2;

        private SkillData _bound;
        private bool _hasBound;
        private bool _blocked;
        private Color _iconColor = Color.white;
        private Color _overlayColor;
        private Color _textColor;

        private Sequence _readyTween;
        private Sequence _deniedTween;
        private Sequence _burstTween;
        private Tween _punchTween;
        private Tween _refundPunchTween;
        private float _pullSeconds;
        private float _pullStart;

        private void Awake()
        {
            if (cooldownOverlay != null)
            {
                if (cooldownBlue.a > 0f)
                    cooldownOverlay.color = cooldownBlue;
                _overlayColor = cooldownOverlay.color;
            }
            if (cooldownText != null)
                _textColor = cooldownText.color;
            if (readyFlash != null)
                readyFlash.enabled = false;
            if (readyRing != null)
                readyRing.enabled = false;
            if (cooldownEdge != null)
                cooldownEdge.enabled = false;
        }

        private float _scaleNow = 1f;
        private float _scaleTarget = 1f;

        private void Update()
        {
            if (Mathf.Approximately(_scaleNow, _scaleTarget))
                return;

            _scaleNow = Mathf.Lerp(_scaleNow, _scaleTarget, 1f - Mathf.Exp(-emphasisFollow * Time.unscaledDeltaTime));
            if (Mathf.Abs(_scaleNow - _scaleTarget) < 0.002f)
                _scaleNow = _scaleTarget;
            transform.localScale = Vector3.one * _scaleNow;
        }

        public void SetComboEmphasis(float intensity, float scale, bool snap = false)
        {
            _scaleTarget = scale;
            if (snap)
            {
                _scaleNow = scale;
                transform.localScale = Vector3.one * scale;
            }

            EnsureRing();
            if (ring != null)
                ring.SetIntensity(intensity, snap);
        }

        private void EnsureRing()
        {
            if (ring == null && frame != null)
            {
                var go = new GameObject("ElementRing", typeof(RectTransform), typeof(Image), typeof(ElementRingView));
                var rect = (RectTransform)go.transform;
                rect.SetParent(frame.rectTransform, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.SetAsFirstSibling();
                ring = go.GetComponent<ElementRingView>();
            }

            if (ring != null && frame != null)
            {
                Rect r = frame.rectTransform.rect;
                float box = Mathf.Min(r.width, r.height);
                float side = box * ringSize;
                ((RectTransform)ring.transform).sizeDelta = new Vector2(side, side);
                ring.SetRingRadius(circleFraction / ringSize);
                ApplyCircleStyle(box);
            }
        }

        [SerializeField, Tooltip("동그란 슬롯 그림 (Resources/HJH/UI 의 slot_circle_frame / slot_circle_bg). 비면 예전 네모 모양")]
        private Sprite circleFrameSprite;
        [SerializeField] private Sprite circleBackSprite;

        [ContextMenu("그림 연결")]
        public void BakeLayout()
        {
#if UNITY_EDITOR
            circleFrameSprite = HudUiKit.Load("slot_circle_frame");
            circleBackSprite = HudUiKit.Load("slot_circle_bg");
            circleWhiteSprite = HudUiKit.Load("slot_circle_white");
#endif
        }

        [SerializeField, Tooltip("번쩍/깜빡임용 흰 원 (Resources/HJH/UI/slot_circle_white). 비면 에디터에서는 자동으로 찾음")]
        private Sprite circleWhiteSprite;

        private Sprite WhiteCircle
        {
            get
            {
#if UNITY_EDITOR
                if (circleWhiteSprite == null)
                    circleWhiteSprite = HudUiKit.Load("slot_circle_white");
#endif
                return circleWhiteSprite;
            }
        }

        private bool _circleApplied;
        private float _circleBox;

        private void ApplyCircleStyle(float box)
        {
            if (box <= 1f)
                return;

            if (!_circleApplied)
            {
                Sprite circleFrame = circleFrameSprite;
                Sprite circleBack = circleBackSprite;
                if (circleFrame == null)
                    return;

                _circleApplied = true;
                frame.sprite = circleFrame;
                frame.type = Image.Type.Simple;
                frame.preserveAspect = true;
                foreach (Shadow shadow in GetComponents<Shadow>())
                    shadow.enabled = false;

                Transform back = transform.Find("Backing");
                if (back != null && back.TryGetComponent(out Image backImage) && circleBack != null)
                {
                    backImage.sprite = circleBack;
                    backImage.color = Color.white;
                    backImage.preserveAspect = true;
                    backImage.enabled = true;
                    ((RectTransform)back).sizeDelta = Vector2.one * box * circleFraction;
                }

                if (cooldownOverlay != null && (WhiteCircle != null || circleBack != null))
                    cooldownOverlay.sprite = WhiteCircle != null ? WhiteCircle : circleBack;
                if (readyFlash != null && (WhiteCircle != null || circleBack != null))
                    readyFlash.sprite = WhiteCircle != null ? WhiteCircle : circleBack;
            }

            if (Mathf.Approximately(_circleBox, box))
                return;

            _circleBox = box;
            if (TryGetComponent(out SlotIconClipper clipper))
            {
                const int steps = 40;
                float radius = box * circleFraction * 0.5f * 0.94f;
                var points = new Vector2[steps];
                for (int i = 0; i < steps; i++)
                {
                    float a = i * Mathf.PI * 2f / steps;
                    points[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                }
                clipper.SetPolygon(points);
            }
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
                .OnKill(() =>
                {
                    _burstTween = null;
                    if (comboGlow != null)
                        comboGlow.enabled = false;
                })
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        public void PlayReady() => PlayReady(readyColor);

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

        public void PlayRefund(float seconds, bool reset)
        {
            if (seconds <= 0f)
                return;

            float now = Time.unscaledTime;
            bool stillPulling = now < _pullStart + refundPullTime;
            _pullSeconds = (stillPulling ? _pullSeconds * (1f - (now - _pullStart) / refundPullTime) : 0f) + seconds;
            _pullStart = now;

            if (reset)
            {
                PlayReady(resetColor);
                ComboBurst(resetColor);
            }
            else
            {
                BlinkWhite();
            }

            RectTransform target = content != null ? content : (RectTransform)transform;
            _refundPunchTween?.Kill(true);
            _refundPunchTween = target.DOPunchScale(Vector3.one * (reset ? resetPunch : refundPunch), reset ? 0.4f : 0.22f, 6, 0.6f)
                .OnKill(() => _refundPunchTween = null)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

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

        private void PlayReady(Color color) => PlayReady(color, flashAlpha, flashDuration, true);

        private void BlinkWhite()
        {
            if (readyFlash == null)
                return;

            _readyTween?.Kill(true);
            readyFlash.enabled = true;
            Color white = new Color(refundBarColor.r, refundBarColor.g, refundBarColor.b, refundFlashAlpha);
            float step = Mathf.Max(0.02f, refundPullTime / (refundBlinks * 2f));
            readyFlash.color = white;
            _readyTween = DOTween.Sequence();
            for (int i = 0; i < refundBlinks; i++)
            {
                _readyTween.AppendCallback(() => readyFlash.color = white);
                _readyTween.AppendInterval(step);
                _readyTween.AppendCallback(() => readyFlash.color = new Color(white.r, white.g, white.b, 0f));
                _readyTween.AppendInterval(step);
            }
            _readyTween
                .OnKill(() =>
                {
                    _readyTween = null;
                    if (readyFlash != null)
                        readyFlash.enabled = false;
                })
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void PlayReady(Color color, float alpha, float duration, bool withRing)
        {
            _readyTween?.Kill(true);
            _readyTween = DOTween.Sequence();

            if (readyFlash != null)
            {
                readyFlash.enabled = true;
                readyFlash.color = new Color(color.r, color.g, color.b, alpha);
                _readyTween.Join(readyFlash.DOFade(0f, duration).SetEase(Ease.OutQuad));
            }

            if (withRing && readyRing != null)
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

            ElementType ringElement = default;
            bool hasType = data != null && data.TryGetElement(out ringElement);
            EnsureRing();
            if (ring != null)
            {
                ring.gameObject.SetActive(hasType);
                if (hasType)
                    ring.Setup(ringElement, (GetInstanceID() & 7) * 1.3f);
            }

            if (elementBadge != null)
            {
                elementBadge.enabled = hasElement && entry.badge != null;
                elementBadge.sprite = entry.badge;
            }
        }

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

            float pull = 0f;
            float pullT = 1f;
            if (_pullSeconds > 0f && duration > 0f)
            {
                pullT = Mathf.Clamp01((Time.unscaledTime - _pullStart) / Mathf.Max(0.01f, refundPullTime));
                if (pullT >= 1f)
                    _pullSeconds = 0f;
                else
                    pull = _pullSeconds / duration * (1f - (1f - (1f - pullT) * (1f - pullT)));
            }

            bool pulling = pull > 0.002f;
            float shown = Mathf.Clamp01(fill + pull);
            cooldownOverlay.enabled = blocked || pulling;
            if (blocked || pulling)
            {
                cooldownOverlay.fillAmount = shown;
                bool lit = pulling && Mathf.FloorToInt(pullT * refundBlinks * 2f) % 2 == 0;
                cooldownOverlay.color = lit ? refundBarColor : _overlayColor;
            }
            UpdateSweepEdge(blocked || pulling, shown);

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
