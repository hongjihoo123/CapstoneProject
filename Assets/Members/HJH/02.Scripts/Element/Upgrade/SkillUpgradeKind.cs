using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element.Upgrade
{
    public enum SkillUpgradeKind
    {
        Damage,
        Cooldown,
        StatusPower,
        Support,
        Range,
        ComboPower,
        ComboDuration,
        FinisherPower,

        CritChance,
        CritDamage,
        AttackSpeed,
        ChargeNeed,
        CritRefundDash,
        CritRefundSkill,
        CritRefundUlt,
        CritRefundSelf,
        PierceDamage,
        CritGauge,
        KillGauge,
        DoubleShot,
        ShrapnelCount,
        UltCritNeed,
        UltWindow,
        ChainStageDamage,
        ChainStageCrit,
        DashCritWindow,
        DashCritWindowTime,
        DashCharge,
        DashRefundUlt,
        CritStackChance,
        CritStreakHaste,
        CritUltProc,
        ChainCap,
        CritStill,
        CritFar,
        DashCritDamage,
        GuaranteedCrits,
        DashGauge,
        ChargeExplosion,
        ChargeDouble,
        DoubleBlast,
        HitRefundSelf,
        ChainReset,
        MarkDuration,
        MarkTaken,
        MarkCritChance,
        MarkCritDamage,
        ShrapnelDense,
        UltCountAny,
        ChainHaste,
        ChainTime,
        ChainStageCritDamage,
        ChainShock,
        MoveSpeed,
    }

    public enum SkillUpgradeScope
    {
        All = 0,
        Fire = 1,
        Water = 2,
        Wind = 3,
        Electric = 4,
        Earth = 5,
        Attack = 10,
        Charged = 11,
        Dash = 12,
        Shock = 13,
        Ultimate = 14,
    }

    public static class SkillUpgradeScopes
    {
        public static SkillUpgradeScope Of(ElementType element) => (SkillUpgradeScope)((int)element + 1);

        public static string Label(SkillUpgradeScope scope) => scope switch
        {
            SkillUpgradeScope.Fire => "불",
            SkillUpgradeScope.Water => "물",
            SkillUpgradeScope.Wind => "바람",
            SkillUpgradeScope.Electric => "번개",
            SkillUpgradeScope.Earth => "땅",
            SkillUpgradeScope.Attack => "평타",
            SkillUpgradeScope.Charged => "충전탄",
            SkillUpgradeScope.Dash => "대쉬",
            SkillUpgradeScope.Shock => "충격파",
            SkillUpgradeScope.Ultimate => "궁극기",
            _ => "모든 원소",
        };

        public static string Format(SkillUpgradeKind kind, float value)
        {
            string n = Mathf.Abs(value).ToString("0.##");
            string plus = value >= 0f ? "+" : "-";
            string minus = value >= 0f ? "-" : "+";
            switch (kind)
            {
                case SkillUpgradeKind.Cooldown:
                    return minus + (Mathf.Abs(value) * 100f).ToString("0.#") + "%";
                case SkillUpgradeKind.HitRefundSelf:
                case SkillUpgradeKind.CritRefundDash:
                case SkillUpgradeKind.CritRefundSkill:
                case SkillUpgradeKind.CritRefundUlt:
                case SkillUpgradeKind.CritRefundSelf:
                case SkillUpgradeKind.DashRefundUlt:
                    return n + "초";
                case SkillUpgradeKind.ChainTime:
                case SkillUpgradeKind.UltWindow:
                case SkillUpgradeKind.DashCritWindowTime:
                    return plus + n + "초";
                case SkillUpgradeKind.ChargeNeed:
                    return minus + n + "발";
                case SkillUpgradeKind.DashGauge:
                case SkillUpgradeKind.CritGauge:
                case SkillUpgradeKind.KillGauge:
                    return plus + n + "발";
                case SkillUpgradeKind.ShrapnelCount:
                    return plus + n + "개";
                case SkillUpgradeKind.UltCritNeed:
                    return minus + n + "회";
                case SkillUpgradeKind.GuaranteedCrits:
                case SkillUpgradeKind.ChargeDouble:
                case SkillUpgradeKind.DoubleBlast:
                case SkillUpgradeKind.ChainReset:
                case SkillUpgradeKind.ShrapnelDense:
                case SkillUpgradeKind.UltCountAny:
                case SkillUpgradeKind.DashCharge:
                case SkillUpgradeKind.ChainCap:
                    return plus + n + "회";
                default:
                    return plus + (Mathf.Abs(value) * 100f).ToString("0.#") + "%";
            }
        }

        public static string Label(SkillUpgradeKind kind) => kind switch
        {
            SkillUpgradeKind.Damage => "스킬 피해",
            SkillUpgradeKind.Cooldown => "재사용 대기시간",
            SkillUpgradeKind.StatusPower => "상태이상 강도",
            SkillUpgradeKind.Support => "회복·쉴드·방어량",
            SkillUpgradeKind.Range => "폭발 범위",
            SkillUpgradeKind.ComboPower => "콤보 효과",
            SkillUpgradeKind.ComboDuration => "콤보 지속시간",
            SkillUpgradeKind.FinisherPower => "피니셔 피해",
            SkillUpgradeKind.CritChance => "치명 확률",
            SkillUpgradeKind.CritDamage => "치명 피해",
            SkillUpgradeKind.AttackSpeed => "공격 속도",
            SkillUpgradeKind.ChargeNeed => "충전 필요 발수",
            SkillUpgradeKind.CritRefundDash => "치명 시 대쉬 쿨 감소",
            SkillUpgradeKind.CritRefundSkill => "치명 시 기본 스킬 쿨 감소",
            SkillUpgradeKind.CritRefundUlt => "치명 시 궁극기 쿨 감소",
            SkillUpgradeKind.CritRefundSelf => "치명 시 쿨 감소",
            SkillUpgradeKind.PierceDamage => "관통할수록 피해",
            SkillUpgradeKind.CritGauge => "치명 시 게이지",
            SkillUpgradeKind.KillGauge => "처치 시 게이지",
            SkillUpgradeKind.DoubleShot => "추가 발사 확률",
            SkillUpgradeKind.ShrapnelCount => "파편 수",
            SkillUpgradeKind.UltCritNeed => "초기화 필요 치명 횟수",
            SkillUpgradeKind.UltWindow => "치명 집계 유예 시간",
            SkillUpgradeKind.ChainStageDamage => "연쇄 단계당 피해",
            SkillUpgradeKind.ChainStageCrit => "연쇄 단계당 치명 확률",
            SkillUpgradeKind.DashCritWindow => "직후 치명 확률",
            SkillUpgradeKind.DashCritWindowTime => "치명 창 지속",
            SkillUpgradeKind.DashCharge => "충전 횟수",
            SkillUpgradeKind.DashRefundUlt => "시전 시 궁극기 쿨 감소",
            SkillUpgradeKind.CritStackChance => "치명 중첩 확률",
            SkillUpgradeKind.CritStreakHaste => "치명 연속 공속",
            SkillUpgradeKind.CritUltProc => "치명 시 궁극기 쿨 -3초 확률",
            SkillUpgradeKind.ChainCap => "연쇄 상한",
            SkillUpgradeKind.CritStill => "가만히 쏠 때 치명 확률(최대)",
            SkillUpgradeKind.CritFar => "먼 적 치명 피해(최대)",
            SkillUpgradeKind.DashCritDamage => "직후 치명 피해",
            SkillUpgradeKind.GuaranteedCrits => "직후 치명 확정 발수",
            SkillUpgradeKind.DashGauge => "대쉬 시 충전탄 게이지",
            SkillUpgradeKind.ChargeExplosion => "적중 폭발 피해 비율",
            SkillUpgradeKind.ChargeDouble => "2연발",
            SkillUpgradeKind.DoubleBlast => "반대 방향 추가 충격파",
            SkillUpgradeKind.HitRefundSelf => "맞은 적당 쿨 감소",
            SkillUpgradeKind.ChainReset => "3명 이상 치명 시 쿨 초기화",
            SkillUpgradeKind.MarkDuration => "표식 지속",
            SkillUpgradeKind.MarkTaken => "표식 적이 받는 피해",
            SkillUpgradeKind.MarkCritChance => "표식 적 평타 치명 확률",
            SkillUpgradeKind.MarkCritDamage => "표식 적 치명 피해",
            SkillUpgradeKind.ShrapnelDense => "파편 수 2배, 한 발 피해 -35%",
            SkillUpgradeKind.UltCountAny => "평타·충격파 치명도 집계",
            SkillUpgradeKind.ChainHaste => "연쇄 중 쿨타임 감소",
            SkillUpgradeKind.ChainTime => "연쇄 유지 시간",
            SkillUpgradeKind.ChainStageCritDamage => "연쇄 단계당 치명 피해",
            SkillUpgradeKind.ChainShock => "치명 전이 피해 비율",
            SkillUpgradeKind.MoveSpeed => "이동속도",
            _ => kind.ToString(),
        };
    }
}
