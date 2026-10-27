using RobotWeapons;

namespace Members.KYR._01_Scripts.Combat
{
    // 스탯 계산을 거쳐 피해를 받는 대상. 스탯 없는 대상은 기존 IDamageable.TakeDamage 로 받음
    public interface IDamageReceiver : IDamageable
    {
        ICombatStats CombatStats { get; }
        DamageResult ReceiveDamage(in DamageInfo info);
    }
}
