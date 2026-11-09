using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.DamageProjectiles;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.BossSkills
{
    public class BossPhaseTransitionSkill : AbstractBossSkill
    {
        [Header("Root Settings")]
        [SerializeField] private BossDamageProjectile rootPrefab;
        [SerializeField] private GameObject warningPrefab;

        [SerializeField] private int rootCount = 7;
        [SerializeField] private float fanAngle = 100f;
        [SerializeField] private float rootSpeed = 12f;
        [SerializeField] private float rootHeight = 0.3f;

        private readonly List<GameObject> _warnings = new();

        protected override IEnumerator Execute()
        {
            Vector3 origin = _enemy.transform.position;
            Quaternion rotation = _enemy.transform.rotation;

            int safeLane = Random.Range(0, rootCount);

            ShowWarnings(origin, rotation, safeLane);

            yield return new WaitForSeconds(SkillSO.PrepareTime);

            ClearWarnings();

            FireRoots(origin, rotation, safeLane);

            yield return new WaitForSeconds(SkillSO.AttackDutation);

            Boss.CompletePhaseTransition();
        }

        private void ShowWarnings(Vector3 origin, Quaternion rotation, int safeLane)
        {
            if (warningPrefab == null)
                return;

            float laneAngle = fanAngle / rootCount;
            float range = SkillSO.Range;
            float width = range * Mathf.Sin(laneAngle * Mathf.Deg2Rad) * 0.9f;

            for (int i = 0; i < rootCount; i++)
            {
                if (i == safeLane)
                    continue;

                float angle = Mathf.Lerp(-fanAngle * 0.5f, fanAngle * 0.5f, (i + 0.5f) / rootCount);

                Vector3 direction = rotation * Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 center = origin + direction * (range * 0.5f);
                center.y += rootHeight;

                GameObject warning = Instantiate(warningPrefab, center, Quaternion.LookRotation(direction));
                warning.transform.localScale = new Vector3(width, 0.1f, range);

                _warnings.Add(warning);
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

                float angle = Mathf.Lerp(-fanAngle * 0.5f, fanAngle * 0.5f, (i + 0.5f) / rootCount);
                Vector3 direction = rotation * Quaternion.Euler(0f, angle, 0f)
                                    * Vector3.forward;

                BossDamageProjectile root = Instantiate(rootPrefab, origin + Vector3.up * rootHeight,
                                                                                    Quaternion.LookRotation(direction));

                root.Initialize(direction, rootSpeed, SkillSO.Damage, _enemy.gameObject);
            }
        }

        private void ClearWarnings()
        {
            foreach (GameObject warning in _warnings)
            {
                if (warning != null)
                    Destroy(warning);
            }

            _warnings.Clear();
        }

        protected override void OnCancelled()
        {
            ClearWarnings();
        }
    }
}