using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyShoutSkill : AbstractEnemySkill
    {
        [SerializeField] private float angle = 45f;

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
            Quaternion rotation = _enemy.transform.rotation;

            _skillModule.AreaVisualizer.Show(AttackArea.Cone(AttackAreaKind.EnemyWarning,
                                                                        center, rotation, SkillSO.SkillRange,
                                                                        angle, SkillSO.SkillPrepareTime + SkillSO.SkillExpandTime));

            yield return new WaitForSeconds(SkillSO.SkillPrepareTime);

            float elapsed = 0f;
            float startRange = 0.1f;

            while (elapsed < SkillSO.SkillExpandTime)
            {
                elapsed += Time.deltaTime;
                float currentRange = Mathf.Lerp(startRange, SkillSO.SkillRange,
                                                            Mathf.Clamp01(elapsed / SkillSO.SkillExpandTime));

                _skillModule.AreaVisualizer.Show(AttackArea.Cone(AttackAreaKind.EnemyWarning,
                                                                            center, rotation, currentRange, angle, Time.deltaTime * 2f));

                yield return null;
            }

            Vector3 forward = rotation * Vector3.forward;
            Fx.ConeBlast(center, forward, SkillSO.SkillRange, angle, EnemyHit.FxColor);
            EnemyHit.Area(_enemy, center, SkillSO.SkillRange, SkillSO.SkillDamage, forward, angle);

            IsUsing = false;
            _skillCoroutine = null;
        }
    }
}