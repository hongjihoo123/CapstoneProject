using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Enemy Dead", story: "[Enemy] Dead", category: "Action/Combat", id: "ead4b0db85b920d5b33893252a6886ea")]
    public partial class EnemyDeadAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;

        protected override Status OnStart()
        {
            if (Enemy == null || Enemy.Value == null)
                return Status.Failure;

            Enemy.Value.Dead();

            return Status.Success;
        }
    }
}