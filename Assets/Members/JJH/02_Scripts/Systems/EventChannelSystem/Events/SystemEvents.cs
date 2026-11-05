using Members.JJH._02_Scripts.Map;
using Members.KYR._01.Scripts.Stats;
using System.Collections.Generic;
using static Members.JJH._02_Scripts.Map.MapUI;

namespace Members.JJH._02_Scripts.Systems.EventChannelSystem.Events
{
    public static class SystemEvents
    {
        public static readonly ToggleMapEvent ToggleMapEvent = new();
        public static readonly SelectRoomEvent SelectRoomEvent = new();
        public static readonly ChangeSceneEvent ChangeSceneEvent = new();

        public static readonly EnemyKilledEvent EnemyKilledEvent = new();

        public static readonly PlayerStatsChangedEvent PlayerStatsChangedEvent = new();
        public static readonly PlayerStatsRequestEvent PlayerStatsRequestEvent = new();
    }


    #region Map System Events

    public class ToggleMapEvent : GameEvent { }

    public class SelectRoomEvent : GameEvent
    {
        public MapChoiceUI Choice;
        public RoomOption RoomOption;

        public SelectRoomEvent Init(MapChoiceUI choice, RoomOption roomOption)
        {
            Choice = choice;
            RoomOption = roomOption;
            return this;
        }
    }

    public class ChangeSceneEvent : GameEvent
    {
        public string SceneName { get; private set; }

        public ChangeSceneEvent Init(string sceneName)
        {
            SceneName = sceneName;
            return this;
        }
    }

    #endregion

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