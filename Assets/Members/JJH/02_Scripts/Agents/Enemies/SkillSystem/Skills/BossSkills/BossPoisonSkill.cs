using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.DamageProjectiles;
using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.BossSkills
{
    public class BossPoisonSkill : AbstractBossSkill
    {
        [Header("Poison Settings")]
        [SerializeField] private BossPoisonProjectile projectilePrefab;
        [SerializeField] private BossPoisonArea areaPrefab;
        [SerializeField] private Transform projectileSpawnPoint;

        [SerializeField] private float projectileSpeed = 10f;
        [SerializeField] private float areaRadius = 2f;
        [SerializeField] private float areaDuration = 5f;
        [SerializeField] private float damageInterval = 0.5f;

        [Header("Phase 2")]
        [SerializeField] private int splashCount = 6;
        [SerializeField] private float splashSpeed = 5f;

        protected override IEnumerator Execute()
        {
            if (!TryGetTargetPosition(out Vector3 targetPosition))
                yield break;

            _skillModule.AreaVisualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning,
                                                                    targetPosition, areaRadius, SkillSO.PrepareTime));

            yield return new WaitForSeconds(SkillSO.PrepareTime);

            if (projectilePrefab == null)
                yield break;

            Vector3 origin = projectileSpawnPoint != null
                ? projectileSpawnPoint.position
                : _enemy.transform.position + Vector3.up;

            Vector3 direction = (targetPosition - origin).normalized;

            BossPoisonProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
            projectile.Initialize(direction, projectileSpeed, _enemy.gameObject, areaPrefab,
                SkillSO.Damage, areaRadius, areaDuration, damageInterval,
                IsPhase2 ? splashCount : 0, splashSpeed);
        }
    }
}