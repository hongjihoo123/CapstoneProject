using System;
using System.Collections.Generic;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01_Scripts.EffectBuffs;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element
{
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
