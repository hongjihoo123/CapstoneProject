using UnityEngine;

namespace RobotWeapons
{
    [CreateAssetMenu(menuName = "Weapon/Healer Data", fileName = "New HealerData")]
    public class HealerData : WeaponData
    {
        [Header("힐샷 (Primary)")]
        public GameObject healShotPrefab;
        public float healShotAmount = 15f;
        public float healShotSpeed = 25f;
        public float healShotAimRange = 30f;

        [Header("힐/딜 겸용 수류탄 (Secondary)")]
        public GameObject grenadePrefab;
        public float throwForce = 14f;
        public float throwUpwardAngle = 35f;
        public float aoeRadius = 3f;
        public float grenadeDamage = 15f;
        public float grenadeHeal = 20f;
        public float grenadeFuseTime = 1.5f;
    }
}
