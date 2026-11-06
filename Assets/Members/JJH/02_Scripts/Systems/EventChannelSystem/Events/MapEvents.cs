using Members.JJH._02_Scripts.Map;
using static Members.JJH._02_Scripts.Map.MapUI;

namespace Members.JJH._02_Scripts.Systems.EventChannelSystem.Events
{
    public static class MapEvents
    {
        public static readonly ToggleMapEvent ToggleMapEvent = new();
        public static readonly SelectRoomEvent SelectRoomEvent = new();
        public static readonly ChangeSceneEvent ChangeSceneEvent = new();
    }

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
}