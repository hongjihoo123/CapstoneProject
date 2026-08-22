using Members.JJH._02_Scripts.Systems.AnimatorSystem;
using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Channels
{
#if UNITY_EDITOR
    [CreateAssetMenu(menuName = "Behavior/Event Channels/AnimationChannel")]
#endif
    [Serializable, GeneratePropertyBag]
    [EventChannelDescription(name: "AnimationChannel", message: "Set Animation to [Clip] [PlayOnce]", category: "Events", id: "9d876ace0324408907402cbd1df4f63d")]
    public sealed partial class AnimationChannel : EventChannel<AnimParamSO, bool> { }
}

