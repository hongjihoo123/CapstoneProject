using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.DamageProjectiles;
using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.BossSkills
{
    public class BossThornSkill : AbstractBossSkill
    {
        [Header("Thorn Settings")]
        [SerializeField] private BossThornProjectile thornPrefab;
        [SerializeField] private BossDamageProjectile shardPrefab;
        [SerializeField] private LayerMask groundLayer;

        [Header("Phase 1")]
        [SerializeField] private int phase1ThornCount = 3;
        [SerializeField] private float phase1ThornInterval = 0.5f;
        [SerializeField] private float phase1ThornStopTime = 0.3f;
        [SerializeField] private float phase1WarningRadius = 1.5f;
        [SerializeField] private float phase1DamageRadius = 1.5f;

        [Header("Phase 2")]
        [SerializeField] private int phase2ThornCount = 3;
        [SerializeField] private float phase2ThornInterval = 0.5f;
        [SerializeField] private float phase2ThornStopTime = 0.3f;
        [SerializeField] private float phase2WarningRadius = 1.5f;
        [SerializeField] private float phase2DamageRadius = 1.5f;
        [SerializeField] private int shardCount = 8;
        [SerializeField] private float shardSpeed = 8f;
        [SerializeField] private float shardDelay = 0.2f;

        private Vector3 _thornPosition;
        private float _warningRadius;
        private float _damageRadius;
        private bool _showWarning;

        protected override void LateUpdate()
        {
            base.LateUpdate();

            if (!_showWarning)
                return;

            _skillModule.AreaVisualizer.Show(AttackArea.Circle(
                AttackAreaKind.EnemyWarning, _thornPosition, _warningRadius, 0.1f));
        }

        protected override IEnumerator Execute()
        {
            if (IsTransition)
                yield break;

            int thornCount = IsPhase2 ? phase2ThornCount : phase1ThornCount;
            float thornInterval = IsPhase2 ? phase2ThornInterval : phase1ThornInterval;
            float thornStopTime = IsPhase2 ? phase2ThornStopTime : phase1ThornStopTime;
            float warningRadius = IsPhase2 ? phase2WarningRadius : phase1WarningRadius;
            float damageRadius = IsPhase2 ? phase2DamageRadius : phase1DamageRadius;

            yield return PlayAnimationAndWait(SkillSetting.PrepareAnimParam, timeout: SkillSetting.PrepareTime);

            yield return PlayAnimationAndWait(SkillSetting.AttackAnimParam,
                () => ExecuteThornAttacks(thornCount, thornInterval, thornStopTime,
                    warningRadius, damageRadius));
        }

        private IEnumerator ExecuteThornAttacks(int thornCount, float thornInterval, float thornStopTime,
                                                                                float warningRadius, float damageRadius)
        {
            const float expandTime = 0.3f;
            const float warningDuration = 0.12f;

            for (int i = 0; i < thornCount; i++)
            {
                float elapsed = 0f;
                Vector3 groundPosition = Vector3.zero;

                while (elapsed < expandTime)
                {
                    if (!TryGetTargetPosition(out Vector3 targetPosition))
                    {
                        _skillModule.AreaVisualizer.Hide(AttackAreaKind.EnemyWarning);
                        yield break;
                    }

                    groundPosition = GetGroundPosition(targetPosition);

                    float t = Mathf.Clamp01(elapsed / expandTime);
                    float currentRadius = Mathf.Lerp(0.1f, warningRadius, t);

                    _skillModule.AreaVisualizer.Hide(AttackAreaKind.EnemyWarning);

                    _skillModule.AreaVisualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning, groundPosition,
                                                                                                             warningRadius, warningDuration));

                    _skillModule.AreaVisualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning, groundPosition,
                                                                                                            currentRadius, warningDuration));

                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (!TryGetTargetPosition(out Vector3 finalTargetPosition))
                {
                    _skillModule.AreaVisualizer.Hide(AttackAreaKind.EnemyWarning);
                    yield break;
                }

                groundPosition = GetGroundPosition(finalTargetPosition);

                _skillModule.AreaVisualizer.Hide(AttackAreaKind.EnemyWarning);
                _skillModule.AreaVisualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning, groundPosition,
                                                                                                                warningRadius, thornStopTime + 0.1f));

                yield return new WaitForSeconds(thornStopTime);

                _skillModule.AreaVisualizer.Hide(AttackAreaKind.EnemyWarning);

                if (thornPrefab != null)
                {
                    BossThornProjectile thorn = Instantiate(thornPrefab, groundPosition, Quaternion.identity);
                    thorn.Play(groundPosition);
                }

                DamageInCircle(groundPosition, damageRadius);

                if (IsPhase2)
                {
                    yield return new WaitForSeconds(shardDelay);
                    FireShards(groundPosition);
                }

                if (i < thornCount - 1)
                    yield return new WaitForSeconds(thornInterval);
            }

            _skillModule.AreaVisualizer.Hide(AttackAreaKind.EnemyWarning);
        }

        private IEnumerator FireShardsAfterDelay(Vector3 position)
        {
            yield return new WaitForSeconds(shardDelay);
            FireShards(position);
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

        protected override void OnCancelled()
        {
            _skillModule.AreaVisualizer.Hide(AttackAreaKind.EnemyWarning);
        }
    }
}