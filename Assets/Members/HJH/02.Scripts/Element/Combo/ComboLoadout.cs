using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element
{
    public static class ComboLoadout
    {
        private static ElementType[] _starter;
        private static readonly List<ElementType[]> _bounds = new();
        public const int MaxBound = 3;
        private static readonly Dictionary<string, ComboEffect> _effects = new();

        public static IComboBindGate Gate { get; set; } = new FreeBindGate();

        public static IReadOnlyList<ElementType> Starter => _starter;
        public static IReadOnlyList<ElementType[]> Bounds => _bounds;
        public static IReadOnlyList<ElementType> Bound => _bounds.Count > 0 ? _bounds[_bounds.Count - 1] : null;
        public static bool HasStarter => _starter != null;

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _starter = null;
            _bounds.Clear();
            _effects.Clear();
            Gate = new FreeBindGate();
            Changed = null;
        }

        public static bool TryGetBaseElements(SkillData weaponSkill, SkillData ultimate, out ElementType basic, out ElementType ult)
        {
            ult = default;
            basic = default;
            return weaponSkill != null && ultimate != null && weaponSkill.TryGetElement(out basic) && ultimate.TryGetElement(out ult);
        }

        public static List<ElementType[]> StarterOptions(ElementType basic, ElementType ult)
        {
            var options = new List<ElementType[]> { new[] { basic, ult } };
            if (basic != ult)
                options.Add(new[] { ult, basic });
            return options;
        }

        public static List<ElementType[]> StarterOptions(SkillData weaponSkill, SkillData ultimate) =>
            TryGetBaseElements(weaponSkill, ultimate, out ElementType basic, out ElementType ult)
                ? StarterOptions(basic, ult)
                : new List<ElementType[]>();

        public static void ChooseStarter(IReadOnlyList<ElementType> sequence)
        {
            if (sequence == null || sequence.Count == 0 || Same(_starter, sequence))
                return;
            _starter = sequence.ToArray();
            Changed?.Invoke();
        }

        public static void EnsureStarterFor(SkillData weaponSkill, SkillData ultimate)
        {
            List<ElementType[]> options = StarterOptions(weaponSkill, ultimate);
            if (options.Count == 0 || (_starter != null && options.Any(o => Same(o, _starter))))
                return;
            ChooseStarter(options[0]);
        }

        public static void BeginRun()
        {
            if (_bounds.Count == 0)
                return;
            _bounds.Clear();
            Changed?.Invoke();
        }

        public static bool IsValidLength(int length, out string reason)
        {
            ComboElementTable table = ComboElementTable.Default;
            int min = table != null ? table.MinLength : 2;
            int max = table != null ? table.MaxLength : 4;
            reason = length < min ? $"최소 {min}칸은 채워야 합니다." : length > max ? $"최대 {max}칸까지입니다." : null;
            return reason == null;
        }

        public static bool CanBind(IReadOnlyList<ElementType> sequence, out string reason)
        {
            if (sequence == null)
            {
                reason = "콤보가 비어 있습니다.";
                return false;
            }
            if (!IsValidLength(sequence.Count, out reason))
                return false;
            if (_starter != null && Same(_starter, sequence) || _bounds.Exists(b => Same(b, sequence)))
            {
                reason = "이미 가지고 있는 콤보입니다.";
                return false;
            }
            ElementType[] overlap = Owned().FirstOrDefault(owned => Contains(owned, sequence) || Contains(sequence, owned));
            if (overlap != null)
            {
                reason = $"가지고 있는 콤보 [{NameOf(overlap)}] 와 겹칩니다. 한 콤보가 다른 콤보 안에 들어가면 안 됩니다.";
                return false;
            }
            if (_bounds.Count >= MaxBound)
            {
                reason = $"바인딩한 콤보는 {MaxBound}개까지입니다. 위 목록에서 지우고 다시 하세요.";
                return false;
            }
            return Gate.CanBind(sequence, out reason);
        }

        public static bool TryBind(IReadOnlyList<ElementType> sequence, out string reason)
        {
            if (!CanBind(sequence, out reason))
                return false;

            _bounds.Add(sequence.ToArray());
            Gate.OnBound(sequence);
            Changed?.Invoke();
            return true;
        }

        public static void Unbind(int index)
        {
            if (index < 0 || index >= _bounds.Count)
                return;
            _bounds.RemoveAt(index);
            Changed?.Invoke();
        }

        public static void Unbind()
        {
            if (_bounds.Count == 0)
                return;
            _bounds.Clear();
            Changed?.Invoke();
        }

        public static ComboEffect Evaluate(IReadOnlyList<ElementType> sequence) =>
            ComboEffect.Evaluate(sequence, ComboElementTable.Default);

        public static string NameOf(IReadOnlyList<ElementType> sequence) =>
            sequence == null ? string.Empty : string.Join(" → ", sequence.Select(ElementComboBook.KoreanName));

        public static float DurationOf(string comboName)
        {
            ComboElementTable table = ComboElementTable.Default;
            return _effects.ContainsKey(comboName) && table != null ? table.Duration : 0f;
        }

        public static string EffectOf(string comboName) =>
            _effects.TryGetValue(comboName, out ComboEffect effect) ? effect.Summary() : string.Empty;

        public static IReadOnlyList<ComboStatBuff.Mod> ModsOf(string comboName) =>
            _effects.TryGetValue(comboName, out ComboEffect effect) ? effect.Total : Array.Empty<ComboStatBuff.Mod>();

        public static void ApplyTo(ElementBuffController controller)
        {
            if (controller == null)
                return;

            ComboElementTable table = ComboElementTable.Default;
            float duration = table != null ? table.Duration : 10f;
            _effects.Clear();

            var combos = new List<ElementBuffController.ElementCombo>();
            var all = new List<ElementType[]>(_bounds) { _starter };
            foreach (ElementType[] sequence in all)
            {
                if (sequence == null || sequence.Length == 0)
                    continue;

                string name = NameOf(sequence);
                if (_effects.ContainsKey(name))
                    continue;

                ComboEffect effect = Evaluate(sequence);
                _effects[name] = effect;

                var buff = ScriptableObject.CreateInstance<ComboStatBuff>();
                buff.name = "Combo_" + name;
                buff.Configure(effect.Total.ToArray(), 0f, 0f);
                buff.SetInfo(name, duration);

                combos.Add(new ElementBuffController.ElementCombo
                {
                    name = name,
                    sequence = sequence,
                    effect = effect.Summary(),
                    effectBuff = buff,
                    duration = duration
                });
            }

            combos.Sort((a, b) => b.sequence.Length.CompareTo(a.sequence.Length));
            controller.SetCombos(combos);
        }

        private static IEnumerable<ElementType[]> Owned()
        {
            if (_starter != null)
                yield return _starter;
            foreach (ElementType[] bound in _bounds)
                yield return bound;
        }

        public static bool Contains(IReadOnlyList<ElementType> outer, IReadOnlyList<ElementType> inner)
        {
            if (outer == null || inner == null || inner.Count == 0 || inner.Count > outer.Count)
                return false;
            for (int start = 0; start + inner.Count <= outer.Count; start++)
            {
                int i = 0;
                while (i < inner.Count && outer[start + i] == inner[i])
                    i++;
                if (i == inner.Count)
                    return true;
            }
            return false;
        }

        private static bool Same(IReadOnlyList<ElementType> a, IReadOnlyList<ElementType> b)
        {
            if (a == null || b == null || a.Count != b.Count)
                return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }
    }
}
