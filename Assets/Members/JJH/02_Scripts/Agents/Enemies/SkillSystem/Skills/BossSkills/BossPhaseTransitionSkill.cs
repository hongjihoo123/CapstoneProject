using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.DamageProjectiles;
using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.BossSkills
{
    public class BossPhaseTransitionSkill : AbstractBossSkill, ITransitionSkill
    {
        [Header("Root Settings")]
        [SerializeField] private MovingRootProjectile rootPrefab;
        [SerializeField] private Transform rootSpawnPoint;

        [SerializeField, Min(2)] private int rootCount = 5;
        [SerializeField] private float rootWidth = 1.5f;
        [SerializeField] private float rootSpeed = 2f;
        [SerializeField] private float rootMaxSpeed = 18f;
        [SerializeField] private float rootAcceleration = 8f;
        [SerializeField] private float rootHeight = 0.3f;
        [SerializeField, Min(1)] private int rootRepeatCount = 3;
        [SerializeField] private float rootRepeatDelay = 1.5f;

        protected override IEnumerator Execute()
        {
            if (!IsTransition || Boss == null)
                yield break;

            Quaternion rotation = _enemy.transform.rotation;

            for (int i = 0; i < rootRepeatCount; i++)
            {
                Vector3 origin = _enemy.transform.position;
                int safeLane = Random.Range(0, rootCount);

                ShowWarnings(origin, rotation, safeLane);

                yield return new WaitForSeconds(SkillSetting.PrepareTime);

                FireRoots(origin, rotation, safeLane);

                if (i < rootRepeatCount - 1)
                    yield return new WaitForSeconds(rootRepeatDelay);
            }

            yield return new WaitForSeconds(SkillSetting.AttackDutation);

            Boss.CompletePhaseTransition();
        }

        private void ShowWarnings(Vector3 origin, Quaternion rotation, int safeLane)
        {
            float range = SkillSetting.Range;
            float laneWidth = rootWidth;

            Vector3 right = rotation * Vector3.right;
            Vector3 forward = rotation * Vector3.forward;

            for (int i = 0; i < rootCount; i++)
            {
                if (i == safeLane)
                    continue;

                float offset = (i - (rootCount - 1) * 0.5f) * laneWidth;
                Vector3 center = rootSpawnPoint.position + right * offset + forward * (range * 0.5f);

                _skillModule.AreaVisualizer.Show(AttackArea.Box(AttackAreaKind.EnemyWarning,
                    center, rotation, new Vector3(laneWidth, 0f, range), SkillSetting.PrepareTime));
            }
        }

        private void FireRoots(Vector3 origin, Quaternion rotation, int safeLane)
        {
            if (rootPrefab == null || rootSpawnPoint == null)
                return;

            Vector3 right = rotation * Vector3.right;
            Vector3 forward = rotation * Vector3.forward;

            for (int i = 0; i < rootCount; i++)
            {
                if (i == safeLane)
                    continue;

                float offset = (i - (rootCount - 1) * 0.5f) * rootWidth;
                Vector3 position = rootSpawnPoint.position + right * offset;

                MovingRootProjectile root = Instantiate(rootPrefab, position, rotation);
                root.Initialize(forward, rootSpeed, rootMaxSpeed, rootAcceleration,
                    SkillSetting.Damage, _enemy.gameObject);
            }
        }

        protected override void OnCancelled()
        {
            _skillModule.AreaVisualizer.Hide(AttackAreaKind.EnemyWarning);
        }
    }
}