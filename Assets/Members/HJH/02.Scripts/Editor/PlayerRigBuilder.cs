using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HJH.EditorTools
{
    // Start 씬의 플레이어 세트(플레이어 + 카메라 + HUD + 스위처 등)를 PlayerRig 프리팹으로 만들고, 맵 씬들에 적용한다. 메뉴: HJH/맵/플레이어/...
    //  - Start 씬은 읽기 전용으로 열어 복사만 하고 저장하지 않는다 (JJH 씬은 건드리지 않음).
    //  - 플레이어는 KYR 의 Player 프리팹 인스턴스라 오버라이드가 유지된다. 세트 안의 서로 참조는 그대로 이어진다.
    //  - 세트에서 빠진(밖의) 씬 오브젝트를 가리키던 참조는 콘솔에 목록으로 알려 준다.
    public static class PlayerRigBuilder
    {
        private const string StartScene = "Assets/Members/JJH/01_Scene/Map_Test/Start.unity";
        private const string RigPath = "Assets/Members/HJH/05.Data/Prefab/PlayerRig.prefab";
        private static readonly string[] MapScenes =
        {
            "Assets/Members/HJH/01.Scene/Map_L_Normal.unity",
            "Assets/Members/HJH/01.Scene/Map_Bridge_Normal.unity",
        };

        // 세트에 넣을 Start 씬 루트 오브젝트 이름 (맵 전용인 지형·프랍·스폰·포탈·NavMesh·PP·MapRenderSettings 는 뺀다)
        private static readonly string[] Include =
        {
            "Player", "Main Camera", "CharacterSwitcher", "EventSystem", "TestHud", "HUD_Canvas",   // HUD_Canvas = 스킬바(Q/E/R/RMB/대시 슬롯)·원소 콤보 HUD (RectTransform 루트라 처음에 빠뜨렸음)
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

                // 세트 밖 씬 오브젝트를 가리키는 참조 점검 (프리팹에는 저장되지 않아 끊김)
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

                MapLightTools.AddPlayerSpot(rig.transform);   // 플레이어 Spot 라이트 (다시 만들어도 유지)
                var prefab = PrefabUtility.SaveAsPrefabAsset(rig, RigPath);
                Debug.Log($"[Rig] PlayerRig 프리팹 생성: {RigPath}\n  포함 {included.Count}개: {string.Join(", ", included)}\n  Start 씬에 없어 빠진 것: {(missing.Count == 0 ? "없음" : string.Join(", ", missing))}\n  세트 밖 참조(끊김) {outside.Count}개{(outside.Count > 0 ? ":\n    " + string.Join("\n    ", outside) : "")}");
                if (prefab == null) Debug.LogError("[Rig] 프리팹 저장 실패");
            }
            finally
            {
                EditorSceneManager.CloseScene(start, true);   // 저장하지 않고 닫음 → Start 씬 파일은 그대로
            }
        }

        // ───────────────────────── 적용 ─────────────────────────
        // 활성 씬에서 옛 카메라·플레이어·EventSystem·이전 PlayerRig 를 치우고 PlayerRig 를 놓는다. playerPos 는 플레이어가 서야 할 월드 위치.
        internal static bool PlaceRigAt(Vector3 playerPos)
        {
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (rigPrefab == null) return false;
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "Main Camera" || go.name == "Player" || go.name == "EventSystem" || go.name == "PlayerRig") Object.DestroyImmediate(go);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, scene);
            // 세트 안 플레이어 위치가 playerPos 가 되도록 세트 전체를 이동 (카메라도 같이 따라옴)
            var player = rig.transform.Find("Player");
            var delta = player != null ? playerPos - player.position : playerPos;
            rig.transform.position += delta;
            return true;
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
                // 시작 위치: 옛 플레이어 → 시작 포탈 → 옛 카메라 앞
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
