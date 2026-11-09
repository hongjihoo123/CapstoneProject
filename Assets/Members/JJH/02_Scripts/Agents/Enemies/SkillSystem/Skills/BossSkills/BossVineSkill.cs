using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.DamageProjectiles;
using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.BossSkills
{
    public class BossVineSkill : AbstractBossSkill
    {
        [Header("Vine Settings")]
        [SerializeField] private float angle = 120f;
        [SerializeField] private float secondSwingDelay = 0.4f;

        [Header("Projectile Settings")]
        [SerializeField] private BossDamageProjectile vinePrefab;
        [SerializeField] private int projectileCount = 5;
        [SerializeField] private float projectileSpeed = 7f;
        [SerializeField] private float projectileSpread = 100f;
        [SerializeField] private Transform projectileSpawnPoint;

        protected override IEnumerator Execute()
        {
            int swingCount = IsPhase2 ? 2 : 1;

            for (int i = 0; i < swingCount; i++)
            {
                Vector3 center = _enemy.transform.position;
                Quaternion rotation = _enemy.transform.rotation;

                _skillModule.AreaVisualizer.Show(AttackArea.Cone(AttackAreaKind.EnemyWarning,
                                                    center, rotation, SkillSO.Range, angle, SkillSO.PrepareTime));

                yield return new WaitForSeconds(SkillSO.PrepareTime);

                DamageInCone(center, rotation, SkillSO.Range, angle);

                if (IsPhase2)
                    FireVines();

                if (i < swingCount - 1)
                    yield return new WaitForSeconds(secondSwingDelay);
            }

            yield return new WaitForSeconds(SkillSO.AttackDutation);
        }

        private void FireVines()
        {
            if (vinePrefab == null)
                return;

            Vector3 origin = projectileSpawnPoint != null
                ? projectileSpawnPoint.position
                : _enemy.transform.position + Vector3.up;

            for (int i = 0; i < projectileCount; i++)
            {
                float angle = projectileCount == 1 ? 0f : Mathf.Lerp(-projectileSpread * 0.5f, projectileSpread * 0.5f,
                                                                                                 i / (float)(projectileCount - 1));
                Vector3 direction = _enemy.transform.rotation * Quaternion.Euler(0f, angle, 0f)
                                                * Vector3.forward;

                BossDamageProjectile projectile = Instantiate(vinePrefab, origin, Quaternion.identity);
                projectile.Initialize(direction, projectileSpeed, SkillSO.Damage, _enemy.gameObject);
            }
        }
    }
}