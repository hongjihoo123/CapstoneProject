using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using M = HJH.EditorTools.MapSpecBuilder;
using E = HJH.EditorTools.MapEdgePolish;

namespace HJH.EditorTools
{
    // 다리 건너편(끝 구역)을 맹그로브 늪지대로. 메뉴: HJH/맵/늪지대/...
    //  - 지형: 구불구불한 얕은 웅덩이 여러 개를 파서 물 판을 깔고, 바닥은 진흙·젖은 이끼·뿌리 질감으로 (시작 구역은 그대로)
    //  - 맹그로브 나무(MangrovePlaceholder)를 길·스폰·포탈을 피해 촘촘히, 일부는 물속에 서 있게 + 웅덩이 둘레에 뿌리
    //  - 낮게 깔린 안개 + 웅덩이 위 반딧불이. 시작 구역은 건드리지 않는다. 모두 이동을 막는 콜라이더는 끈다.
    public static class MapSwampStyle
    {
        private const string PropDir = "Assets/Members/HJH/05.Data/MapProps/Prefabs/";
        private const string JwDir = "Assets/Members/HJH/09.JungleWater/Generated/Prefabs/";
        private const string WaterMat = "Assets/Members/HJH/09.JungleWater/Generated/Materials/M_JungleWater.mat";
        private const string FogMat = "Assets/Members/HJH/05.Data/Map/MapL_GroundFog.mat";
        private const string Group = "Swamp";

