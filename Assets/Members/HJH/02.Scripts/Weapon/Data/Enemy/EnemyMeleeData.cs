using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH.WeaponSystem.WeaponSystem.Scripts.Data.Enemy
{
    [CreateAssetMenu(menuName = "Weapon/Enemy Melee Data")]
    public class EnemyMeleeData : WeaponData
    {
        public float meleeDamage = 10f;
        public float attackCooldown = 1.5f;
    }
}
