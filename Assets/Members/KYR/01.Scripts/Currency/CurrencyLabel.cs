using TMPro;
using UnityEngine;

namespace Members.KYR._01.Scripts.Currency
{
    public class CurrencyLabel : MonoBehaviour
    {
        [SerializeField] private CurrencyType type;
        [SerializeField] private TMP_Text text;

        private void OnEnable()
        {
            CurrencyService currency = GameBootstrap.Currency;
            if (currency == null || text == null)
                return;

            currency.OnChanged += Handle;
            text.text = currency.Get(type).ToString();
        }

        private void OnDisable()
        {
            if (GameBootstrap.Currency != null)
                GameBootstrap.Currency.OnChanged -= Handle;
        }

        private void Handle(CurrencyType changed, int value)
        {
            if (text != null && changed == type)
                text.text = value.ToString();
        }
    }
}
