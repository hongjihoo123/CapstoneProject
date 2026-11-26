using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HJH.EditorTools
{
    public static class PlayerRigBuilder
    {
        private const string StartScene = "Assets/Members/JJH/01_Scene/Map_Test/Start.unity";
        private const string RigPath = "Assets/Members/HJH/05.Data/Prefab/PlayerRig.prefab";
        private static readonly string[] MapScenes =
        {
            "Assets/Members/HJH/01.Scene/Map_L_Normal.unity",
            "Assets/Members/HJH/01.Scene/Map_Bridge_Normal.unity",
        };

        private static readonly string[] Include =
        {
            "Player", "Main Camera", "CharacterSwitcher", "EventSystem", "TestHud", "HUD_Canvas",
            "Map_Canvas", "MinimapCamera", "Scene_Transition_Canvas",
            "Elements_Stack_Manager", "Elements_Stack_Canvas", "AimCursor", "RunGold", "AttackAreaVisualizer",
        };

        [MenuItem("HJH/맵/플레이어/1 Start 씬에서 플레이어 세트 프리팹 만들기")]
        private static void BuildRig()
        {
            if (EditorApplication.isPlaying) return;
            if (!File.Exists(StartScene)) { Debug.LogError("[Rig] Start 씬을 못 찾았습니다: " + StartScene); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var start = EditorSceneManager.OpenScene(StartScene, OpenSceneMode.Additive);
            try
            {
                var rig = new GameObject("PlayerRig");
                SceneManager.MoveGameObjectToScene(rig, start);
                var included = new List<string>(); var missing = new List<string>();
                foreach (var name in Include)
                {
                    var go = start.GetRootGameObjects().FirstOrDefault(g => g.name == name);
                    if (go == null) { missing.Add(name); continue; }
                    go.transform.SetParent(rig.transform, true);
                    included.Add(name);
                }
                if (included.Count == 0) { Debug.LogError("[Rig] 넣을 오브젝트를 하나도 못 찾았습니다."); return; }

                var outside = new List<string>();
                foreach (var comp in rig.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null) continue;
                    var so = new SerializedObject(comp); var it = so.GetIterator();
                    while (it.NextVisible(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                        var o = it.objectReferenceValue; if (EditorUtility.IsPersistent(o)) continue;
                        var og = o as GameObject ?? (o as Component)?.gameObject; if (og == null) continue;
                        if (!og.transform.IsChildOf(rig.transform) && outside.Count < 40) outside.Add($"{comp.gameObject.name}/{comp.GetType().Name}.{it.propertyPath} → {og.name}");
                    }
                }

                MapLightTools.AddPlayerSpot(rig.transform);
                EnsureWaterEmitter(rig.transform);
                var prefab = PrefabUtility.SaveAsPrefabAsset(rig, RigPath);
                Debug.Log($"[Rig] PlayerRig 프리팹 생성: {RigPath}\n  포함 {included.Count}개: {string.Join(", ", included)}\n  Start 씬에 없어 빠진 것: {(missing.Count == 0 ? "없음" : string.Join(", ", missing))}\n  세트 밖 참조(끊김) {outside.Count}개{(outside.Count > 0 ? ":\n    " + string.Join("\n    ", outside) : "")}");
                if (prefab == null) Debug.LogError("[Rig] 프리팹 저장 실패");
            }
            finally
            {
                EditorSceneManager.CloseScene(start, true);
            }
        }

        internal static bool PlaceRigAt(Vector3 playerPos)
        {
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (rigPrefab == null) return false;
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "Main Camera" || go.name == "Player" || go.name == "EventSystem" || go.name == "PlayerRig") Object.DestroyImmediate(go);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, scene);
            var player = rig.transform.Find("Player");
            var delta = player != null ? playerPos - player.position : playerPos;
            rig.transform.position += delta;
            return true;
        }

        internal static bool EnsureWaterEmitter(Transform rigRoot)
        {
            Transform player = rigRoot.name == "Player" ? rigRoot : rigRoot.Find("Player");
            if (player == null) return false;
            if (player.GetComponent<Assets.Members.HJH.Environment.WaterRippleEmitter>() == null)
                player.gameObject.AddComponent<Assets.Members.HJH.Environment.WaterRippleEmitter>();
            return true;
        }

        [MenuItem("HJH/맵/플레이어/4 물결 발생기 달기 (PlayerRig 프리팹, 모든 맵에 적용)")]
        private static void AddWaterEmitterMenu()
        {
            if (EditorApplication.isPlaying) return;
            var root = PrefabUtility.LoadPrefabContents(RigPath);
            try
            {
                if (!EnsureWaterEmitter(root.transform)) { Debug.LogWarning("[Rig] PlayerRig 안에서 Player 를 못 찾았습니다."); return; }
                PrefabUtility.SaveAsPrefabAsset(root, RigPath);
                Debug.Log("[Rig] PlayerRig 의 Player 에 WaterRippleEmitter 를 달았습니다. 물속을 걸으면 물결이 생깁니다 (Submerge Tolerance 를 키우면 얕은 물에서도 반응).");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [MenuItem("HJH/맵/플레이어/3 지금 열린 씬에 플레이어 세트 적용 (선택한 오브젝트 자리 우선)")]
        private static void ApplyToActiveScene()
        {
            if (EditorApplication.isPlaying) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RigPath) == null) { Debug.LogWarning("[Rig] 먼저 1번으로 프리팹을 만드세요."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            Vector3 pos = FindStartPos(scene, out string how);

            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null && how != "옛 Player")
                pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y + 0.1f;

            bool ok = PlaceRigAt(pos);
            if (ok) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Rig] {scene.name}: {(ok ? "플레이어 세트 적용" : "실패")} — 위치 {pos} ({how}). 저장은 하지 않았습니다. 다른 자리에 두려면 PlayerRig 를 옮기거나, 원하는 자리의 오브젝트를 선택하고 다시 실행하세요. (옛 Main Camera / Player / EventSystem 은 치웠습니다)");
        }

        private static Vector3 FindStartPos(UnityEngine.SceneManagement.Scene scene, out string how)
        {
            Transform sel = Selection.activeTransform;
            if (sel != null && sel.gameObject.scene == scene && sel.root.name != "PlayerRig")
            {
                how = "선택한 오브젝트 " + sel.name;
                return sel.position;
            }
            var all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            var oldPlayer = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Player");
            if (oldPlayer != null) { how = "옛 Player"; return oldPlayer.transform.position; }
            var portal = all.FirstOrDefault(t => t.gameObject.scene == scene && t.name.ToLower().Contains("portal") && t.name.ToLower().Contains("start"));
            if (portal != null) { how = "Portal_Start"; return portal.position + new Vector3(1.5f, 0.1f, 1.5f); }
            var portals = all.FirstOrDefault(t => t.gameObject.scene == scene && t.name == "Portals");
            if (portals != null && portals.childCount > 0) { how = "Portals 첫 자식 " + portals.GetChild(0).name; return portals.GetChild(0).position + new Vector3(1.5f, 0.1f, 1.5f); }
            var view = SceneView.lastActiveSceneView;
            if (view != null) { how = "지금 씬 뷰가 보는 자리"; return view.pivot; }
            how = "기본값(0,0,0)";
            return Vector3.zero;
        }

        [MenuItem("HJH/맵/플레이어/2 맵 씬들에 플레이어 세트 적용 (L맵·다리 맵)")]
        private static void ApplyToMapScenes()
        {
            if (EditorApplication.isPlaying) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RigPath) == null) { Debug.LogWarning("[Rig] 먼저 1번으로 프리팹을 만드세요."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var log = new List<string>();
            foreach (var path in MapScenes)
            {
                if (!File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Vector3? pos = null;
                var oldPlayer = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Player");
                if (oldPlayer != null) pos = oldPlayer.transform.position;
                if (pos == null)
                {
                    var portal = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name.ToLower().Contains("portal") && t.name.ToLower().Contains("start"));
                    if (portal != null) pos = portal.position + new Vector3(1.5f, 0.1f, 1.5f);
                }
                if (pos == null)
                {
                    var cam = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Main Camera");
                    pos = cam != null ? new Vector3(cam.transform.position.x, 0.1f, cam.transform.position.z + 9f) : Vector3.zero;
                }
                bool ok = PlaceRigAt(pos.Value);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.Add($"{Path.GetFileNameWithoutExtension(path)}: {(ok ? "적용" : "실패")} (플레이어 위치 {pos.Value})");
            }
            Debug.Log("[Rig] 맵 씬 적용 결과\n  " + string.Join("\n  ", log) + "\n  (옛 Main Camera / Player / EventSystem 은 치웠고, 새 맵은 맵 만들기 메뉴가 자동으로 같은 세트를 놓습니다.)");
        }
    }
}
