using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Enemy Attack", story: "[Enemy] Attack", category: "Action", id: "6af70a90fb7425aeb4805e1b7a3bfb46")]
    public partial class AttackAction : Action
    {
    [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;
        private float _elapsedTime;
        private float _attackTime;

        protected override Status OnStart()
        {
            if (Enemy.Value == null || Enemy.Value.EnemyData == null)
                return Status.Failure;

            _elapsedTime = 0f;
            _attackTime = Enemy.Value.EnemyData.AttackCooltime;

            Enemy.Value.Attack();

            if (_attackTime <= 0f)
                return Status.Success;

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            _elapsedTime += Time.deltaTime;

            if (_elapsedTime >= _attackTime)
                return Status.Success;

            return Status.Running;
        }

        protected override void OnEnd()
        {
            _elapsedTime = 0f;
            _attackTime = 0f;
        }
    }
}