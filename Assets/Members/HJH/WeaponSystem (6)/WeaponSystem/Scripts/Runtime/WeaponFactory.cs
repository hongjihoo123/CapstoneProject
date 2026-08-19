using System.Collections.Generic;
using UnityEngine;

namespace RobotWeapons
{
    public static class WeaponFactory
    {
        public static IWeapon Create(WeaponData data, IEnumerable<UpgradeData> savedUpgrades = null)
        {
            if (data == null)
            {
                Debug.LogError("WeaponFactory.Create: WeaponData가 null입니다.");
                return null;
            }

            WeaponBase weapon = data switch
            {
                TankerWeaponData d => new TankerWeapon(d),
                LaserDealerData d => new LaserDealerWeapon(d),
                GunDealerData d => new GunDealerWeapon(d),
                MeleeSubDealerData d => new MeleeSubDealerWeapon(d),
                HealerData d => new HealerWeapon(d),
                _ => null
            };

            if (weapon != null && savedUpgrades != null)
                weapon.RestoreUpgrades(savedUpgrades);

            return weapon;
        }
    }
}
