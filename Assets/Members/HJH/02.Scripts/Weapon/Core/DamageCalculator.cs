using Members.KYR._01_Scripts.Stats;
using UnityEngine;

namespace RobotWeapons
{
    // 스탯만 받아서 최종 피해를 돌려주는 계산기. 누가 때렸는지/맞았는지는 모름 (스탯만 앎)
    public interface IDamageCalculator
    {
        float Calculate(in AttackStats attack, in DefenseStats defense);
    }

    public sealed class StatDamageCalculator : IDamageCalculator
    {
        public float Calculate(in AttackStats attack, in DefenseStats defense)
        {
            float dealt = StatFormulas.Outgoing(
                attack.Amount, attack.AttackCoeff, attack.Attack,
                attack.HasElement ? attack.ElementCoeff : 0f,
                attack.HasElement ? attack.ElementDamage : 0f);

            return StatFormulas.Incoming(
                dealt,
                defense.Defense,
                attack.ArmorPenetration,
                defense.ElementDefense,
                attack.ElementPenetration,
                defense.DamageReduction,
                attack.HasElement);
        }
    }

    // 타격 확인 → 스탯 모으기 → 계산기 → 최종 피해 전달. 피해는 전부 여기를 지난다.
    public static class DamageCalculator
    {
        // 다른 공식을 쓰고 싶으면 이것만 바꾸면 됨
        public static IDamageCalculator Current { get; set; } = new StatDamageCalculator();

        public static float Resolve(in AttackStats attack, IDamageable target)
        {
            DefenseStats defense = target is IDefenseStatProvider provider
                ? provider.GetDefenseStats(attack.Element)
                : DefenseStats.None;
            return Current.Calculate(attack, defense);
        }

        // 계산 후 대상에게 최종 피해만 넘김. 반환값 = 실제로 넘긴 피해
        public static float Apply(in AttackStats attack, IDamageable target)
        {
            if (target == null)
                return 0f;

            float final = Resolve(attack, target);
            if (target is IFinalDamageReceiver receiver)
                receiver.ApplyFinalDamage(final, attack);
            else
                target.TakeDamage(final, attack.Source);   // 적: 기존 TakeDamage 그대로 (최종값만 받음)
            return final;
        }

        // 숫자만 들어온 피해(적 공격 등)를 공격 스탯으로 바꿈. 공격자가 스탯을 주면 그걸 씀
        public static AttackStats FromSource(float amount, GameObject source, Members.JJH._02_Scripts.ElementsSystem.ElementType? element)
        {
            IAttackStatProvider attacker = source != null ? source.GetComponentInParent<IAttackStatProvider>() : null;
            if (attacker == null)
                return AttackStats.Raw(amount, source, element);

            // 이미 계산된 숫자라 공격력은 다시 곱하지 않고 관통 스탯만 가져옴
            AttackStats stats = attacker.BuildAttack(amount, 1f, element, 0f);
            stats.AttackCoeff = 1f;
            stats.Attack = 1f;
            stats.ElementCoeff = 0f;
            return stats;
        }
    }
}
