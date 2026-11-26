using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element.Upgrade
{
    public interface ISkillUpgradeSource
    {
        float Bonus(SkillUpgradeKind kind, SkillUpgradeScope scope);
    }

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

    public static class SkillUpgrades
    {
        public static SkillUpgradeBoard Board { get; } = new();

        public static float Bonus(SkillUpgradeKind kind, SkillUpgradeScope scope = SkillUpgradeScope.All) =>
            Board.Bonus(kind, scope);

        public static float Multiplier(SkillUpgradeKind kind, SkillUpgradeScope scope = SkillUpgradeScope.All) =>
            Mathf.Max(0f, 1f + Board.Bonus(kind, scope));

        public static float Bonus(SkillUpgradeKind kind, SkillData skill) => SlotBonus(kind, skill);

        public static float Multiplier(SkillUpgradeKind kind, SkillData skill) =>
            Mathf.Max(0f, 1f + SlotBonus(kind, skill));

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

        public static float Exact(SkillUpgradeKind kind, SkillUpgradeScope scope) => Board.Exact(kind, scope);

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
