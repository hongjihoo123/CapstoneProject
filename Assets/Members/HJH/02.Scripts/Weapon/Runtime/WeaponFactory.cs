using Assets.Members.HJH.WeaponSystem.WeaponSystem.Scripts.Data.Enemy;
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
                ChainWeaponData d => new ChainWeapon(d),
                GunDealerData d => new GunDealerWeapon(d),
                HitscanGunData d => new HitscanGunWeapon(d),
                EnemyGunData d => new EnemyGunWeapon(d),
                EnemyMeleeData d => new EnemyMeleeWeapon(d),
                _ => null
            };

            if (weapon != null && savedUpgrades != null)
                weapon.RestoreUpgrades(savedUpgrades);

            return weapon;
        }
    }
}
