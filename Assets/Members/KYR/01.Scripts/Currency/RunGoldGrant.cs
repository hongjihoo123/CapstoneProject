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
        }

        private void OnDisable()
        {
            systemChannel?.RemoveListener<EnemyKilledEvent>(Handle);
        }

        private void Handle(EnemyKilledEvent evt)
        {
            GameBootstrap.Currency?.Add(CurrencyType.RunGold, evt.Amount);
        }
    }
}
