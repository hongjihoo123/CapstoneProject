using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Channels
{
#if UNITY_EDITOR
    [CreateAssetMenu(menuName = "Behavior/Event Channels/StateChannel")]
#endif
    [Serializable, GeneratePropertyBag]
    [EventChannelDescription(name: "StateChannel", message: "Set CurrentState to [State]", category: "Events", id: "e06c61012415511a9dfb27ce54471e92")]
    public sealed partial class StateChannel : EventChannel<EnemyState> { }
}

