using Members.JJH._02_Scripts.Agents.Effects;
using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.DamageProjectiles;
using Members.JJH._02_Scripts.Systems.ObjectPoolSystem.Runtime;
using RobotWeapons;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies
{
    public class RangeEnemy : AbstractEnemy
    {
        [Header("Normal Attack")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private PoolItemSO projectilePoolItem;

        private PoolInitializer _poolInitializer;

        public override void NormalAttack()
        {
            if (_poolInitializer == null)
                _poolInitializer = FindFirstObjectByType<PoolInitializer>();

            if (_poolInitializer == null) return;

            PoisonProjectile projectile = _poolInitializer.Pop<PoisonProjectile>(projectilePoolItem);

            Vector3 direction = transform.forward;
            Vector3 position = firePoint != null ? firePoint.position : transform.position;

            projectile.Init(position, direction, EnemyData.AttackDamage, gameObject, Sensor.TargetLayer, Sensor.ObstacleLayer);
        }
    }
}
