using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // Fighting-game style call-out: a slanted bar slams in from the left with the combo name,
    // holds, then slips out to the right. The finisher version is bigger and flashes the screen.
    // Each call-out is one DOTween sequence; a new call-out kills the running one.
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
        [SerializeField] private float comboStartScale = 1.15f;
        [SerializeField] private float finisherStartScale = 1.3f;
        [SerializeField] private float flashDuration = 0.25f;
        [SerializeField] private float flashAlpha = 0.35f;

        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _home;
        private Sequence _show;
        private Tween _flash;

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
            Show("COMBO!", comboName, inputs, comboBarColor, comboTitleSize, comboStartScale);
        }

        public void ShowFinisher(int count, string names)
        {
            Show($"FINISH  x{count}", "피니시", names, finisherBarColor, finisherTitleSize, finisherStartScale);
            Flash();
        }

        private void Show(string headerText, string titleText, string detailText, Color barColor, float titleSize, float startScale)
        {
            header.text = headerText;
            title.text = titleText;
            title.fontSize = title.fontSizeMax = titleSize;
            detail.text = detailText;
            bar.color = barColor;

            _show?.Kill();
            _rect.anchoredPosition = _home + Vector2.left * travel;
            _rect.localScale = Vector3.one * startScale;
            _group.alpha = 1f;

            _show = DOTween.Sequence()
                .Append(_rect.DOAnchorPosX(_home.x, slideIn).SetEase(Ease.OutQuad))
                .Join(_rect.DOScale(1f, slideIn).SetEase(Ease.OutQuad))
                .AppendInterval(hold)
                .Append(_rect.DOAnchorPosX(_home.x + travel * 0.5f, slideOut).SetEase(Ease.InQuad))
                .Join(_group.DOFade(0f, slideOut))
                .OnKill(() => _show = null)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void Flash()
        {
            _flash?.Kill();
            screenFlash.enabled = true;
            Color c = screenFlash.color;
            screenFlash.color = new Color(c.r, c.g, c.b, flashAlpha);
            _flash = screenFlash.DOFade(0f, flashDuration)
                .OnKill(() =>
                {
                    _flash = null;
                    if (screenFlash != null)
                        screenFlash.enabled = false;
                })
                .SetUpdate(true)
                .SetLink(gameObject);
        }
    }
}
