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

        [SerializeField] private int thornCount = 3;
        [SerializeField] private float thornInterval = 0.5f;
        [SerializeField] private float warningRadius = 1.5f;
        [SerializeField] private float damageRadius = 1.5f;

        [Header("Shard Settings")]
        [SerializeField] private int shardCount = 8;
        [SerializeField] private float shardSpeed = 8f;
        [SerializeField] private float shardDelay = 0.2f;

        protected override IEnumerator Execute()
        {
            for (int i = 0; i < thornCount; i++)
            {
                if (!TryGetTargetPosition(out Vector3 targetPosition))
                    yield break;

                Vector3 groundPosition = GetGroundPosition(targetPosition);

                _skillModule.AreaVisualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning,
                                                                groundPosition, warningRadius, SkillSO.PrepareTime));

                yield return new WaitForSeconds(SkillSO.PrepareTime);

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
            if (shardPrefab == null)
                return;

            for (int i = 0; i < shardCount; i++)
            {
                float angle = 360f / shardCount * i;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

                BossDamageProjectile projectile = Instantiate(shardPrefab, position + Vector3.up * 0.5f, Quaternion.identity);

                projectile.Initialize(direction, shardSpeed, SkillSO.Damage, _enemy.gameObject);
            }
        }
    }
}