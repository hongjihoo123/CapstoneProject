using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // Fighting-game style call-out: a slanted bar slams in from the left with the combo name,
    // holds, then slips out to the right. The finisher version is bigger and flashes the screen.
    [RequireComponent(typeof(CanvasGroup))]
    public class ComboBanner : MonoBehaviour
    {
        [SerializeField] private Image bar;
        [SerializeField] private TMP_Text header;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text detail;
        [SerializeField] private Image screenFlash;
        [SerializeField] private Color comboBarColor = new Color(0.42f, 0.28f, 0.03f, 0.92f);
        [SerializeField] private Color finisherBarColor = new Color(0.55f, 0.07f, 0.1f, 0.95f);
        [SerializeField] private float comboTitleSize = 54f;
        [SerializeField] private float finisherTitleSize = 72f;
        [SerializeField] private float slideIn = 0.12f;
        [SerializeField] private float hold = 0.75f;
        [SerializeField] private float slideOut = 0.2f;
        [SerializeField] private float travel = 420f;
        [SerializeField] private float flashDuration = 0.25f;
        [SerializeField] private float flashAlpha = 0.35f;

        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _home;
        private float _time = -1f;
        private float _flashTime = -1f;
        private bool _finisher;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            _home = _rect.anchoredPosition;
            _group.alpha = 0f;
            screenFlash.enabled = false;
        }

        public void ShowCombo(string comboName, string inputs)
        {
            Show("COMBO!", comboName, inputs, comboBarColor, comboTitleSize, finisher: false);
        }

        public void ShowFinisher(int count, string names)
        {
            Show($"FINISH  x{count}", "피니시", names, finisherBarColor, finisherTitleSize, finisher: true);
        }

        private void Show(string headerText, string titleText, string detailText, Color barColor, float titleSize, bool finisher)
        {
            header.text = headerText;
            title.text = titleText;
            title.fontSize = title.fontSizeMax = titleSize;
            detail.text = detailText;
            bar.color = barColor;
            _finisher = finisher;
            _time = 0f;

            if (finisher)
            {
                _flashTime = 0f;
                screenFlash.enabled = true;
            }
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateFlash(dt);

            if (_time < 0f)
                return;

            _time += dt;
            float total = slideIn + hold + slideOut;
            float x;
            float alpha = 1f;
            float scale = 1f;

            if (_time < slideIn)
            {
                float t = _time / slideIn;
                x = -travel * (1f - t) * (1f - t);
                scale = Mathf.Lerp(_finisher ? 1.3f : 1.15f, 1f, t);
            }
            else if (_time < slideIn + hold)
            {
                x = 0f;
            }
            else if (_time < total)
            {
                float t = (_time - slideIn - hold) / slideOut;
                x = travel * 0.5f * t * t;
                alpha = 1f - t;
            }
            else
            {
                _time = -1f;
                _group.alpha = 0f;
                return;
            }

            _rect.anchoredPosition = _home + new Vector2(x, 0f);
            _rect.localScale = Vector3.one * scale;
            _group.alpha = alpha;
        }

        private void UpdateFlash(float dt)
        {
            if (_flashTime < 0f)
                return;

            _flashTime += dt;
            float t = Mathf.Clamp01(_flashTime / flashDuration);
            Color c = screenFlash.color;
            screenFlash.color = new Color(c.r, c.g, c.b, flashAlpha * (1f - t));
            if (t >= 1f)
            {
                _flashTime = -1f;
                screenFlash.enabled = false;
            }
        }
    }
}
