using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "EnemyAttack", story: "[Enemy] Attack", category: "Action", id: "6af70a90fb7425aeb4805e1b7a3bfb46")]
    public partial class AttackAction : Action
    {
    [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;
        protected override Status OnStart()
        {
            if (Enemy.Value == null)
                return Status.Failure;

            Enemy.Value.Attack();

            return Status.Success;
        }
    }
}