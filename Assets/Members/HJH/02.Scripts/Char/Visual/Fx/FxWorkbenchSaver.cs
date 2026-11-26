#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    public static class FxWorkbenchSaver
    {
        public static bool IsChanged(FxWorkbench.Part part) =>
            part.Copy != null && part.Snapshot != Snapshot(part.Copy);

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
                    to = Object.Instantiate(from.gameObject, target).transform;
                    to.name = from.name;
                }

                used.Add(to);
                pairs.Add((from, to));
            }

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

            var removed = new List<Component>();
            foreach (Component component in target.GetComponents<Component>())
                if (component != null && !keep.Contains(component))
                    removed.Add(component);
            removed.Sort((a, b) => (a is Renderer ? 1 : 0).CompareTo(b is Renderer ? 1 : 0));
            foreach (Component component in removed)
                Object.DestroyImmediate(component);
        }

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
