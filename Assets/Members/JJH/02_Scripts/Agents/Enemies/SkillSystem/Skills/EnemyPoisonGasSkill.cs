using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.DamageProjectiles;
using Members.JJH._02_Scripts.Systems.ObjectPoolSystem.Runtime;
using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyPoisonGasSkill : AbstractEnemySkill
    {
        [Header("Poison Gas")]
        [SerializeField] private PoisonGas gasPrefab;
        [SerializeField] private float areaDuration = 5f;
        [SerializeField] private float damageInterval = 0.5f;

        private PoolInitializer _poolInitializer;

        protected override IEnumerator Execute()
        {
            Vector3 center = _enemy.transform.position;
            float range = SkillSetting.Range;
            float prepareTime = SkillSetting.PrepareTime;

            _skillModule.AreaVisualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning, center, range,
                                                                                                prepareTime + SkillSetting.AttackDutation));

            yield return PlayAnimationAndWait(SkillSetting.PrepareAnimParam, timeout: prepareTime);

            PlaySkillVfx();

            yield return PlayAnimationAndWait(SkillSetting.AttackAnimParam,
                                                                    () => ExecutePoisonArea(center, range));

            _skillModule.AreaVisualizer.Hide(AttackAreaKind.EnemyWarning);
        }

        private IEnumerator ExecutePoisonArea(Vector3 center, float range)
        {
            if (_poolInitializer == null)
                _poolInitializer = FindFirstObjectByType<PoolInitializer>();

            PoisonGas area = _poolInitializer.Pop<PoisonGas>(gasPrefab.PoolItem);
            area.transform.SetPositionAndRotation(center, Quaternion.identity);
            area.Initialize(_enemy.gameObject, range, SkillSetting.Damage, areaDuration, damageInterval);

            yield break;
        }

        protected override void DrawRangeGizmo()
        {
            Gizmos.DrawWireSphere(_enemy.transform.position, SkillSetting.Range);
        }
    }
}