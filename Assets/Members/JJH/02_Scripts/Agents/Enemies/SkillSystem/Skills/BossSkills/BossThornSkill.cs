using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.DamageProjectiles;
using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.BossSkills
{
    public class BossThornSkill : AbstractBossSkill
    {
        [Header("Thorn Settings")]
        [SerializeField] private GameObject thornPrefab;
        [SerializeField] private BossDamageProjectile shardPrefab;
        [SerializeField] private LayerMask groundLayer;

        [Header("Phase 1")]
        [SerializeField] private int phase1ThornCount = 3;
        [SerializeField] private float phase1ThornInterval = 0.5f;
        [SerializeField] private float phase1WarningRadius = 1.5f;
        [SerializeField] private float phase1DamageRadius = 1.5f;

        [Header("Phase 2")]
        [SerializeField] private int phase2ThornCount = 3;
        [SerializeField] private float phase2ThornInterval = 0.5f;
        [SerializeField] private float phase2WarningRadius = 1.5f;
        [SerializeField] private float phase2DamageRadius = 1.5f;
        [SerializeField] private int shardCount = 8;
        [SerializeField] private float shardSpeed = 8f;
        [SerializeField] private float shardDelay = 0.2f;

        protected override IEnumerator Execute()
        {
            if (IsTransition)
                yield break;

            int thornCount = IsPhase2 ? phase2ThornCount : phase1ThornCount;
            float thornInterval = IsPhase2 ? phase2ThornInterval : phase1ThornInterval;
            float warningRadius = IsPhase2 ? phase2WarningRadius : phase1WarningRadius;
            float damageRadius = IsPhase2 ? phase2DamageRadius : phase1DamageRadius;

            for (int i = 0; i < thornCount; i++)
            {
                if (!TryGetTargetPosition(out Vector3 targetPosition))
                    yield break;

                Vector3 groundPosition = GetGroundPosition(targetPosition);

                _skillModule.AreaVisualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning,
                    groundPosition, warningRadius, SkillSetting.PrepareTime));

                yield return new WaitForSeconds(SkillSetting.PrepareTime);

                if (thornPrefab != null)
                    Instantiate(thornPrefab, groundPosition, Quaternion.identity);

                DamageInCircle(groundPosition, damageRadius);

                if (IsPhase2)
                {
                    yield return new WaitForSeconds(shardDelay);
                    FireShards(groundPosition);
                }

                yield return new WaitForSeconds(thornInterval);
            }
        }

        private Vector3 GetGroundPosition(Vector3 position)
        {
            if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down,
                out RaycastHit hit, 10f, groundLayer))
                return hit.point;

            return position;
        }

        private void FireShards(Vector3 position)
        {
            if (shardPrefab == null || shardCount <= 0)
                return;

            for (int i = 0; i < shardCount; i++)
            {
                float angle = 360f / shardCount * i;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

                BossDamageProjectile projectile = Instantiate(shardPrefab,
                    position + Vector3.up * 0.5f, Quaternion.identity);

                projectile.Initialize(direction, shardSpeed, SkillSetting.Damage, _enemy.gameObject);
            }
        }
    }
}