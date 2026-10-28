using Members.KYR._01_Scripts.Stats;
using Members.KYR._01.Scripts.Stats;
using UnityEngine;

namespace Members.KYR._01.Scripts.Combat
{
    public static class DamageCalculator
    {
        public static float Compute(in DamageInfo info, ICombatStats defender)
        {
            return Incoming(Outgoing(info), info, defender);
        }

        public static float Outgoing(in DamageInfo info)
        {
            ICombatStats attacker = info.Attacker ?? NeutralCombatStats.Instance;
            float elementDamage = info.Element.HasValue
                ? attacker.Get(PlayerElementStats.DamageStat(info.Element.Value))
                : 0f;

            return StatFormulas.Outgoing(
                info.Amount,
                info.AttackCoefficient,
                attacker.Get(PlayerStatId.Damage),
                info.ElementCoefficient,
                elementDamage);
        }

        public static float Incoming(float dealt, in DamageInfo info, ICombatStats defender)
        {
            if (dealt <= 0f)
                return 0f;
            if (defender == null)
                return dealt;

            ICombatStats attacker = info.Attacker ?? NeutralCombatStats.Instance;
            bool hasElement = info.Element.HasValue;
            float eleDef = 0f;
            float elePen = 0f;
            if (hasElement)
            {
                eleDef = defender.Get(PlayerElementStats.DefenseStat(info.Element.Value));
                elePen = attacker.Get(PlayerElementStats.PenetrationStat(info.Element.Value));
            }

            return StatFormulas.Incoming(
                dealt,
                defender.Get(PlayerStatId.Defense),
                attacker.Get(PlayerStatId.ArmorPenetration),
                eleDef,
                elePen,
                defender.Get(PlayerStatId.DamageReduction),
                defender.Get(PlayerStatId.IncomingDamageTaken),
                hasElement);
        }

        public static ICombatStats StatsOf(GameObject source)
        {
            if (source == null)
                return null;
            IDamageReceiver receiver = source.GetComponentInParent<IDamageReceiver>();
            return receiver?.CombatStats;
        }
    }
}
