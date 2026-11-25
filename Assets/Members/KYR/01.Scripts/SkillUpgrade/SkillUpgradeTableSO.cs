using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using UnityEngine;

namespace Members.KYR._01.Scripts.SkillUpgrade
{
    // 스킬별 기본 데미지 강화표. 스킬마다 데미지 규모가 달라서 레벨별 비용/증가량을 스킬마다 따로 적는다.
    [CreateAssetMenu(fileName = "SkillUpgradeTable", menuName = "SO/Skill/SkillUpgradeTable", order = 0)]
    public class SkillUpgradeTableSO : ScriptableObject
    {
        [Serializable]
        public class Level
        {
            [Min(0), Tooltip("이 레벨로 올리는 데 드는 런 골드")]
            public int cost = 50;

            [Tooltip("이 레벨에서 추가로 붙는 고정 데미지. 타격 1회(틱 1회)마다 더해진다")]
            public float flatDamage;

            [Tooltip("이 레벨에서 추가로 붙는 배율. 0.1 = +10%. 고정 데미지를 더한 뒤 곱한다")]
            public float percentDamage;
        }

        [Serializable]
        public class Entry
        {
            public SkillData skill;
            public List<Level> levels = new List<Level>();

            public int MaxLevel => levels != null ? levels.Count : 0;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        // 대쉬처럼 런타임에 복제된 스킬도 같은 이름이면 찾는다.
        public Entry Find(SkillData skill)
        {
            if (skill == null || entries == null)
                return null;

            foreach (Entry entry in entries)
            {
                if (entry != null && entry.skill == skill)
                    return entry;
            }

            string key = SkillUpgradeService.KeyOf(skill);
            foreach (Entry entry in entries)
            {
                if (entry != null && entry.skill != null && SkillUpgradeService.KeyOf(entry.skill) == key)
                    return entry;
            }

            return null;
        }
    }
}
