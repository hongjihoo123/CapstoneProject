using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Enemy Protect", story: "[Enemy] Protect", category: "Action/Combat", id: "16c63702d0c234bbbdfc7ebdcd76bb94")]
    public partial class EnemyProtectAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;

        protected override Status OnStart()
        {
            if (Enemy.Value == null)
                return Status.Failure;

            Enemy.Value.Protect();

            return Status.Success;
        }
    }
}

