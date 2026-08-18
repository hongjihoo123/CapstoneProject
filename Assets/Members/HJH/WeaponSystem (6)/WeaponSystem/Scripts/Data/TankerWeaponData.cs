using UnityEngine;

namespace RobotWeapons
{
    [CreateAssetMenu(menuName = "Weapon/Tanker Data", fileName = "New TankerData")]
    public class TankerWeaponData : WeaponData
    {
        public enum Mode { MeleeOnly, RangedOnly, Both }

        [Header("모드 (인스펙터에서 껐다 켰다)")]
        public Mode mode = Mode.Both;

        [Header("근접")]
        public float meleeDamage = 10f;
        public string meleeSwingAnimId = "Tanker_MeleeSwing";

        [Header("원거리")]
        public float rangedDamagePerShot = 8f;
        public GameObject projectilePrefab;
        public float projectileSpeed = 25f;
    }
}
