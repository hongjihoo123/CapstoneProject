using Members.KYR._01.Scripts.Stats;
using UnityEngine;

namespace Members.KYR._01_Scripts.Stats
{
    public static class StatFormulas
    {
        private const float DefaultK = 100f;
        private static CombatFormulaManagerSO _settings;

        public static CombatFormulaManagerSO Settings
        {
            get
            {
                if (_settings == null)
                    _settings = Resources.Load<CombatFormulaManagerSO>("CombatFormula");
                return _settings;
            }
            set => _settings = value;
        }

        public static float DefenseK => Settings != null ? Settings.DefenseK : DefaultK;
        public static float ElementK => Settings != null ? Settings.ElementK : DefaultK;

        public static float Mitigation(float rating, float k)
        {
            if (rating <= 0f || k <= 0f)
                return 0f;
            return rating / (rating + k);
        }

        public static float Outgoing(
            float amount,
            float attackCoefficient,
            float damage,
            float elementCoefficient,
            float elementDamage)
        {
            float scale = attackCoefficient * damage + elementCoefficient * elementDamage;
            return amount * Mathf.Max(0f, scale);
        }

        public static float Incoming(
            float dealt,
            float defense,
            float armorPenetration,
            float elementDefense,
            float elementPenetration,
            float damageReduction,
            float damageTakenMultiplier,
            bool hasElement)
        {
            float taken = dealt;
            if (hasElement)
            {
                float ele = Mathf.Max(0f, elementDefense - elementPenetration);
                taken *= 1f - Mitigation(ele, ElementK);
            }

            float def = Mathf.Max(0f, defense - armorPenetration);
            taken *= 1f - Mitigation(def, DefenseK);
            taken *= 1f - Mathf.Clamp01(damageReduction);
            taken *= Mathf.Max(0f, damageTakenMultiplier);
            return Mathf.Max(0f, taken);
        }
    }
}
