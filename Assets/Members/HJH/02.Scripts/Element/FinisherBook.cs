using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element
{
    // 피니시 스킬 콤보 표 (원소 3개 순서 → 저장되는 피니시 스킬).
    // 버프 콤보(JJH 표)랑 따로임. 체인 끝이 이 순서로 끝나면 그 피니시 스킬이 저장되고, C 로 피니시 쓸 때 같이 터짐.
    // 기본 표는 Resources/HJH/FinisherBook (씬에서 따로 안 넣어도 됨)
    [CreateAssetMenu(menuName = "Skill/Finisher Book (피니시 스킬 콤보)")]
    public class FinisherBook : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string name;
            public ElementType[] sequence = new ElementType[3];
            [Tooltip("피니시 때 같이 터지는 스킬 (05.Data/Skill/Finisher)")]
            public SkillData skill;
        }

        public const string DefaultPath = "HJH/FinisherBook";
        private static FinisherBook _default;

        [SerializeField] private List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries;

        public static FinisherBook Default => _default != null ? _default : _default = Resources.Load<FinisherBook>(DefaultPath);

        // 체인의 마지막 입력들이 이 순서로 끝나면 true (긴 거 우선)
        public bool TryFindAtEnd(IReadOnlyList<ElementType> inputs, out Entry found)
        {
            found = null;
            foreach (Entry entry in entries)
            {
                if (entry.skill == null || entry.sequence == null || entry.sequence.Length == 0 || entry.sequence.Length > inputs.Count)
                    continue;

                int offset = inputs.Count - entry.sequence.Length;
                bool match = true;
                for (int i = 0; i < entry.sequence.Length && match; i++)
                    match = inputs[offset + i] == entry.sequence[i];

                if (match && (found == null || entry.sequence.Length > found.sequence.Length))
                    found = entry;
            }
            return found != null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _default = null;
    }
}
