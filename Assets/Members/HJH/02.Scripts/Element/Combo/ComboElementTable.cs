using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Element.Upgrade;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;
using Mod = Assets.Members.HJH._02.Scripts.Element.ComboStatBuff.Mod;

namespace Assets.Members.HJH._02.Scripts.Element
{
    [CreateAssetMenu(menuName = "Combo/Element Table")]
    public class ComboElementTable : ScriptableObject
    {
        [Serializable]
        public class ElementEffect
        {
            public ElementType element;
            public Mod[] mods = Array.Empty<Mod>();
        }

        [Serializable]
        public class PairBonus
        {
            public ElementType first;
            public ElementType second;
            [Tooltip("미리보기에 보이는 이름 (예: 증폭, 상성)")] public string label;
            public Mod[] mods = Array.Empty<Mod>();
        }

        private const string DefaultPath = "HJH/ComboElementTable";
        private static ComboElementTable _default;

        public static ComboElementTable Default =>
            _default != null ? _default : _default = Resources.Load<ComboElementTable>(DefaultPath);

        [SerializeField, Min(1)] private int minLength = 2;
        [SerializeField, Min(1)] private int maxLength = 4;
        [SerializeField, Min(0.1f)] private float duration = 10f;
        [SerializeField, Tooltip("콤보 길이별 전체 효과 배율 (0번 = 최소 길이, 보통 2칸). 최소 길이보다 짧으면 배율 없음(x1)")] private float[] lengthScale = { 1.75f, 2.5f, 4f };
        [SerializeField] private List<ElementEffect> elements = new();
        [SerializeField] private List<PairBonus> pairs = new();

        public int MinLength => minLength;
        public int MaxLength => maxLength;
        public float Duration => duration * SkillUpgrades.Multiplier(SkillUpgradeKind.ComboDuration);
        public IReadOnlyList<ElementEffect> Elements => elements;
        public IReadOnlyList<PairBonus> Pairs => pairs;

        public float ScaleFor(int length) =>
            lengthScale == null || lengthScale.Length == 0 || length < minLength ? 1f : lengthScale[Mathf.Clamp(length - minLength, 0, lengthScale.Length - 1)];

        public ElementEffect EffectOf(ElementType element) => elements.Find(e => e.element == element);

        public PairBonus PairOf(ElementType first, ElementType second) =>
            pairs.Find(p => p.first == first && p.second == second);

        public void Set(int min, int max, float time, float[] scale, List<ElementEffect> elementList, List<PairBonus> pairList)
        {
            minLength = min;
            maxLength = max;
            duration = time;
            lengthScale = scale;
            elements = elementList ?? new List<ElementEffect>();
            pairs = pairList ?? new List<PairBonus>();
        }
    }
}
