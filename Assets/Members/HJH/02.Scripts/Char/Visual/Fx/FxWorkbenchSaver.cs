#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // FxWorkbench의 수정용 사본(Play 중에 고친 것)을 원래 프리팹 파일에 써 넣음. 에디터에서만 있음.
    // 보통 Play 중에 고친 값은 Play를 끄면 사라지지만, 이걸로 프리팹에 남김.
    //
    // 방법: 프리팹 파일을 열어서(LoadPrefabContents) 사본과 같은 위치의 오브젝트끼리 짝을 지어
    //   - 컴포넌트 값 복사 (CopySerialized) / 사본에만 있는 자식·컴포넌트는 추가 / 사본에서 지운 건 삭제
    //   - 자식 위치·회전·크기, 켜짐/꺼짐, 순서 맞춤
    //   - 복사된 참조(서브 에미터, untinted 목록 등)가 사본 쪽을 가리키면 프리팹 쪽 짝으로 바꿔줌
    // 그리고 저장(SaveAsPrefabAsset). 프리팹 파일의 GUID와 루트는 그대로라 스킬 에셋 연결은 안 깨짐.
    public static class FxWorkbenchSaver
    {
        public static bool IsChanged(FxWorkbench.Part part) =>
            part.Copy != null && part.Snapshot != Snapshot(part.Copy);

        // 인스펙터 표시용: 0.5초에 한 번만 실제로 비교 (큰 프리팹은 비교가 무거움)
        public static bool IsChangedCached(FxWorkbench.Part part)
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - part.CheckedAt > 0.5)
            {
                part.CheckedAt = now;
                part.WasChanged = IsChanged(part);
            }
            return part.WasChanged;
        }

        public static int SaveChanged(FxWorkbench bench)
        {
            int saved = 0;
            foreach (FxWorkbench.Part part in bench.Parts)
            {
                if (!IsChanged(part))
                    continue;
                if (Save(part))
                    saved++;
            }

            if (saved > 0)
                AssetDatabase.SaveAssets();
            return saved;
        }

        public static bool Save(FxWorkbench.Part part)
        {
            string path = AssetDatabase.GetAssetPath(part.Prefab);
            if (string.IsNullOrEmpty(path))
                return false;

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var map = new Dictionary<Object, Object>
                {
                    [part.Copy] = contents,
                    [part.Copy.transform] = contents.transform
                };
                var copied = new List<Component>();

                // 루트는 이름·위치·켜짐을 건드리지 않음 (작업대가 바꿔놓은 것이라)
                SyncComponents(part.Copy, contents, map, copied);
                SyncChildren(part.Copy.transform, contents.transform, map, copied);
                Remap(copied, map);

                PrefabUtility.SaveAsPrefabAsset(contents, path, out bool success);
                if (!success)
                {
                    Debug.LogError($"[FxWorkbench] 저장 실패: {path}");
                    return false;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            part.Snapshot = Snapshot(part.Copy);
            Debug.Log($"[FxWorkbench] 프리팹에 저장함: {path}", part.Prefab);
            return true;
        }

        // 사본의 지금 상태를 글자로 (바뀌었는지 비교용)
        public static string Snapshot(GameObject root)
        {
            var text = new StringBuilder();
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            {
                text.Append('/').Append(node.name).Append(node.gameObject.activeSelf).Append(node.GetSiblingIndex());
                if (node != root.transform)
                    text.Append(node.localPosition).Append(node.localRotation).Append(node.localScale);
                foreach (Component component in node.GetComponents<Component>())
                {
                    if (component == null || component is Transform)
                        continue;
                    text.Append(component.GetType().Name).Append(EditorJsonUtility.ToJson(component));
                }
            }
            return text.ToString();
        }

        private static void SyncChildren(Transform source, Transform target, Dictionary<Object, Object> map, List<Component> copied)
        {
            // 이름이 같은 자식이 여럿일 수 있어서 "이름 + 몇 번째" 로 짝을 지음
            var byName = new Dictionary<string, List<Transform>>();
            foreach (Transform child in target)
            {
                if (!byName.TryGetValue(child.name, out List<Transform> list))
                    byName[child.name] = list = new List<Transform>();
                list.Add(child);
            }

            var seen = new Dictionary<string, int>();
            var used = new HashSet<Transform>();
            var pairs = new List<(Transform source, Transform target)>();

            for (int i = 0; i < source.childCount; i++)
            {
                Transform from = source.GetChild(i);
                seen.TryGetValue(from.name, out int index);
                seen[from.name] = index + 1;

                Transform to = byName.TryGetValue(from.name, out List<Transform> candidates) && index < candidates.Count
                    ? candidates[index]
                    : null;

                if (to == null)
                {
                    // 사본에 새로 생긴 자식 → 프리팹에도 통째로 추가
                    to = Object.Instantiate(from.gameObject, target).transform;
                    to.name = from.name;
                }

                used.Add(to);
                pairs.Add((from, to));
            }

            // 사본에서 지운 자식 → 프리팹에서도 삭제
            var removed = new List<GameObject>();
            foreach (Transform child in target)
                if (!used.Contains(child))
                    removed.Add(child.gameObject);
            foreach (GameObject gone in removed)
                Object.DestroyImmediate(gone);

            for (int i = 0; i < pairs.Count; i++)
            {
                (Transform from, Transform to) = pairs[i];
                to.SetSiblingIndex(i);
                to.localPosition = from.localPosition;
                to.localRotation = from.localRotation;
                to.localScale = from.localScale;
                to.gameObject.SetActive(from.gameObject.activeSelf);
                to.gameObject.layer = from.gameObject.layer;

                map[from.gameObject] = to.gameObject;
                map[from] = to;
                SyncComponents(from.gameObject, to.gameObject, map, copied);
                SyncChildren(from, to, map, copied);
            }
        }

        private static void SyncComponents(GameObject source, GameObject target, Dictionary<Object, Object> map, List<Component> copied)
        {
            var keep = new HashSet<Component> { target.transform };
            var countByType = new Dictionary<System.Type, int>();

            foreach (Component from in source.GetComponents<Component>())
            {
                if (from == null || from is Transform)
                    continue;

                System.Type type = from.GetType();
                countByType.TryGetValue(type, out int index);
                countByType[type] = index + 1;

                Component[] matches = target.GetComponents(type);
                Component to = index < matches.Length ? matches[index] : target.AddComponent(type);
                if (to == null)
                    continue;

                EditorUtility.CopySerialized(from, to);
                keep.Add(to);
                copied.Add(to);
                map[from] = to;
            }

            // 사본에서 뺀 컴포넌트 → 프리팹에서도 삭제 (렌더러는 파티클 시스템이 필요로 해서 나중에)
            var removed = new List<Component>();
            foreach (Component component in target.GetComponents<Component>())
                if (component != null && !keep.Contains(component))
                    removed.Add(component);
            removed.Sort((a, b) => (a is Renderer ? 1 : 0).CompareTo(b is Renderer ? 1 : 0));
            foreach (Component component in removed)
                Object.DestroyImmediate(component);
        }

        // 복사해 온 값 안의 참조가 사본(씬) 쪽 오브젝트를 가리키면, 프리팹 안의 짝으로 바꿔줌
        private static void Remap(List<Component> copied, Dictionary<Object, Object> map)
        {
            foreach (Component component in copied)
            {
                if (component == null)
                    continue;

                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                bool changed = false;
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                        continue;
                    Object value = property.objectReferenceValue;
                    if (value != null && map.TryGetValue(value, out Object replacement))
                    {
                        property.objectReferenceValue = replacement;
                        changed = true;
                    }
                }

                if (changed)
                    serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
#endif
