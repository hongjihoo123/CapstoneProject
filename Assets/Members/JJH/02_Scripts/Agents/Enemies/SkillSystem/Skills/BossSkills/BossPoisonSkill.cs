using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.DamageProjectiles;
using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.BossSkills
{
    public class BossPoisonSkill : AbstractBossSkill
    {
        [Header("Poison Settings")]
        [SerializeField] private BossPoisonAreaProjectile projectilePrefab;
        [SerializeField] private PoisonArea areaPrefab;
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField] private float arcHeight = 4f;

        [Header("Phase 1")]
        [SerializeField] private float phase1AreaRadius = 2f;
        [SerializeField] private float phase1AreaDuration = 5f;
        [SerializeField] private float phase1DamageInterval = 0.5f;
        [SerializeField] private float phase1FlightTime = 1.5f;
        [Header("Phase 2")]
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

            float areaRadius = IsPhase2 ? phase2AreaRadius : phase1AreaRadius;
            float areaDuration = IsPhase2 ? phase2AreaDuration : phase1AreaDuration;
            float damageInterval = IsPhase2 ? phase2DamageInterval : phase1DamageInterval;
            float flightTime = IsPhase2 ? phase2FlightTime : phase1FlightTime;
            int currentSplashCount = IsPhase2 ? splashCount : 0;

            if (!TryGetTargetPosition(out Vector3 targetPosition) || projectilePrefab == null)
                yield break;

            _skillModule.AreaVisualizer.Show(AttackArea.Box(AttackAreaKind.EnemyWarning,
                targetPosition, Quaternion.identity, new Vector3(10, 1, 10), SkillSetting.PrepareTime));

            yield return PlayAnimationAndWait(SkillSetting.PrepareAnimParam, timeout: SkillSetting.PrepareTime);

            yield return PlayAnimationAndWait(SkillSetting.AttackAnimParam,
                () => ExecutePoisonAttack(targetPosition, flightTime, areaRadius,
                    areaDuration, damageInterval, currentSplashCount));
        }

        private IEnumerator ExecutePoisonAttack(Vector3 targetPosition, float flightTime,
            float areaRadius, float areaDuration, float damageInterval, int currentSplashCount)
        {
            Vector3 origin = projectileSpawnPoint != null
                ? projectileSpawnPoint.position
                : _enemy.transform.position + Vector3.up;

            BossPoisonAreaProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);

            projectile.InitializeArc(targetPosition, flightTime, arcHeight, _enemy.gameObject, areaPrefab,
                SkillSetting.Damage, areaRadius, areaDuration, damageInterval,
                currentSplashCount, splashSpeed);

            yield break;
        }
    }
}