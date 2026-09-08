using System;
using Unity.Behavior;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Conditions
{
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(name: "CheckTargetInAttackRange", story: "Check [Target] in [Enemy] AttackRange", category: "Conditions", id: "a3e1fd31e1f1e1462d0263b14d7aeb7b")]
    public partial class CheckTargetInAttackRangeCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;

        public override bool IsTrue()
        {
            if (Enemy.Value == null || Enemy.Value.Sensor == null
                || Enemy.Value.EnemyData == null || Target.Value == null)
                return false;

            return Enemy.Value.Sensor.IsTargetInRange(Enemy.Value.EnemyData.AttackRange, out Collider hitCollider);
        }
    }
}