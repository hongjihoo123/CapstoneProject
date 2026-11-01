using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Wait for Enemy attack", story: "Wait for [Enemy] attack [Target]", category: "Action/Combat", id: "5a14c3c3d0138a368b2707028b3532d6")]
    public partial class WaitForEnemyAttackAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;
        [SerializeReference] public BlackboardVariable<GameObject> Target;

        protected override Status OnStart()
        {
            if (Enemy.Value == null || Target.Value == null ||
                Enemy.Value.SkillModule == null)
                return Status.Failure;


            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            Vector3 direction = Target.Value.transform.position - Enemy.Value.transform.position;
            direction.y = 0f;

            float distance = direction.magnitude;

            if (!Enemy.Value.SkillModule.HasSkillInRange(distance))
                return Status.Failure;

            return Enemy.Value.SkillModule.HasAvailableSkill(distance)
                ? Status.Success
                : Status.Running;
        }
    }
}

