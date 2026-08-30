using UnityEngine;

namespace RobotWeapons
{
    public abstract class WeaponData : ScriptableObject
    {
        public string weaponName = "New Weapon";
        public WeaponType type;
        [TextArea] public string description;

        //public float cooldown = 0.5f;
        public float resourceMax = 100f;
        public float reloadDuration = 1.5f;
    }
}
