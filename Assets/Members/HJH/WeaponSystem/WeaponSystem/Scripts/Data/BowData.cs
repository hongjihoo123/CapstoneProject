using UnityEngine;

namespace RobotWeapons
{
    // 기계식 활. 한조 기본 활처럼 누르고 있으면 당겨지고(충전), 떼면 발사.
    // 연사는 한조 E(스톰애로우)의 1/5 속도 = 최소 발사 간격 1초.
    [CreateAssetMenu(menuName = "Weapon/Bow Data", fileName = "New BowData")]
    public class BowData : WeaponData
    {
        [Header("발사 간격")]
        public float minFireInterval = 1f;

        [Header("당기는 정도에 비례 (데미지/사거리)")]
        public float maxChargeTime = 1.2f;
        public float minDamage = 15f;
        public float maxDamage = 55f;
        public float minLaunchSpeed = 20f;
        public float maxLaunchSpeed = 45f;

        [Header("화살")]
        public GameObject arrowPrefab;
        public GameObject defaultHitEffectPrefab;

        [Header("풀드로우 시 무기 흔들림")]
        public float fullDrawShakeAmount = 0.02f;
        public float fullDrawShakeSpeed = 25f;
    }
}
