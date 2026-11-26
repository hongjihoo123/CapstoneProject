using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01.Scripts.Currency;
using UnityEngine;

namespace Members.KYR._01.Scripts.SkillUpgrade
{
    public enum SkillUpgradeResult
    {
        Ok,
        NoSkill,
        NotUpgradable,
        MaxLevel,
        NotEnoughGold
    }

    // 런 동안 유지되는 스킬 기본 데미지 강화 진행도. 방(씬)이 바뀌어도 남고, RunReset.Begin에서 지워진다.
    // 강화할 때 표의 값을 누적해 두므로, 피해 계산은 표 없이 여기만 보면 된다.
    public static class SkillUpgradeService
    {
        private struct State
        {
            public int Level;
            public float Flat;
            public float Percent;
        }

        private static readonly Dictionary<string, State> States = new Dictionary<string, State>();

        // (스킬 키, 새 레벨)
        public static event Action<string, int> Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            States.Clear();
            Changed = null;
        }

        public static string KeyOf(SkillData skill) => skill != null ? skill.name : string.Empty;

        public static int GetLevel(SkillData skill) =>
            skill != null && States.TryGetValue(KeyOf(skill), out State state) ? state.Level : 0;

        public static float GetFlatBonus(SkillData skill) =>
            skill != null && States.TryGetValue(KeyOf(skill), out State state) ? state.Flat : 0f;

        public static float GetPercentBonus(SkillData skill) =>
            skill != null && States.TryGetValue(KeyOf(skill), out State state) ? state.Percent : 0f;

        // 스킬 피해 1회의 기본값에 강화를 적용한다 (계수 공식 전에 불린다).
        public static float ModifyDamage(SkillData skill, float amount)
        {
            if (skill == null || amount <= 0f || !States.TryGetValue(KeyOf(skill), out State state))
                return amount;

            return Mathf.Max(0f, (amount + state.Flat) * (1f + state.Percent));
        }

        // 다음 레벨 정보. 없으면 null.
        public static SkillUpgradeTableSO.Level NextLevel(SkillUpgradeTableSO table, SkillData skill)
        {
            SkillUpgradeTableSO.Entry entry = table != null ? table.Find(skill) : null;
            if (entry == null)
                return null;

            int level = GetLevel(skill);
            return level < entry.MaxLevel ? entry.levels[level] : null;
        }

        public static SkillUpgradeResult CanUpgrade(SkillUpgradeTableSO table, SkillData skill)
        {
            if (skill == null)
                return SkillUpgradeResult.NoSkill;

            SkillUpgradeTableSO.Entry entry = table != null ? table.Find(skill) : null;
            if (entry == null || entry.MaxLevel == 0)
                return SkillUpgradeResult.NotUpgradable;

            SkillUpgradeTableSO.Level next = NextLevel(table, skill);
            if (next == null)
                return SkillUpgradeResult.MaxLevel;

            CurrencyService currency = GameBootstrap.Currency;
            if (next.cost > 0 && (currency == null || !currency.CanAfford(CurrencyType.RunGold, next.cost)))
                return SkillUpgradeResult.NotEnoughGold;

            return SkillUpgradeResult.Ok;
        }

        public static bool TryUpgrade(SkillUpgradeTableSO table, SkillData skill)
        {
            if (CanUpgrade(table, skill) != SkillUpgradeResult.Ok)
                return false;

            SkillUpgradeTableSO.Level next = NextLevel(table, skill);
            if (next.cost > 0 && !GameBootstrap.Currency.TrySpend(CurrencyType.RunGold, next.cost))
                return false;

            string key = KeyOf(skill);
            States.TryGetValue(key, out State state);
            state.Level++;
            state.Flat += next.flatDamage;
            state.Percent += next.percentDamage;
            States[key] = state;

            Changed?.Invoke(key, state.Level);
            return true;
        }

        public static void ResetRun()
        {
            if (States.Count == 0)
                return;

            States.Clear();
            Changed?.Invoke(string.Empty, 0);
        }

        // "+12 / +10%" 같은 짧은 문구.
        public static string Describe(float flat, float percent)
        {
            string text = string.Empty;
            if (!Mathf.Approximately(flat, 0f))
                text = (flat > 0f ? "+" : "") + flat.ToString("0.##");
            if (!Mathf.Approximately(percent, 0f))
                text += (text.Length > 0 ? " / " : "") + (percent > 0f ? "+" : "") + (percent * 100f).ToString("0.#") + "%";
            return text.Length > 0 ? text : "+0";
        }
    }
}
