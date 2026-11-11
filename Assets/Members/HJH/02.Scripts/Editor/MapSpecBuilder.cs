using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HJH.EditorTools
{
    // 맵 스펙(윤곽선·물·다리·길·진흙 띠·시작/끝)만 정의하면 씬 전체를 만든다. 메뉴: HJH/맵/새 맵/...
    // 파이프라인: Map_L_Normal 씬을 복사(카메라·태양·저녁 Volume 유지) → 지형/물/안개 → 보이지 않는 벽 → 다리 → 숲 → 바위·꽃·뿌리·물가 → 반딧불이 → 포탈·스폰 → 라이트 프로브.
    // 스케치 좌표는 픽셀이고 PxScale(0.2m/px)로 미터로 바꾼다. 다른 맵은 Spec 하나만 더 추가하면 된다.
    public static class MapSpecBuilder
    {
        private const float PxScale = 0.2f, PxH = 570f;
        private const string SceneDir = "Assets/Members/HJH/01.Scene/";
        private const string SourceScene = SceneDir + "Map_L_Normal.unity";
        private const string DataDir = "Assets/Members/HJH/05.Data/Map/";
        private const string PropDir = "Assets/Members/HJH/05.Data/MapProps/Prefabs/";
        private const string FxDir = "Assets/Members/HJH/05.Data/Fx/Firefly/";
        private const string LayerDir = "Assets/Members/HJH/09.JungleWater/Generated/Terrain/";
        private const string TreePrefab = "Assets/Members/KM/Assets/Props/Natural/Rock/Plane.002 (1) 1.prefab";
        private const string PlayerPrefab = "Assets/Members/HJH/05.Data/Prefab/Player.prefab";

        private class Spec
        {
            public string sceneName;
            public List<Vector2[]> blobs = new List<Vector2[]>();
            public Vector2[] water;
            public Vector2 bridgeA, bridgeB;
            public List<Vector2[]> routes = new List<Vector2[]>();   // 시작→다리 앞, 다리 뒤→끝
            public List<Vector2[]> mud = new List<Vector2[]>();
            public Vector2 start, end;
            public float waterY = -0.12f;
            // ── 아래는 새 맵용 선택 항목 (다리 맵은 기본값 그대로) ──
            public bool bridge = true;                 // false 면 다리·다리 입구 등불 없음
            public int[] swamp = { 1 };                // 늪지대로 꾸밀 구역(blobs 번호)
            public int surround;                       // 0 언덕+숲, 1 주변이 가라앉은 절벽, 2 물에 잠긴 맹그로브 숲
            public Vector2[] fall;                     // 폭포가 떨어지는 가장자리 선 (절벽 맵)
            public Vector2 clear; public float clearR; // 아무것도 놓지 않을 자리 (상점 오브젝트 자리)
            public bool spawns = true;                 // 적 스폰 표시를 만들지
            public bool keepBackup;                    // true 면 덮어쓰기 창 대신 기존 씬을 _RemovedBackup 에 복사하고 진행
            public float pitDepth = 11f;
        }

        private static Vector2[] M(params float[] xz)   // 미터 좌표 그대로 (x 동쪽, z 북쪽)
        {
            var r = new Vector2[xz.Length / 2];
            for (int i = 0; i < r.Length; i++) r[i] = new Vector2(xz[i * 2], xz[i * 2 + 1]);
            return r;
        }

        // 일반 맵 (스케치 1·4): 왼쪽 아래 시작 → 위로 솟은 몸통 → 오른쪽 끝. 전체가 늪지대, 사방이 가라앉은 절벽, 오목한 곳에서 폭포가 떨어진다.
        private static Spec CliffSwamp()
        {
            var s = new Spec { sceneName = "_HJH_Map", bridge = false, swamp = new[] { 0 }, surround = 1, keepBackup = true };
            s.blobs.Add(M(8, 10, 14, 5, 26, 3, 40, 4, 46, 8, 48, 16, 48, 26, 50, 34, 56, 38, 66, 38, 72, 34, 74, 26, 80, 22, 90, 24, 96, 32, 96, 44, 90, 52,
                          80, 56, 70, 60, 58, 66, 44, 68, 32, 66, 24, 60, 20, 50, 20, 40, 18, 30, 10, 26, 5, 18));
            s.fall = M(56, 38, 66, 38);
            s.routes.Add(M(16, 14, 30, 14, 36, 26, 34, 44, 44, 56, 60, 54, 74, 46, 82, 40, 88, 36));
            s.mud.Add(M(12, 20, 28, 22, 30, 40, 40, 60));
            s.mud.Add(M(56, 60, 70, 52, 86, 46, 90, 30));
            s.start = new Vector2(16, 14); s.end = new Vector2(88, 36);
            return s;
        }

        // 상점 맵 (스케치 3·5): 작은 콩 모양. 전체가 늪지대, 둘레는 물에 잠긴 맹그로브 숲. 오른쪽에 상점 오브젝트 자리를 비워 둔다.
        private static Spec Shop()
        {
            var s = new Spec { sceneName = "Map_Shop", bridge = false, swamp = new[] { 0 }, surround = 2, spawns = false, keepBackup = true };
            s.blobs.Add(M(22, 6, 30, 3, 40, 4, 48, 9, 54, 17, 56, 26, 54, 34, 48, 40, 40, 44, 30, 46, 20, 46, 12, 43, 8, 37, 9, 31, 14, 27, 20, 23, 22, 17, 20, 11));
            s.routes.Add(M(30, 9, 34, 20, 30, 32, 16, 39));
            s.mud.Add(M(26, 10, 30, 24, 22, 36));
            s.start = new Vector2(30, 9); s.end = new Vector2(16, 39);
            s.clear = new Vector2(45, 26); s.clearR = 6.5f;
            return s;
        }

        [MenuItem("HJH/맵/새 맵/절벽 늪지대 맵 만들기 (_HJH_Map, 폭포)")]
        private static void BuildCliff() => Build(CliffSwamp());

        [MenuItem("HJH/맵/새 맵/상점 맵 만들기 (Map_Shop, 맹그로브 숲)")]
        private static void BuildShop() => Build(Shop());

        // 방금 만든 맵에 기존 꾸미기 도구들을 순서대로 돌리고 저장한다 (다리 맵을 꾸민 것과 같은 도구).
        [MenuItem("HJH/맵/새 맵/지금 맵 한 번에 꾸미기 (늪지대→버섯→조명→꽃→폭포→저장)")]
        private static void DecorateAll()
        {
            if (EditorApplication.isPlaying) return;
            if (!UseSpecFor(EditorSceneManager.GetActiveScene().name)) { Debug.LogWarning("[SpecBuild] 맵 스펙이 있는 씬에서만 됩니다."); return; }
            bool hasFall = S.fall != null;
            var steps = new List<string>
            {
                "HJH/맵/늪지대/다리 건너편 늪지대 꾸미기 (맹그로브)",
                "HJH/맵/늪지대/통나무 + 야광 버섯 놓기 (늪지대)",
                "HJH/맵/조명/발광 꽃·버섯에 빛 달기 (주변 돌·땅이 빛 받게)",
                "HJH/맵/정리/빈 바닥에 발광 꽃 무리 놓기 (플레이 구역)",
            };
            if (hasFall) steps.Add("HJH/맵/폭포/'폭포' 오브젝트를 폭포로 만들기 (Cube 면 + 흐르는 UV + 파티클)");
            foreach (string m in steps)
                if (!EditorApplication.ExecuteMenuItem(m)) Debug.LogWarning("[SpecBuild] 메뉴 실행 실패: " + m);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[SpecBuild] 꾸미기 끝, 씬 저장함.");
        }

        // 씬 이름으로 스펙을 고른다 (꾸미기 도구들이 구역·길을 물어볼 때). 모르는 씬이면 false.
        internal static bool UseSpecFor(string sceneName)
        {
            switch (sceneName)
            {
                case "Map_Bridge_Normal": S = BridgeNormal(); return true;
                case "_HJH_Map": S = CliffSwamp(); return true;
                case "Map_Shop": S = Shop(); return true;
                default: return false;
            }
        }
        internal static bool InClear(Vector2 p, float extra = 0f) => S.clearR > 0f && Vector2.Distance(p, S.clear) < S.clearR + extra;
        internal static Vector2[] SpecFall => S.fall;

        private static Vector2 Px(float x, float y) => new Vector2(x * PxScale, (PxH - y) * PxScale);
        private static Vector2[] Px(params float[] xy)
        {
            var r = new Vector2[xy.Length / 2];
            for (int i = 0; i < r.Length; i++) r[i] = Px(xy[i * 2], xy[i * 2 + 1]);
            return r;
        }

        // 일반 맵 2: 두 구역이 얕은 물로 갈라지고 다리로 건넌다 (스케치 기준)
        private static Spec BridgeNormal()
        {
            var s = new Spec { sceneName = "Map_Bridge_Normal" };
            s.blobs.Add(Px(100, 262, 150, 236, 205, 232, 250, 262, 298, 290, 335, 322, 346, 362, 336, 402, 320, 442, 304, 472, 280, 496, 240, 508, 190, 510, 140, 500, 102, 478, 85, 440, 80, 382, 84, 322));
            s.blobs.Add(Px(300, 72, 338, 42, 398, 32, 450, 48, 488, 88, 500, 158, 492, 228, 470, 274, 430, 294, 386, 290, 344, 272, 305, 242, 292, 192, 292, 128));
            s.water = Px(196, 196, 205, 150, 250, 124, 290, 124, 296, 196, 310, 236, 352, 262, 430, 296, 446, 322, 420, 358, 382, 386, 346, 392, 336, 330, 300, 300, 250, 268, 212, 240);
            s.bridgeA = Px(252, 282); s.bridgeB = Px(352, 262);
            s.routes.Add(Px(118, 462, 165, 440, 230, 430, 282, 402, 282, 355, 250, 318, 252, 282));
            s.routes.Add(Px(352, 262, 400, 246, 442, 200, 452, 140, 420, 98, 330, 86));
            s.mud.Add(Px(135, 262, 180, 252, 212, 305, 262, 372, 322, 398));
            s.mud.Add(Px(300, 128, 348, 158, 390, 214, 430, 262));
            s.start = Px(120, 462); s.end = Px(318, 92);
            return s;
        }

        [MenuItem("HJH/맵/새 맵/일반2 다리 맵 만들기 (스케치)")]
        private static void BuildBridge() => Build(BridgeNormal());

        // ───────────────────────── 기하 ─────────────────────────
        private static Spec S;
        internal static void UseBridgeSpec() { S = BridgeNormal(); }          // 콜라이더 정리 등 다른 도구가 플레이 영역을 물어볼 때
        internal static float PlayDist(Vector2 p) => DistPlay(p);
        // 다리 건너편(끝) 구역 질의 (늪지대 도구용)
        internal static bool InEnd(Vector2 p) { foreach (int i in S.swamp) if (InPoly(S.blobs[i], p)) return true; return false; }
        internal static float EndDepth(Vector2 p) { foreach (int i in S.swamp) if (InPoly(S.blobs[i], p)) return DistPoly(S.blobs[i], p); return 0f; }
        internal static float RouteDist(Vector2 p) => DistRoute(p);
        internal static float SpecWaterY => S.waterY;
        // 암벽 쌓기 도구용: 시작 구역(blobs[0]) 윤곽과 물·다리 질의
        internal static Vector2[] SpecBlob(int i) => S.blobs[i];
        internal static bool SpecInBlob(int i, Vector2 p) => InPoly(S.blobs[i], p);
        internal static bool SpecInWater(Vector2 p) => InWater(p);
        internal static Vector2[] SpecWater() => S.water;
        internal static float SpecBridgeDist(Vector2 p) => S.bridge ? Seg(p, S.bridgeA, S.bridgeB) : 999f;
        private static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - a) / (b - a)));

        private static bool InPoly(Vector2[] poly, Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
            return c;
        }
        private static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }
        private static float DistPoly(Vector2[] poly, Vector2 p, bool closed = true)
        {
            float best = float.MaxValue; int n = closed ? poly.Length : poly.Length - 1;
            for (int i = 0; i < n; i++) best = Mathf.Min(best, Seg(p, poly[i], poly[(i + 1) % poly.Length]));
            return best;
        }
        private static bool InBlob(Vector2 p) { foreach (var b in S.blobs) if (InPoly(b, p)) return true; return false; }
        private static bool InPlay(Vector2 p) => InBlob(p) || (S.bridge && (Vector2.Distance(p, S.bridgeA) < 3f || Vector2.Distance(p, S.bridgeB) < 3f));
        private static float DistBlobEdge(Vector2 p) { float d = float.MaxValue; foreach (var b in S.blobs) d = Mathf.Min(d, DistPoly(b, p)); return d; }
        private static float DistPlay(Vector2 p)   // 구역 밖으로의 거리 (안이면 0)
        {
            if (InPlay(p)) return 0f;
            float d = DistBlobEdge(p);
            if (S.bridge) { d = Mathf.Min(d, Vector2.Distance(p, S.bridgeA) - 3f); d = Mathf.Min(d, Vector2.Distance(p, S.bridgeB) - 3f); }
            return Mathf.Max(0f, d);
        }
        private static float Depth(Vector2 p) => InBlob(p) ? DistBlobEdge(p) : 0f;   // 구역 안쪽 깊이
        private static bool InWater(Vector2 p) => S.water != null && InPoly(S.water, p);
        private static float DistWater(Vector2 p) => S.water != null ? DistPoly(S.water, p) : 999f;
        private static float DistRoute(Vector2 p) { float d = float.MaxValue; foreach (var r in S.routes) d = Mathf.Min(d, DistPoly(r, p, false)); if (S.bridge) d = Mathf.Min(d, Seg(p, S.bridgeA, S.bridgeB)); return d; }
        private static float DistMud(Vector2 p) { float d = float.MaxValue; foreach (var r in S.mud) d = Mathf.Min(d, DistPoly(r, p, false)); return d; }
        private static Vector2 NearestEdge(Vector2 p)
        {
            Vector2 best = p; float bd = float.MaxValue;
            foreach (var poly in S.blobs)
                for (int i = 0; i < poly.Length; i++)
                {
                    var a = poly[i]; var b = poly[(i + 1) % poly.Length]; var ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f)); var c = a + ab * t;
                    float d = Vector2.Distance(p, c); if (d < bd) { bd = d; best = c; }
                }
            return best;
        }

        private static float Hill(Vector2 p, float dOut)
        {
            float n = Mathf.PerlinNoise(p.x * 0.08f + 3f, p.y * 0.08f + 7f) * 2.2f + Mathf.PerlinNoise(p.x * 0.21f, p.y * 0.21f) * 0.8f;
            return Smooth(0.6f, 5.5f, dOut) * 5.8f + n * Smooth(2f, 8f, dOut);
        }

        private static float HeightAt(Vector2 p)
        {
            if (InPlay(p))
            {
                float flat = (Mathf.PerlinNoise(p.x * 0.15f, p.y * 0.15f) - 0.5f) * 0.10f;
                if (S.fall != null) { float df = DistPoly(S.fall, p, false); if (df < 4.5f) return Mathf.Lerp(S.waterY - 0.3f, flat, Smooth(1.5f, 4.5f, df)); }   // 폭포로 흘러드는 얕은 물길
                return flat;
            }
            if (S.surround == 1)   // 사방이 가라앉은 절벽: 가장자리에서 2m 만에 뚝 떨어지고 바닥은 울퉁불퉁
            {
                float dc = DistPlay(p);
                float n1 = Mathf.PerlinNoise(p.x * 0.06f + 3f, p.y * 0.06f + 7f) * 2.5f + Mathf.PerlinNoise(p.x * 0.2f, p.y * 0.2f) * 0.8f;
                return -Smooth(0.3f, 2.4f, dc) * S.pitDepth + n1 * Smooth(3f, 12f, dc);
            }
            if (S.surround == 2)   // 물에 잠긴 맹그로브 숲: 16m 까지 얕은 물 바닥, 그 뒤로 언덕
            {
                float dm = DistPlay(p);
                float bottom = S.waterY - 0.3f - 0.35f * Smooth(0f, 4f, dm) + (Mathf.PerlinNoise(p.x * 0.7f, p.y * 0.7f) - 0.5f) * 0.08f;
                return Mathf.Lerp(bottom, Hill(p, Mathf.Max(0f, dm - 16f)), Smooth(16f, 20f, dm));
            }
            float dOut = DistPlay(p), hill = Hill(p, dOut);
            if (InWater(p))
            {
                float dW = DistWater(p);
                float noise = (Mathf.PerlinNoise(p.x * 0.7f, p.y * 0.7f) - 0.5f) * 0.06f;
                float bottom = S.waterY - 0.14f - 0.40f * Smooth(0f, 4f, dW) + noise;
                float shore = Mathf.Lerp(0f, bottom, Smooth(0f, 2.4f, dOut));
                return Mathf.Lerp(hill, shore, Smooth(0f, 2.5f, dW));
            }
            return hill;
        }

        // ───────────────────────── 빌드 ─────────────────────────
        private static void Build(Spec spec)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[SpecBuild] 플레이 중에는 못 합니다."); return; }
            if (!File.Exists(SourceScene)) { Debug.LogError("[SpecBuild] 원본 씬(Map_L_Normal)이 없습니다."); return; }
            string path = SceneDir + spec.sceneName + ".unity";
            if (File.Exists(path) && spec.keepBackup)
            {
                string bdir = "_RemovedBackup/" + System.DateTime.Now.ToString("yyyy-MM-dd") + "/" + SceneDir;
                Directory.CreateDirectory(bdir);
                File.Copy(path, bdir + spec.sceneName + System.DateTime.Now.ToString("_HHmmss") + ".unity", true);
                Debug.Log("[SpecBuild] 기존 씬 백업: " + bdir);
            }
            else if (File.Exists(path) && !EditorUtility.DisplayDialog("씬 덮어쓰기", $"{spec.sceneName} 이 이미 있습니다. 새로 만들면 직접 손본 내용이 사라집니다. 계속할까요?", "덮어쓰기", "취소")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            S = spec;
            if (File.Exists(path)) { File.Copy(SourceScene, path, true); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }   // .meta(GUID) 는 그대로 두고 내용만 교체
            else AssetDatabase.CopyAsset(SourceScene, path);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Lightmapping.lightingDataAsset = null;   // 원본의 베이크 데이터는 이 맵과 맞지 않음

            // 원본 내용 정리: 태양·카메라·Volume 만 남김
            var root = GameObject.Find("Map_L_Normal");
            if (root == null) root = new GameObject();
            root.name = spec.sceneName;
            foreach (Transform c in new List<Transform>(root.GetComponentsInChildren<Transform>(true)))
            {
                if (c == null || c == root.transform || c.parent != root.transform) continue;
                if (c.name == "Directional Light") continue;
                Object.DestroyImmediate(c.gameObject);
            }
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "Chip3" || go.name == "LightProbes_Map") Object.DestroyImmediate(go);

            var props = new GameObject("Props").transform; props.SetParent(root.transform, false);
            var rng = new System.Random(20261110);
            float R() => (float)rng.NextDouble();
            float L(float lo, float hi) => Mathf.Lerp(lo, hi, R());

            // 범위
            Vector2 mn = new Vector2(1e9f, 1e9f), mx = new Vector2(-1e9f, -1e9f);
            foreach (var b in spec.blobs) foreach (var p in b) { mn = Vector2.Min(mn, p); mx = Vector2.Max(mx, p); }
            if (spec.water != null) foreach (var p in spec.water) { mn = Vector2.Min(mn, p); mx = Vector2.Max(mx, p); }
            var center = (mn + mx) * 0.5f;
            const float TSize = 160f, THeight = 16f; const int HRes = 513, ARes = 512;
            float BaseY = spec.surround == 1 ? -(spec.pitDepth + 3f) : -2f;   // 절벽 맵은 아래로 파야 해서 바닥 기준을 내림
            var origin = new Vector2(Mathf.Round(center.x - TSize / 2f), Mathf.Round(center.y - TSize / 2f));

            Terrain terrain = BuildTerrain(root.transform, origin, TSize, THeight, BaseY, HRes, ARes, spec);
            float Ground(Vector2 p) => terrain.SampleHeight(new Vector3(p.x, 0, p.y)) + terrain.transform.position.y;

            BuildWaterAndFog(root.transform, mn, mx, center, spec);
            BuildWalls(root.transform, spec);
            if (spec.bridge) BuildBridge(root.transform, spec);
            BuildFall(root.transform, spec);

            var prefabTree = Load(TreePrefab);
            var rockA = Load(PropDir + "Rock_A.prefab"); var rockB = Load(PropDir + "Rock_B.prefab");
            var roots = Load(PropDir + "Roots_A.prefab"); var flower = Load(PropDir + "GlowFlowers.prefab");
            var lantern = Load(PropDir + "Lantern.prefab");
            var massPrefabs = new List<GameObject>();
            foreach (var n in new[] { "RockMass_A", "RockMass_B", "RockMass_C" }) { var m = Load(PropDir + n + ".prefab"); if (m != null) massPrefabs.Add(m); }
            var fireflies = Load(FxDir + "FireflyZone.prefab");

            GameObject Put(GameObject prefab, Transform parent, Vector2 p, float sink, Vector3 scale, float tilt, bool colliders = false, bool bySize = false)
            {
                var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                g.transform.position = new Vector3(p.x, Ground(p) - sink, p.y);
                g.transform.rotation = Quaternion.Euler(L(-tilt, tilt), R() * 360f, L(-tilt, tilt));
                g.transform.localScale = scale;
                if (bySize) { var mr = g.GetComponentInChildren<MeshRenderer>(); float ext = mr != null ? Mathf.Max(mr.bounds.size.x, mr.bounds.size.z) : 0f; foreach (var c in g.GetComponentsInChildren<Collider>()) c.enabled = ext >= 0.8f; }   // 조약돌(0.8m 미만)만 콜라이더 끔
                else if (!colliders) foreach (var c in g.GetComponentsInChildren<Collider>()) c.enabled = false;
                return g;
            }

            // 숲: 구역 밖 1.2~16m 띠에 지터 격자로 (콜라이더 없음, 먼 것은 그림자 끔)
            int trees = 0;
            if (spec.surround != 0) trees = BuildSurround(props, spec, mn, mx, Ground, rng);
            else if (prefabTree != null)
            {
                var forest = new GameObject("Forest").transform; forest.SetParent(props, false);
                for (float x = mn.x - 24f; x < mx.x + 24f; x += 3.2f)
                    for (float z = mn.y - 24f; z < mx.y + 24f; z += 3.2f)
                    {
                        var p = new Vector2(x + R() * 3.2f, z + R() * 3.2f);
                        if (InPlay(p) || InWater(p)) continue;
                        float d = DistPlay(p);
                        if (d < 1.2f || d > 16f) continue;
                        if (Ground(p) < spec.waterY + 0.1f) continue;
                        var g = Put(prefabTree, forest, p, 0.05f, Vector3.one * L(0.9f, 1.4f), 0f);
                        if (d > 9f) foreach (var r in g.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                        trees++;
                    }
            }

            // 포탈·스폰
            BuildPortals(root.transform, spec, lantern, Ground);
            var avoid = new List<Vector2> { spec.start, spec.end };
            if (spec.spawns) BuildSpawns(root.transform, spec, Ground, avoid);

            // 큰 바위(암반 덩어리): 구역 가장자리 안쪽 2~6.5m, 길에서 5m 떨어져서
            int masses = 0;
            var massPts = new List<Vector2>();
            {
                var g = new GameObject("RockMass_Auto").transform; g.SetParent(props, false);
                for (int t = 0; t < 3000 && masses < (spec.clearR > 0f ? 3 : 12); t++)
                {
                    var p = new Vector2(L(mn.x, mx.x), L(mn.y, mx.y));
                    if (!InBlob(p)) continue; float d = Depth(p);
                    if (d < 2f || d > 6.5f || DistRoute(p) < 5f || Blocked(p, 6f)) continue;
                    bool bad = false; foreach (var a in avoid) if (Vector2.Distance(a, p) < 7f) { bad = true; break; }
                    foreach (var q in massPts) if (Vector2.Distance(q, p) < 12f) { bad = true; break; }
                    if (bad) continue;
                    GameObject mp = massPrefabs.Count > 0 ? massPrefabs[rng.Next(massPrefabs.Count)] : rockB;
                    if (mp == null) break;
                    var go = Put(mp, g, p, 0f, Vector3.one * (massPrefabs.Count > 0 ? L(0.85f, 1.25f) : L(2.5f, 4f)), 0f, true);
                    massPts.Add(p); masses++;
                }
            }

            // 돌무리·꽃·뿌리·물가 (전부 콜라이더 없음)
            int rocks = 0, chips = 0, flowers = 0, rootsN = 0, edge = 0;
            var deco = new GameObject("Deco").transform; deco.SetParent(props, false);
            for (int c = 0, t = 0; c < 26 && t < 1500; t++)
            {
                var p = new Vector2(L(mn.x, mx.x), L(mn.y, mx.y));
                if (!InBlob(p) || Depth(p) < 1.2f || DistRoute(p) < 2.2f || rockA == null || Blocked(p, 1f)) continue;
                float s = L(0.45f, 0.9f);
                Put(R() < 0.5f ? rockA : (rockB ?? rockA), deco, p, L(0.08f, 0.22f), new Vector3(s * L(0.9f, 1.3f), s * L(0.6f, 0.95f), s * L(0.9f, 1.3f)), 8f, bySize: true); rocks++;
                int k = 2 + rng.Next(0, 3);
                for (int i = 0; i < k; i++)
                {
                    float a = R() * 6.28f, dd = L(0.6f, 1.5f); var q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dd;
                    if (!InBlob(q) || DistRoute(q) < 1.2f) continue;
                    float cs = L(0.12f, 0.3f); Put(rockA, deco, q, 0.03f, new Vector3(cs * L(0.9f, 1.4f), cs * L(0.6f, 1f), cs * L(0.9f, 1.4f)), 18f, bySize: true); chips++;
                }
                c++;
            }
            for (int c = 0, t = 0; c < 18 && t < 1200; t++)
            {
                var p = new Vector2(L(mn.x, mx.x), L(mn.y, mx.y));
                if (!InBlob(p) || Depth(p) < 1f || DistRoute(p) < 2f || flower == null || Blocked(p, 1f)) continue;
                int k = 3 + rng.Next(0, 3);
                for (int i = 0; i < k; i++)
                {
                    var q = p + new Vector2(L(-0.8f, 0.8f), L(-0.8f, 0.8f)); if (!InBlob(q)) continue;
                    float s = L(0.28f, 0.5f); Put(flower, deco, q, 0.03f, new Vector3(s, s * L(0.9f, 1.2f), s), 6f); flowers++;
                }
                c++;
            }
            for (int c = 0, t = 0; c < 10 && t < 800; t++)
            {
                var p = new Vector2(L(mn.x, mx.x), L(mn.y, mx.y));
                if (!InBlob(p) || Depth(p) < 1f || Depth(p) > 4f || DistRoute(p) < 2.6f || roots == null || Blocked(p, 1f)) continue;
                float s = L(0.35f, 0.6f); Put(roots, deco, p, 0.05f, new Vector3(s, s * L(0.8f, 1.1f), s), 5f); rootsN++; c++;
            }
            // 물가: 물 윤곽을 따라 구역 쪽에 돌·꽃·뿌리
            for (int i = 0; S.water != null && i < S.water.Length; i++)
            {
                var a = S.water[i]; var b = S.water[(i + 1) % S.water.Length]; float len = Vector2.Distance(a, b);
                var dir = (b - a) / Mathf.Max(len, 0.01f); var nrm = new Vector2(-dir.y, dir.x);
                for (float s = 0f; s < len; s += L(1.8f, 3.0f))
                {
                    var bp = a + dir * s;
                    var inward = InPlay(bp + nrm * 1.5f) ? nrm : (InPlay(bp - nrm * 1.5f) ? -nrm : Vector2.zero);
                    if (inward == Vector2.zero) continue;
                    var p = bp + inward * L(0.4f, 1.6f);
                    if (!InPlay(p) || Seg(p, S.bridgeA, S.bridgeB) < 3.5f) continue;
                    float roll = R();
                    if (roll < 0.5f && rockA != null) { float sc = L(0.2f, 0.55f); Put(rockA, deco, p, L(0.05f, 0.15f), new Vector3(sc * L(0.9f, 1.4f), sc * L(0.6f, 0.95f), sc * L(0.9f, 1.4f)), 10f, bySize: true); edge++; }
                    else if (roll < 0.8f && flower != null) { float sc = L(0.3f, 0.5f); Put(flower, deco, p, 0.03f, new Vector3(sc, sc, sc), 6f); edge++; }
                    else if (roots != null) { float sc = L(0.3f, 0.5f); Put(roots, deco, p, 0.05f, new Vector3(sc, sc, sc), 5f); edge++; }
                }
            }

            // 반딧불이: 구역 가장자리 나무 쪽, 라이트는 가장자리로 비껴
            int ff = 0;
            if (fireflies != null)
            {
                var g = new GameObject("Fireflies").transform; g.SetParent(props, false);
                var placed = new List<Vector2>();
                for (int t = 0; t < 4000 && placed.Count < 18; t++)
                {
                    var p = new Vector2(L(mn.x, mx.x), L(mn.y, mx.y));
                    if (!InBlob(p)) continue; float d = Depth(p);
                    if (d < 2f || d > 5.5f || Blocked(p, 2f)) continue;
                    bool bad = false; foreach (var q in placed) if (Vector2.Distance(q, p) < 9f) { bad = true; break; }
                    if (bad) continue;
                    var dir = (NearestEdge(p) - p).normalized;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(fireflies, g);
                    go.transform.position = new Vector3(p.x, Ground(p), p.y);
                    var li = go.GetComponentInChildren<Light>();
                    if (li != null) li.transform.position = new Vector3(p.x + dir.x * 1.6f, Ground(p) + 2.3f, p.y + dir.y * 1.6f);
                    placed.Add(p); ff++;
                }
            }

            // 라이트 프로브
            {
                var pg = new GameObject("LightProbes_Map"); var grp = pg.AddComponent<LightProbeGroup>();
                var pts = new List<Vector3>();
                for (float x = mn.x; x <= mx.x; x += 5f)
                    for (float z = mn.y; z <= mx.y; z += 5f)
                    {
                        var p = new Vector2(x, z); if (!InPlay(p)) continue;
                        float gy = Ground(p); pts.Add(new Vector3(x, gy + 1f, z)); pts.Add(new Vector3(x, gy + 3f, z));
                    }
                grp.probePositions = pts.ToArray();
            }
            if (spec.surround == 1) BuildRim(props, spec, rng);
            BuildClearMarker(root.transform, spec, Ground);
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(DataDir + "MapL_Lighting.lighting");
            if (ls != null) Lightmapping.lightingSettings = ls;

            // 플레이어 세트(Start 씬에서 만든 PlayerRig 프리팹)를 시작 지점에 배치. 프리팹이 없으면 기존 카메라 위치만 옮김.
            var startPos = new Vector3(spec.start.x + 1.5f, Ground(spec.start) + 0.1f, spec.start.y + 1.5f);
            if (!PlayerRigBuilder.PlaceRigAt(startPos))
            {
                var cam = GameObject.Find("Main Camera");
                if (cam != null) { cam.transform.position = new Vector3(spec.start.x, Ground(spec.start) + 16f, spec.start.y - 9f); cam.transform.rotation = Quaternion.Euler(60f, 0f, 0f); }
                if (GameObject.Find("Player") == null)
                {
                    var pp = Load(PlayerPrefab);
                    if (pp != null) { var pl = (GameObject)PrefabUtility.InstantiatePrefab(pp); pl.transform.position = startPos; }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SpecBuild] {spec.sceneName} 생성: 나무 {trees}, 큰 바위 {masses}, 돌무리 {rocks}(+작은 돌 {chips}), 꽃 {flowers}, 뿌리 {rootsN}, 물가 {edge}, 반딧불이 {ff}곳. 이 씬에서는 기존 'L맵 전용' 메뉴(가장자리/다듬기)를 쓰지 마세요. 다시 만들려면 같은 메뉴(덮어쓰기).");
        }

        // ───────────────────────── 새 맵용: 비울 자리 · 둘레 · 절벽 · 폭포 자리 ─────────────────────────
        private const string JwProps = "Assets/Members/HJH/09.JungleWater/Generated/Prefabs/Props/";
        private const string StoneDir = "Assets/Members/HJH/05.Data/MapProps/Stones/Prefabs";

        // 아무것도 놓으면 안 되는 곳: 상점 자리, 폭포로 흘러드는 물길
        private static bool Blocked(Vector2 p, float margin) => InClear(p, margin) || (S.fall != null && DistPoly(S.fall, p, false) < 5f + margin);
        internal static bool SpecBlocked(Vector2 p, float margin) => Blocked(p, margin);

        private static float RenderHeight(GameObject g, out float width)
        {
            width = 0f;
            var rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return 0f;
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            width = Mathf.Max(b.size.x, b.size.z);
            return b.size.y;
        }

        // 프리팹 원래 크기와 상관없이 원하는 높이(m)로 맞춰 놓는다. 콜라이더는 끈다 (구역 밖 장식).
        private static GameObject PutSized(GameObject prefab, Transform parent, Vector3 pos, float height, float yaw, bool shadows)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            g.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            float h = RenderHeight(g, out _);
            if (h > 0.01f) g.transform.localScale *= height / h;
            foreach (var c in g.GetComponentsInChildren<Collider>()) c.enabled = false;
            if (!shadows) foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
            return g;
        }

        // 구역 둘레: surround 1 = 가라앉은 바닥의 어두운 숲, surround 2 = 물에 선 맹그로브 나무 + 발치의 뿌리
        private static int BuildSurround(Transform props, Spec spec, Vector2 mn, Vector2 mx, System.Func<Vector2, float> ground, System.Random rng)
        {
            float R() => (float)rng.NextDouble();
            float L(float lo, float hi) => Mathf.Lerp(lo, hi, R());
            var trees = new List<GameObject>();
            for (int i = 0; i < 3; i++) { var t = Load(JwProps + $"PF_JungleTree_{i}.prefab"); if (t != null) trees.Add(t); }
            if (trees.Count == 0) { Debug.LogWarning("[SpecBuild] PF_JungleTree_0~2 프리팹이 없어 둘레 숲을 건너뜁니다."); return 0; }
            var forest = new GameObject(spec.surround == 1 ? "Forest_Pit" : "Forest_Mangrove").transform; forest.SetParent(props, false);
            int n = 0;
            bool pit = spec.surround == 1;
            float cell = pit ? 4.2f : 2.4f, far = pit ? 34f : 15f, near = pit ? 4f : 0.9f;
            for (float x = mn.x - far - 4f; x < mx.x + far + 4f; x += cell)
                for (float z = mn.y - far - 4f; z < mx.y + far + 4f; z += cell)
                {
                    var p = new Vector2(x + R() * cell, z + R() * cell);
                    float d = DistPlay(p);
                    if (d < near || d > far) continue;
                    if (!pit && R() > (d < 7f ? 0.75f : 0.5f)) continue;
                    float h = pit ? L(7f, 11f) : (d < 5f ? L(4.5f, 7f) : L(6f, 9.5f));
                    PutSized(trees[rng.Next(trees.Count)], forest, new Vector3(p.x, ground(p) - 0.15f, p.y), h, R() * 360f, !pit && d < 6f);
                    n++;
                }
            if (!pit)   // 맹그로브 줄기·뿌리: 물가에서 8m 까지 나무 발치에 깔림
            {
                var rootsA = Load(PropDir + "Roots_A.prefab"); var rootsB = Load(JwProps + "PF_MangroveRoots_0.prefab");
                var g = new GameObject("Mangrove_Roots").transform; g.SetParent(props, false);
                for (float x = mn.x - 10f; x < mx.x + 10f; x += 2.2f)
                    for (float z = mn.y - 10f; z < mx.y + 10f; z += 2.2f)
                    {
                        var p = new Vector2(x + R() * 2.2f, z + R() * 2.2f);
                        float d = DistPlay(p);
                        if (d < 0.3f || d > 8f || R() > 0.55f) continue;
                        GameObject pf = (rootsB != null && R() < 0.4f) ? rootsB : rootsA;
                        if (pf == null) continue;
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, g);
                        float s = L(0.4f, 0.8f);
                        go.transform.SetPositionAndRotation(new Vector3(p.x, Mathf.Max(ground(p), spec.waterY - 0.2f) - 0.05f, p.y), Quaternion.Euler(L(-5f, 5f), R() * 360f, L(-5f, 5f)));
                        go.transform.localScale = new Vector3(s, s * L(0.8f, 1.2f), s);
                        foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
                    }
            }
            return n;
        }

        // 절벽 면: 구역 가장자리 바로 바깥·아래에 돌을 세 줄로 쌓아 지형 절벽을 가린다 (폭포 자리는 폭포 도구가 쌓음)
        private static void BuildRim(Transform props, Spec spec, System.Random rng)
        {
            float R() => (float)rng.NextDouble();
            float L(float lo, float hi) => Mathf.Lerp(lo, hi, R());
            var stones = new List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("Stone_Big t:Prefab", new[] { StoneDir }))
            { var go = Load(AssetDatabase.GUIDToAssetPath(guid)); if (go != null && go.name.StartsWith("Stone_Big")) stones.Add(go); }
            if (stones.Count == 0) { Debug.LogWarning("[SpecBuild] Stone_Big 프리팹이 없어 절벽 돌을 건너뜁니다."); return; }
            var group = new GameObject("CliffRim").transform; group.SetParent(props, false);
            float[] rowY = { -1.7f, -5.2f, -8.6f };
            foreach (var poly in spec.blobs)
                for (int i = 0; i < poly.Length; i++)
                {
                    Vector2 a = poly[i], b = poly[(i + 1) % poly.Length]; float len = Vector2.Distance(a, b);
                    Vector2 dir = (b - a) / Mathf.Max(len, 0.01f), nrm = new Vector2(dir.y, -dir.x);
                    if (InBlob((a + b) * 0.5f + nrm * 0.6f)) nrm = -nrm;   // 바깥쪽
                    for (float s = 0f; s < len; s += 2.8f)
                    {
                        Vector2 e = a + dir * (s + L(-0.4f, 0.4f));
                        if (spec.fall != null && DistPoly(spec.fall, e, false) < 1.5f) continue;
                        for (int row = 0; row < rowY.Length; row++)
                        {
                            float size = L(4.2f, 5.6f);
                            Vector2 p = e + nrm * (size * 0.45f + row * 0.45f + L(0f, 0.5f));
                            var go = (GameObject)PrefabUtility.InstantiatePrefab(stones[rng.Next(stones.Count)], group);
                            go.transform.rotation = Quaternion.Euler(L(-10f, 10f), R() * 360f, L(-10f, 10f));
                            float h = RenderHeight(go, out float w);
                            go.transform.localScale *= size / Mathf.Max(0.01f, Mathf.Max(w, h * 0.8f));
                            Vector3 want = new Vector3(p.x, rowY[row] + L(-0.5f, 0.5f), p.y);
                            go.transform.position = want;
                            var rs = go.GetComponentsInChildren<Renderer>();
                            if (rs.Length > 0) { Bounds bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds); go.transform.position += want - bb.center; }
                            foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
                            if (row > 0) foreach (var r in rs) r.shadowCastingMode = ShadowCastingMode.Off;
                            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                        }
                    }
                }
        }

        // 폭포 자리: 가장자리 선마다 "폭포" 밑에 Cube(면 안내)를 세우고, 그 안쪽에 흘러드는 물 판을 깐다. 실제 폭포는 HJH/맵/폭포 메뉴가 만든다.
        private static void BuildFall(Transform root, Spec spec)
        {
            if (spec.fall == null || spec.fall.Length < 2) return;
            var fall = new GameObject("폭포").transform; fall.SetParent(root, false);
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Members/HJH/09.JungleWater/Generated/Materials/M_JungleWater.mat");
            for (int i = 0; i < spec.fall.Length - 1; i++)
            {
                Vector2 a = spec.fall[i], b = spec.fall[i + 1]; float len = Vector2.Distance(a, b);
                Vector2 dir = (b - a) / len, outward = new Vector2(dir.y, -dir.x);
                if (InBlob((a + b) * 0.5f + outward * 0.8f)) { dir = -dir; outward = -outward; }   // Cube 의 로컬 +X 가 절벽 바깥을 향하게
                Vector2 mid = (a + b) * 0.5f + outward * 0.7f;
                float top = 0.15f, bottom = -spec.pitDepth + 0.5f;
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Cube"; cube.transform.SetParent(fall, false);
                cube.transform.SetPositionAndRotation(new Vector3(mid.x, (top + bottom) * 0.5f, mid.y), Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y)));
                cube.transform.localScale = new Vector3(1f, top - bottom, len);

                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.name = "Water_Stream"; plane.transform.SetParent(root, false);
                Vector2 pc = (a + b) * 0.5f - outward * 2.6f;
                plane.transform.SetPositionAndRotation(new Vector3(pc.x, spec.waterY, pc.y), Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.y)));
                plane.transform.localScale = new Vector3((len + 5f) / 10f, 1f, 0.52f);
                Object.DestroyImmediate(plane.GetComponent<Collider>());
                var mr = plane.GetComponent<MeshRenderer>(); if (waterMat != null) mr.sharedMaterial = waterMat; mr.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // 상점 오브젝트 자리 표시 (빈 오브젝트 하나). 여기 반경 안에는 아무 장식도 놓지 않는다.
        private static void BuildClearMarker(Transform root, Spec spec, System.Func<Vector2, float> ground)
        {
            if (spec.clearR <= 0f) return;
            var go = new GameObject($"ShopSpot (상점 오브젝트 자리, 반경 {spec.clearR:0.#}m 비움)");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(spec.clear.x, ground(spec.clear), spec.clear.y);
        }

        private static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        // ───────────────────────── 지형 ─────────────────────────
        private static Terrain BuildTerrain(Transform parent, Vector2 origin, float size, float height, float baseY, int hres, int ares, Spec spec)
        {
            var data = new TerrainData { heightmapResolution = hres, size = new Vector3(size, height, size), alphamapResolution = ares };
            var h = new float[hres, hres];
            for (int zi = 0; zi < hres; zi++)
            {
                if (zi % 64 == 0) EditorUtility.DisplayProgressBar("지형 높이", zi + "/" + hres, zi / (float)hres);
                for (int xi = 0; xi < hres; xi++)
                {
                    var p = new Vector2(origin.x + xi * size / (hres - 1), origin.y + zi * size / (hres - 1));
                    h[zi, xi] = Mathf.Clamp01((HeightAt(p) - baseY) / height);
                }
            }
            data.SetHeights(0, 0, h);
            var layers = new[]
            {
                AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerDir + "TL_Moss.terrainlayer"),
                AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerDir + "TL_Mud.terrainlayer"),
                AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerDir + "TL_WetMoss.terrainlayer"),
                AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerDir + "TL_MangroveRoots.terrainlayer"),
            };
            data.terrainLayers = layers;
            var a = new float[ares, ares, 4];
            for (int zi = 0; zi < ares; zi++)
            {
                if (zi % 64 == 0) EditorUtility.DisplayProgressBar("지형 질감", zi + "/" + ares, zi / (float)ares);
                for (int xi = 0; xi < ares; xi++)
                {
                    var p = new Vector2(origin.x + (xi + 0.5f) * size / ares, origin.y + (zi + 0.5f) * size / ares);
                    bool play = InPlay(p), water = InWater(p);
                    float dOut = play ? 0f : DistPlay(p);
                    float moss = 1f, mud = 0f, wet = 0f, roots = 0f;
                    if (play)
                    {
                        float dm = DistMud(p), dr = DistRoute(p);
                        mud = (1f - Smooth(1.6f, 3.4f, dm)) * 0.9f;
                        mud = Mathf.Max(mud, (1f - Smooth(1.2f, 2.6f, dr)) * 0.35f);
                        mud = Mathf.Max(mud, Smooth(0.62f, 0.8f, Mathf.PerlinNoise(p.x * 0.09f + 40f, p.y * 0.09f + 40f)) * 0.25f);
                        float dw = water ? 0f : DistWater(p); wet = (1f - Smooth(0f, 1.8f, dw)) * 0.6f;
                    }
                    else if (water || (S.surround == 2 && dOut < 17f)) { wet = 1f; moss = 0.2f; }
                    else { roots = Smooth(0.8f, 4f, dOut) * 0.85f; if (dOut > 18f) { mud = 0.8f; roots = 0f; } wet = (1f - Smooth(0f, 1.8f, DistWater(p))) * 0.6f; if (S.surround == 1) { mud = 0.7f; roots = 0.35f; moss = 0.3f; } }
                    float sum = moss + mud + wet + roots;
                    a[zi, xi, 0] = moss / sum; a[zi, xi, 1] = mud / sum; a[zi, xi, 2] = wet / sum; a[zi, xi, 3] = roots / sum;
                }
            }
            EditorUtility.ClearProgressBar();
            data.SetAlphamaps(0, 0, a);
            string dp = DataDir + spec.sceneName + "_TerrainData.asset";
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(dp) != null) AssetDatabase.DeleteAsset(dp);
            AssetDatabase.CreateAsset(data, dp);
            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(origin.x, baseY, origin.y);
            var t = go.GetComponent<Terrain>();
            t.drawInstanced = true; t.heightmapPixelError = 4f;
            return t;
        }

        // ───────────────────────── 물 · 안개 ─────────────────────────
        private static void BuildWaterAndFog(Transform root, Vector2 mn, Vector2 mx, Vector2 center, Spec spec)
        {
            var group = new GameObject("Water_Fog").transform; group.SetParent(root, false);
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Members/HJH/09.JungleWater/Generated/Materials/M_JungleWater.mat");
            if (spec.water == null && spec.surround != 2)
            {
                // 큰 물이 없는 맵(절벽 맵): 낮은 안개만 깔고, 가라앉은 바닥에는 깊이감을 주는 안개를 한 겹 더
                BuildFogPlane(group, "GroundFog", center, 1.0f, 16f);
                if (spec.surround == 1) BuildFogPlane(group, "PitFog", center, -spec.pitDepth * 0.55f, 26f);
                return;
            }
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Water"; plane.transform.SetParent(group, false);
            Vector2 wmn = new Vector2(1e9f, 1e9f), wmx = new Vector2(-1e9f, -1e9f);
            if (spec.water != null) foreach (var p in spec.water) { wmn = Vector2.Min(wmn, p); wmx = Vector2.Max(wmx, p); }
            else { wmn = mn - Vector2.one * 22f; wmx = mx + Vector2.one * 22f; }   // 맹그로브 숲 전체를 덮는 물
            float side = Mathf.Max(wmx.x - wmn.x, wmx.y - wmn.y) + 4f;
            var wc = (wmn + wmx) * 0.5f;
            plane.transform.position = new Vector3(wc.x, spec.waterY, wc.y);
            plane.transform.localScale = new Vector3(side / 10f, 1f, side / 10f);
            Object.DestroyImmediate(plane.GetComponent<Collider>());
            var wr = plane.GetComponent<MeshRenderer>();
            if (waterMat != null) wr.sharedMaterial = waterMat;
            wr.shadowCastingMode = ShadowCastingMode.Off;

            var sim = new GameObject("WaterRippleSimulation").AddComponent<Assets.Members.HJH.Environment.WaterRippleSimulation>();
            sim.transform.SetParent(group, false);
            var so = new SerializedObject(sim);
            so.FindProperty("simShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Members/HJH/09.JungleWater/Shaders/JungleRippleSim.shader");
            so.FindProperty("waterSurface").objectReferenceValue = wr;
            so.ApplyModifiedPropertiesWithoutUndo();

            var fogMat = AssetDatabase.LoadAssetAtPath<Material>(DataDir + "MapL_GroundFog.mat") ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Members/HJH/05.Data/Fx/M_JungleFog_Stylized.mat");
            var fog = GameObject.CreatePrimitive(PrimitiveType.Plane);
            fog.name = "GroundFog"; fog.transform.SetParent(group, false);
            fog.transform.position = new Vector3(center.x, 1.0f, center.y);
            fog.transform.localScale = new Vector3(16f, 1f, 16f);
            Object.DestroyImmediate(fog.GetComponent<Collider>());
            if (fogMat != null) fog.GetComponent<MeshRenderer>().sharedMaterial = fogMat;
        }

        private static void BuildFogPlane(Transform group, string name, Vector2 center, float y, float scale)
        {
            var fogMat = AssetDatabase.LoadAssetAtPath<Material>(DataDir + "MapL_GroundFog.mat") ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Members/HJH/05.Data/Fx/M_JungleFog_Stylized.mat");
            var fog = GameObject.CreatePrimitive(PrimitiveType.Plane);
            fog.name = name; fog.transform.SetParent(group, false);
            fog.transform.position = new Vector3(center.x, y, center.y);
            fog.transform.localScale = new Vector3(scale, 1f, scale);
            Object.DestroyImmediate(fog.GetComponent<Collider>());
            if (fogMat != null) fog.GetComponent<MeshRenderer>().sharedMaterial = fogMat;
            fog.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ───────────────────────── 보이지 않는 벽 ─────────────────────────
        private static void BuildWalls(Transform root, Spec spec)
        {
            var group = new GameObject("Boundary_Walls").transform; group.SetParent(root, false);
            int n = 0;
            foreach (var poly in spec.blobs)
                for (int i = 0; i < poly.Length; i++)
                {
                    var a = poly[i]; var b = poly[(i + 1) % poly.Length]; float len = Vector2.Distance(a, b);
                    var dir = (b - a) / len; int segs = Mathf.Max(1, Mathf.CeilToInt(len / 2f)); float sl = len / segs;
                    for (int s = 0; s < segs; s++)
                    {
                        var mid = a + dir * (sl * (s + 0.5f));
                        if (spec.bridge && Seg(mid, spec.bridgeA, spec.bridgeB) < 2.8f) continue;        // 다리 입구는 열어 둠
                        var go = new GameObject("Wall_" + (n++)); go.transform.SetParent(group, false);
                        go.transform.position = new Vector3(mid.x, 4f, mid.y);
                        go.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.y));
                        var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(0.6f, 8f, sl + 0.3f);
                    }
                }
        }

        // ───────────────────────── 다리 (임시 절차 모델) ─────────────────────────
        private static Transform BuildBridge(Transform root, Spec spec)
        {
            var wood = AssetDatabase.LoadAssetAtPath<Material>(PropDir.Replace("Prefabs/", "Materials/") + "MapBridge_Wood.mat");
            if (wood == null)
            {
                wood = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MapBridge_Wood" };
                wood.SetColor("_BaseColor", new Color(0.22f, 0.15f, 0.10f)); wood.SetFloat("_Smoothness", 0.1f);
                AssetDatabase.CreateAsset(wood, PropDir.Replace("Prefabs/", "Materials/") + "MapBridge_Wood.mat");
            }
            var a = spec.bridgeA; var b = spec.bridgeB; float len = Vector2.Distance(a, b); var dir = (b - a) / len;
            var g = new GameObject("Bridge").transform; g.SetParent(root, false);
            g.position = new Vector3((a.x + b.x) * 0.5f, 0.05f, (a.y + b.y) * 0.5f);
            g.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));   // 로컬 z = 다리 방향
            const float width = 3.6f;
            var rng = new System.Random(5);
            for (float z = -len / 2f + 0.3f; z < len / 2f; z += 0.52f)
            {
                var pl = GameObject.CreatePrimitive(PrimitiveType.Cube); pl.transform.SetParent(g, false);
                pl.transform.localPosition = new Vector3(0f, 0f, z);
                pl.transform.localRotation = Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 4f, 0f);
                pl.transform.localScale = new Vector3(width, 0.12f, 0.46f);
                pl.GetComponent<MeshRenderer>().sharedMaterial = wood; Object.DestroyImmediate(pl.GetComponent<Collider>());
            }
            foreach (float sx in new[] { -width / 2f, width / 2f })
            {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube); rail.transform.SetParent(g, false);
                rail.transform.localPosition = new Vector3(sx, 0.85f, 0f); rail.transform.localScale = new Vector3(0.12f, 0.1f, len);
                rail.GetComponent<MeshRenderer>().sharedMaterial = wood; Object.DestroyImmediate(rail.GetComponent<Collider>());
                for (float z = -len / 2f; z <= len / 2f + 0.01f; z += 2.5f)
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cube); post.transform.SetParent(g, false);
                    post.transform.localPosition = new Vector3(sx, 0.45f, z); post.transform.localScale = new Vector3(0.16f, 0.9f, 0.16f);
                    post.GetComponent<MeshRenderer>().sharedMaterial = wood; Object.DestroyImmediate(post.GetComponent<Collider>());
                }
            }
            var deck = new GameObject("Deck"); deck.transform.SetParent(g, false);
            var bc = deck.AddComponent<BoxCollider>(); bc.size = new Vector3(width, 0.3f, len); bc.center = new Vector3(0f, -0.05f, 0f);
            return g;
        }

        // ───────────────────────── 포탈 · 스폰 ─────────────────────────
        private static Material Emissive(string name, Color c)
        {
            string path = DataDir + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c * 0.4f); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 2.2f);
            EditorUtility.SetDirty(m); return m;
        }

        private static void BuildPortals(Transform root, Spec spec, GameObject lantern, System.Func<Vector2, float> ground)
        {
            var group = new GameObject("Portals").transform; group.SetParent(root, false);
            foreach (var (pos, name, col) in new[] { (spec.start, "Portal_Start", new Color(0.2f, 0.9f, 1f)), (spec.end, "Portal_End", new Color(1f, 0.55f, 0.15f)) })
            {
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder); disc.name = name; disc.transform.SetParent(group, false);
                disc.transform.position = new Vector3(pos.x, ground(pos) + 0.04f, pos.y); disc.transform.localScale = new Vector3(3.2f, 0.04f, 3.2f);
                disc.GetComponent<MeshRenderer>().sharedMaterial = Emissive("MapB_" + name, col); Object.DestroyImmediate(disc.GetComponent<Collider>());
                var lg = new GameObject("Light"); lg.transform.SetParent(disc.transform, false); lg.transform.localPosition = new Vector3(0f, 40f, 0f);
                var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = col; l.range = 9f; l.intensity = 3f; l.shadows = LightShadows.None;
                if (lantern != null)
                    foreach (float s in new[] { -2.4f, 2.4f })
                    {
                        var p = pos + new Vector2(s, 0f);
                        var lt = (GameObject)PrefabUtility.InstantiatePrefab(lantern, group);
                        lt.transform.position = new Vector3(p.x, ground(p), p.y); lt.transform.rotation = Quaternion.Euler(0f, s > 0 ? 200f : 160f, 0f);
                        foreach (var c in lt.GetComponentsInChildren<Collider>()) c.enabled = false;
                    }
            }
            if (lantern != null && spec.bridge)   // 다리 양끝
                foreach (var end in new[] { spec.bridgeA, spec.bridgeB })
                    foreach (float s in new[] { -2.2f, 2.2f })
                    {
                        var d = (spec.bridgeB - spec.bridgeA).normalized; var n = new Vector2(-d.y, d.x);
                        var p = end + n * s;
                        var lt = (GameObject)PrefabUtility.InstantiatePrefab(lantern, group);
                        lt.transform.position = new Vector3(p.x, ground(p), p.y);
                        foreach (var c in lt.GetComponentsInChildren<Collider>()) c.enabled = false;
                    }
        }

        private static void BuildSpawns(Transform root, Spec spec, System.Func<Vector2, float> ground, List<Vector2> avoid)
        {
            var group = new GameObject("EnemySpawns (markers only)").transform; group.SetParent(root, false);
            var mat = Emissive("MapB_SpawnMarker", new Color(1f, 0.15f, 0.2f));
            var roles = new[] { "근접", "원거리", "근접", "원거리", "엘리트", "주력/원거리" };
            // 길(시작→다리, 다리 뒤→끝)에서 일정 비율 지점을 찾고, 구역 안쪽으로 4m 비껴 놓는다
            var pts = new List<Vector2>();
            void Along(Vector2[] route, params float[] fr)
            {
                float total = 0f; for (int i = 0; i < route.Length - 1; i++) total += Vector2.Distance(route[i], route[i + 1]);
                foreach (var f in fr)
                {
                    float want = total * f, acc = 0f;
                    for (int i = 0; i < route.Length - 1; i++)
                    {
                        float seg = Vector2.Distance(route[i], route[i + 1]);
                        if (acc + seg >= want)
                        {
                            var p = Vector2.Lerp(route[i], route[i + 1], (want - acc) / Mathf.Max(seg, 0.01f));
                            var d = (route[i + 1] - route[i]).normalized; var n = new Vector2(-d.y, d.x);
                            var c1 = p + n * 4f; var c2 = p - n * 4f;
                            pts.Add(InBlob(c1) && (!InBlob(c2) || Depth(c1) > Depth(c2)) ? c1 : (InBlob(c2) ? c2 : p));
                            break;
                        }
                        acc += seg;
                    }
                }
            }
            Along(spec.routes[0], 0.35f, 0.62f, 0.85f);
            if (spec.routes.Count > 1) Along(spec.routes[1], 0.3f, 0.6f, 0.9f);
            else Along(spec.routes[0], 0.22f, 0.5f, 0.74f, 0.93f);   // 길이 하나뿐인 맵은 같은 길에 이어서
            for (int i = 0; i < pts.Count; i++)
            {
                var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                m.name = $"Spawn_{i + 1} [{roles[i % roles.Length]}]"; m.transform.SetParent(group, false);
                m.transform.position = new Vector3(pts[i].x, ground(pts[i]) + 0.3f, pts[i].y); m.transform.localScale = Vector3.one * 0.6f;
                m.GetComponent<MeshRenderer>().sharedMaterial = mat; Object.DestroyImmediate(m.GetComponent<Collider>());
                avoid.Add(pts[i]);
            }
        }
    }
}
