using Members.JJH._02_Scripts.ElementsSystem;
using System;

namespace Members.KYR._01.Scripts.Stats
{
    public static class PlayerElementStats
    {
        public static PlayerStatId PenetrationStat(ElementType element) => element switch
        {
            ElementType.Fire => PlayerStatId.FirePenetration,
            ElementType.Water => PlayerStatId.WaterPenetration,
            ElementType.Wind => PlayerStatId.WindPenetration,
            ElementType.Electric => PlayerStatId.ElectricPenetration,
            ElementType.Earth => PlayerStatId.EarthPenetration,
            _ => throw new ArgumentOutOfRangeException(nameof(element), element, null)
        };
    }
}