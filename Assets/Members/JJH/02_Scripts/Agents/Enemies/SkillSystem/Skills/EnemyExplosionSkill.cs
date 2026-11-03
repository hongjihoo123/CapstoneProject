using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyExplosionSkill : AbstractEnemySkill
    {
        protected override IEnumerator Execute()
        {
            Vector3 center = _enemy.transform.position;
            float prepare = SkillSO.PrepareTime;
            float expand = SkillSO.ExpandTime;
            float range = SkillSO.Range;

            _skillModule.AreaVisualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning,
                center, range, prepare + expand));

            yield return new WaitForSeconds(prepare);
            yield return ExpandCircleWarning(center, range, expand);

            _enemy.VFX.PlayVfx(SkillSO.VFXParam.HashValue);
            DamageInCircle(center, range);
        }
    }
}