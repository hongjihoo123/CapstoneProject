using System.Collections;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Element;
using Members.KYR._01_Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // Finisher stock: one diamond pip per stockable combo with the stocked combo names beside it,
    // and "[R] FINISH" that glows READY once anything is stocked (pulses harder when full).
    // A newly stocked combo pops its pip; the finisher flares every pip and empties them.
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
        [SerializeField] private float flareDuration = 0.45f;

        private int _shown;
        private int _popIndex = -1;
        private float _popTime;
        private Coroutine _flare;

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
            if (_flare != null)
                return;

            int count = chain.Stock.Count;
            if (count > 0 && count >= _shown)
            {
                _popIndex = count - 1;
                _popTime = 0f;
            }
            Render();
        }

        private void HandleFinisher(IReadOnlyList<ElementComboBook.Recipe> released)
        {
            if (_flare != null)
                StopCoroutine(_flare);
            _flare = StartCoroutine(Flare(released.Count));
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
            int count = chain.Stock.Count;
            if (readyLabel != null)
            {
                readyLabel.enabled = count > 0 && _flare == null;
                bool full = count >= chain.MaxStock;
                readyLabel.text = full ? "MAX" : "READY";
                float speed = full ? 10f : 4f;
                readyLabel.alpha = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * speed));
            }

            if (_popIndex < 0 || _flare != null || _popIndex >= pips.Length)
                return;

            _popTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_popTime / popDuration);
            pips[_popIndex].rectTransform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, 1f - (1f - t) * (1f - t));
            pips[_popIndex].color = Color.Lerp(Color.white, stockedPip, t);
            if (t >= 1f)
                _popIndex = -1;
        }

        private IEnumerator Flare(int count)
        {
            count = Mathf.Min(count, pips.Length);
            for (float time = 0f; time < flareDuration; time += Time.unscaledDeltaTime)
            {
                float t = time / flareDuration;
                for (int i = 0; i < count; i++)
                {
                    pips[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(2f, 1f, t);
                    pips[i].color = Color.Lerp(flarePip, emptyPip, t * t);
                }
                yield return null;
            }

            _flare = null;
            _popIndex = -1;
            Render();
        }
    }
}
