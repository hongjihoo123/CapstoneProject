using UnityEngine;

namespace RobotWeapons
{
    [CreateAssetMenu(menuName = "Weapon/Laser Dealer Data", fileName = "New LaserDealerData")]
    public class LaserDealerData : WeaponData
    {
        public float damagePerSecond = 20f;
        public float range = 15f;
        public float resourceDrainPerSecond = 15f;

        [Header("동일 타겟 지속 조준 시 데미지 증폭")]
        public float rampUpRate = 1f;
        public float maxRampMultiplier = 3f;
    }
}
