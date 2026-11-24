using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element.Upgrade
{
    // 아웃게임 스킬 트리가 올려주는 "스킬 강화" 종류. 값은 전부 합산 % (0.1 = +10%, Cooldown 은 쿨 -10%).
    // 새 종류 = 여기 한 줄 + 그 값을 읽는 곳 한 군데 (SkillUpgrades.Bonus 로 읽음).
    public enum SkillUpgradeKind
    {
        Damage,         // 스킬 피해량 (직격 + 화상 틱)                       -> PlayerSkillContext.DealDamage / SkillEffects 화상
        Cooldown,       // 스킬 재사용 대기시간 감소 (기존 쿨감 스탯에 더해짐)   -> GenericSkillState
        StatusPower,    // 둔화·속박·기절·화상 시간, 밀치기 거리                -> SkillEffects.Apply
        Support,        // 회복량, 쉴드량, 받는 피해 감소량                    -> SkillEffects.ApplySelf
        Range,          // 폭발·원형 범위 반지름                              -> ElementSkillCast.Impact
        ComboPower,     // 콤보 버프 효과 크기 (범위 SkillUpgradeScope.All 만)  -> ComboEffect
        ComboDuration,  // 콤보 버프 지속 시간                                -> ComboElementTable.Duration
        FinisherPower,  // 피니셔(C) 피해 배율                                -> ElementComboChain.ScaleFor

        // ---- 건슬링어 스킬 트리 (슬롯 범위로 읽음: Attack/Charged/Dash/Shock/Ultimate)
        CritChance,        // 치명 확률 (0.05 = +5%p)
        CritDamage,        // 치명 피해 (0.1 = 치명 배율 +0.1)
        AttackSpeed,       // 공격 속도
        ChargeNeed,        // 충전탄 게이지가 차는 데 필요한 발수 감소 (값 = 발수)
        CritRefundDash,    // 이 슬롯이 치명을 낼 때 대쉬 쿨 감소 (값 = 초)
        CritRefundSkill,   // ... 기본 스킬(충격파) 쿨 감소
        CritRefundUlt,     // ... 궁극기 쿨 감소
        CritRefundSelf,    // ... 자기 자신 쿨 감소
        PierceDamage,      // 충전탄: 앞서 관통한 적 1명당 피해 증가
        CritGauge,         // 이 슬롯 치명 시 충전탄 게이지 +발수
        KillGauge,         // 처치 시 충전탄 게이지 +발수
        DoubleShot,        // 평타를 한 번 더 쏠 확률
        ShrapnelCount,     // 궁극기 파편 수 (값 = 개수)
        UltCritNeed,       // 궁극기 초기화에 필요한 파편 치명 횟수 감소 (값 = 횟수)
        UltWindow,         // 파편 치명 집계 유예 시간 (값 = 초)
        ChainStageDamage,  // 궁극기 연쇄 단계당 피해
        ChainStageCrit,    // 궁극기 연쇄 단계당 치명 확률
        DashCritWindow,    // 대쉬 직후 치명 확률 (값 = 확률)
        DashCritWindowTime,// 대쉬 직후 창 지속 (값 = 초)
        DashCharge,        // 대쉬 충전 수 (값 = 개수)
        DashRefundUlt,     // 대쉬할 때마다 궁극기 쿨 감소 (값 = 초)
        CritStackChance,   // 치명 시 3초간 치명 확률 중첩 (값 = 1중첩당 확률, 최대 5중첩)
        CritStreakHaste,   // 평타 치명 5연속 시 5초간 공속 (값 = 공속 증가)
        CritUltProc,       // 치명 시 이 확률로 궁극기 쿨 -3초 (값 = 확률)
        ChainCap,          // 궁극기 연쇄 상한 증가 (값 = 횟수). 회차마다 필요 치명 +2, 피해 -15%
        CritStill,         // 정조준: 안 움직일수록 평타 치명 확률 증가 (값 = 최대치, 2초 가만히 있으면 가득)
        CritFar,           // 급소 사격: 먼 적일수록 평타 치명 피해 증가 (값 = 최대치, 14m에서 가득)
        DashCritDamage,    // 대쉬 직후 창 동안 치명 피해
        GuaranteedCrits,   // 백스텝 장전: 대쉬 직후 평타 N발 치명 확정
        DashGauge,         // 대쉬 시 충전탄 게이지 +발수
        ChargeExplosion,   // 폭발탄: 충전탄이 적중한 자리에서 폭발 (값 = 충전탄 피해 대비 비율)
        ChargeDouble,      // 과충전: 충전탄 2연발
        DoubleBlast,       // 이중 충격파: 반대 방향으로 한 번 더
        HitRefundSelf,     // 맞은 적 1명당 자기 쿨 감소 (값 = 초)
        ChainReset,        // 연쇄 충격: 한 번에 3명 이상 치명이면 쿨 초기화 (내부 쿨 2초)
        MarkDuration,      // 표식 지속시간
        MarkTaken,         // 표식 적이 받는 피해
        MarkCritChance,    // 표식 적에게 평타 치명 확률
        MarkCritDamage,    // 표식 적에게 치명 피해
        ShrapnelDense,     // 집속탄: 파편 수 2배, 파편 한 발 피해 -35%
        UltCountAny,       // 정밀 집계: 궁극기 후 4초간 평타·충격파 치명도 필요 횟수에 포함
        ChainHaste,        // 연쇄 중 모든 쿨타임이 빠르게 (값 = 쿨감)
        ChainTime,         // 연쇄 유지 시간
        ChainStageCritDamage, // 궁극기 연쇄 단계당 치명 피해
        ChainShock,        // 충격파 사슬: 충격파 치명이 주변 적에게 전이 (값 = 피해 비율)
        MoveSpeed,         // 이동속도
    }

    // 어느 원소 스킬에 적용되나. All = 전부 (원소 없는 스킬 포함)
    public enum SkillUpgradeScope
    {
        All = 0,
        Fire = 1,
        Water = 2,
        Wind = 3,
        Electric = 4,
        Earth = 5,
        // 슬롯 범위 (건슬링어 스킬 트리). 원소와 별개로 "어느 칸의 스킬인가"
        Attack = 10,     // 평타
        Charged = 11,    // 패시브 충전탄
        Dash = 12,       // 대쉬
        Shock = 13,      // 기본 스킬 (충격파)
        Ultimate = 14,   // 궁극기
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

        // 값 표시: 퍼센트 / 초 / 발·개·회. 줄어드는 종류는 부호를 뒤집어 "-"로 읽히게
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
