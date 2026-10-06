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

        [SerializeReference] public BlackboardVariable<bool> IsMove;

        private float _cooldownTimer = 0f;

        protected override Status OnStart()
        {
            if (Enemy.Value == null || Enemy.Value.EnemyData == null)
                return Status.Failure;

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (Time.time < _cooldownTimer)
                return Status.Running;

            Enemy.Value.Attack();
            IsMove.Value = false;

            _cooldownTimer = Time.time + Enemy.Value.EnemyData.AttackCooltime;

            return Status.Success;
        }
    }
}