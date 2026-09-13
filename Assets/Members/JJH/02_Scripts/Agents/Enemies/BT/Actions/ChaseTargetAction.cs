using Members.JJH._02_Scripts.Agents.Modules;
using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Chase Target", story: "[Enemy] chase [Target]", category: "Action/Navigation", id: "aa9256dd7d934aa6de4744e3c043a6c4")]
    public partial class ChaseTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;
        [SerializeReference] public BlackboardVariable<GameObject> Target;

        [Header("Chase Setting")]
        [SerializeField] private float destinationUpdateDistance = 0.2f;

        private ISensor _sensor;
        private INavMesh _navMeshAgent;

        private Vector3 _targetPos;
        private Vector3 _lastTargetPos;

        protected override Status OnStart()
        {
            if (Enemy.Value == null || Enemy.Value.Sensor == null ||
                Enemy.Value.EnemyNavMeshAgent == null || Target.Value == null)
                return Status.Failure;

            if (Enemy.Value.IsAlive == false)
                return Status.Success;

            _navMeshAgent = Enemy.Value.EnemyNavMeshAgent;
            _sensor = Enemy.Value.Sensor;

            _targetPos = Target.Value.transform.position;
            _lastTargetPos = _targetPos;

            _navMeshAgent.MoveTo(_targetPos);

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (Target.Value == null)
                return Status.Failure;

            if (Enemy.Value.IsAlive == false)
                return Status.Success;

            bool isInRadius = _sensor.IsTargetInRange(Enemy.Value.EnemyData.DetectRange, out Collider hitCollider);

            if (!isInRadius)
                return Status.Failure;

            _targetPos = Target.Value.transform.position;

            Vector3 direction = _targetPos - Enemy.Value.transform.position;
            direction.y = 0f;

            float distanceToTarget = direction.magnitude;
            if (distanceToTarget <= Enemy.Value.EnemyData.AttackRange)
            {
                _navMeshAgent.KeepChase(false);

                if (direction.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);

                    Enemy.Value.transform.rotation =
                        Quaternion.RotateTowards(Enemy.Value.transform.rotation, targetRotation,
                                                                   Enemy.Value.EnemyNavMeshAgent.NavMeshAgent.angularSpeed * Time.deltaTime);
                }

                float angle = Vector3.Angle(Enemy.Value.transform.forward, direction);

                if (angle <= 1f)
                    return Status.Success;

                return Status.Running;
            }

            if (Vector3.Distance(_lastTargetPos, _targetPos) >= destinationUpdateDistance)
            {
                _lastTargetPos = _targetPos;
                _navMeshAgent.MoveTo(_targetPos);
            }

            return Status.Running;
        }

        protected override void OnEnd()
        {
            _navMeshAgent.StopImmediately();
        }
    }
}