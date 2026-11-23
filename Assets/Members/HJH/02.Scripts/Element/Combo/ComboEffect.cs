using System.Collections.Generic;
using System.Text;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01.Scripts.Stats;
using Mod = Assets.Members.HJH._02.Scripts.Element.ComboStatBuff.Mod;

namespace Assets.Members.HJH._02.Scripts.Element
{
    // 원소 순서 하나의 콤보 효과 계산 결과 (ComboElementTable 기준).
    //  - Parts: 어디서 온 효과인지 한 줄씩 (불 / 물 / 불→물 상성 ...) → 바인딩 창 미리보기
    //  - Total: 같은 스탯끼리 합치고 길이 배율까지 곱한 최종 효과 → 실제 버프
    public sealed class ComboEffect
    {
        public readonly struct Part
        {
            public readonly string Source;
            public readonly Mod Mod;
            public readonly bool IsBonus;

            public Part(string source, Mod mod, bool isBonus)
            {
                Source = source;
                Mod = mod;
                IsBonus = isBonus;
            }
        }

        private readonly List<Part> _parts = new();
        private readonly List<Mod> _total = new();

        public IReadOnlyList<Part> Parts => _parts;
        public IReadOnlyList<Mod> Total => _total;
        public float Scale { get; private set; } = 1f;

        public static ComboEffect Evaluate(IReadOnlyList<ElementType> sequence, ComboElementTable table)
        {
            var effect = new ComboEffect();
            if (sequence == null || table == null || sequence.Count == 0)
                return effect;

            for (int i = 0; i < sequence.Count; i++)
            {
                ComboElementTable.ElementEffect basic = table.EffectOf(sequence[i]);
                if (basic != null)
                    effect.AddParts(ElementComboBook.KoreanName(sequence[i]), basic.mods, false);

                if (i == 0)
                    continue;

                ComboElementTable.PairBonus pair = table.PairOf(sequence[i - 1], sequence[i]);
                if (pair != null)
                {
                    string source = $"{ElementComboBook.KoreanName(pair.first)}→{ElementComboBook.KoreanName(pair.second)} {pair.label}".TrimEnd();
                    effect.AddParts(source, pair.mods, true);
                }
            }

            effect.Scale = table.ScaleFor(sequence.Count);
            effect.Sum();
            return effect;
        }

        private void AddParts(string source, Mod[] mods, bool bonus)
        {
            if (mods == null)
                return;
            foreach (Mod mod in mods)
                _parts.Add(new Part(source, mod, bonus));
        }

        private void Sum()
        {
            foreach (Part part in _parts)
            {
                int index = _total.FindIndex(m => m.stat == part.Mod.stat && m.type == part.Mod.type);
                if (index < 0)
                    _total.Add(part.Mod);
                else
                    _total[index] = new Mod(part.Mod.stat, part.Mod.type, _total[index].value + part.Mod.value);
            }

            for (int i = _total.Count - 1; i >= 0; i--)
            {
                Mod mod = _total[i];
                if (UnityEngine.Mathf.Approximately(mod.value, 0f))
                    _total.RemoveAt(i);
                else
                    _total[i] = new Mod(mod.stat, mod.type, mod.value * Scale);
            }
        }

        // "공격력 +25% · 마법력 +20%"
        public string Summary(string separator = " · ")
        {
            if (_total.Count == 0)
                return "효과 없음";

            var text = new StringBuilder();
            foreach (Mod mod in _total)
            {
                if (text.Length > 0)
                    text.Append(separator);
                text.Append(Describe(mod));
            }
            return text.ToString();
        }

        public static string Describe(Mod mod) => $"{StatName(mod.stat)} {Amount(mod)}";

        public static string Amount(Mod mod)
        {
            string sign = mod.value >= 0f ? "+" : "-";
            float abs = UnityEngine.Mathf.Abs(mod.value);
            return mod.type == StatModifierType.Flat ? $"{sign}{abs:0}" : $"{sign}{abs * 100f:0}%";
        }

        public static string StatName(PlayerStatId stat) => stat switch
        {
            PlayerStatId.Damage => "공격력",
            PlayerStatId.MagicDamage => "마법력",
            PlayerStatId.AttackSpeed => "공격 속도",
            PlayerStatId.MoveSpeed => "이동 속도",
            PlayerStatId.ArmorDefense => "물리 방어력",
            PlayerStatId.MagicDefense => "마법 방어력",
            PlayerStatId.DashSpeed => "대시 속도",
            PlayerStatId.MaxHp => "최대 체력",
            PlayerStatId.IncomingDamageTaken => "받는 피해",
            PlayerStatId.SkillCooldownReduction => "쿨타임 감소",
            _ => stat.ToString()
        };
    }
}
