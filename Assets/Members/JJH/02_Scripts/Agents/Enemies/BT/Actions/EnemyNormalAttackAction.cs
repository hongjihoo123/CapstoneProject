using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Enemy NormalAttack", story: "[Enemy] NormalAttack", category: "Action/Combat", id: "6af70a90fb7425aeb4805e1b7a3bfb46")]
    public partial class EnemyNormalAttackAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;

        private float _cooldownTimer = 0f;

        protected override Status OnStart()
        {
            if (Enemy.Value == null || Enemy.Value.EnemyData == null ||
                Enemy.Value.SkillModule == null || Enemy.Value.SkillModule.IsSkillUsing())
                return Status.Failure;

            if (Time.time < _cooldownTimer)
                return Status.Running;

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (Time.time < _cooldownTimer)
                return Status.Running;

            if (Enemy.Value.SkillModule.IsSkillUsing())
                return Status.Running;

            if (Enemy.Value.SkillModule.LastSkillEndTime >= 0f &&
                Time.time < Enemy.Value.SkillModule.LastSkillEndTime + Enemy.Value.EnemyData.AttackCooltime)
                return Status.Running;

            Enemy.Value.NormalAttack();
            _cooldownTimer = Time.time + Enemy.Value.EnemyData.AttackCooltime;

            return Status.Success;
        }
    }
}