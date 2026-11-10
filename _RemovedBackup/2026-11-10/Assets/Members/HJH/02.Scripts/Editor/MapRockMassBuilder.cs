using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HJH.EditorTools
{
    // 큰 돌덩이(베이스) 위에 중간 바위 → 작은 바위를 층층이 붙인 "암반 덩어리" 프리팹을 만든다 (칠대죄 레퍼런스: 큰 판암 + 단 + 옆에 붙은 돌).
    // 기존 Rock_A / Rock_B 프리팹만 조합하므로 새 모델 비용 없음. 콜라이더는 베이스와 2단만 켜고 나머지는 시각용.
    //   HJH/맵/암반 덩어리/프리팹 3종 만들기          → 05.Data/MapProps/Prefabs/RockMass_{A,B,C}.prefab
    //   HJH/맵/암반 덩어리/씬 뷰 중앙에 하나 놓기     → 선택한 프리팹(없으면 RockMass_A)을 터레인 위에 배치
    public static class MapRockMassBuilder
    {
        private const string PrefabFolder = "Assets/Members/HJH/05.Data/MapProps/Prefabs";
        private const string RockA = PrefabFolder + "/Rock_A.prefab";
        private const string RockB = PrefabFolder + "/Rock_B.prefab";

        [MenuItem("HJH/맵/암반 덩어리/프리팹 3종 만들기")]
        private static void BuildAll()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[RockMass] 플레이 중에는 못 합니다."); return; }
            var a = AssetDatabase.LoadAssetAtPath<GameObject>(RockA);
            var b = AssetDatabase.LoadAssetAtPath<GameObject>(RockB);
            if (a == null || b == null) { Debug.LogError("[RockMass] Rock_A / Rock_B 프리팹이 없습니다. 먼저 Tripo 프랍 반입을 돌리세요."); return; }

            string[] names = { "RockMass_A", "RockMass_B", "RockMass_C" };
            // 변종마다 베이스 폭/모양이 다르게 (A 넓은 판, B 길쭉, C 높은 단)
            var shapes = new[] { new Vector3(3.2f, 1.5f, 2.6f), new Vector3(3.8f, 1.3f, 2.0f), new Vector3(2.6f, 2.1f, 2.6f) };
            for (int v = 0; v < 3; v++) Build(names[v], a, b, shapes[v], 7001 + v * 97);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RockMass] 프리팹 3종 생성: " + PrefabFolder + "/RockMass_{A,B,C}.prefab (피벗 = 바닥 중앙, 바닥 일부는 땅에 묻힘)");
        }

        private static void Build(string name, GameObject a, GameObject b, Vector3 baseScale, int seed)
        {
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            float L(float lo, float hi) => Mathf.Lerp(lo, hi, R());

            var root = new GameObject(name);
            try
            {
                // 1) 베이스: 가장 큰 돌덩이. 납작하고 넓게, 아래가 땅에 묻히게 살짝 내림.
                var baseRock = Add(root.transform, b, new Vector3(0f, -0.3f, 0f), baseScale * L(0.95f, 1.1f), R() * 360f, true);
                baseRock.name = "Base";
                float bw = 3.0f * baseScale.x * 0.5f; // Rock_B 폭 약 3m 기준 반폭
                float bd = 3.0f * baseScale.z * 0.5f;

                // 2) 2단: 베이스 윗면 뒤쪽에 얹은 중간 바위 (계단 느낌)
                var tier = Add(root.transform, a, new Vector3(L(-0.25f, 0.25f) * bw, 0.9f * baseScale.y, -0.25f * bd),
                               baseScale * L(0.55f, 0.7f), R() * 360f, true);
                tier.name = "Tier2";

                // 3) 옆구리 붙임: 베이스 둘레에 중간 크기 4~5개, 반쯤 묻고 베이스와 겹치게
                int n = 4 + rng.Next(0, 2);
                for (int i = 0; i < n; i++)
                {
                    float ang = (i / (float)n + L(-0.08f, 0.08f)) * Mathf.PI * 2f;
                    var pos = new Vector3(Mathf.Cos(ang) * bw * L(0.85f, 1.05f), L(-0.35f, -0.15f), Mathf.Sin(ang) * bd * L(0.85f, 1.05f));
                    float s = L(0.5f, 0.95f);
                    var r = Add(root.transform, R() < 0.5f ? a : b, pos, new Vector3(s * L(0.9f, 1.3f), s * L(0.6f, 0.95f), s * L(0.9f, 1.3f)), R() * 360f, false);
                    r.name = "Side" + i;
                }

                // 4) 윗면/단 위에 작은 돌 3~4개 (디테일)
                int m = 3 + rng.Next(0, 2);
                for (int i = 0; i < m; i++)
                {
                    float ang = R() * Mathf.PI * 2f;
                    float rad = L(0.1f, 0.6f);
                    var pos = new Vector3(Mathf.Cos(ang) * bw * rad, L(0.7f, 1.2f) * baseScale.y, Mathf.Sin(ang) * bd * rad);
                    float s = L(0.18f, 0.35f);
                    var r = Add(root.transform, a, pos, new Vector3(s, s * L(0.7f, 1f), s * L(0.9f, 1.2f)), R() * 360f, false);
                    r.name = "Chip" + i;
                }

                string path = PrefabFolder + "/" + name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject Add(Transform parent, GameObject prefab, Vector3 localPos, Vector3 scale, float yaw, bool collider)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = scale;
            if (!collider)
                foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
            return go;
        }

        // ㄴ자 외곽선 / 길 / 웅덩이 (MapLNormalBuilder, MapLPolish 와 같은 값)
        private static readonly Vector2[] Outline =
        {
            new Vector2(5, 60), new Vector2(25, 60), new Vector2(25, 30), new Vector2(30, 30),
            new Vector2(30, 25), new Vector2(60, 25), new Vector2(60, 5), new Vector2(30, 5),
            new Vector2(30, 0), new Vector2(0, 0), new Vector2(0, 30), new Vector2(5, 30),
        };
        private static readonly Vector2[] PathPts =
        {
            new Vector2(15, 56), new Vector2(15, 44), new Vector2(13, 32), new Vector2(15, 22),
            new Vector2(24, 17), new Vector2(34, 18), new Vector2(46, 19), new Vector2(57, 15),
        };
        private const string AutoRoot = "RockMass_Auto";

        [MenuItem("HJH/맵/암반 덩어리/가장자리에 자동 배치 (다시 돌리면 교체)")]
        private static void AutoPlace()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[RockMass] 플레이 중에는 못 합니다."); return; }
            var terrain = Terrain.activeTerrain;
            var prefabs = new GameObject[3];
            string[] names = { "RockMass_A", "RockMass_B", "RockMass_C" };
            for (int i = 0; i < 3; i++) prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/" + names[i] + ".prefab");
            if (terrain == null || prefabs[0] == null) { Debug.LogWarning("[RockMass] Terrain 이나 RockMass 프리팹이 없습니다. 먼저 '프리팹 3종 만들기'를 돌리세요."); return; }

            // 피해야 할 오브젝트 위치 (랜턴·포탈·스폰·발광 꽃)
            var avoid = new System.Collections.Generic.List<Vector2>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                string n = t.name;
                if (n.Contains("Lantern") || n.Contains("Portal") || n.Contains("Spawn") || n.StartsWith("GlowFlowers"))
                    avoid.Add(new Vector2(t.position.x, t.position.z));
            }

            var old = GameObject.Find(AutoRoot);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject(AutoRoot);
            Undo.RegisterCreatedObjectUndo(root, "Auto rock mass");
            var props = GameObject.Find("Props");
            if (props != null) root.transform.SetParent(props.transform, false);

            var rng = new System.Random(1109);
            var placed = new System.Collections.Generic.List<Vector2>();
            int tries = 0;
            const int target = 12;
            while (placed.Count < target && tries++ < 4000)
            {
                var p = new Vector2((float)rng.NextDouble() * 62f - 1f, (float)rng.NextDouble() * 62f - 1f);
                if (!Inside(p)) continue;
                float edge = DistToEdge(p);
                if (edge < 2.8f || edge > 6.5f) continue;                     // 가장자리 띠 안에만 (여백은 큰 바위, 길은 비움)
                if (DistToPath(p) < 5.5f) continue;                           // 흙길 위는 비움
                if (p.x > 33f && p.x < 57f && p.y > 4f && p.y < 16f) continue; // 웅덩이 쪽 제외
                bool bad = false;
                foreach (var a in avoid) if (Vector2.Distance(a, p) < 5f) { bad = true; break; }
                if (bad) continue;
                foreach (var q in placed) if (Vector2.Distance(q, p) < 11f) { bad = true; break; }
                if (bad) continue;
                var wp = new Vector3(p.x, 0f, p.y);
                float gy = terrain.SampleHeight(wp) + terrain.transform.position.y;
                // 평지만 (주변 높이차가 크면 비탈)
                float h2 = terrain.SampleHeight(wp + new Vector3(3f, 0f, 0f)) + terrain.transform.position.y;
                float h3 = terrain.SampleHeight(wp + new Vector3(0f, 0f, 3f)) + terrain.transform.position.y;
                if (Mathf.Abs(h2 - gy) > 0.6f || Mathf.Abs(h3 - gy) > 0.6f) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[rng.Next(0, 3)], root.transform);
                Undo.RegisterCreatedObjectUndo(go, "Auto rock mass");
                go.transform.position = new Vector3(p.x, gy, p.y);
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                float s = Mathf.Lerp(0.85f, 1.25f, (float)rng.NextDouble());
                go.transform.localScale = new Vector3(s, s * Mathf.Lerp(0.9f, 1.2f, (float)rng.NextDouble()), s);
                placed.Add(p);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[RockMass] 암반 덩어리 {placed.Count}개를 가장자리에 배치했습니다 (목표 {target}). 마음에 안 드는 건 RockMass_Auto 아래에서 지우거나 옮기세요.");
        }

        private static bool Inside(Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = Outline.Length - 1; i < Outline.Length; j = i++)
                if ((Outline[i].y > p.y) != (Outline[j].y > p.y) &&
                    p.x < (Outline[j].x - Outline[i].x) * (p.y - Outline[i].y) / (Outline[j].y - Outline[i].y) + Outline[i].x)
                    c = !c;
            return c;
        }

        private static float DistToEdge(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Outline.Length; i++)
            {
                var a = Outline[i]; var b = Outline[(i + 1) % Outline.Length];
                best = Mathf.Min(best, DistSeg(p, a, b));
            }
            return best;
        }

        private static float DistToPath(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < PathPts.Length - 1; i++) best = Mathf.Min(best, DistSeg(p, PathPts[i], PathPts[i + 1]));
            return best;
        }

        private static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }

        // 선택한 암반 덩어리(없으면 RockMass_Auto 아래 전부) 주변을 중간 돌 · 미세한 돌 · 작은 꽃으로 채우고, 꽃 곁에 약한 파란 라이트를 둔다.
        // 모두 시각용(콜라이더 없음). 덩어리 아래 "Fill" 자식에 모이며, 다시 돌리면 그 덩어리의 Fill 만 교체된다.
        private const string FlowerPrefab = PrefabFolder + "/GlowFlowers.prefab";

        [MenuItem("HJH/맵/암반 덩어리/선택한 돌 무더기 주변 밀도 채우기 (돌+꽃+파란 빛)")]
        private static void FillAround()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[RockMass] 플레이 중에는 못 합니다."); return; }
            var terrain = Terrain.activeTerrain;
            var rockA = AssetDatabase.LoadAssetAtPath<GameObject>(RockA);
            var rockB = AssetDatabase.LoadAssetAtPath<GameObject>(RockB);
            var flower = AssetDatabase.LoadAssetAtPath<GameObject>(FlowerPrefab);
            if (terrain == null || rockA == null || flower == null) { Debug.LogWarning("[RockMass] Terrain 이나 Rock_A / GlowFlowers 프리팹이 없습니다."); return; }

            var sel = Selection.gameObjects;
            if (sel.Length == 0) { Debug.LogWarning("[RockMass] 채울 돌 무더기를 씬에서 선택하세요 (여러 개를 선택하면 하나의 무더기로 봅니다)."); return; }

            // 선택한 것 전체를 하나의 무더기로 보고 경계를 합친다 (이전에 채운 RockFill 은 제외)
            Bounds b = default; bool has = false;
            foreach (var go in sel)
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    bool inFill = false;
                    for (var tt = r.transform; tt != null; tt = tt.parent) if (tt.name.StartsWith("RockFill_")) { inFill = true; break; }
                    if (inFill) continue;
                    if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
                }
            }
            if (!has) { Debug.LogWarning("[RockMass] 선택한 오브젝트에 렌더러가 없습니다."); return; }

            Undo.IncrementCurrentGroup();
            int grp = Undo.GetCurrentGroup();
            int rocks = 0, chips = 0, flowers = 0, lights = 0;
            const int clusterCount = 1;

            {
                var center = new Vector2(b.center.x, b.center.z);
                var ext = new Vector2(b.extents.x, b.extents.z);

                // 같은 자리에 이전에 채운 것이 있으면 교체 (이름에 위치가 들어 있음)
                string fillName = $"RockFill_{Mathf.RoundToInt(center.x)}_{Mathf.RoundToInt(center.y)}";
                var oldFill = GameObject.Find(fillName);
                if (oldFill != null) Undo.DestroyObjectImmediate(oldFill);
                var fill = new GameObject(fillName);
                Undo.RegisterCreatedObjectUndo(fill, "Fill rock mass");
                var propsParent = GameObject.Find("Props");
                if (propsParent != null) fill.transform.SetParent(propsParent.transform, false);

                var rng = new System.Random(Mathf.RoundToInt(center.x * 10f) ^ Mathf.RoundToInt(center.y * 7f) ^ 4411);
                float R() => (float)rng.NextDouble();
                float L(float lo, float hi) => Mathf.Lerp(lo, hi, R());

                Vector3 Ground(Vector2 p)
                {
                    var w = new Vector3(p.x, 0f, p.y);
                    return new Vector3(p.x, terrain.SampleHeight(w) + terrain.transform.position.y, p.y);
                }
                Vector2 Ring(float lo, float hi)
                {
                    float a = R() * Mathf.PI * 2f;
                    float k = L(lo, hi);
                    return center + new Vector2(Mathf.Cos(a) * (ext.x + 0.3f) * k, Mathf.Sin(a) * (ext.y + 0.3f) * k);
                }
                bool Ok(Vector2 p) => Inside(p) && DistToPath(p) > 3.2f && DistToEdge(p) > 0.8f;
                GameObject Put(GameObject prefab, Vector2 p, float sink, Vector3 scale, float tilt)
                {
                    var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, fill.transform);
                    Undo.RegisterCreatedObjectUndo(g, "Fill rock mass");
                    var gp = Ground(p); gp.y -= sink;
                    g.transform.position = gp;
                    g.transform.rotation = Quaternion.Euler(L(-tilt, tilt), R() * 360f, L(-tilt, tilt));
                    g.transform.localScale = scale;
                    foreach (var c in g.GetComponentsInChildren<Collider>()) c.enabled = false;
                    return g;
                }

                // 중간 크기 돌 5~7개: 덩어리에 붙어서, 반쯤 묻힘
                int nMid = 5 + rng.Next(0, 3);
                for (int i = 0; i < nMid; i++)
                {
                    var p = Ring(0.95f, 1.35f); if (!Ok(p)) continue;
                    float s = L(0.45f, 0.85f);
                    Put(R() < 0.5f ? rockA : (rockB != null ? rockB : rockA), p, L(0.1f, 0.3f),
                        new Vector3(s * L(0.9f, 1.3f), s * L(0.6f, 0.95f), s * L(0.9f, 1.3f)), 8f);
                    rocks++;
                }
                // 미세한 돌 10~14개: 넓게 흩뿌림
                int nChip = 10 + rng.Next(0, 5);
                for (int i = 0; i < nChip; i++)
                {
                    var p = Ring(0.85f, 1.9f); if (!Ok(p)) continue;
                    float s = L(0.07f, 0.17f);
                    Put(rockA, p, 0.02f, new Vector3(s * L(0.9f, 1.4f), s * L(0.6f, 1f), s * L(0.9f, 1.4f)), 20f);
                    chips++;
                }
                // 작은 꽃 무리: 한쪽 틈에 3~5송이를 모아서 박고, 한가운데에 파란 라이트 1개
                int nFl = 3 + rng.Next(0, 3);
                var clump = Ring(0.95f, 1.2f);
                int placedFl = 0; var sum = Vector3.zero;
                for (int i = 0; i < nFl; i++)
                {
                    var p = clump + new Vector2(L(-0.9f, 0.9f), L(-0.9f, 0.9f));
                    if (!Ok(p)) continue;
                    float s = L(0.28f, 0.5f);
                    var g = Put(flower, p, 0.03f, new Vector3(s, s * L(0.9f, 1.2f), s), 6f);
                    sum += g.transform.position; placedFl++; flowers++;
                }
                if (placedFl > 0)
                {
                    var lgo = new GameObject("FlowerLight");
                    Undo.RegisterCreatedObjectUndo(lgo, "Fill rock mass");
                    lgo.transform.SetParent(fill.transform, true);
                    lgo.transform.position = sum / placedFl + Vector3.up * 0.55f;
                    var li = lgo.AddComponent<Light>();
                    li.type = LightType.Point;
                    li.color = new Color(0.25f, 0.65f, 1f);
                    li.intensity = 2.2f;
                    li.range = 4.5f;
                    li.shadows = LightShadows.None;
                    li.lightmapBakeType = LightmapBakeType.Baked; // 실시간 비용 없이 베이크로 (라이트 개수 부담 ↓)
                    lights++;
                }
            }

            Undo.CollapseUndoOperations(grp);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[RockMass] 무더기 {clusterCount}개 주변: 중간 돌 {rocks}, 미세한 돌 {chips}, 꽃 {flowers}송이, 파란 라이트 {lights}개 (모두 콜라이더 없음, 라이트는 Baked 모드).");
        }

        [MenuItem("HJH/맵/암반 덩어리/자동 배치·채우기 결과 전부 지우기")]
        private static void ClearGenerated()
        {
            if (EditorApplication.isPlaying) return;
            int n = 0;
            // 자동 배치 묶음(RockMass_Auto)과 채우기 결과(RockFill_*, 덩어리 아래 Fill)만 지운다. 직접 놓은 RockMass 와 바위 변주 결과(Rock_Detail)는 그대로.
            var rx = new System.Text.RegularExpressions.Regex(@"^(RockMass_Auto|RockFill_.*|Fill)$");
            var list = new System.Collections.Generic.List<GameObject>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (rx.IsMatch(t.name)) list.Add(t.gameObject);
            foreach (var go in list)
            {
                if (go == null) continue;
                Undo.DestroyObjectImmediate(go);
                n++;
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[RockMass] 자동 배치/채우기 결과 {n}개 삭제 (직접 놓은 돌은 그대로).");
        }

        [MenuItem("HJH/맵/암반 덩어리/씬 뷰 중앙에 하나 놓기")]
        private static void PlaceOne()
        {
            if (EditorApplication.isPlaying) return;
            var prefab = Selection.activeObject as GameObject;
            if (prefab == null || !EditorUtility.IsPersistent(prefab) || !prefab.name.StartsWith("RockMass"))
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/RockMass_A.prefab");
            if (prefab == null) { Debug.LogWarning("[RockMass] 먼저 '프리팹 3종 만들기'를 돌리세요."); return; }

            var view = SceneView.lastActiveSceneView;
            var pos = view != null ? view.pivot : Vector3.zero;
            var terrain = Terrain.activeTerrain;
            if (terrain != null) pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(go, "Place rock mass");
            go.transform.position = pos;
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
