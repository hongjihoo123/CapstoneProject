using System;
using Unity.Behavior;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Conditions
{
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(name: "Enemy use normal attack to Target", story: "[Enemy] use normal attack to [Target]", category: "Conditions", id: "af70d47dd1b1736e95121136d114269d")]
    public partial class EnemyUseNormalAttackToTargetCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;
        [SerializeReference] public BlackboardVariable<GameObject> Target;

        public override bool IsTrue()
        {
            if (Enemy.Value == null || Target.Value == null)
                return false;

            Vector3 direction = Target.Value.transform.position - Enemy.Value.transform.position;
            direction.y = 0f;
            float distance = direction.magnitude;

            if (Enemy.Value.SkillModule.HasAvailableSkill(distance))
                return false;

            return distance <= Enemy.Value.EnemyData.AttackRange;
        }
    }
}
