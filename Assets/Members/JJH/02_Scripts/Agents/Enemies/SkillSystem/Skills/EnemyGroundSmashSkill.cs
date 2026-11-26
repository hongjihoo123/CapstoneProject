using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyGroundSmashSkill : AbstractEnemySkill
    {
        protected override IEnumerator Execute()
        {
            Vector3 center = _enemy.transform.position;
            float range = SkillSetting.Range;
            float prepareTime = SkillSetting.PrepareTime;

            _skillModule.AreaVisualizer.Show(
                AttackArea.Circle(AttackAreaKind.EnemyWarning, center, range, prepareTime + SkillSetting.AttackDutation));

            yield return PlayAnimationAndWait(SkillSetting.PrepareAnimParam, timeout: prepareTime);

            PlaySkillVfx();

            yield return PlayAnimationAndWait(SkillSetting.AttackAnimParam,
                () => ExecuteGroundSmash(center, range));

            _skillModule.AreaVisualizer.Hide(AttackAreaKind.EnemyWarning);
        }

        private IEnumerator ExecuteGroundSmash(Vector3 center, float range)
        {
            DamageInCircle(center, range);
            yield break;
        }

        protected override void DrawRangeGizmo()
        {
            Gizmos.DrawWireSphere(_enemy.transform.position, SkillSetting.Range);
        }
    }
}