using Members.JJH._02_Scripts.Agents.Enemies;
using System;
using Unity.Behavior;
using Unity.VisualScripting;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Conditions
{
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(name: "Check Target in Range", story: "Check [Target] in [Enemy] [Range] UseAttackRange[UseAttackRange] UseDetectRange[UseDetectRange]", category: "Conditions", id: "de22a6d60a14d71ca5cdceb1dd981da1")]
    public partial class CheckTargetInRangeCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;
        [SerializeReference] public BlackboardVariable<float> Range;

        [Header("설정값 사용하기")]
        [SerializeReference] public BlackboardVariable<bool> UseAttackRange;
        [SerializeReference] public BlackboardVariable<bool> UseDetectRange;

        public override bool IsTrue()
        {
            if (Enemy.Value == null || Enemy.Value.Sensor == null)
                return false;

            if (UseAttackRange.Value && UseDetectRange.Value)
            {
                Debug.LogError("UseAttackRange와 UseDetecRange의 값이 둘 다 true이면 안됩니다.");
                return false;
            }

            Vector3 direction = Target.Value.transform.position - Enemy.Value.transform.position;
            direction.y = 0f;

            float distance = direction.magnitude;

            if (UseAttackRange.Value)
                return distance <= Enemy.Value.EnemyData.AttackRange;

            if (UseDetectRange.Value)
                return Enemy.Value.Sensor.IsTargetInRange(Enemy.Value.EnemyData.DetectRange, out _);

            return distance <= Range.Value;
        }
    }
}