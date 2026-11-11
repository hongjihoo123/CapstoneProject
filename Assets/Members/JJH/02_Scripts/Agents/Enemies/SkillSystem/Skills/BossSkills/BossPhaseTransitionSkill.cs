using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.DamageProjectiles;
using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.BossSkills
{
    public class BossPhaseTransitionSkill : AbstractBossSkill
    {
        [Header("Root Settings")]
        [SerializeField] private BossDamageProjectile rootPrefab;

        [SerializeField, Min(2)] private int rootCount = 7;
        [SerializeField] private float fanAngle = 100f;
        [SerializeField] private float rootSpeed = 12f;
        [SerializeField] private float rootHeight = 0.3f;

        protected override IEnumerator Execute()
        {
            if (!IsTransition || Boss == null)
                yield break;

            Vector3 origin = _enemy.transform.position;
            Quaternion rotation = _enemy.transform.rotation;

            int safeLane = Random.Range(0, rootCount);

            ShowWarnings(origin, rotation, safeLane);

            yield return new WaitForSeconds(SkillSO.PrepareTime);

            FireRoots(origin, rotation, safeLane);

            yield return new WaitForSeconds(SkillSO.AttackDutation);

            Boss.CompletePhaseTransition();
        }

        private void ShowWarnings(Vector3 origin, Quaternion rotation, int safeLane)
        {
            float laneAngle = fanAngle / rootCount;
            float range = SkillSO.Range;
            float width = range * Mathf.Sin(laneAngle * Mathf.Deg2Rad) * 0.9f;

            for (int i = 0; i < rootCount; i++)
            {
                if (i == safeLane)
                    continue;

                float angle = Mathf.Lerp(-fanAngle * 0.5f, fanAngle * 0.5f,
                    (i + 0.5f) / rootCount);

                Vector3 direction = rotation * Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 center = origin + direction * (range * 0.5f);

                _skillModule.AreaVisualizer.Show(AttackArea.Box(AttackAreaKind.EnemyWarning,
                    center, Quaternion.LookRotation(direction), new Vector3(width, 0f, range), SkillSO.PrepareTime));
            }
        }

        private void FireRoots(Vector3 origin, Quaternion rotation, int safeLane)
        {
            if (rootPrefab == null)
                return;

            for (int i = 0; i < rootCount; i++)
            {
                if (i == safeLane)
                    continue;

                float angle = Mathf.Lerp(-fanAngle * 0.5f, fanAngle * 0.5f,
                    (i + 0.5f) / rootCount);

                Vector3 direction = rotation * Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

                BossDamageProjectile root = Instantiate(rootPrefab,
                    origin + Vector3.up * rootHeight, Quaternion.LookRotation(direction));

                root.Initialize(direction, rootSpeed, SkillSO.Damage, _enemy.gameObject);
            }
        }

        protected override void OnCancelled()
        {
            _skillModule.AreaVisualizer.Hide(AttackAreaKind.EnemyWarning);
        }
    }
}