using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyDashSkill : AbstractEnemySkill, IEnemySkill
    {
        [SerializeField] private float dashDistance = 10f;
        [SerializeField] private float dashDuration = 0.2f;

        private Coroutine _dashCoroutine;
        private FxHandle _trail;    // 대쉬 잔상 (임시 이펙트)
        private readonly HashSet<IDamageable> _dashHit = new HashSet<IDamageable>();   // 한 번 대쉬에 한 대만

        private void OnDisable()
        {
            _trail?.Kill();
            _trail = null;
        }

        public override void UseSkill()
        {
            base.UseSkill();

            _dashCoroutine = StartCoroutine(UseSkillCoroutine());
        }

        private IEnumerator UseSkillCoroutine()
        {
            IsUsing = true;

            _skillModule.AreaVisualizer.Show(
                            AttackArea.Box(AttackAreaKind.EnemyWarning,
                                                     _enemy.transform.position + _enemy.transform.forward * (dashDistance * 0.5f),
                                                     _enemy.transform.rotation, new Vector3(2f, 0f, dashDistance),
                                                     SkillSO.SkillPrepareTime));

            yield return new WaitForSeconds(SkillSO.SkillPrepareTime);

            Vector3 startPosition = _enemy.transform.position;
            Vector3 direction = _enemy.transform.forward;
            direction.y = 0f;
            direction.Normalize();

            Vector3 targetPosition = startPosition + direction * dashDistance;
            float elapsed = 0f;

            _dashHit.Clear();
            _trail = Fx.DashTrail(_enemy.transform, EnemyHit.FxColor);
            _trail.SetValue(1f);
            Fx.Shockwave(startPosition, EnemyHit.FxColor, 1.2f);

            while (elapsed < dashDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / dashDuration);
                _enemy.transform.position = Vector3.Lerp(startPosition, targetPosition, progress);
                // 경고 박스 폭(2m)에 맞춰 반지름 1m로 지나가면서 판정
                EnemyHit.Area(_enemy, _enemy.transform.position, 1f, SkillSO.SkillDamage, alreadyHit: _dashHit);
                yield return null;
            }

            _enemy.transform.position = targetPosition;
            _trail.Kill();
            _trail = null;
            Fx.Shockwave(targetPosition, EnemyHit.FxColor, 1.5f);
            IsUsing = false;
            _dashCoroutine = null;
        }
    }
}