namespace Members.JJH._02_Scripts.Systems.EventChannelSystem.Events
{
    public static class MoneyEvents
    {
        public static readonly EnemyKilledEvent EnemyKilledEvent = new();
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
}