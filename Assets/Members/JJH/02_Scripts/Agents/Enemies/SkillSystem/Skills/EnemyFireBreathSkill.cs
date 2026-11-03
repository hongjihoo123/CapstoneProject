using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyFireBreathSkill : AbstractEnemySkill
    {
        [Header("Fire Breath Settings")]
        [SerializeField] private float angle = 45f;

        protected override IEnumerator Execute()
        {
            Vector3 center = _enemy.transform.position;
            Quaternion rotation = _enemy.transform.rotation;
            float prepare = SkillSO.PrepareTime;
            float expand = SkillSO.ExpandTime;
            float range = SkillSO.Range;

            _skillModule.AreaVisualizer.Show(AttackArea.Cone(AttackAreaKind.EnemyWarning,
                center, rotation, range, angle, prepare + expand));

            yield return new WaitForSeconds(prepare);
            yield return ExpandConeWarning(center, rotation, range, angle, expand);

            _enemy.VFX.PlayVfx(SkillSO.VFXParam.HashValue);
            DamageInCone(center, rotation, range, angle);

            yield return new WaitForSeconds(SkillSO.AttackDutation);
        }

#if UNITY_EDITOR

        protected override void DrawRangeGizmo()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
            DrawGizmoCone(transform.position, transform.rotation, SkillSO.Range, angle);
        }

#endif
    }
}