        private static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - a) / (b - a)));
        private static GameObject Load(string p) => AssetDatabase.LoadAssetAtPath<GameObject>(p);

        [MenuItem("HJH/맵/늪지대/다리 건너편 늪지대 꾸미기 (맹그로브)")]
        private static void Build()
        {
            if (EditorApplication.isPlaying) return;
            var terrain = Terrain.activeTerrain;
            if (terrain == null || !M.UseSpecFor(EditorSceneManager.GetActiveScene().name)) { Debug.LogWarning("[Swamp] 맵 스펙이 있는 씬(Map_Bridge_Normal / _HJH_Map / Map_Shop)에서만 동작합니다."); return; }
            // (스펙은 위에서 씬 이름으로 골랐음)
            float waterY = M.SpecWaterY;

            var old = GameObject.Find(Group);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject(Group); Undo.RegisterCreatedObjectUndo(root, "Swamp");
            var props = GameObject.Find("Props"); if (props != null) root.transform.SetParent(props.transform, false);
            var gWater = new GameObject("Swamp_Water").transform; gWater.SetParent(root.transform, false);
            var gTrees = new GameObject("Swamp_Trees").transform; gTrees.SetParent(root.transform, false);
            var gDeco = new GameObject("Swamp_Deco").transform; gDeco.SetParent(root.transform, false);

            // 피할 점: 포탈·스폰·랜턴
            var avoid = new List<Vector2>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith("Spawn_") || t.name.StartsWith("Portal_")) avoid.Add(new Vector2(t.position.x, t.position.z));

            var rng = new System.Random(20261111);
            float R() => (float)rng.NextDouble();
            float L(float lo, float hi) => Mathf.Lerp(lo, hi, R());

            // 끝 구역 범위
            Vector2 mn = new Vector2(1e9f, 1e9f), mx = new Vector2(-1e9f, -1e9f);
            var probe = new Vector2[0];
            for (float x = 0f; x < 130f; x += 1f)
                for (float z = 0f; z < 130f; z += 1f)
                { var p = new Vector2(x, z); if (M.InEnd(p)) { mn = Vector2.Min(mn, p); mx = Vector2.Max(mx, p); } }
            if (mx.x < mn.x) { Debug.LogWarning("[Swamp] 끝 구역을 찾지 못했습니다."); return; }

            // ── 웅덩이 14개 선정
            var pools = new List<E.Pond>();
            for (int t = 0; t < 6000 && pools.Count < 14; t++)
            {
                var c = new Vector2(L(mn.x, mx.x), L(mn.y, mx.y));
                if (!M.InEnd(c)) continue;
                if (M.SpecBlocked(c, 4f)) continue;   // 상점 자리·폭포 물길은 비움
                var pd = new E.Pond { c = c, ax = L(1.8f, 4.4f), az = L(1.3f, 3.0f), yaw = R() * Mathf.PI, p1 = R() * 6.28f, p2 = R() * 6.28f, p3 = R() * 6.28f };
                float rr = pd.R;
                if (M.EndDepth(c) < 2.4f + rr * 0.4f) continue;
                if (M.RouteDist(c) < 3.2f + rr * 0.55f) continue;
                bool bad = false;
                foreach (var a in avoid) if (Vector2.Distance(a, c) < 4f + rr * 0.6f) { bad = true; break; }
                foreach (var q in pools) if (Vector2.Distance(q.c, c) < (q.R + rr) * 0.75f) { bad = true; break; }
                if (!bad) pools.Add(pd);
            }

            // ── 지형 깎기
            var data = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(data, "Swamp terrain");
            int hres = data.heightmapResolution; var origin = terrain.transform.position; var size = data.size;
            var h = data.GetHeights(0, 0, hres, hres);
            const float bank = 0.55f;
            foreach (var pd in pools)
            {
                float reach = pd.R * (1f + bank) + 1f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((pd.c.x - reach - origin.x) / size.x * (hres - 1))), x1 = Mathf.Min(hres - 1, Mathf.CeilToInt((pd.c.x + reach - origin.x) / size.x * (hres - 1)));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((pd.c.y - reach - origin.z) / size.z * (hres - 1))), z1 = Mathf.Min(hres - 1, Mathf.CeilToInt((pd.c.y + reach - origin.z) / size.z * (hres - 1)));
                for (int zi = z0; zi <= z1; zi++)
                    for (int xi = x0; xi <= x1; xi++)
                    {
                        var w = new Vector2(origin.x + xi / (float)(hres - 1) * size.x, origin.z + zi / (float)(hres - 1) * size.z);
                        float u = E.U(pd, w); if (u > 1f + bank) continue;
                        float h0 = origin.y + h[zi, xi] * size.y;
                        float noise = (Mathf.PerlinNoise(w.x * 0.9f, w.y * 0.9f) - 0.5f) * 0.08f;
                        float shore = waterY - 0.08f, bottom = shore - 0.34f * (1f - Mathf.Clamp01(u * u)) + noise;
                        float nh = u <= 1f ? Mathf.Min(h0, bottom) : Mathf.Lerp(Mathf.Min(h0, shore), h0, Smooth(1f, 1f + bank, u));
                        h[zi, xi] = Mathf.Clamp01((nh - origin.y) / size.y);
                    }
            }
            data.SetHeights(0, 0, h);

            // ── 바닥 질감: 끝 구역만 진흙 · 젖은 이끼 · 뿌리 중심으로 (0 이끼,1 진흙,2 젖은 이끼,3 뿌리, 4+ 바닥 스타일 레이어는 비움)
            int ares = data.alphamapResolution, nl = data.alphamapLayers;
            var map = data.GetAlphamaps(0, 0, ares, ares);
            for (int zi = 0; zi < ares; zi++)
                for (int xi = 0; xi < ares; xi++)
                {
                    var p = new Vector2(origin.x + (xi + 0.5f) / ares * size.x, origin.z + (zi + 0.5f) / ares * size.z);
                    if (!M.InEnd(p)) continue;
                    float poolU = 99f; foreach (var pd in pools) poolU = Mathf.Min(poolU, E.U(pd, p));
                    float inPool = 1f - Smooth(0.9f, 1.5f, poolU);                    // 웅덩이와 둑
                    float lane = Smooth(1.8f, 3.4f, M.RouteDist(p));                  // 길 위는 덜 질척하게
                    float nMud = Smooth(0.45f, 0.62f, Mathf.PerlinNoise(p.x * 0.10f + 3f, p.y * 0.10f + 8f));
                    float nWet = Smooth(0.5f, 0.66f, Mathf.PerlinNoise(p.x * 0.08f + 41f, p.y * 0.08f + 6f));
                    float nRoot = Smooth(0.58f, 0.74f, Mathf.PerlinNoise(p.x * 0.16f + 9f, p.y * 0.16f + 31f));
                    float mud = Mathf.Clamp01(0.45f + nMud * 0.5f) * (0.5f + 0.5f * lane);
                    float wet = Mathf.Clamp01(inPool + nWet * 0.55f) * (0.4f + 0.6f * lane);
                    float roots = nRoot * 0.5f * lane;
                    for (int l = 0; l < nl; l++) map[zi, xi, l] = 0f;
                    map[zi, xi, 0] = 0.08f; map[zi, xi, 1] = mud; map[zi, xi, 2] = wet; map[zi, xi, 3] = roots;
                    float sum = 0f; for (int l = 0; l < nl; l++) sum += map[zi, xi, l];
                    for (int l = 0; l < nl; l++) map[zi, xi, l] /= sum;
                }
            data.SetAlphamaps(0, 0, map);
            EditorUtility.SetDirty(data);

            float Ground(Vector2 p) => terrain.SampleHeight(new Vector3(p.x, 0, p.y)) + origin.y;
            bool InAnyPool(Vector2 p, float k) { foreach (var pd in pools) if (E.U(pd, p) < k) return true; return false; }

            // ── 물 판
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>(WaterMat);
            int wi = 0;
            foreach (var pd in pools)
            {
                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.name = "WaterSwamp_" + (wi++); plane.transform.SetParent(gWater, false);
                float side = pd.R * (2f + bank * 2f) + 1f;
                plane.transform.position = new Vector3(pd.c.x, waterY + 0.002f, pd.c.y);
                plane.transform.localScale = new Vector3(side / 10f, 1f, side / 10f);
                Object.DestroyImmediate(plane.GetComponent<Collider>());
                var mr = plane.GetComponent<MeshRenderer>(); if (waterMat != null) mr.sharedMaterial = waterMat; mr.shadowCastingMode = ShadowCastingMode.Off;
            }

            GameObject Put(GameObject prefab, Transform parent, Vector2 p, float sink, Vector3 scale, float tilt)
            {
                var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                g.transform.position = new Vector3(p.x, Ground(p) - sink, p.y);
                g.transform.rotation = Quaternion.Euler(L(-tilt, tilt), R() * 360f, L(-tilt, tilt));
                g.transform.localScale = scale;
                foreach (var c in g.GetComponentsInChildren<Collider>()) c.enabled = false;
                return g;
            }

            // ── 맹그로브 나무: 지터 격자, 길·스폰·포탈 피해서. 일부는 물속에 서 있음.
            var mangrove = Load(JwDir + "PF_JungleTree.prefab") ?? Load(JwDir + "MangrovePlaceholder.prefab");   // 다리 맵처럼 잎 달린 나무를 먼저 씀 (없으면 임시 모델)
            int trees = 0;
            if (mangrove != null)
                for (float x = mn.x; x < mx.x; x += 4.4f)
                    for (float z = mn.y; z < mx.y; z += 4.4f)
                    {
                        var p = new Vector2(x + R() * 4.4f, z + R() * 4.4f);
                        if (!M.InEnd(p) || M.EndDepth(p) < 1.0f) continue;
                        if (M.RouteDist(p) < 4.2f || M.SpecBlocked(p, 1.5f)) continue;
                        bool bad = false; foreach (var a in avoid) if (Vector2.Distance(a, p) < 5.5f) { bad = true; break; }
                        if (bad) continue;
                        bool inWater = InAnyPool(p, 1.0f);
                        if (R() > (inWater ? 0.35f : 0.62f)) continue;                 // 밀도
                        var g = Put(mangrove, gTrees, p, inWater ? 0.15f : 0.05f, Vector3.one * L(0.9f, 1.5f), 2f);
                        foreach (var r in g.GetComponentsInChildren<MeshRenderer>()) if (M.EndDepth(p) > 9f) r.shadowCastingMode = ShadowCastingMode.Off;
                        trees++;
                    }

            // ── 뿌리·돌: 웅덩이 둘레
            var rootsA = Load(PropDir + "Roots_A.prefab"); var rootsB = Load(JwDir + "Props/PF_MangroveRoots_0.prefab");
            var rock = Load(PropDir + "Rock_A.prefab");
            int deco = 0;
            foreach (var pd in pools)
            {
                int n = Mathf.Clamp(Mathf.RoundToInt((pd.ax + pd.az) * 2.2f), 6, 18);
                for (int i = 0; i < n; i++)
                {
                    float a = (i + L(-0.3f, 0.3f)) / n * Mathf.PI * 2f;
                    var p = pd.c + new Vector2(Mathf.Cos(a) * pd.ax * L(1.0f, 1.5f), Mathf.Sin(a) * pd.az * L(1.0f, 1.5f));
                    if (!M.InEnd(p) || M.EndDepth(p) < 0.8f || M.RouteDist(p) < 2.2f || M.SpecBlocked(p, 0.5f)) continue;
                    float roll = R();
                    if (roll < 0.55f && (rootsA != null || rootsB != null)) { var pf = (rootsB != null && R() < 0.5f) ? rootsB : rootsA; float s = L(0.35f, 0.7f); Put(pf, gDeco, p, 0.08f, new Vector3(s, s * L(0.8f, 1.2f), s), 6f); deco++; }
                    else if (rock != null) { float s = L(0.25f, 0.6f); Put(rock, gDeco, p, 0.1f, new Vector3(s * L(0.9f, 1.4f), s * L(0.6f, 0.9f), s * L(0.9f, 1.4f)), 10f); deco++; }
                }
            }

            // ── 낮게 깔린 안개: 끝 구역만 덮는 판 하나 (밀도는 안개 머티리얼 따라)
            var fogMat = AssetDatabase.LoadAssetAtPath<Material>(FogMat);
            if (fogMat != null)
            {
                var fog = GameObject.CreatePrimitive(PrimitiveType.Plane);
                fog.name = "SwampMist"; fog.transform.SetParent(root.transform, false);
                var c2 = (mn + mx) * 0.5f;
                fog.transform.position = new Vector3(c2.x, 0.35f, c2.y);
                float sd = Mathf.Max(mx.x - mn.x, mx.y - mn.y) + 10f;
                fog.transform.localScale = new Vector3(sd / 10f, 1f, sd / 10f);
                Object.DestroyImmediate(fog.GetComponent<Collider>());
                fog.GetComponent<MeshRenderer>().sharedMaterial = fogMat; fog.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            // ── 웅덩이 위 반딧불이 (큰 웅덩이 6개)
            var ff = Load("Assets/Members/HJH/05.Data/Fx/Firefly/FireflyZone.prefab");
            int fz = 0;
            if (ff != null)
                foreach (var pd in pools)
                {
                    if (fz >= 6) break; if (pd.R < 3.5f) continue;
                    var g = (GameObject)PrefabUtility.InstantiatePrefab(ff, root.transform);
                    g.transform.position = new Vector3(pd.c.x, Ground(pd.c) + 0.4f, pd.c.y); fz++;
                }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Swamp] 늪지대 적용: 웅덩이 {pools.Count}개, 맹그로브 {trees}그루, 뿌리·돌 {deco}개, 반딧불이 {fz}곳 (끝 구역만, 시작 구역은 그대로). 마음에 안 들면 Ctrl+Z 또는 {Group} 삭제(지형은 Ctrl+Z).");
        }
    }
}
