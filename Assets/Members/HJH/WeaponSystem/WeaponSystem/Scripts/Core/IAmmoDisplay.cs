using UnityEngine;

namespace RobotWeapons
{
    public interface IAmmoDisplay
    {
        bool TryGetAmmoText(out string text);
    }

    public static class AmmoText
    {
        private const string Reloading = "재장전 중...";

        public static string Format(int current, int max, bool isReloading) =>
            isReloading ? Reloading : $"{current} / {max}";

        public static string ForResource(IWeapon weapon) =>
            Format(Mathf.CeilToInt(weapon.CurrentResource), Mathf.CeilToInt(weapon.MaxResource), weapon.IsReloading);
    }
}
