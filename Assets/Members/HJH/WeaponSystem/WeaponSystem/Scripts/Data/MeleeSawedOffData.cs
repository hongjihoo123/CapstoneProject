using UnityEngine;

namespace RobotWeapons
{
    [CreateAssetMenu(menuName = "Weapon/Melee SawedOff Data", fileName = "New MeleeSawedOffData")]
    public class MeleeSawedOffData : WeaponData
    {
        [Header("근접 (칼)")]
        public float meleeDamage = 8f;
        public string[] comboAnimIds = { "SubDealer_Attack1", "SubDealer_Attack2", "SubDealer_Attack3" };

        [Header("소드오프 샷건 (발로란트 쇼티 참고)")]
        public int pelletCount = 20;
        public float spreadAngle = 12f;
        public float damagePerPellet = 6f;
        public float shotgunRange = 8f;
        public float damageFalloffAtMaxRange = 0.7f;
        public int shotgunMaxAmmo = 2;
        public float shotgunReloadDuration = 1.8f;
        public GameObject defaultHitEffectPrefab;
        public GameObject bulletPrefab;
        public float bulletSpeed = 60f;
    }
}
