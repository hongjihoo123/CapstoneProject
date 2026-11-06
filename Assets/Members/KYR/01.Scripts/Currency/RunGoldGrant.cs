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
        }

        private void OnDisable()
        {
            systemChannel?.RemoveListener<EnemyKilledEvent>(Handle);
            systemChannel?.RemoveListener<GainGoldEvent>(HandleGain);
        }

        private void Handle(EnemyKilledEvent evt) => Add(evt.Amount);

        private void HandleGain(GainGoldEvent evt) => Add(evt.Amount);

        private static void Add(int amount)
        {
            GameBootstrap.Currency?.Add(CurrencyType.RunGold, amount);
        }
    }
}
