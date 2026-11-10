using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HJH.EditorTools
{
    // 가장자리 다듬기 (지형 깎기 허용, 중앙 길은 손대지 않음). 메뉴: HJH/맵/가장자리/...
    //  1) 연못 더 뿌리기: 가장자리 띠(외곽선에서 3~12m)에만 자연스러운 곡선 웅덩이를 지형에 깎고 물 판을 깐다. 길(2번의 PathPts) 4.5m 안은 건드리지 않음.
    //  2) 나무 가까이: 외곽선 안쪽 0.8~2.8m 에 나무를 한 줄 더 (콜라이더 없음, 길·연못·포탈·스폰 주변은 피함).
    //  3) 흙길 옅게: 길 칠(진흙 레이어)을 40% 이하로 줄이고 이끼로 돌려준다 (다시 돌려도 같은 결과).
    public static class MapEdgePolish
    {
        private const string MatPath = "Assets/Members/HJH/09.JungleWater/Generated/Materials/M_JungleWater.mat";
        private const string TreeFallback = "Assets/Members/KM/Assets/Props/Natural/Rock/Plane.002 (1) 1.prefab";
        private const string PondGroup = "Ponds_Extra";
        private const string TreeGroup = "Trees_Near";

        private static readonly Vector2[] PathPts =
        {
            new Vector2(15, 56), new Vector2(15, 44), new Vector2(13, 32), new Vector2(15, 22),
            new Vector2(24, 17), new Vector2(34, 18), new Vector2(46, 19), new Vector2(57, 15),
        };
        private static readonly Vector2[] Outline =
        {
            new Vector2(5, 60), new Vector2(25, 60), new Vector2(25, 30), new Vector2(30, 30),
            new Vector2(30, 25), new Vector2(60, 25), new Vector2(60, 5), new Vector2(30, 5),
            new Vector2(30, 0), new Vector2(0, 0), new Vector2(0, 30), new Vector2(5, 30),
        };

        internal struct Pond { public Vector2 c; public float ax, az, yaw, p1, p2, p3; public float R => Mathf.Max(ax, az) * 1.45f; }

        private static bool Guard()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[EdgePolish] 플레이 중에는 못 합니다."); return false; }
            return true;
        }

        // 곡선 웅덩이의 경계 배율: 각도에 따라 0.7~1.4 로 일렁여서 각지지 않은 유기적 모양
        internal static float Wob(Pond p, float th)
            => 1f + 0.22f * Mathf.Sin(2f * th + p.p1) + 0.14f * Mathf.Sin(3f * th + p.p2) + 0.08f * Mathf.Sin(5f * th + p.p3);

        // 원점 대비 정규화 거리 u (u<1 = 물 안)
        internal static float U(Pond p, Vector2 w)
        {
            var d = w - p.c;
            float cs = Mathf.Cos(-p.yaw), sn = Mathf.Sin(-p.yaw);
            var l = new Vector2(d.x * cs - d.y * sn, d.x * sn + d.y * cs);
            var q = new Vector2(l.x / p.ax, l.y / p.az);
            float th = Mathf.Atan2(q.y, q.x);
            return q.magnitude / Wob(p, th);
        }

        // ───────────────────────── 1. 연못 ─────────────────────────
        private static void Ponds()
        {
            if (!Guard()) return;
            var terrain = Terrain.activeTerrain;
            if (terrain == null) { Debug.LogWarning("[EdgePolish] Terrain 이 없습니다."); return; }
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);

            // 기존 물 높이를 따른다
            float waterY = -0.12f;
            var existing = GameObject.Find("Water");
            if (existing != null) waterY = existing.transform.position.y;

            var group = GameObject.Find(PondGroup);
            if (group == null)
            {
                group = new GameObject(PondGroup);
                Undo.RegisterCreatedObjectUndo(group, "Ponds");
                var props = GameObject.Find("Props");
                if (props != null) group.transform.SetParent(props.transform, false);
            }

            // 피할 것: 기존 연못(Water*), 포탈/스폰/랜턴, 기존 프랍 렌더러
            var ponds = new List<Pond>();
            var avoidPts = new List<Vector2>();
            var lanternPts = new List<Vector2>();
            var avoidBounds = new List<Bounds>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                string n = t.name;
                if (n.Contains("Portal") || n.Contains("Spawn")) avoidPts.Add(new Vector2(t.position.x, t.position.z)); else if (n.Contains("Lantern")) lanternPts.Add(new Vector2(t.position.x, t.position.z));
            }
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                bool skip = false;
                for (var x = r.transform; x != null; x = x.parent)
                {
                    string n = x.name;
                    if (n == PondGroup || n == TreeGroup || n.Contains("Canopy") || n.Contains("Forest") || n.Contains("Boundary") ||
                        n.Contains("Fog") || n.Contains("Wall") || n.StartsWith("Flora_") || n.Contains("Bloom")) { skip = true; break; }
                }
                if (skip || r.bounds.size.x > 25f) continue;
                if (r.bounds.size.x >= 0.8f || r.bounds.size.z >= 0.8f) avoidBounds.Add(r.bounds); // 아주 작은 돌·꽃은 무시
            }
            // 기존/이전 연못은 반지름 원으로 환산해 겹침 방지
            var existingCircles = new List<(Vector2 c, float r)>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r.name == "Water" || r.name.StartsWith("WaterPond_"))
                    existingCircles.Add((new Vector2(r.bounds.center.x, r.bounds.center.z), Mathf.Max(r.bounds.extents.x, r.bounds.extents.z)));
            }

            var data = terrain.terrainData;
            int res = data.heightmapResolution;
            var origin = terrain.transform.position;
            var size = data.size;
            Undo.RegisterCompleteObjectUndo(data, "Ponds carve");
            float[,] h = data.GetHeights(0, 0, res, res);

            var rng = new System.Random(Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f) & 0xFFFF);
            float R() => (float)rng.NextDouble();
            float L(float lo, float hi) => Mathf.Lerp(lo, hi, R());

            int have = group.transform.childCount, target = 7, made = 0;
            for (int tries = 0; have + made < target && tries < 8000; tries++)
            {
                var c = new Vector2(L(1f, 59f), L(1f, 59f));
                if (!Inside(c)) continue;
                float edge = DistEdge(c);
                if (edge < 3.0f || edge > 12f) continue;       // 가장자리 띠에만 (중앙부는 손대지 않음)
                var pond = new Pond
                {
                    c = c, ax = L(1.2f, 2.8f), az = L(1.0f, 2.0f), yaw = R() * Mathf.PI,
                    p1 = R() * 6.28f, p2 = R() * 6.28f, p3 = R() * 6.28f,
                };
                float rr = pond.R + 0.8f; // 둑까지
                if (DistPath(c) < 3.5f + pond.R * 0.5f) continue;   // 길 가운데는 비움
                if (DistEdge(c) < pond.R * 0.35f) continue;
                bool bad = false;
                foreach (var a in avoidPts) if (Vector2.Distance(a, c) < pond.R + 3f) { bad = true; break; }
                if (bad) continue;
                foreach (var lp in lanternPts) if (Vector2.Distance(lp, c) < pond.R * 0.6f + 1.2f) { bad = true; break; }
                if (bad) continue;
                foreach (var e in existingCircles) if (Vector2.Distance(e.c, c) < e.r + rr + 1.5f) { bad = true; break; }
                if (bad) continue;
                foreach (var q in ponds) if (Vector2.Distance(q.c, c) < q.R + rr + 2f) { bad = true; break; }
                if (bad) continue;
                foreach (var b in avoidBounds)
                {
                    float dx = Mathf.Max(b.min.x - c.x, 0f, c.x - b.max.x), dz = Mathf.Max(b.min.z - c.y, 0f, c.y - b.max.z);
                    if (Mathf.Sqrt(dx * dx + dz * dz) < pond.R * 0.75f) { bad = true; break; }
                }
                if (bad) continue;
                ponds.Add(pond); made++;
            }

            // 지형 깎기
            float bank = 0.55f;
            foreach (var pnd in ponds)
            {
                float reach = pnd.R * (1f + bank) + 1f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((pnd.c.x - reach - origin.x) / size.x * (res - 1)));
                int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((pnd.c.x + reach - origin.x) / size.x * (res - 1)));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((pnd.c.y - reach - origin.z) / size.z * (res - 1)));
                int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((pnd.c.y + reach - origin.z) / size.z * (res - 1)));
                for (int zi = z0; zi <= z1; zi++)
                    for (int xi = x0; xi <= x1; xi++)
                    {
                        var w = new Vector2(origin.x + xi / (float)(res - 1) * size.x, origin.z + zi / (float)(res - 1) * size.z);
                        float u = U(pnd, w);
                        if (u > 1f + bank) continue;
                        float h0 = origin.y + h[zi, xi] * size.y;
                        float noise = (Mathf.PerlinNoise(w.x * 0.9f, w.y * 0.9f) - 0.5f) * 0.08f;
                        float shore = waterY - 0.08f;
                        float bottom = shore - 0.38f * (1f - Mathf.Clamp01(u * u)) + noise;
                        float newH;
                        if (u <= 1f) newH = Mathf.Min(h0, bottom);
                        else
                        {
                            float k = Mathf.SmoothStep(0f, 1f, (u - 1f) / bank);
                            newH = Mathf.Lerp(Mathf.Min(h0, shore), h0, k);
                        }
                        h[zi, xi] = Mathf.Clamp01((newH - origin.y) / size.y);
                    }
            }
            data.SetHeights(0, 0, h);
            EditorUtility.SetDirty(data);

            // 물 판: 연못마다 정사각 판 하나 (지면이 수면보다 높은 곳은 가려지고, 깎인 곳만 보인다 → 유기적 윤곽)
            int idx = have;
            foreach (var pnd in ponds)
            {
                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Undo.RegisterCreatedObjectUndo(plane, "Ponds");
                plane.name = "WaterPond_" + (idx++);
                plane.transform.SetParent(group.transform, false);
                float side = pnd.R * (2f + bank * 2f) + 1f;
                plane.transform.position = new Vector3(pnd.c.x, waterY, pnd.c.y);
                plane.transform.localScale = new Vector3(side / 10f, 1f, side / 10f);
                Object.DestroyImmediate(plane.GetComponent<Collider>());
                var mr = plane.GetComponent<MeshRenderer>();
                if (waterMat != null) mr.sharedMaterial = waterMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[EdgePolish] 연못 {made}개 추가 (이미 있던 {have}개 포함 총 {have + made}). 가장자리 띠에만 깎았고 길 쪽은 그대로. 마음에 안 들면 Ctrl+Z 또는 {PondGroup} 삭제 후 지형은 Ctrl+Z.");
        }

        // ───────────────────────── 2. 나무 가까이 ─────────────────────────
        private static void NearTrees()
        {
            if (!Guard()) return;
            var terrain = Terrain.activeTerrain;
            if (terrain == null) { Debug.LogWarning("[EdgePolish] Terrain 이 없습니다."); return; }

            // 원본 나무 프리팹들: Boundary_Trees 자식들의 원본, 없으면 KM 나무
            var sources = new List<GameObject>();
            var bt = GameObject.Find("Boundary_Trees");
            if (bt != null)
                foreach (Transform c in bt.transform)
                {
                    var src = PrefabUtility.GetCorrespondingObjectFromSource(c.gameObject);
                    if (src != null && !sources.Contains(src)) sources.Add(src);
                    if (sources.Count >= 4) break;
                }
            if (sources.Count == 0)
            {
                var f = AssetDatabase.LoadAssetAtPath<GameObject>(TreeFallback);
                if (f != null) sources.Add(f);
            }
            if (sources.Count == 0) { Debug.LogWarning("[EdgePolish] 나무 프리팹을 못 찾았습니다."); return; }

            var old = GameObject.Find(TreeGroup);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject(TreeGroup);
            Undo.RegisterCreatedObjectUndo(root, "Near trees");
            var props = GameObject.Find("Props");
            if (props != null) root.transform.SetParent(props.transform, false);

            var avoid = new List<Vector2>();
            var lant = new List<Vector2>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                string n = t.name;
                if (n.Contains("Portal") || n.Contains("Spawn")) avoid.Add(new Vector2(t.position.x, t.position.z)); else if (n.Contains("Lantern")) lant.Add(new Vector2(t.position.x, t.position.z));
            }
            var waters = new List<Bounds>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (r.name == "Water" || r.name.StartsWith("WaterPond_")) waters.Add(r.bounds);

            var rng = new System.Random(8821);
            float R() => (float)rng.NextDouble();
            float L(float lo, float hi) => Mathf.Lerp(lo, hi, R());

            int placed = 0;
            var placedPts = new List<Vector2>();
            for (int i = 0; i < Outline.Length; i++)
            {
                var a = Outline[i]; var b = Outline[(i + 1) % Outline.Length];
                var dir = (b - a); float len = dir.magnitude; dir /= len;
                var nrm = new Vector2(-dir.y, dir.x);
                if (!Inside((a + b) * 0.5f + nrm * 0.5f)) nrm = -nrm;
                for (float s = L(0.5f, 2f); s < len; s += L(3.0f, 4.2f))
                {
                    var p = a + dir * s + nrm * L(0.8f, 2.8f);
                    if (!Inside(p) || DistEdge(p) < 0.6f) continue;
                    if (DistPath(p) < 3.5f) continue;
                    bool bad = false;
                    foreach (var q in avoid) if (Vector2.Distance(q, p) < 4f) { bad = true; break; }
                    if (bad) continue;
                    foreach (var q in lant) if (Vector2.Distance(q, p) < 1.6f) { bad = true; break; }
                    if (bad) continue;
                    foreach (var w in waters) if (new Rect(w.min.x - 1f, w.min.z - 1f, w.size.x + 2f, w.size.z + 2f).Contains(p)) { bad = true; break; }
                    if (bad) continue;
                    foreach (var q in placedPts) if (Vector2.Distance(q, p) < 2.2f) { bad = true; break; }
                    if (bad) continue;

                    var g = (GameObject)PrefabUtility.InstantiatePrefab(sources[rng.Next(0, sources.Count)], root.transform);
                    Undo.RegisterCreatedObjectUndo(g, "Near trees");
                    float gy = terrain.SampleHeight(new Vector3(p.x, 0, p.y)) + terrain.transform.position.y;
                    g.transform.position = new Vector3(p.x, gy - 0.05f, p.y);
                    g.transform.rotation = Quaternion.Euler(0f, R() * 360f, 0f);
                    float sc = L(0.85f, 1.25f);
                    g.transform.localScale = Vector3.one * sc;
                    foreach (var c in g.GetComponentsInChildren<Collider>()) c.enabled = false; // 이동 방해 없음 (바깥 안전 벽은 그대로)
                    placedPts.Add(p); placed++;
                }
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[EdgePolish] 가까운 나무 {placed}그루 추가 (원본 프리팹 {sources.Count}종, 콜라이더 없음). 너무 많으면 {TreeGroup} 의 일부를 지우세요.");
        }

        // ───────────────────────── 3. 흙길 옅게 ─────────────────────────
        private static void LightenPath()
        {
            if (!Guard()) return;
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return;
            var data = terrain.terrainData;
            if (data.alphamapLayers < 2) return;
            Undo.RegisterCompleteObjectUndo(data, "Lighten path");
            int res = data.alphamapResolution;
            var map = data.GetAlphamaps(0, 0, res, res);
            var origin = terrain.transform.position; var size = data.size;
            const float cap = 0.4f;
            int changed = 0;
            for (int zi = 0; zi < res; zi++)
                for (int xi = 0; xi < res; xi++)
                {
                    var p = new Vector2(origin.x + (xi + 0.5f) / res * size.x, origin.z + (zi + 0.5f) / res * size.z);
                    if (DistPath(p) > 4.2f) continue;
                    float m = map[zi, xi, 1];
                    if (m <= cap) continue;
                    map[zi, xi, 0] += m - cap;
                    map[zi, xi, 1] = cap;
                    changed++;
                }
            data.SetAlphamaps(0, 0, map);
            EditorUtility.SetDirty(data);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[EdgePolish] 흙길 {changed}칸을 옅게 (진흙 최대 {cap * 100:0}%, 나머지는 이끼로). 더 옅게 하려면 cap 값을 낮추세요.");
        }

        // ───────────────────────── 공용 ─────────────────────────
        internal static bool Inside(Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = Outline.Length - 1; i < Outline.Length; j = i++)
                if ((Outline[i].y > p.y) != (Outline[j].y > p.y) &&
                    p.x < (Outline[j].x - Outline[i].x) * (p.y - Outline[i].y) / (Outline[j].y - Outline[i].y) + Outline[i].x)
                    c = !c;
            return c;
        }

        internal static float DistEdge(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Outline.Length; i++) best = Mathf.Min(best, Seg(p, Outline[i], Outline[(i + 1) % Outline.Length]));
            return best;
        }

        internal static float DistPath(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < PathPts.Length - 1; i++) best = Mathf.Min(best, Seg(p, PathPts[i], PathPts[i + 1]));
            return best;
        }

        internal static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
