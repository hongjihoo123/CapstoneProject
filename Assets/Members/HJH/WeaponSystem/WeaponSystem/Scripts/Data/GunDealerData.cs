using UnityEngine;

namespace RobotWeapons
{
    [CreateAssetMenu(menuName = "Weapon/Gun Dealer Data", fileName = "New GunDealerData")]
    public class GunDealerData : WeaponData
    {
        public enum FireMode { Auto, Semi, Burst }

        public float damagePerBullet = 12f;
        public GameObject projectilePrefab;
        public float projectileSpeed = 40f;
        public float aimRange = 100f;

        [Header("발사 방식")]
        public FireMode fireMode = FireMode.Auto;

        [Header("연사/발사 간격")]
        public float fireRate = 6f;
        public float ultimateFireRate = 60;

        [Header("탄 튀김")]
        public float baseSpreadAngle = 1.5f;
        public float maxSpreadAngle = 8f;
        public float spreadGrowthPerShot = 1.2f;
        public float spreadRecoverPerSecond = 6f;

        [Header("조준")]
        public float aimSpreadMultiplier = 0.4f;

        [Header("반동")]
        public float recoilPerShotVertical = 1.2f;
        public float recoilPerShotHorizontalMin = -0.3f;
        public float recoilPerShotHorizontalMax = 0.3f;
        public float dutchKickMin = -40f;
        public float dutchKickMax = 40f;

        [Header("점사 (Fire Mode = Burst 일 때만 사용)")]
        public int burstCount = 3;
        public float burstSafetyDuration = 1f;
    }
}