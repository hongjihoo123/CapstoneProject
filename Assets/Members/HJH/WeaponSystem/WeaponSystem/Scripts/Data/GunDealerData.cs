using UnityEngine;

namespace RobotWeapons
{
    [CreateAssetMenu(menuName = "Weapon/Gun Dealer Data", fileName = "New GunDealerData")]
    public class GunDealerData : WeaponData
    {
        public float damagePerBullet = 12f;
        public GameObject projectilePrefab;
        public float projectileSpeed = 40f;
        public float aimRange = 100f;

        [Header("연사")]
        public float fireRate = 6f;

        [Header("탄 튀김 (스프레드)")]
        public float baseSpreadAngle = 1.5f;
        public float maxSpreadAngle = 8f;
        public float spreadGrowthPerShot = 1.2f;
        public float spreadRecoverPerSecond = 6f;

        [Header("조준 (Secondary)")]
        public float aimSpreadMultiplier = 0.4f;

        [Header("반동 (에임 자체가 밀림)")]
        public float recoilPerShotVertical = 1.2f;
        public float recoilPerShotHorizontalMin = -0.3f;
        public float recoilPerShotHorizontalMax = 0.3f;
        public float dutchKickMin = -40f;
        public float dutchKickMax = 40f;
    }
}
