using Members.KYR._01.Scripts.Stats;
using System.Collections.Generic;

namespace Members.JJH._02_Scripts.Systems.EventChannelSystem.Events
{
    public static class SystemEvents
    {
        public static readonly EnemyKilledEvent EnemyKilledEvent = new();

        public static readonly PlayerStatsChangedEvent PlayerStatsChangedEvent = new();
        public static readonly PlayerStatsRequestEvent PlayerStatsRequestEvent = new();
    }

    #region Money System SO

    public class EnemyKilledEvent : GameEvent
    {
        public int Amount { get; private set; }

        public EnemyKilledEvent Init(int amount)
        {
            Amount = amount;
            return this;
        }
    }

    #endregion

    #region Player Stat Events

    public class PlayerStatsChangedEvent : GameEvent
    {
        public IReadOnlyList<float> Values { get; private set; }

        public float Get(PlayerStatId id) => Values[(int)id];

        public PlayerStatsChangedEvent Init(IReadOnlyList<float> values)
        {
            Values = values;
            return this;
        }
    }

    public class PlayerStatsRequestEvent : GameEvent { }

    #endregion
}