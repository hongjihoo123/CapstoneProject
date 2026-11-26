using System;
using Members.JJH._02_Scripts.Systems.AnimatorSystem;
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

        [SerializeReference] public BlackboardVariable<AnimParamSO> AnimParam;

        private EnemyRenderer _enemyRenderer;
        private float _cooldownTimer = 0f;
        private bool _isAttacking;
        private bool _hasAttacked;
        private bool _isAnimationEnd;

        protected override Status OnStart()
        {
            if (Enemy.Value == null || Enemy.Value.EnemyData == null || AnimParam == null || AnimParam.Value == null)
                return Status.Failure;

            _enemyRenderer = Enemy.Value.Renderer as EnemyRenderer;

            if (_enemyRenderer == null)
                return Status.Failure;

            _isAttacking = false;
            _hasAttacked = false;
            _isAnimationEnd = false;

            _enemyRenderer.OnAttackEvent += OnAttack;
            _enemyRenderer.OnAnimationEndEvent += OnAnimationEnd;

            if (Time.time >= _cooldownTimer)
                StartAttack();

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (Enemy.Value == null)
                return Status.Failure;

            if (!_isAttacking)
            {
                if (Time.time < _cooldownTimer)
                    return Status.Running;

                StartAttack();
            }

            if (!_isAnimationEnd)
                return Status.Running;

            return Status.Success;
        }

        private void StartAttack()
        {
            _isAttacking = true;
            _cooldownTimer = Time.time + Enemy.Value.EnemyData.AttackCooltime;

            Enemy.Value.PlayAnimation(AnimParam.Value, 0.1f);
        }

        private void OnAttack()
        {
            if (!_isAttacking || _hasAttacked)
                return;

            _hasAttacked = true;
            Enemy.Value.NormalAttack();
        }

        private void OnAnimationEnd()
            => _isAnimationEnd = true;

        protected override void OnEnd()
        {
            if (_enemyRenderer != null)
            {
                _enemyRenderer.OnAttackEvent -= OnAttack;
                _enemyRenderer.OnAnimationEndEvent -= OnAnimationEnd;
            }

            _isAttacking = false;
        }
    }
}