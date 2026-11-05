using Members.KYR._01.Scripts.Stats;

namespace Members.KYR._01.Scripts.Combat
{
    public interface ICombatStats
    {
        float Get(PlayerStatId id);
    }

    public sealed class NeutralCombatStats : ICombatStats
    {
        public static readonly NeutralCombatStats Instance = new();

        private NeutralCombatStats() { }

        public float Get(PlayerStatId id)
        {
            switch (id)
            {
                case PlayerStatId.Damage:
                case PlayerStatId.IncomingDamageTaken:
                case PlayerStatId.HealReceived:
                    return 1f;
                default:
                    return 0f;
            }
        }
    }
}
