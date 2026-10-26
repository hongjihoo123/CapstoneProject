using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyDashSkill : AbstractEnemySkill
    {
        private Coroutine _dashCoroutine;

        public override void UseSkill()
        {
            base.UseSkill();

            _dashCoroutine = StartCoroutine(UseSkillCoroutine());
        }

        private IEnumerator UseSkillCoroutine()
        {
            IsUsing = true;

            float prepareTime = SkillSO.PrepareTime;
            float expandTime = SkillSO.ExpandTime;
            float skillRange = SkillSO.Range;

            _skillModule.AreaVisualizer.Show(AttackArea.Box(AttackAreaKind.EnemyWarning,
                                                     _enemy.transform.position + _enemy.transform.forward * (skillRange * 0.5f),
                                                     _enemy.transform.rotation, new Vector3(2f, 0f, skillRange + 1.5f), prepareTime));

            yield return new WaitForSeconds(prepareTime);

            Vector3 startPosition = _enemy.transform.position;
            Vector3 direction = _enemy.transform.forward;
            direction.y = 0f;
            direction.Normalize();

            Vector3 targetPosition = startPosition + direction * (skillRange + 1.5f);
            float elapsed = 0f;

            while (elapsed < expandTime)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / expandTime);
                _enemy.transform.position = Vector3.Lerp(startPosition, targetPosition, progress);
                yield return null;
            }

            _enemy.transform.position = targetPosition;
            IsUsing = false;
            _dashCoroutine = null;
        }
    }
}