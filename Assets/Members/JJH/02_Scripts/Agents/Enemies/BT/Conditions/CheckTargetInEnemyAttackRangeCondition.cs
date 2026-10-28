using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem;
using System;
using Unity.Behavior;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Conditions
{
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(name: "Check Target in Enemy AttackRange", story: "Check [Target] in [Enemy] AttackRange", category: "Conditions", id: "f8c17f8c9dac299e43c88d19eb6aced0")]
    public partial class CheckTargetInEnemyAttackRangeCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;

        private IEnemySkillModule _skillModule;

        public override bool IsTrue()
        {
            if (Enemy.Value == null || Target.Value == null || Enemy.Value.SkillModule == null)
                return false;

            _skillModule = Enemy.Value.SkillModule;

            Vector3 direction = Target.Value.transform.position - Enemy.Value.transform.position;
            direction.y = 0f;

            float distanceToTarget = direction.magnitude;

            return distanceToTarget <= Enemy.Value.EnemyData.AttackRange ||
                   _skillModule.HasAvailableSkill(distanceToTarget);
        }
    }
}