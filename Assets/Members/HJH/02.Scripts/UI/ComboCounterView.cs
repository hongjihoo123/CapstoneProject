using Assets.Members.HJH._02.Scripts.Element;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // Chain window gauge: shows how long until the chain breaks (refilled by every element skill).
    // Blinks in the last moments; on break it drops away and fades. No numbers or ranks.
    [RequireComponent(typeof(CanvasGroup))]
    public class ComboCounterView : MonoBehaviour
    {
        [SerializeField] private ElementComboChain chain;
        [SerializeField] private Image gaugeFill;
        [SerializeField] private Color gaugeColor = new Color(1f, 0.55f, 0.2f);
        [SerializeField] private float warnTime = 0.8f;
        [SerializeField] private float breakDuration = 0.4f;
        [SerializeField] private float breakDrop = 20f;

        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _home;
        private Sequence _break;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            _home = _rect.anchoredPosition;
            _group.alpha = 0f;
            gaugeFill.color = gaugeColor;

            // Scenes built with the old counter still have number / rank / CHAIN texts: hide them.
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
                text.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            chain.InputAdded += HandleInputAdded;
            chain.ChainBroken += HandleChainBroken;
        }

        private void OnDisable()
        {
            chain.InputAdded -= HandleInputAdded;
            chain.ChainBroken -= HandleChainBroken;
        }

        private void HandleInputAdded(bool shifted) => _break?.Kill();

        // Drops away and fades. While it plays the tween owns position and alpha.
        private void HandleChainBroken(int finalCount)
        {
            _break?.Kill();
            gaugeFill.fillAmount = 0f;
            _break = DOTween.Sequence()
                .Join(_rect.DOAnchorPosY(_home.y - breakDrop, breakDuration).SetEase(Ease.InQuad))
                .Join(_group.DOFade(0f, breakDuration).SetEase(Ease.Linear))
                .OnKill(() =>
                {
                    _break = null;
                    if (_rect != null)
                        _rect.anchoredPosition = _home;
                })
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void LateUpdate()
        {
            if (_break != null)
                return;

            if (chain.ChainCount > 0)
            {
                gaugeFill.fillAmount = Mathf.Clamp01(chain.Remaining / chain.ChainWindow);
                _group.alpha = chain.Remaining < warnTime
                    ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 16f))
                    : 1f;
            }
            else
            {
                _group.alpha = 0f;
            }
        }
    }
}
