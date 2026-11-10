using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using E = HJH.EditorTools.MapEdgePolish;

namespace HJH.EditorTools
{
    // 1) 내가 판 연못 지우기: Ponds_Extra 의 WaterPond_* 자리를 주변 높이로 메우고 물 판을 지운다.
    // 2) 기존 웅덩이 옆으로 넓히기: 이미 파여 있는 웅덩이(수면 아래 지형)를 찾아, 그 가장자리에 곡선 덩어리를 붙여 파서 자연스럽게 키운다.
    // 3) 메인 광장 지형 꾸미기: 코너 광장(0~30, 0~30)에 Shape of Dreams 식 단차(가장자리 2단 턱) + 완만한 굴곡 + 바닥 얼룩을 만든다.
    //    지형이 바뀌면 그 위의 프랍(돌, 나무, 랜턴, 스폰 표식)을 원래 묻힌 깊이 그대로 새 지면에 다시 맞춘다. 중앙 길·스폰 주변은 평평하게 둔다.
    public static class MapArenaPolish
    {
        private const string WaterMat = "Assets/Members/HJH/09.JungleWater/Generated/Materials/M_JungleWater.mat";
        private const string LobeGroup = "Ponds_Lobes";
        private static readonly string BackupPath = "Library/MapL_ArenaHeights.bin";

        private static readonly Rect Plaza = new Rect(-1f, -1f, 34f, 34f);

