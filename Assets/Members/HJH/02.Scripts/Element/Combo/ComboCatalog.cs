using System;
using System.Collections.Generic;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01_Scripts.EffectBuffs;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element
{
    // 게임에 있는 콤보 전부 (속성 순서 + 효과). 픽 화면에서 이 중 몇 개를 골라 들고 감.
    //  - alwaysOn: 고르는 대상이 아니고 항상 켜져 있음 (속성 하나짜리 기본 콤보)
    //  - 나머지: 픽 화면 "콤보 커스터마이징"에서 골라야 게임에 들어감 (ComboLoadout)
    // 에셋은 Resources/HJH/ComboCatalog. 메뉴 HJH > 콤보 > 임시 콤보 목록 만들기 로 채움
    [CreateAssetMenu(menuName = "Combo/Combo Catalog")]
    public class ComboCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string name;
            public ElementType[] sequence = Array.Empty<ElementType>();
            [Tooltip("화면에 보여줄 효과 설명")] public string effect;
            [Tooltip("실제 효과. 비어 있으면 시간만 돌아감")] public EffectBuff buff;
            public float duration = 10f;
            [Tooltip("켜면 항상 적용 (커스터마이징 목록에 안 나옴)")] public bool alwaysOn;
        }

        private const string DefaultPath = "HJH/ComboCatalog";
        private static ComboCatalog _default;

        public static ComboCatalog Default => _default != null ? _default : _default = Resources.Load<ComboCatalog>(DefaultPath);

        [SerializeField] private List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries;

        public void SetEntries(List<Entry> list) => entries = list ?? new List<Entry>();
    }
}
