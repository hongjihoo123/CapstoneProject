using RobotWeapons;

namespace Members.KYR._01.Scripts.Combat
{
    public interface IDamageReceiver : IDamageable
    {
        ICombatStats CombatStats { get; }
        DamageResult ReceiveDamage(in DamageInfo info);
    }
}
