using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyScreamSkill : AbstractEnemySkill
    {
        [Header("Fire Breath Settings")]
        [SerializeField] private float angle = 45f;

        protected override IEnumerator Execute()
        {
            Vector3 center = _enemy.transform.position;
            Quaternion rotation = _enemy.transform.rotation;
            float prepare = SkillSetting.PrepareTime;
            float expand = SkillSetting.ExpandTime;
            float range = SkillSetting.Range;

            _skillModule.AreaVisualizer.Show(AttackArea.Cone(AttackAreaKind.EnemyWarning,
                center, rotation, range, angle, prepare + expand + SkillSetting.AttackDutation));

            yield return PlayAnimationAndWait(SkillSetting.PrepareAnimParam, timeout: prepare);
            yield return ExpandConeWarning(center, rotation, range, angle, expand);

            yield return PlayAnimationAndWait(SkillSetting.AttackAnimParam,
                () => ExecuteScream(center, rotation, range, angle));

            _skillModule.AreaVisualizer.Hide(AttackAreaKind.EnemyWarning);
        }

        private IEnumerator ExecuteScream(Vector3 center, Quaternion rotation, float range, float angle)
        {
            PlaySkillVfx();
            DamageInCone(center, rotation, range, angle);

            yield return new WaitForSeconds(SkillSetting.AttackDutation);
        }

#if UNITY_EDITOR

        protected override void DrawRangeGizmo()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
            DrawGizmoCone(transform.position, transform.rotation, SkillSetting.Range, angle);
        }

#endif
    }
}