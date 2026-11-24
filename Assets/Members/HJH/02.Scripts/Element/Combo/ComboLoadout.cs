using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element
{
    // 지금 런에서 들고 있는 콤보 (씬이 바뀌어도 기억, CharacterSelection 과 같은 방식).
    //  - Starter: 픽 화면에서 고른 맛보기 콤보. 캐릭터 우클릭 원소 A, 궁극기 원소 B 로 A→B 또는 B→A 중 하나
    //  - Bound:   상점(바인딩 창)에서 직접 짠 콤보. 2~4칸, 아무 원소나. 다시 바인딩하면 교체
    //  - 효과는 둘 다 ComboElementTable 로 계산 (원소 효과 + 이웃 보너스 × 길이 배율)
    //  - 바뀌면 Changed → ElementComboChain 이 콤보 표를 다시 만듦
    // 나중에 재화/레벨 조건은 Gate 를 바꿔 끼우면 됨 (IComboBindGate)
    public static class ComboLoadout
    {
        private static ElementType[] _starter;
        private static readonly List<ElementType[]> _bounds = new();
        public const int MaxBound = 3;
        private static readonly Dictionary<string, ComboEffect> _effects = new();

        public static IComboBindGate Gate { get; set; } = new FreeBindGate();

        public static IReadOnlyList<ElementType> Starter => _starter;
        public static IReadOnlyList<ElementType[]> Bounds => _bounds;
        // 가장 최근에 바인딩한 콤보 (없으면 null). 예전 코드/튜토리얼 호환용
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

        // ---------------------------------------------------------------- 맛보기 콤보

        // 캐릭터 고유 스킬(우클릭) 원소와 궁극기 원소. 둘 중 하나라도 원소가 없으면 false
        public static bool TryGetBaseElements(SkillData weaponSkill, SkillData ultimate, out ElementType basic, out ElementType ult)
        {
            ult = default;
            basic = default;
            return weaponSkill != null && ultimate != null && weaponSkill.TryGetElement(out basic) && ultimate.TryGetElement(out ult);
        }

        // A→B, B→A (같은 원소면 하나만)
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

        // 고른 맛보기 콤보가 이 캐릭터 것이 아니면(캐릭터를 바꿨거나 픽 없이 맵을 열었을 때) 첫 번째 것으로
        public static void EnsureStarterFor(SkillData weaponSkill, SkillData ultimate)
        {
            List<ElementType[]> options = StarterOptions(weaponSkill, ultimate);
            if (options.Count == 0 || (_starter != null && options.Any(o => Same(o, _starter))))
                return;
            ChooseStarter(options[0]);
        }

        // 새 런 시작 (픽 화면 "게임 시작"): 맛보기만 남기고 바인딩한 콤보는 지움
        public static void BeginRun()
        {
            if (_bounds.Count == 0)
                return;
            _bounds.Clear();
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- 바인딩

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

        // 바인딩한 콤보 하나 지우기 (index = Bounds 번호)
        public static void Unbind(int index)
        {
            if (index < 0 || index >= _bounds.Count)
                return;
            _bounds.RemoveAt(index);
            Changed?.Invoke();
        }

        // 바인딩한 콤보 전부 지우기
        public static void Unbind()
        {
            if (_bounds.Count == 0)
                return;
            _bounds.Clear();
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- 효과 / 이름

        public static ComboEffect Evaluate(IReadOnlyList<ElementType> sequence) =>
            ComboEffect.Evaluate(sequence, ComboElementTable.Default);

        public static string NameOf(IReadOnlyList<ElementType> sequence) =>
            sequence == null ? string.Empty : string.Join(" → ", sequence.Select(ElementComboBook.KoreanName));

        // 콤보 이름 -> 효과가 켜져 있는 전체 시간(초)
        public static float DurationOf(string comboName)
        {
            ComboElementTable table = ComboElementTable.Default;
            return _effects.ContainsKey(comboName) && table != null ? table.Duration : 0f;
        }

        // 콤보 이름 -> 화면에 보여줄 효과 설명. 없으면 빈 글자
        public static string EffectOf(string comboName) =>
            _effects.TryGetValue(comboName, out ComboEffect effect) ? effect.Summary() : string.Empty;

        // JJH 콤보 표를 "맛보기 + 바인딩" 으로 바꿈. 긴 콤보가 먼저 오게
        // (컨트롤러가 앞에서부터 찾아서, 짧은 게 앞에 있으면 긴 콤보의 끝부분에 걸려 짧은 쪽 효과가 나감)
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
