using System;
using Members.JJH._02_Scripts.ElementsSystem;

namespace Members.KYR._01_Scripts.Stats
{
    public static class PlayerElementStats
    {
        public static PlayerStatId DamageStat(ElementType element) => element switch
        {
            ElementType.Fire => PlayerStatId.FireDamage,
            ElementType.Water => PlayerStatId.WaterDamage,
            ElementType.Wind => PlayerStatId.WindDamage,
            ElementType.Electric => PlayerStatId.ElectricDamage,
            ElementType.Earth => PlayerStatId.EarthDamage,
            _ => throw new ArgumentOutOfRangeException(nameof(element), element, null)
        };

        public static PlayerStatId DefenseStat(ElementType element) => element switch
        {
            ElementType.Fire => PlayerStatId.FireDefense,
            ElementType.Water => PlayerStatId.WaterDefense,
            ElementType.Wind => PlayerStatId.WindDefense,
            ElementType.Electric => PlayerStatId.ElectricDefense,
            ElementType.Earth => PlayerStatId.EarthDefense,
            _ => throw new ArgumentOutOfRangeException(nameof(element), element, null)
        };
    }
}
