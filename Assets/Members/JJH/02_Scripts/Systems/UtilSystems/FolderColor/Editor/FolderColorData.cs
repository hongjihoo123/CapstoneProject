using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Members.JJH._02_Scripts.Systems.UtilSystems.FolderColor.Editor
{
    /// <summary>
    /// 폴더별 지정 색상 정보를 프로젝트 에셋(.asset)으로 저장하는 데이터 컨테이너.
    /// 폴더는 경로가 아닌 GUID로 추적하므로 폴더를 이동/이름변경해도 색상이 유지된다.
    /// 에셋으로 저장되어 버전관리에 포함되므로 팀원과도 공유된다.
    /// </summary>
    public class FolderColorData : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string guid;
            public Color color;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<string, Color> _lookup;

        // 에셋이 존재하지 않을 때 새로 생성할 기본 경로(이 스크립트와 동일한 Editor 폴더).
        private const string DefaultAssetPath = "Assets/DevLib/FolderColor/Editor/FolderColorData.asset";

        private static FolderColorData _instance;

        // FindAssets(에셋 DB 전체 스캔)는 비싸다. 결과가 null이어도 캐싱해서
        // ProjectWindow 콜백이 매 항목/매 이벤트마다 다시 검색하지 않도록 한다.
        private static bool _searched;

        /// <summary>
        /// 이미 존재하는 에셋을 찾아 반환한다. 없으면 null(읽기 전용 경로에서 사용).
        /// 색상을 실제로 지정할 때는 <see cref="GetOrCreate"/>를 사용한다.
        /// </summary>
        public static FolderColorData Instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (_searched) return null;

                _instance = Load();
                _searched = true;
                return _instance;
            }
        }

        /// <summary>에셋을 찾고, 없으면 새로 생성하여 반환한다(쓰기 경로용).</summary>
        public static FolderColorData GetOrCreate()
        {
            if (Instance != null) return _instance;

            _instance = Create();
            _searched = true;
            return _instance;
        }

        private static FolderColorData Load()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(FolderColorData));
            if (guids.Length == 0) return null;
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<FolderColorData>(path);
        }

        private static FolderColorData Create()
        {
            var data = CreateInstance<FolderColorData>();
            AssetDatabase.CreateAsset(data, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            return data;
        }

        private Dictionary<string, Color> Lookup
        {
            get
            {
                if (_lookup == null) RebuildLookup();
                return _lookup;
            }
        }

        private void RebuildLookup()
        {
            _lookup = new Dictionary<string, Color>(entries.Count);
            foreach (var e in entries)
            {
                if (!string.IsNullOrEmpty(e.guid))
                    _lookup[e.guid] = e.color;
            }
        }

        public bool TryGetColor(string guid, out Color color)
            => Lookup.TryGetValue(guid, out color);

        public void SetColor(string guid, Color color)
        {
            if (string.IsNullOrEmpty(guid)) return;

            bool found = false;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].guid == guid)
                {
                    entries[i] = new Entry { guid = guid, color = color };
                    found = true;
                    break;
                }
            }
            if (!found) entries.Add(new Entry { guid = guid, color = color });

            Lookup[guid] = color;
            Save();
        }

        public void ClearColor(string guid)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].guid == guid)
                {
                    entries.RemoveAt(i);
                    break;
                }
            }
            _lookup?.Remove(guid);
            Save();
        }

        private void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
            EditorApplication.RepaintProjectWindow();
        }

        // 다른 도메인 리로드/직렬화 이후 캐시를 무효화하여 entries로부터 다시 만들도록 한다.
        private void OnEnable() => _lookup = null;
    }
}
