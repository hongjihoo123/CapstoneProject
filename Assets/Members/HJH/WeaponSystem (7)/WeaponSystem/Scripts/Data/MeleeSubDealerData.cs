using UnityEngine;

namespace RobotWeapons
{
    [CreateAssetMenu(menuName = "Weapon/Melee SubDealer Data", fileName = "New MeleeSubDealerData")]
    public class MeleeSubDealerData : WeaponData
    {
        public float damage = 8f;
        public string[] comboAnimIds = { "SubDealer_Attack1", "SubDealer_Attack2", "SubDealer_Attack3" };
    }
}
