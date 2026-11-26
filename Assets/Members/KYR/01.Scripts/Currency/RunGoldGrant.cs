using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using UnityEngine;

namespace Members.KYR._01.Scripts.Currency
{
    // Money_EventChannel 의 재화 이벤트를 받아 CurrencyService 에 더한다.
    //  - EnemyKilledEvent / GainGoldEvent : 런 골드
    //  - GainCurrencyEvent                : 지정한 재화 (런 골드, 메타 소울 등)
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
