using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "FindTarget", story: "[Enemy] Find [Target]", category: "Action", id: "52447dd9636caa8f604c4492e81708e8")]
    public partial class EnemyFindTargetAction : Action
    {
    [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
        protected override Status OnStart()
        {
            if (Enemy.Value == null || Enemy.Value.Sensor == null)
                return Status.Failure;

            if (Enemy.Value.Sensor.IsTargetInRange(Enemy.Value.EnemyData.DetectRange, out Collider hitCollider))
            {
                Target.Value = hitCollider.gameObject;
                return Status.Success;
            }

            return Status.Failure;
        }
    }
}

