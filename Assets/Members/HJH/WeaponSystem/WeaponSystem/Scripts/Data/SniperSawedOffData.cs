using UnityEngine;

namespace RobotWeapons
{
    // 저격 수치는 발로란트 마샬 참고: 오퍼레이터보다 연사 빠르고, 줌 배율 낮고, 스코프인 빠름.
    [CreateAssetMenu(menuName = "Weapon/Sniper SawedOff Data", fileName = "New SniperSawedOffData")]
    public class SniperSawedOffData : WeaponData
    {
        [Header("저격")]
        public float sniperDamage = 75f;
        public float sniperFireRate = 1.6f;
        public int sniperMaxAmmo = 5;
        public float sniperReloadDuration = 2.3f;
        public float sniperRange = 100f;
        public float hipFireSpreadAngle = 4f;
        public float zoomedSpreadAngle = 0f;

        [Header("줌")]
        public float defaultFOV = 60f;
        public float zoomFOV = 40f;
        public float zoomTransitionSpeed = 10f;
        public float zoomSwayAmount = 0.4f;
        public float zoomSwaySpeed = 1.2f;
        public float postFireZoomLockDuration = 0.3f;

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
