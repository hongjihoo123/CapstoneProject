using System;
using System.Collections.Generic;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;
using Mod = Assets.Members.HJH._02.Scripts.Element.ComboStatBuff.Mod;

namespace Assets.Members.HJH._02.Scripts.Element
{
    // 콤보 효과를 정하는 표. 콤보 효과 = 원소마다의 기본 효과 + 이웃한 두 원소의 보너스, 전체에 길이 배율.
    //  - elements: 원소 하나가 들어갈 때마다 붙는 효과 (불 = 공격력, 물 = 마법력 ...). 같은 원소를 여러 번 넣으면 그만큼 쌓임
    //  - pairs: 앞 원소 → 뒤 원소 순서로 붙어 있을 때 추가 효과 (불→불 증폭, 불→물 상성 ...). 없으면 보너스 없음
    //  - 효과는 스탯 묶음(mods)이라 공격속도 같은 다른 스탯을 넣어도 됨
    // 에셋은 Resources/HJH/ComboElementTable. 메뉴 HJH > 콤보 > 원소 효과 표 만들기 로 임시값을 채움
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
        [SerializeField, Tooltip("콤보 길이별 전체 효과 배율 (0번 = 1칸)")] private float[] lengthScale = { 1.25f, 1.75f, 2.5f, 4f };
        [SerializeField] private List<ElementEffect> elements = new();
        [SerializeField] private List<PairBonus> pairs = new();

        public int MinLength => minLength;
        public int MaxLength => maxLength;
        public float Duration => duration;
        public IReadOnlyList<ElementEffect> Elements => elements;
        public IReadOnlyList<PairBonus> Pairs => pairs;

        public float ScaleFor(int length) =>
            lengthScale == null || lengthScale.Length == 0 ? 1f : lengthScale[Mathf.Clamp(length - 1, 0, lengthScale.Length - 1)];

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
