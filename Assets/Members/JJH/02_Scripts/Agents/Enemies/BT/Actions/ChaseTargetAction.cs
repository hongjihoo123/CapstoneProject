using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem;
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
        [SerializeReference] private BlackboardVariable<float> DestinationUpdateDistance = new(0.2f);

        private ISensor _sensor;
        private INavMesh _navMeshAgent;
        private IEnemySkillModule _skillModule;

        private Vector3 _targetPos;
        private Vector3 _lastTargetPos;

        protected override Status OnStart()
        {
            if (Enemy.Value == null || Enemy.Value.Sensor == null ||
                Enemy.Value.NavMeshAgent == null || Target.Value == null)
                return Status.Failure;

            _sensor = Enemy.Value.Sensor;
            _navMeshAgent = Enemy.Value.NavMeshAgent;
            _skillModule = Enemy.Value.SkillModule;

            _targetPos = Target.Value.transform.position;
            _lastTargetPos = _targetPos;

            _navMeshAgent.MoveTo(_targetPos);

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (Target.Value == null)
                return Status.Failure;

            bool isInRadius = _sensor.IsTargetInRange(Enemy.Value.EnemyData.DetectRange, out Collider hitCollider);

            if (!isInRadius)
                return Status.Failure;

            _targetPos = Target.Value.transform.position;

            Vector3 direction = _targetPos - Enemy.Value.transform.position;
            direction.y = 0f;

            float distanceToTarget = direction.magnitude;
            if (distanceToTarget <= Enemy.Value.EnemyData.AttackRange ||
                    _skillModule.HasAvailableSkill(distanceToTarget))
            {
                _navMeshAgent.KeepChase(false);

                bool aligned = _navMeshAgent.RotateToTarget(_targetPos, 1f);
                return aligned ? Status.Success : Status.Running;
            }

            _navMeshAgent.KeepChase(true);
            if (Vector3.Distance(_lastTargetPos, _targetPos) >= DestinationUpdateDistance)
            {
                _lastTargetPos = _targetPos;
                _navMeshAgent.MoveTo(_targetPos);
            }

            return Status.Running;
        }
    }
}