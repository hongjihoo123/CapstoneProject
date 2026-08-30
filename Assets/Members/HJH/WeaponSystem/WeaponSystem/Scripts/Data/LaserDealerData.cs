using UnityEngine;

namespace RobotWeapons
{
    [CreateAssetMenu(menuName = "Weapon/Laser Dealer Data", fileName = "New LaserDealerData")]
    public class LaserDealerData : WeaponData
    {
        public float damagePerSecond = 20f;
        public float range = 15f;

        [Header("장전/과열 시스템 붙이기 전까지는 미사용")]
        public float resourceDrainPerSecond = 15f;

        [Header("동일 타겟 지속 조준 시 데미지 증폭")]
        public float rampUpRate = 1f;
        public float maxRampMultiplier = 3f;

        [Header("게이지 - 적중 시 충전, 가득 차면 자동으로 에너지볼 발사")]
        public float maxGauge = 100f;
        public float gaugeChargePerSecond = 40f;
        public GameObject energyBallPrefab;
        public float energyBallDamage = 60f;
        public float energyBallSpeed = 20f;
        public float energyBallAimRange = 40f;
        public float energyBallCooldown = 1.5f;
    }
}
