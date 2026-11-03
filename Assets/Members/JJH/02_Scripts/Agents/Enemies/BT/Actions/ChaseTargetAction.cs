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
                Enemy.Value.NavMeshAgent == null || Enemy.Value.SkillModule == null ||
                Target.Value == null)
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
            if (Enemy.Value == null || Target.Value == null)
                return Status.Failure;

            EnemyDataSO enemyData = Enemy.Value.EnemyData;

            if (!_sensor.IsTargetInRange(enemyData.DetectRange, out Collider _))
                return Status.Failure;

            _targetPos = Target.Value.transform.position;

            // 시야에 없으면 계속 따라가기
            if (!_sensor.IsTargetInSight(enemyData.DetectRange, enemyData.AttackSight))
                return Chase();

            Vector3 direction = _targetPos - Enemy.Value.transform.position;
            direction.y = 0f;
            float distance = direction.magnitude;

            bool canNormalAttack = enemyData.CanNormalAttack && distance <= enemyData.AttackRange;
            bool skillAvailable = _skillModule.HasAvailableSkill(distance);
            bool normalAttackInRange = enemyData.CanNormalAttack && distance <= enemyData.AttackRange;
            bool skillInRange = _skillModule.HasSkillInRange(distance);

            // 일반 공격/스킬 가능하면 멈추고 바라본 후 Success
            if (canNormalAttack || skillAvailable)
            {
                _navMeshAgent.KeepChase(false);
                bool aligned = _navMeshAgent.RotateToTarget(_targetPos, 1f);
                return aligned ? Status.Success : Status.Running;
            }

            // 사거리 되지만 쿨타임 중이면 기다리기
            if (skillInRange && !enemyData.CanNormalAttack || normalAttackInRange)
            {
                _navMeshAgent.KeepChase(false);
                _navMeshAgent.RotateToTarget(_targetPos, 1f);
                return Status.Running;
            }

            return Chase();
        }

        private Status Chase()
        {
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