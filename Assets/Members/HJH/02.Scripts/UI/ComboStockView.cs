using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Element;
using DG.Tweening;
using Members.KYR._01_Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // Finisher stock: one diamond pip per stockable combo with the stocked combo names beside it,
    // and "[R] FINISH" that glows READY once anything is stocked (pulses harder when full).
    // A newly stocked combo pops its pip; the finisher flares every pip and empties them.
    // Pop and flare are DOTween one-shots; the READY pulse is a continuous state.
    public class ComboStockView : MonoBehaviour
    {
        [SerializeField] private ElementComboChain chain;
        [SerializeField] private PlayerAgent player;
        [SerializeField] private Image[] pips;
        [SerializeField] private TMP_Text[] names;
        [SerializeField] private TMP_Text keyLabel;
        [SerializeField] private TMP_Text readyLabel;
        [SerializeField] private Color emptyPip = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField] private Color stockedPip = new Color(1f, 0.83f, 0.3f, 1f);
        [SerializeField] private Color flarePip = new Color(1f, 0.35f, 0.3f, 1f);
        [SerializeField] private float popDuration = 0.2f;
        [SerializeField] private float popScale = 1.8f;
        [SerializeField] private float flareDuration = 0.45f;
        [SerializeField] private float flareScale = 2f;

        private int _shown;
        private Sequence _pop;
        private Sequence _flare;

        private void Start()
        {
            if (keyLabel != null)
                keyLabel.text = $"[{ComboKeyMap.Key(player, chain.FinisherSlot)}] FINISH";
            Render();
        }

        private void OnEnable()
        {
            chain.StockChanged += HandleStockChanged;
            chain.FinisherReleased += HandleFinisher;
        }

        private void OnDisable()
        {
            chain.StockChanged -= HandleStockChanged;
            chain.FinisherReleased -= HandleFinisher;
        }

        private void HandleStockChanged()
        {
            // The flare re-renders when it ends; the finisher empties the stock anyway.
            if (_flare != null)
                return;

            int count = chain.Stock.Count;
            bool grew = count > 0 && count >= _shown;
            Render();
            if (grew && count - 1 < pips.Length)
                Pop(pips[count - 1]);
        }

        private void Pop(Image pip)
        {
            _pop?.Kill(true);
            pip.color = Color.white;
            pip.rectTransform.localScale = Vector3.one * popScale;
            _pop = DOTween.Sequence()
                .Join(pip.rectTransform.DOScale(1f, popDuration).SetEase(Ease.OutQuad))
                .Join(pip.DOColor(stockedPip, popDuration))
                .OnKill(() => _pop = null)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void HandleFinisher(IReadOnlyList<ElementComboBook.Recipe> released)
        {
            _pop?.Kill(true);
            _flare?.Kill();

            int count = Mathf.Min(released.Count, pips.Length);
            _flare = DOTween.Sequence();
            for (int i = 0; i < count; i++)
            {
                Image pip = pips[i];
                pip.color = flarePip;
                pip.rectTransform.localScale = Vector3.one * flareScale;
                _flare.Join(pip.rectTransform.DOScale(1f, flareDuration).SetEase(Ease.Linear));
                _flare.Join(pip.DOColor(emptyPip, flareDuration).SetEase(Ease.InQuad));
            }

            _flare.OnKill(() =>
                {
                    _flare = null;
                    if (this != null)
                        Render();
                })
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void Render()
        {
            IReadOnlyList<ElementComboBook.Recipe> stock = chain.Stock;
            _shown = stock.Count;

            for (int i = 0; i < pips.Length; i++)
            {
                pips[i].color = i < stock.Count ? stockedPip : emptyPip;
                pips[i].rectTransform.localScale = Vector3.one;
            }

            for (int i = 0; i < names.Length; i++)
                names[i].text = i < stock.Count ? stock[i].Name : string.Empty;
        }

        private void Update()
        {
            if (readyLabel == null)
                return;

            int count = chain.Stock.Count;
            readyLabel.enabled = count > 0 && _flare == null;
            bool full = count >= chain.MaxStock;
            readyLabel.text = full ? "MAX" : "READY";
            float speed = full ? 10f : 4f;
            readyLabel.alpha = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * speed));
        }
    }
}
