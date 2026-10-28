using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Members.KYR._01_Scripts.Combat
{
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly float AttackCoefficient;
        public readonly ElementType? Element;
        public readonly float ElementCoefficient;
        public readonly GameObject Source;
        public readonly ICombatStats Attacker;

        public DamageInfo(
            float amount,
            GameObject source,
            ICombatStats attacker,
            float attackCoefficient = 1f,
            ElementType? element = null,
            float elementCoefficient = 0f)
        {
            Amount = amount;
            Source = source;
            Attacker = attacker;
            AttackCoefficient = attackCoefficient;
            Element = element;
            ElementCoefficient = element.HasValue ? elementCoefficient : 0f;
        }
    }

    public readonly struct DamageResult
    {
        public readonly float Damage;
        public readonly float ShieldAbsorbed;
        public readonly float HealthLost;
        public readonly bool Killed;

        public DamageResult(float damage, float shieldAbsorbed, float healthLost, bool killed)
        {
            Damage = damage;
            ShieldAbsorbed = shieldAbsorbed;
            HealthLost = healthLost;
            Killed = killed;
        }
    }
}
