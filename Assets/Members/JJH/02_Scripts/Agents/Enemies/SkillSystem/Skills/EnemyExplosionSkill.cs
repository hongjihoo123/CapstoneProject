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

            PlaySkillVfx();
            DamageInCircle(center, range);
        }

#if UNITY_EDITOR

        protected override void DrawRangeGizmo()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.9f);
            DrawGizmoCircle(transform.position, SkillSO.Range);
        }

#endif
    }
}