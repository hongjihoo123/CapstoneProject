using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using UnityEngine;

namespace Members.KYR._01.Scripts.Currency
{
    public class RunGoldGrant : MonoBehaviour
    {
        [SerializeField] private EventChannelSO systemChannel;

        private void OnEnable()
        {
            systemChannel?.AddListener<EnemyKilledEvent>(Handle);
            systemChannel?.AddListener<GainGoldEvent>(HandleGain);
            systemChannel?.AddListener<GainCurrencyEvent>(HandleCurrency);
        }

        private void OnDisable()
        {
            systemChannel?.RemoveListener<EnemyKilledEvent>(Handle);
            systemChannel?.RemoveListener<GainGoldEvent>(HandleGain);
            systemChannel?.RemoveListener<GainCurrencyEvent>(HandleCurrency);
        }

        private void Handle(EnemyKilledEvent evt) => Add(CurrencyType.RunGold, evt.Amount);

        private void HandleGain(GainGoldEvent evt) => Add(CurrencyType.RunGold, evt.Amount);

        private void HandleCurrency(GainCurrencyEvent evt) => Add(evt.Type, evt.Amount);

        private static void Add(CurrencyType type, int amount)
        {
            if (amount > 0)
                GameBootstrap.Currency?.Add(type, amount);
        }
    }
}
