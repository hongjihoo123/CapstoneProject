using UnityEngine;

namespace RobotWeapons
{
    [CreateAssetMenu(menuName = "Weapon/Enemy Gun Data", fileName = "New EnemyGunData")]
    public class EnemyGunData : WeaponData
    {
        public enum FireMode { Auto, Semi, Burst }

        [Header("기본 스펙")]
        public float damagePerBullet = 8f;
        public GameObject projectilePrefab;
        public float projectileSpeed = 40f;
        public float aimRange = 15f;

        [Header("발사 방식")]
        public FireMode fireMode = FireMode.Auto;
        public float fireRate = 3f;

        [Header("탄 퍼짐 (= 명중률)")]
        public float baseSpreadAngle = 4f;
        public float maxSpreadAngle = 10f;
        public float spreadGrowthPerShot = 1f;
        public float spreadRecoverPerSecond = 6f;

        [Header("점사 (Fire Mode = Burst 일 때만 사용)")]
        public int burstCount = 3;
        public float burstSafetyDuration = 1f;
    }
}