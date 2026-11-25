using Members.KYR._01.Scripts.Currency;

namespace Members.JJH._02_Scripts.Systems.EventChannelSystem.Events
{
    public static class MoneyEvents
    {
        public static readonly EnemyKilledEvent EnemyKilledEvent = new();
        public static readonly GainGoldEvent GainGoldEvent = new();
        public static readonly GainCurrencyEvent GainCurrencyEvent = new();
    }

    public class EnemyKilledEvent : GameEvent
    {
        public int Amount { get; private set; }

        public EnemyKilledEvent Init(int amount)
        {
            Amount = amount;
            return this;
        }
    }

    public class GainGoldEvent : GameEvent
    {
        public int Amount { get; private set; }

        public GainGoldEvent Init(int amount)
        {
            Amount = amount;
            return this;
        }
    }

    // 재화 종류를 골라서 지급 Money_EventChannel 로 보내면 RunGoldGrant가 더 한ㅁ
    // moneyChannel.RaiseEvent(MoneyEvents.GainCurrencyEvent.Init(CurrencyType.MetaSoul, 10)); 이런식
    public class GainCurrencyEvent : GameEvent
    {
        public CurrencyType Type { get; private set; }
        public int Amount { get; private set; }

        public GainCurrencyEvent Init(CurrencyType type, int amount)
        {
            Type = type;
            Amount = amount;
            return this;
        }
    }
}