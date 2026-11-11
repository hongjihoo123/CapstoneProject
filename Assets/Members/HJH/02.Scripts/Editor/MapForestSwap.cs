using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HJH.EditorTools
{
    // 맵 1·2(Map_L_Normal, Map_Bridge_Normal) 씬에 있는 기존 숲 나무(KM 'Plane.002 (1) 1')를 PF_JungleTree_0~2 로 교체.
    //  - 같은 자리, 무작위 방향, 높이는 원래 나무와 비슷하게 맞춤 (크기 비율로 계산해서 숲 실루엣이 변하지 않게)
    //  - 그림자·정적 플래그·콜라이더 켬/끔·부모·순서는 그대로 물려받음
    //  - 씬 파일은 프로젝트 밖 _RemovedBackup/<날짜>/ 에 먼저 복사해 둠 (되돌리려면 그 파일을 덮어쓰면 됨)
    public static class MapForestSwap
    {
        private const string KmTree = "Assets/Members/KM/Assets/Props/Natural/Rock/Plane.002 (1) 1.prefab";
        private const string JwDir = "Assets/Members/HJH/09.JungleWater/Generated/Prefabs/Props/";
        private static readonly string[] Scenes =
        {
            "Assets/Members/HJH/01.Scene/Map_L_Normal.unity",
            "Assets/Members/HJH/01.Scene/Map_Bridge_Normal.unity",
        };

        [MenuItem("HJH/맵/나무/숲 나무를 PF_JungleTree 로 교체 (맵 1·2 씬 모두)")]
        private static void SwapAll()
        {
            if (EditorApplication.isPlaying) return;

            var prefabs = new List<GameObject>();
            for (int i = 0; i < 3; i++)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(JwDir + $"PF_JungleTree_{i}.prefab");
                if (p != null) prefabs.Add(p);
            }
            if (prefabs.Count == 0) { Debug.LogWarning("[ForestSwap] PF_JungleTree_0~2 프리팹을 찾지 못했습니다."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string original = EditorSceneManager.GetActiveScene().path;
            string backup = "_RemovedBackup/" + DateTime.Now.ToString("yyyy-MM-dd") + "/Assets/Members/HJH/01.Scene/";
            Directory.CreateDirectory(backup);
            foreach (string s in Scenes)
                if (File.Exists(s)) File.Copy(s, backup + Path.GetFileName(s), true);

            int total = 0;
            var report = new List<string>();
            foreach (string path in Scenes)
            {
                if (!File.Exists(path)) { report.Add(Path.GetFileName(path) + ": 없음"); continue; }
                Scene sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int n = Swap(sc, prefabs);
                total += n;
                report.Add($"{sc.name}: {n}그루");
                if (n > 0) EditorSceneManager.SaveScene(sc);
            }
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);

            Debug.Log($"[ForestSwap] 숲 나무 {total}그루를 PF_JungleTree 로 교체: {string.Join(", ", report)}. 씬 백업: {backup}");
        }

        private static int Swap(Scene scene, List<GameObject> prefabs)
        {
            var targets = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var go = t.gameObject;
                    if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;
                    var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
                    if (src != null && AssetDatabase.GetAssetPath(src) == KmTree) targets.Add(go);
                }
            if (targets.Count == 0) return 0;

            // 새 나무 종류별 원래 높이 (크기 맞추기용)
            var nativeH = new float[prefabs.Count];
            for (int i = 0; i < prefabs.Count; i++)
            {
                var tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], scene);
                nativeH[i] = Mathf.Max(0.5f, HeightOf(tmp, 3f));
                UnityEngine.Object.DestroyImmediate(tmp);
            }

            var rng = new System.Random(7);
            int count = 0;
            foreach (var old in targets)
            {
                float oldH = HeightOf(old, 6f);
                int idx = rng.Next(prefabs.Count);
                Transform parent = old.transform.parent;
                var nu = parent != null
                    ? (GameObject)PrefabUtility.InstantiatePrefab(prefabs[idx], parent)
                    : (GameObject)PrefabUtility.InstantiatePrefab(prefabs[idx], scene);

                nu.name = "Tree_" + prefabs[idx].name.Replace("PF_JungleTree_", "J");
                nu.transform.position = old.transform.position;
                nu.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                nu.transform.localScale = Vector3.one * Mathf.Clamp(oldH * 1.05f / nativeH[idx], 0.4f, 5f);
                nu.layer = old.layer;
                GameObjectUtility.SetStaticEditorFlags(nu, GameObjectUtility.GetStaticEditorFlags(old));

                // 그림자·콜라이더 상태 물려받기
                var oldRenderer = old.GetComponentInChildren<MeshRenderer>(true);
                if (oldRenderer != null)
                    foreach (var r in nu.GetComponentsInChildren<MeshRenderer>(true)) r.shadowCastingMode = oldRenderer.shadowCastingMode;
                bool anyCollider = false;
                foreach (var c in old.GetComponentsInChildren<Collider>(true)) anyCollider |= c.enabled;
                foreach (var c in nu.GetComponentsInChildren<Collider>(true)) c.enabled = anyCollider;

                nu.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
                UnityEngine.Object.DestroyImmediate(old);
                count++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return count;
        }

        private static float HeightOf(GameObject go, float fallback)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return fallback;
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b.size.y > 0.01f ? b.size.y : fallback;
        }
    }
}
