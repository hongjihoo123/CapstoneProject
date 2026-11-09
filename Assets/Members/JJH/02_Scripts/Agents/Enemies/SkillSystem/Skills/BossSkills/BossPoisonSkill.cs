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

        [Header("Phase 1")]
        [SerializeField] private float phase1ProjectileSpeed = 10f;
        [SerializeField] private float phase1AreaRadius = 2f;
        [SerializeField] private float phase1AreaDuration = 5f;
        [SerializeField] private float phase1DamageInterval = 0.5f;

        [Header("Phase 2")]
        [SerializeField] private float phase2ProjectileSpeed = 10f;
        [SerializeField] private float phase2AreaRadius = 2f;
        [SerializeField] private float phase2AreaDuration = 5f;
        [SerializeField] private float phase2DamageInterval = 0.5f;
        [SerializeField] private int splashCount = 6;
        [SerializeField] private float splashSpeed = 5f;

        protected override IEnumerator Execute()
        {
            if (IsTransition)
                yield break;

            float projectileSpeed = IsPhase2 ? phase2ProjectileSpeed : phase1ProjectileSpeed;
            float areaRadius = IsPhase2 ? phase2AreaRadius : phase1AreaRadius;
            float areaDuration = IsPhase2 ? phase2AreaDuration : phase1AreaDuration;
            float damageInterval = IsPhase2 ? phase2DamageInterval : phase1DamageInterval;

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