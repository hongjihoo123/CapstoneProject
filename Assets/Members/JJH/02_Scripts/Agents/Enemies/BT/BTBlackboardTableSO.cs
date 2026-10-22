using System;
using System.Collections.Generic;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT
{
    [CreateAssetMenu(fileName = "Blackboard table", menuName = "SO/Enemy/BT Blackboard Table", order = 0)]
    public class BTBlackboardTableSO : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string name;
            public SerializableGUID guid;
        }

        [Tooltip("베이크 소스가 되는 BT Graph, 유닛의 BT Agent에 꽂는것과 동일하면 된다.")]
        [SerializeField] private BehaviorGraph graph;

        [Tooltip("베이크 결과임. 직접 수정하면 안된다.")]
        [SerializeField] private List<Entry> entries = new();

        public BehaviorGraph Graph => graph;
        public IReadOnlyList<Entry> Entries => entries;

        //SO 로드시에 딱 한번만 생성되는거. 유닛수와 무관하게 딕셔너리는 그래프당 한개만 생성된다.
        private Dictionary<string, SerializableGUID> _map;
        public IReadOnlyDictionary<string, SerializableGUID> Map => _map ??= BuildMap();

        private Dictionary<string, SerializableGUID> BuildMap()
        {
            var map = new Dictionary<string, SerializableGUID>(entries.Count);
            foreach (Entry e in entries)
            {
                map[e.name] = e.guid; //딕셔너리화 시킨다.
            }

            if (map.Count == 0)
                Debug.LogError($"[{name}] 블랙보드 테이블이 비어있습니다. 인스펙터에서 Bake를 수행하세요");

            return map;
        }

        public bool TryGetGuid(string varName, out SerializableGUID guid)
            => _map.TryGetValue(varName, out guid);

        public SerializableGUID GetGuid(string varName)
        {
            if (Map.TryGetValue(varName, out SerializableGUID guid))
                return guid;
            Debug.LogError($"[{name}] 블랙보드 변수를 찾을 수 없음 - {varName}");
            return default;
        }

#if UNITY_EDITOR
        public (bool isSuccess, string message, int count) Bake()
        {
            _map = null; //런타임 캐시를 무효화 하고 

            if (graph == null)
                return (false, "그래프 에셋이 비어있습니다.", 0);

            //그래프에서 블랙보드의 참조를 가져온다.
            BlackboardReference bbRef = graph.BlackboardReference;
            if (bbRef == null || bbRef.Blackboard == null)
                return (false, "그래프의 블랙보드가 없습니다. 그래프를 한번은 저장해야합니다.", 0);

            entries.Clear();
            //해당 블랙보드에 있는 모든 변수들을 가져온다.
            foreach (BlackboardVariable v in bbRef.Blackboard.Variables)
            {
                if (v == null || string.IsNullOrEmpty(v.Name))
                    continue; //유효하지 않은 값은 걸러내고
                entries.Add(new Entry { name = v.Name, guid = v.GUID });
            }
            return (true, $"그래프에서 변수 {entries.Count}개를 베이크했습니다.", entries.Count);
        }

        //에디터에서 모든 변수가 잘 베이크되었는지 검증하기 위한 함수
        public HashSet<string> GetBakedNames()
        {
            var set = new HashSet<string>();
            foreach (Entry e in entries)
                set.Add(e.name);
            return set;
        }
#endif
    }
}