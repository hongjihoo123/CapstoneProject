using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyMeleeBombSkill : AbstractEnemySkill
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
            _skillModule.AreaVisualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning,
                                                                                center, SkillSO.SkillRange,
                                                                                SkillSO.SkillPrepareTime + SkillSO.SkillExpandTime));

            yield return new WaitForSeconds(SkillSO.SkillPrepareTime);

            float elapsed = 0f;
            float startRadius = 0.1f;

            while (elapsed < SkillSO.SkillExpandTime)
            {
                elapsed += Time.deltaTime;
                float radius = Mathf.Lerp(startRadius, SkillSO.SkillRange, Mathf.Clamp01(elapsed / SkillSO.SkillExpandTime));

                _skillModule.AreaVisualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning,
                                                                                                    center, radius, Time.deltaTime * 2f));

                yield return null;
            }

            Explode(center, SkillSO.SkillRange);

            IsUsing = false;
            _skillCoroutine = null;
        }

        private void Explode(Vector3 center, float radius)
        {
        }
    }
}