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
        [SerializeField] private float arcHeight = 4f;

        [Header("Phase 1")]
        [SerializeField] private float phase1ProjectileSpeed = 10f;
        [SerializeField] private float phase1AreaRadius = 2f;
        [SerializeField] private float phase1AreaDuration = 5f;
        [SerializeField] private float phase1DamageInterval = 0.5f;
        [SerializeField] private float phase1FlightTime = 1.5f;
        [Header("Phase 2")]
        [SerializeField] private float phase2ProjectileSpeed = 10f;
        [SerializeField] private float phase2AreaRadius = 2f;
        [SerializeField] private float phase2AreaDuration = 5f;
        [SerializeField] private float phase2DamageInterval = 0.5f;
        [SerializeField] private int splashCount = 6;
        [SerializeField] private float splashSpeed = 5f;
        [SerializeField] private float phase2FlightTime = 1.5f;

        protected override IEnumerator Execute()
        {
            if (IsTransition)
                yield break;

            float projectileSpeed = IsPhase2 ? phase2ProjectileSpeed : phase1ProjectileSpeed;
            float areaRadius = IsPhase2 ? phase2AreaRadius : phase1AreaRadius;
            float areaDuration = IsPhase2 ? phase2AreaDuration : phase1AreaDuration;
            float damageInterval = IsPhase2 ? phase2DamageInterval : phase1DamageInterval;
            float flightTime = IsPhase2 ? phase2FlightTime : phase1FlightTime;

            if (!TryGetTargetPosition(out Vector3 targetPosition))
                yield break;

            _skillModule.AreaVisualizer.Show(AttackArea.Box(AttackAreaKind.EnemyWarning,
                targetPosition, Quaternion.identity, new Vector3(10, 1, 10), SkillSetting.PrepareTime));

            yield return new WaitForSeconds(SkillSetting.PrepareTime);

            if (projectilePrefab == null)
                yield break;

            Vector3 origin = projectileSpawnPoint != null
                ? projectileSpawnPoint.position
                : _enemy.transform.position + Vector3.up;

            BossPoisonProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);

            projectile.InitializeArc(targetPosition, flightTime, arcHeight, _enemy.gameObject, areaPrefab,
                                                        SkillSetting.Damage, areaRadius, areaDuration, damageInterval,
                                                        IsPhase2 ? splashCount : 0, splashSpeed);
        }
    }
}