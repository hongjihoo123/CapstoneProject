using Members.JJH._02_Scripts.Agents.Enemies;
using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Face Target", story: "[Enemy] face [Target]", category: "Action/GameObject", id: "0231f050413fa385aad91cf6c5061703")]
    public partial class FaceTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;
        [SerializeReference] public BlackboardVariable<GameObject> Target;

        [SerializeField] private BlackboardVariable<float> RotationSpeed = new(10f);

        protected override Status OnStart()
        {
            if (Enemy.Value == null || Target.Value == null)
                return Status.Failure;

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            Vector3 direction = Target.Value.transform.position - Enemy.Value.transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
                return Status.Success;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            Enemy.Value.transform.rotation = Quaternion.Slerp(Enemy.Value.transform.rotation, targetRotation,
                                                                                                RotationSpeed.Value * Time.deltaTime);

            if (Quaternion.Angle(Enemy.Value.transform.rotation, targetRotation) < 1f)
            {
                Enemy.Value.transform.rotation = targetRotation;
                return Status.Success;
            }

            return Status.Running;
        }
    }
}