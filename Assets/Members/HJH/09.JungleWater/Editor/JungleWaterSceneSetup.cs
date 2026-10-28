using System.Collections.Generic;
using System.Text;
using Members.KYR._01_Scripts;
using Members.KYR._01.Scripts.Player;
using Unity.AI.Navigation;
using Unity.AI.Navigation.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Assets.Members.HJH.Environment.EditorTools
{
    // 메뉴: HJH > Jungle Water > 열린 씬에 물 연결 + NavMesh 다시 굽기
    // 정글 물을 다른 씬(예: Shader__Skill)에 합친 뒤 한 번 누르면:
    //  1. 물 시스템이 'Water' 판 위치를 따라가게 연결 (옮겨도 물결이 맞는 자리에 생김)
    //  2. 플레이어·적(NavMeshAgent)에 물결 발생기 추가, 안개는 플레이어 주변에서 걷힘
    //  3. 스킬 물튀김 리스너 추가
    //  4. NavMesh: 겹친 Surface 정리 + 콜라이더 기준으로 다시 굽기 (물·안개 판은 콜라이더가 없어 제외됨)
    public static class JungleWaterSceneSetup
    {
        [MenuItem("HJH/Jungle Water/열린 씬에 물 연결 + NavMesh 다시 굽기")]
        public static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            var log = new StringBuilder($"[JungleWater] '{scene.name}' 정리 결과\n");

            var sim = Object.FindFirstObjectByType<WaterRippleSimulation>();
            if (sim == null)
            {
                Debug.LogError("[JungleWater] 씬에 WaterRippleSimulation(JungleWaterSystem)이 없습니다. JungleWaterTest 씬에서 물 관련 오브젝트를 먼저 복사해 오세요.");
                return;
            }

            ConnectWater(sim, log);
            DisableTestWalker(log);
            AddEmitters(sim, log);
            AddSkillListener(sim, log);
            FixNavMesh(log);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        private static void ConnectWater(WaterRippleSimulation sim, StringBuilder log)
        {
            GameObject water = GameObject.Find("Water");
            var so = new SerializedObject(sim);
            if (water != null && water.TryGetComponent(out Renderer renderer))
            {
                so.FindProperty("waterSurface").objectReferenceValue = renderer;
                Bounds b = renderer.bounds;
                log.AppendLine($" · 물 시스템 ← 'Water' 판 (높이 {b.center.y:0.##}, 영역 {b.min.x:0.#},{b.min.z:0.#} ~ {b.max.x:0.#},{b.max.z:0.#})");
            }
            else
            {
                log.AppendLine(" · ⚠ 'Water' 오브젝트를 못 찾음 → 물 높이/영역을 인스펙터에서 직접 맞춰 주세요");
            }

            var player = Object.FindFirstObjectByType<PlayerAgent>();
            if (player != null)
            {
                so.FindProperty("revealTarget").objectReferenceValue = player.transform;
                log.AppendLine($" · 안개 걷힘 대상 ← {player.name}");
            }

            so.ApplyModifiedProperties();
        }

        // 진짜 플레이어가 있으면 테스트 캡슐은 끈다 (같은 WASD/Space 를 써서 같이 움직임). 지우지는 않음
        private static void DisableTestWalker(StringBuilder log)
        {
            if (Object.FindFirstObjectByType<PlayerAgent>() == null)
                return;

            foreach (WaterTestWalker walker in Object.FindObjectsByType<WaterTestWalker>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(walker.gameObject, "Disable TestWalker");
                walker.gameObject.SetActive(false);
                log.AppendLine($" · 테스트 캡슐 끔: {walker.name} (플레이어와 같은 키를 써서)");
            }
        }

        private static void AddEmitters(WaterRippleSimulation sim, StringBuilder log)
        {
            var targets = new List<GameObject>();
            foreach (PlayerAgent p in Object.FindObjectsByType<PlayerAgent>(FindObjectsSortMode.None))
                targets.Add(p.gameObject);
            foreach (NavMeshAgent a in Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
                targets.Add(a.gameObject);

            int added = 0;
            foreach (GameObject go in targets)
            {
                if (go.GetComponent<WaterRippleEmitter>() != null)
                    continue;
                Undo.AddComponent<WaterRippleEmitter>(go);
                added++;
            }
            log.AppendLine($" · 물결 발생기 추가: {added}개 (플레이어 + 적, 이미 있던 건 그대로)");
        }

        private static void AddSkillListener(WaterRippleSimulation sim, StringBuilder log)
        {
            if (sim.GetComponent<WaterSkillSplashListener>() != null)
                return;
            Undo.AddComponent<WaterSkillSplashListener>(sim.gameObject);
            log.AppendLine(" · 스킬 물튀김 리스너 추가 (모든 스킬의 범위 공격·적중 위치에서 물이 튐)");
        }

        private static void FixNavMesh(StringBuilder log)
        {
            NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.InstanceID);
            NavMeshSurface main = null;
            foreach (NavMeshSurface s in surfaces)
            {
                if (s.agentTypeID != 0)
                    continue;   // 다른 에이전트 타입용은 건드리지 않음
                if (main == null)
                {
                    main = s;
                    continue;
                }
                // 같은 에이전트 타입으로 씬 전체를 굽는 Surface 가 둘이면 NavMesh 가 겹친다 → 끄기 (지우진 않음)
                Undo.RecordObject(s, "Disable duplicate NavMeshSurface");
                s.enabled = false;
                log.AppendLine($" · 겹치는 NavMeshSurface 끔: {s.name}");
            }

            if (main == null)
            {
                main = Undo.AddComponent<NavMeshSurface>(new GameObject("NavMesh"));
                log.AppendLine(" · NavMeshSurface 새로 만듦: 'NavMesh'");
            }

            Undo.RecordObject(main, "Configure NavMeshSurface");
            main.enabled = true;
            main.collectObjects = CollectObjects.All;
            // 렌더 메시 기준이면 물 판·안개 판(콜라이더 없음)까지 땅으로 굽힌다 → 콜라이더 기준
            main.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            main.layerMask = ~LayerMaskOf("Ignore Raycast", "Water", "UI", "Player", "Enemy", "Weakpoint");
            main.ignoreNavMeshAgent = true;
            main.ignoreNavMeshObstacle = true;
            EditorUtility.SetDirty(main);

            NavMeshAssetManager.instance.StartBakingSurfaces(new Object[] { main });
            log.AppendLine($" · NavMesh 다시 굽는 중: {main.name} (콜라이더 기준, 플레이어·적·물 레이어 제외)");
        }

        private static int LayerMaskOf(params string[] names)
        {
            int mask = 0;
            foreach (string n in names)
            {
                int layer = LayerMask.NameToLayer(n);
                if (layer >= 0)
                    mask |= 1 << layer;
            }
            return mask;
        }
    }
}
