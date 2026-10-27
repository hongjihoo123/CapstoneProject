using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyMeleeBombSkill : AbstractEnemySkill
    {
        private Coroutine _skillCoroutine;
        private FxHandle _charge;   // 터지기 전까지 몸에 모이는 기운 (임시 이펙트)

        private void OnDisable()
        {
            _charge?.Kill();
            _charge = null;
        }

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

            _charge = Fx.Charge(center + Vector3.up, EnemyHit.FxColor);

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
            _charge?.Kill();
            _charge = null;

            Fx.Explosion(center + Vector3.up * 0.5f, radius, EnemyHit.FxColor);
            Fx.Shockwave(center, EnemyHit.FxColor, radius);
            EnemyHit.Area(_enemy, center, radius, SkillSO.SkillDamage);
        }
    }
}