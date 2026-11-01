using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyExplosionSkill : AbstractEnemySkill
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
            float prepareTime = SkillSO.PrepareTime;
            float expandTime = SkillSO.ExpandTime;
            float skillRange = SkillSO.Range;

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

            Collider[] hits = Physics.OverlapSphere(center, skillRange, _skillModule.TargetLayer);
            foreach (Collider hit in hits)
                ApplyDamage(hit);

            IsUsing = false;
            _skillCoroutine = null;
        }
    }
}