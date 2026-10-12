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
        public float aimRange = 15f; // AI의 공격 사거리인데 너가 정해둔 사거리랑 똑같아야함

        [Header("발사 방식")]
        public FireMode fireMode = FireMode.Auto;
        public float fireRate = 3f; // 발사속도임. 초당 nf 발 만큼 나간다는 뜻이에야

        [Header("탄 퍼짐 (= 명중률)")]
        public float baseSpreadAngle = 4f;
        public float maxSpreadAngle = 10f;
        public float spreadGrowthPerShot = 1f;
        public float spreadRecoverPerSecond = 6f;

        [Header("점사 (Fire Mode = Burst 일 때만 사용)")]
        public int burstCount = 3; // 한번에 몇발 쏠 지
        public float burstSafetyDuration = 1f; // 그냥 이건 한번에 여러발 쏠 수 있는 걸 방지차원에 써둔거여
    }
}