        private static bool Guard()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Arena] 플레이 중에는 못 합니다."); return false; }
            return Terrain.activeTerrain != null;
        }

        private static float WaterY()
        {
            var w = GameObject.Find("Water");
            return w != null ? w.transform.position.y : -0.12f;
        }

        private static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - a) / (b - a)));

        // ───────────────────────── 1. 내 연못 지우기 ─────────────────────────
        private static void RemoveMine()
        {
            if (!Guard()) return;
            var terrain = Terrain.activeTerrain;
            var group = GameObject.Find("Ponds_Extra");
            if (group == null) { Debug.Log("[Arena] Ponds_Extra 가 없습니다. 지울 것이 없습니다."); return; }

            var data = terrain.terrainData;
            int res = data.heightmapResolution;
            var origin = terrain.transform.position; var size = data.size;
            Undo.RegisterCompleteObjectUndo(data, "Remove ponds");
            var h = data.GetHeights(0, 0, res, res);
            int n = 0;
            foreach (Transform pl in group.transform)
            {
                var c = new Vector2(pl.position.x, pl.position.z);
                float half = pl.localScale.x * 5f;      // Plane 은 10m 기준
                float ring = 0f; int cnt = 0;
                for (int i = 0; i < 24; i++)
                {
                    float a = i / 24f * Mathf.PI * 2f;
                    var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (half * 1.02f);
                    ring += terrain.SampleHeight(new Vector3(p.x, 0, p.y)) + origin.y; cnt++;
                }
                float hr = ring / cnt;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((c.x - half - origin.x) / size.x * (res - 1)));
                int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((c.x + half - origin.x) / size.x * (res - 1)));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((c.y - half - origin.z) / size.z * (res - 1)));
                int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((c.y + half - origin.z) / size.z * (res - 1)));
                for (int zi = z0; zi <= z1; zi++)
                    for (int xi = x0; xi <= x1; xi++)
                    {
                        var w = new Vector2(origin.x + xi / (float)(res - 1) * size.x, origin.z + zi / (float)(res - 1) * size.z);
                        float d = Vector2.Distance(w, c);
                        if (d > half) continue;
                        float h0 = origin.y + h[zi, xi] * size.y;
                        float k = Smooth(half * 0.75f, half, d);            // 가장자리는 원래 높이로 자연스럽게 이어짐
                        float target = hr + (Mathf.PerlinNoise(w.x * 0.4f, w.y * 0.4f) - 0.5f) * 0.06f;
                        h[zi, xi] = Mathf.Clamp01((Mathf.Lerp(target, h0, k) - origin.y) / size.y);
                    }
                n++;
            }
            data.SetHeights(0, 0, h);
            EditorUtility.SetDirty(data);
            Undo.DestroyObjectImmediate(group);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Arena] 내가 판 연못 {n}곳을 메우고 Ponds_Extra 를 지웠습니다.");
        }

        // ───────────────────────── 2. 기존 웅덩이 옆으로 넓히기 ─────────────────────────
        private static void WidenExisting()
        {
            if (!Guard()) return;
            var terrain = Terrain.activeTerrain;
            var data = terrain.terrainData;
            int res = data.heightmapResolution;
            var origin = terrain.transform.position; var size = data.size;
            float waterY = WaterY();
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>(WaterMat);

            var h = data.GetHeights(0, 0, res, res);
            // 파여 있는 칸(수면보다 낮은 칸) 찾아서 덩어리로 묶는다
            var label = new int[res, res];
            var comps = new List<List<Vector2Int>>();
            for (int zi = 0; zi < res; zi++)
                for (int xi = 0; xi < res; xi++)
                {
                    if (label[zi, xi] != 0 || origin.y + h[zi, xi] * size.y > waterY - 0.03f) continue;
                    var comp = new List<Vector2Int>(); var q = new Queue<Vector2Int>();
                    q.Enqueue(new Vector2Int(xi, zi)); label[zi, xi] = comps.Count + 1;
                    while (q.Count > 0)
                    {
                        var c = q.Dequeue(); comp.Add(c);
                        foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) })
                        {
                            int nx = c.x + d.x, nz = c.y + d.y;
                            if (nx < 0 || nz < 0 || nx >= res || nz >= res || label[nz, nx] != 0) continue;
                            if (origin.y + h[nz, nx] * size.y > waterY - 0.03f) continue;
                            label[nz, nx] = comps.Count + 1; q.Enqueue(new Vector2Int(nx, nz));
                        }
                    }
                    if (comp.Count >= 24) comps.Add(comp); else foreach (var c in comp) label[c.y, c.x] = -1;
                }
            if (comps.Count == 0) { Debug.LogWarning("[Arena] 파여 있는 웅덩이를 못 찾았습니다."); return; }

            var avoidPts = new List<Vector2>(); var lanternPts = new List<Vector2>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                string nm = t.name;
                if (nm.Contains("Portal") || nm.Contains("Spawn")) avoidPts.Add(new Vector2(t.position.x, t.position.z));
                else if (nm.Contains("Lantern")) lanternPts.Add(new Vector2(t.position.x, t.position.z));
            }

            var rng = new System.Random(Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f) & 0xFFFF);
            float R() => (float)rng.NextDouble();
            float L(float lo, float hi) => Mathf.Lerp(lo, hi, R());

            Vector2 Cell(Vector2Int c) => new Vector2(origin.x + c.x / (float)(res - 1) * size.x, origin.z + c.y / (float)(res - 1) * size.z);

            var lobes = new List<E.Pond>();
            foreach (var comp in comps)
            {
                var cen = Vector2.zero; foreach (var c in comp) cen += Cell(c); cen /= comp.Count;
                // 경계 칸
                var border = new List<Vector2Int>();
                foreach (var c in comp)
                    foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) })
                    {
                        int nx = c.x + d.x, nz = c.y + d.y;
                        if (nx < 0 || nz < 0 || nx >= res || nz >= res) continue;
                        if (label[nz, nx] != label[c.y, c.x]) { border.Add(c); break; }
                    }
                if (border.Count == 0) continue;
                int want = Mathf.Clamp(2 + comp.Count / 400, 2, 4);
                for (int k = 0, tries = 0; k < want && tries < 80; tries++)
                {
                    var bc = Cell(border[rng.Next(border.Count)]);
                    var dir = (bc - cen); if (dir.sqrMagnitude < 0.01f) continue; dir.Normalize();
                    var pond = new E.Pond { ax = L(1.4f, 3.0f), az = L(1.0f, 2.0f), yaw = R() * Mathf.PI, p1 = R() * 6.28f, p2 = R() * 6.28f, p3 = R() * 6.28f };
                    pond.c = bc + dir * (Mathf.Max(pond.ax, pond.az) * L(0.55f, 0.95f)); // 가장자리에 걸치게
                    if (!E.Inside(pond.c) || E.DistEdge(pond.c) < 2.0f) continue;
                    if (E.DistPath(pond.c) < 3.0f + pond.R * 0.4f) continue;
                    bool bad = false;
                    foreach (var a in avoidPts) if (Vector2.Distance(a, pond.c) < pond.R + 2.5f) { bad = true; break; }
                    if (bad) continue;
                    foreach (var lp in lanternPts) if (Vector2.Distance(lp, pond.c) < pond.R * 0.5f + 1f) { bad = true; break; }
                    if (bad) continue;
                    foreach (var o in lobes) if (Vector2.Distance(o.c, pond.c) < (o.R + pond.R) * 0.55f) { bad = true; break; }
                    if (bad) continue;
                    lobes.Add(pond); k++;
                }
            }
            if (lobes.Count == 0) { Debug.LogWarning("[Arena] 붙일 자리를 못 찾았습니다 (길·랜턴·포탈에 막힘)."); return; }

            Undo.RegisterCompleteObjectUndo(data, "Widen ponds");
            const float bank = 0.55f;
            foreach (var pnd in lobes)
            {
                float reach = pnd.R * (1f + bank) + 1f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((pnd.c.x - reach - origin.x) / size.x * (res - 1)));
                int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((pnd.c.x + reach - origin.x) / size.x * (res - 1)));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((pnd.c.y - reach - origin.z) / size.z * (res - 1)));
                int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((pnd.c.y + reach - origin.z) / size.z * (res - 1)));
                for (int zi = z0; zi <= z1; zi++)
                    for (int xi = x0; xi <= x1; xi++)
                    {
                        var w = Cell(new Vector2Int(xi, zi));
                        float u = E.U(pnd, w);
                        if (u > 1f + bank) continue;
                        float h0 = origin.y + h[zi, xi] * size.y;
                        float noise = (Mathf.PerlinNoise(w.x * 0.9f, w.y * 0.9f) - 0.5f) * 0.08f;
                        float shore = waterY - 0.08f;
                        float bottom = shore - 0.38f * (1f - Mathf.Clamp01(u * u)) + noise;
                        float newH = u <= 1f ? Mathf.Min(h0, bottom)
                                             : Mathf.Lerp(Mathf.Min(h0, shore), h0, Smooth(1f, 1f + bank, u));
                        h[zi, xi] = Mathf.Clamp01((newH - origin.y) / size.y);
                    }
            }
            data.SetHeights(0, 0, h);
            EditorUtility.SetDirty(data);

            var group = GameObject.Find(LobeGroup);
            if (group == null)
            {
                group = new GameObject(LobeGroup);
                Undo.RegisterCreatedObjectUndo(group, "Lobes");
                var props = GameObject.Find("Props");
                if (props != null) group.transform.SetParent(props.transform, false);
            }
            int idx = group.transform.childCount;
            foreach (var pnd in lobes)
            {
                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Undo.RegisterCreatedObjectUndo(plane, "Lobes");
                plane.name = "WaterLobe_" + (idx++);
                plane.transform.SetParent(group.transform, false);
                float side = pnd.R * (2f + bank * 2f) + 1f;
                plane.transform.position = new Vector3(pnd.c.x, waterY + 0.002f, pnd.c.y);
                plane.transform.localScale = new Vector3(side / 10f, 1f, side / 10f);
                Object.DestroyImmediate(plane.GetComponent<Collider>());
                var mr = plane.GetComponent<MeshRenderer>();
                if (waterMat != null) mr.sharedMaterial = waterMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Arena] 기존 웅덩이 {comps.Count}곳 가장자리에 곡선 덩어리 {lobes.Count}개를 이어서 팠습니다 (Ponds_Lobes). 마음에 안 들면 Ctrl+Z.");
        }

        // ───────────────────────── 3. 메인 광장 지형 ─────────────────────────
        private static float Raise(Vector2 p)
        {
            if (!E.Inside(p)) return 0f;
            float d = E.DistEdge(p) + (Mathf.PerlinNoise(p.x * 0.12f + 5f, p.y * 0.12f + 9f) - 0.5f) * 3.2f; // 등고선이 구불구불
            float step1 = 1f - Smooth(5.4f, 6.8f, d);   // 바깥쪽 1단 (+0.55m)
            float step2 = 1f - Smooth(2.6f, 4.0f, d);   // 안쪽 2단 (+0.60m)
            float rim = (Mathf.PerlinNoise(p.x * 0.5f, p.y * 0.5f) - 0.5f) * 0.16f * (step1 > 0.05f ? 1f : 0f);
            return 0.55f * step1 + 0.60f * step2 + rim;
        }

        private static float Undulate(Vector2 p)
        {
            float d = E.DistEdge(p);
            return (Mathf.PerlinNoise(p.x * 0.07f + 10f, p.y * 0.07f + 3f) - 0.5f) * 0.22f * Smooth(6f, 9f, d);
        }

        private static void Arena()
        {
            if (!Guard()) return;
            var terrain = Terrain.activeTerrain;
            var data = terrain.terrainData;
            int res = data.heightmapResolution;
            var origin = terrain.transform.position; var size = data.size;
            float waterY = WaterY();

            var protect = new List<Vector2>();   // 스폰·포탈 주변은 평평하게
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if ((t.name.Contains("Spawn") && !t.name.StartsWith("EnemySpawns")) || t.name.Contains("Portal")) protect.Add(new Vector2(t.position.x, t.position.z));

            float Mask(Vector2 p)
            {
                float m = Smooth(3.5f, 6.0f, E.DistPath(p));
                foreach (var q in protect) m = Mathf.Min(m, Smooth(2.5f, 5f, Vector2.Distance(q, p)));
                return m;
            }

            // 인덱스 범위
            int x0 = Mathf.Max(0, Mathf.FloorToInt((Plaza.xMin - origin.x) / size.x * (res - 1)));
            int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((Plaza.xMax - origin.x) / size.x * (res - 1)));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((Plaza.yMin - origin.z) / size.z * (res - 1)));
            int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((Plaza.yMax - origin.z) / size.z * (res - 1)));

            var h = data.GetHeights(0, 0, res, res);

            // 다시 실행해도 높이가 쌓이지 않게: 처음 실행 때의 광장 높이를 저장해 두고 매번 거기서 시작
            if (File.Exists(BackupPath))
            {
                using (var br = new BinaryReader(File.OpenRead(BackupPath)))
                {
                    int bx0 = br.ReadInt32(), bx1 = br.ReadInt32(), bz0 = br.ReadInt32(), bz1 = br.ReadInt32();
                    if (bx0 == x0 && bx1 == x1 && bz0 == z0 && bz1 == z1)
                        for (int zi = z0; zi <= z1; zi++) for (int xi = x0; xi <= x1; xi++) h[zi, xi] = br.ReadSingle();
                }
            }
            else
            {
                using (var bw = new BinaryWriter(File.Create(BackupPath)))
                {
                    bw.Write(x0); bw.Write(x1); bw.Write(z0); bw.Write(z1);
                    for (int zi = z0; zi <= z1; zi++) for (int xi = x0; xi <= x1; xi++) bw.Write(h[zi, xi]);
                }
            }

            // 프랍 재배치 준비: 지금 지면 기준 묻힌 깊이를 기억
            var snaps = new List<(Transform t, float off)>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                string nm = t.name;
                if (nm.StartsWith("Wall_") || nm.Contains("Player") || nm.Contains("Camera") || nm.Contains("Water") || nm.Contains("Fog") ||
                    nm.Contains("Terrain") || nm.Contains("Directional") || nm.StartsWith("Boundary_Walls") || nm == "Props" || nm == "Map_L_Normal") continue;
                var go = t.gameObject;
                bool root = PrefabUtility.IsPartOfPrefabInstance(go) ? PrefabUtility.IsOutermostPrefabInstanceRoot(go)
                                                                     : (go.GetComponent<Renderer>() != null && t.parent != null && t.parent.GetComponent<Renderer>() == null);
                if (!root) continue;
                if (!Plaza.Contains(new Vector2(t.position.x, t.position.z))) continue;
                float ground = terrain.SampleHeight(new Vector3(t.position.x, 0, t.position.z)) + origin.y;
                snaps.Add((t, t.position.y - ground));
            }

            Undo.RegisterCompleteObjectUndo(data, "Arena terrain");
            for (int zi = z0; zi <= z1; zi++)
                for (int xi = x0; xi <= x1; xi++)
                {
                    var p = new Vector2(origin.x + xi / (float)(res - 1) * size.x, origin.z + zi / (float)(res - 1) * size.z);
                    if (!E.Inside(p)) continue;
                    float h0 = origin.y + h[zi, xi] * size.y;
                    if (h0 < waterY + 0.2f) continue;                       // 웅덩이는 메우지 않음
                    float dh = (Raise(p) + Undulate(p)) * Mask(p);
                    h[zi, xi] = Mathf.Clamp01((h0 + dh - origin.y) / size.y);
                }
            data.SetHeights(0, 0, h);

            // 프랍을 새 지면에 다시 맞춤
            int moved = 0;
            foreach (var (t, off) in snaps)
            {
                if (t == null) continue;
                float ng = terrain.SampleHeight(new Vector3(t.position.x, 0, t.position.z)) + origin.y;
                var pos = t.position; float ny = ng + off;
                if (Mathf.Abs(pos.y - ny) < 0.01f) continue;
                Undo.RecordObject(t, "Arena snap");
                pos.y = ny; t.position = pos; moved++;
            }

            // 바닥 얼룩: 진흙·젖은 이끼 얼룩 + 턱 비탈은 뿌리/바위 질감 (max 로 합성하므로 다시 실행해도 같은 결과)
            if (data.alphamapLayers >= 4)
            {
                int ar = data.alphamapResolution;
                var map = data.GetAlphamaps(0, 0, ar, ar);
                for (int zi = 0; zi < ar; zi++)
                    for (int xi = 0; xi < ar; xi++)
                    {
                        var p = new Vector2(origin.x + (xi + 0.5f) / ar * size.x, origin.z + (zi + 0.5f) / ar * size.z);
                        if (!Plaza.Contains(p) || !E.Inside(p)) continue;
                        float m = Mask(p);
                        float mud = Smooth(0.58f, 0.78f, Mathf.PerlinNoise(p.x * 0.09f + 40f, p.y * 0.09f + 40f)) * 0.5f * m;
                        float wet = Smooth(0.62f, 0.82f, Mathf.PerlinNoise(p.x * 0.14f + 77f, p.y * 0.14f + 12f)) * 0.4f * m;
                        float rock = Smooth(0.25f, 0.9f, Raise(p) / 1.15f) * 0.75f;
                        map[zi, xi, 1] = Mathf.Max(map[zi, xi, 1], mud);
                        map[zi, xi, 2] = Mathf.Max(map[zi, xi, 2], wet);
                        map[zi, xi, 3] = Mathf.Max(map[zi, xi, 3], rock);
                        float sum = 0f; for (int l = 0; l < 4; l++) sum += map[zi, xi, l];
                        if (sum > 0f) for (int l = 0; l < 4; l++) map[zi, xi, l] /= sum;
                    }
                data.SetAlphamaps(0, 0, map);
            }

            EditorUtility.SetDirty(data);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Arena] 메인 광장: 가장자리 2단 턱 + 굴곡 + 바닥 얼룩 적용, 프랍 {moved}개를 새 지면에 다시 맞춤. 다시 실행하면 처음 상태에서 새로 만듭니다(백업: {BackupPath}). 되돌리려면 Ctrl+Z.");
        }
    }
}
