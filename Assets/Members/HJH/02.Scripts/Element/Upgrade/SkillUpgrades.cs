using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element.Upgrade
{
    // 스킬이 "지금 걸려 있는 강화"를 읽는 좁은 창구 (스킬 쪽은 이것만 앎. 트리/저장 방식은 모름)
    public interface ISkillUpgradeSource
    {
        // 합산 보너스 (0.1 = +10%). scope 가 정확히 일치하는 것 + All
        float Bonus(SkillUpgradeKind kind, SkillUpgradeScope scope);
    }

    // 강화 묶음 하나(출처 = 인챈트 트리 등)를 올려두는 판. 출처별로 넣고 빼면 합계가 다시 계산됨
    public sealed class SkillUpgradeBoard : ISkillUpgradeSource
    {
        public readonly struct Entry
        {
            public readonly SkillUpgradeKind Kind;
            public readonly SkillUpgradeScope Scope;
            public readonly float Value;

            public Entry(SkillUpgradeKind kind, SkillUpgradeScope scope, float value)
            {
                Kind = kind;
                Scope = scope;
                Value = value;
            }
        }

        private readonly Dictionary<object, List<Entry>> _layers = new();
        private readonly Dictionary<(SkillUpgradeKind, SkillUpgradeScope), float> _totals = new();

        public event System.Action Changed;

        public void Set(object source, IEnumerable<Entry> entries)
        {
            if (source == null)
                return;

            var list = new List<Entry>(entries ?? System.Array.Empty<Entry>());
            if (list.Count == 0)
                _layers.Remove(source);
            else
                _layers[source] = list;
            Rebuild();
        }

        public void Clear(object source)
        {
            if (source != null && _layers.Remove(source))
                Rebuild();
        }

        public float Bonus(SkillUpgradeKind kind, SkillUpgradeScope scope)
        {
            float sum = _totals.TryGetValue((kind, SkillUpgradeScope.All), out float all) ? all : 0f;
            if (scope != SkillUpgradeScope.All && _totals.TryGetValue((kind, scope), out float own))
                sum += own;
            return sum;
        }

        public float Exact(SkillUpgradeKind kind, SkillUpgradeScope scope) =>
            _totals.TryGetValue((kind, scope), out float value) ? value : 0f;

        private void Rebuild()
        {
            _totals.Clear();
            foreach (List<Entry> layer in _layers.Values)
            {
                foreach (Entry entry in layer)
                {
                    var key = (entry.Kind, entry.Scope);
                    _totals[key] = (_totals.TryGetValue(key, out float old) ? old : 0f) + entry.Value;
                }
            }
            Changed?.Invoke();
        }
    }

    // 게임 전체에서 쓰는 강화 판. 스킬 코드는 여기서만 읽는다.
    public static class SkillUpgrades
    {
        public static SkillUpgradeBoard Board { get; } = new();

        public static float Bonus(SkillUpgradeKind kind, SkillUpgradeScope scope = SkillUpgradeScope.All) =>
            Board.Bonus(kind, scope);

        // 1 + 보너스 (곱셈용). 음수로 0 아래가 되지 않게
        public static float Multiplier(SkillUpgradeKind kind, SkillUpgradeScope scope = SkillUpgradeScope.All) =>
            Mathf.Max(0f, 1f + Board.Bonus(kind, scope));

        // 스킬 에셋 기준: 원소가 있는 스킬은 그 원소 + All, 없는 스킬은 All 만
        public static float Bonus(SkillUpgradeKind kind, SkillData skill) => SlotBonus(kind, skill);

        public static float Multiplier(SkillUpgradeKind kind, SkillData skill) =>
            Mathf.Max(0f, 1f + SlotBonus(kind, skill));

        // 칸(슬롯)별 범위. 스킬 상태가 만들어질 때 등록 (같은 에셋이 칸을 옮기면 다시 등록)
        private static readonly Dictionary<SkillData, SkillUpgradeScope> SlotScopes = new();

        public static SkillUpgradeScope SlotScopeOf(SkillSlotId slot) => slot switch
        {
            SkillSlotId.Dash => SkillUpgradeScope.Dash,
            SkillSlotId.Weapon => SkillUpgradeScope.Shock,
            SkillSlotId.Ultimate => SkillUpgradeScope.Ultimate,
            _ => SkillUpgradeScope.All,
        };

        public static void BindSlot(SkillData skill, SkillSlotId slot)
        {
            if (skill != null)
                SlotScopes[skill] = SlotScopeOf(slot);
        }

        public static SkillUpgradeScope SlotScopeOf(SkillData skill) =>
            skill != null && SlotScopes.TryGetValue(skill, out SkillUpgradeScope scope) ? scope : SkillUpgradeScope.All;

        // 정확히 이 범위에 걸린 값만 (All 은 더하지 않음)
        public static float Exact(SkillUpgradeKind kind, SkillUpgradeScope scope) => Board.Exact(kind, scope);

        // 칸 범위까지 합친 값: 원소 범위(+All) + 칸 범위
        public static float SlotBonus(SkillUpgradeKind kind, SkillData skill)
        {
            SkillUpgradeScope slot = SlotScopeOf(skill);
            return Board.Bonus(kind, ScopeOf(skill)) + (slot == SkillUpgradeScope.All ? 0f : Board.Exact(kind, slot));
        }

        public static SkillUpgradeScope ScopeOf(SkillData skill) =>
            skill != null && skill.TryGetElement(out ElementType element)
                ? SkillUpgradeScopes.Of(element)
                : SkillUpgradeScope.All;
    }
}
