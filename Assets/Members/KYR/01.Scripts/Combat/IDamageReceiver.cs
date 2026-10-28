using RobotWeapons;

namespace Members.KYR._01_Scripts.Combat
{
    public interface IDamageReceiver : IDamageable
    {
        ICombatStats CombatStats { get; }
        DamageResult ReceiveDamage(in DamageInfo info);
    }
}
