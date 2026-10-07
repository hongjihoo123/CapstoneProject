using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // One input of the combo trail, drawn like a fighting-game input: key cap in the element's color,
    // element name underneath. States: empty, filled (slams in), ghost (pulsing "press this next"),
    // and a gold flash when it was part of a combo that just landed.
    //
    // Final transform = trail pose (set every frame by ElementComboHud while it slides / breaks)
    //                 x slam and flash multipliers (one-shot DOTween tweens on two floats).
    [RequireComponent(typeof(CanvasGroup))]
    public class ComboInputCell : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image ring;
        [SerializeField] private Image badge;
        [SerializeField] private TMP_Text keyText;
        [SerializeField] private TMP_Text elementText;
        [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.22f);
        [SerializeField] private float slamDuration = 0.16f;
        [SerializeField] private float slamScale = 1.45f;
        [SerializeField] private float flashDuration = 0.7f;
        [SerializeField] private float flashScale = 1.25f;
        [SerializeField] private float ghostPulseSpeed = 6f;

        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _home;
        private Vector2 _offset;
        private float _poseScale = 1f;
        private float _slamMul = 1f;
        private float _flashMul = 1f;
        private Tween _slam;
        private Sequence _flash;
        private bool _ghost;
        private Color _ghostColor;

        public Vector2 Home => _home;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            _home = _rect.anchoredPosition;
            ShowEmpty();
        }

        public void ShowEmpty()
        {
            _ghost = false;
            background.color = emptyColor;
            badge.enabled = false;
            keyText.text = string.Empty;
            elementText.text = string.Empty;
            ring.enabled = _flash != null;
        }

        public void ShowInput(ElementPalette.Entry entry, string key, string elementName, bool slam)
        {
            _ghost = false;
            background.color = Frame(entry.color);
            SetContent(entry, key, elementName, 1f);
            ring.enabled = _flash != null;

            if (!slam)
                return;

            _slam?.Kill();
            _slamMul = slamScale;
            _slam = DOTween.To(() => _slamMul, value => _slamMul = value, 1f, slamDuration)
                .SetEase(Ease.OutQuad)
                .OnKill(() => _slamMul = 1f)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        public void ShowGhost(ElementPalette.Entry entry, string key, string elementName)
        {
            _ghost = true;
            _ghostColor = entry.color;
            background.color = emptyColor;
            SetContent(entry, key, elementName, 0.5f);
            ring.enabled = true;
        }

        public void Flash(Color color)
        {
            _flash?.Kill();
            ring.enabled = true;
            ring.color = new Color(color.r, color.g, color.b, 1f);
            _flashMul = flashScale;
            _flash = DOTween.Sequence()
                .Join(DOTween.To(() => _flashMul, value => _flashMul = value, 1f, flashDuration))
                .Join(ring.DOFade(0f, flashDuration))
                .OnKill(() =>
                {
                    _flash = null;
                    _flashMul = 1f;
                    if (ring != null)
                        ring.enabled = _ghost;
                })
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        // Pose set by the trail (slide / break animations); slam and flash scale on top of it.
        public void SetPose(Vector2 offset, float scale, float alpha)
        {
            _offset = offset;
            _poseScale = scale;
            _group.alpha = alpha;
        }

        public void ResetPose() => SetPose(Vector2.zero, 1f, 1f);

        private void SetContent(ElementPalette.Entry entry, string key, string elementName, float alpha)
        {
            badge.enabled = entry.badge != null;
            badge.sprite = entry.badge;
            badge.color = new Color(1f, 1f, 1f, alpha);
            keyText.text = key;
            keyText.color = new Color(1f, 1f, 1f, alpha);
            elementText.text = elementName;
            elementText.color = new Color(entry.color.r, entry.color.g, entry.color.b, alpha);
        }

        // The frame sprite has a half-transparent center: full color on the ornament, a deep wash inside,
        // so the white key label stays readable.
        private static Color Frame(Color color) => new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f, 1f);

        private void LateUpdate()
        {
            // The flash owns the ring while it plays; the ghost pulse is a continuous state.
            if (_flash == null && _ghost)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * ghostPulseSpeed);
                ring.color = new Color(_ghostColor.r, _ghostColor.g, _ghostColor.b, Mathf.Lerp(0.3f, 0.95f, pulse));
            }

            _rect.anchoredPosition = _home + _offset;
            _rect.localScale = Vector3.one * (_poseScale * _slamMul * _flashMul);
        }
    }
}
