using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyGroundSlamSkill : AbstractEnemySkill
    {
        private Coroutine _skillCoroutine;

        public override void UseSkill()
        {
            base.UseSkill();

            _skillCoroutine = StartCoroutine(UseSkillCoroutine());
        }

        private IEnumerator UseSkillCoroutine()
        {
            IsUsing = true;

            Vector3 center = _enemy.transform.position;
            float prepareTime = SkillSO.SkillPrepareTime;
            float expandTime = SkillSO.SkillExpandTime;
            float skillRange = SkillSO.SkillRange;

            _skillModule.AreaVisualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning,
                                                                   center, skillRange, prepareTime + expandTime));

            yield return new WaitForSeconds(prepareTime);

            float elapsed = 0f;
            float startRadius = 0.1f;

            while (elapsed < expandTime)
            {
                elapsed += Time.deltaTime;
                float radius = Mathf.Lerp(startRadius, skillRange, Mathf.Clamp01(elapsed / expandTime));

                _skillModule.AreaVisualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning,
                                                                                                    center, radius, Time.deltaTime * 2f));

                yield return null;
            }

            Explode(center, skillRange);

            IsUsing = false;
            _skillCoroutine = null;
        }

        private void Explode(Vector3 center, float radius)
        {
        }
    }
}