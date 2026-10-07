using Assets.Members.HJH._02.Scripts.Element;
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

        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _home;
        private float _breakTime = -1f;

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

        private void HandleInputAdded(bool shifted) => _breakTime = -1f;

        private void HandleChainBroken(int finalCount) => _breakTime = 0f;

        private void LateUpdate()
        {
            Vector2 offset = Vector2.zero;

            if (_breakTime >= 0f)
            {
                _breakTime += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_breakTime / breakDuration);
                offset = Vector2.down * (20f * t * t);
                _group.alpha = 1f - t;
                gaugeFill.fillAmount = 0f;
                if (t >= 1f)
                    _breakTime = -1f;
            }
            else if (chain.ChainCount > 0)
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

            _rect.anchoredPosition = _home + offset;
        }
    }
}
