using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace RobotWeapons
{
    // 때리는 쪽 스탯 묶음. 타격이 확인되면 이걸 계산기로 넘긴다.
    public struct AttackStats
    {
        public float Amount;            // 스킬/무기 기본 피해
        public float AttackCoeff;       // 공격력 계수
        public float Attack;            // 공격력 (배율, 1 = 그대로)
        public ElementType? Element;
        public float ElementCoeff;
        public float ElementDamage;
        public float ArmorPenetration;
        public float ElementPenetration;
        public GameObject Source;

        public bool HasElement => Element.HasValue;

        // 스탯이 없는 공격자(적 등): 받은 숫자를 그대로 피해로 쓴다
        public static AttackStats Raw(float amount, GameObject source, ElementType? element = null) => new()
        {
            Amount = amount,
            AttackCoeff = 1f,
            Attack = 1f,
            Element = element,
            Source = source,
        };
    }

    // 맞는 쪽 스탯 묶음
    public struct DefenseStats
    {
        public float Defense;
        public float ElementDefense;
        public float DamageReduction;

        public static DefenseStats None => default;
    }

    // 공격 스탯을 줄 수 있는 쪽 (플레이어). 없으면 AttackStats.Raw 로 처리됨
    public interface IAttackStatProvider
    {
        AttackStats BuildAttack(float amount, float attackCoeff, ElementType? element, float elementCoeff);
    }

    // 방어 스탯을 줄 수 있는 쪽. 없으면 방어 0 으로 계산됨 (지금 적은 이거 없음)
    public interface IDefenseStatProvider
    {
        DefenseStats GetDefenseStats(ElementType? element);
    }

    // 계산이 끝난 최종 피해를 받는 쪽. 없으면 IDamageable.TakeDamage(최종값) 으로 넘어간다
    public interface IFinalDamageReceiver
    {
        void ApplyFinalDamage(float finalDamage, in AttackStats attack);
    }
}
