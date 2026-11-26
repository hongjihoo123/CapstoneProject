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
            float prepare = SkillSetting.PrepareTime;
            float range = SkillSetting.Range;

            _skillModule.AreaVisualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning,
                                                                                                center, range, prepare));

            yield return PlayAnimationAndWait(SkillSetting.PrepareAnimParam, timeout: prepare);

            yield return PlayAnimationAndWait(SkillSetting.AttackAnimParam,
                () => ExecuteExplosion(center, range));

            _skillModule.AreaVisualizer.Hide(AttackAreaKind.EnemyWarning);
        }

        private IEnumerator ExecuteExplosion(Vector3 center, float range)
        {
            PlaySkillVfx();
            DamageInCircle(center, range);
            yield break;
        }

#if UNITY_EDITOR

        protected override void DrawRangeGizmo()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.9f);
            DrawGizmoCircle(transform.position, SkillSetting.Range);
        }

#endif
    }
}