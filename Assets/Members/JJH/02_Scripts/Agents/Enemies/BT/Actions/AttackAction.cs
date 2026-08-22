using Members.JJH._02_Scripts.Agents.Enemies;
using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Attack", story: "[Enemy] Attack", category: "Combat", id: "6af70a90fb7425aeb4805e1b7a3bfb46")]
public partial class AttackAction : Action
{
    [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;

    protected override Status OnStart()
    {
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}

