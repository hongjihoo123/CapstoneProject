using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HJH.EditorTools
{
    public static class MapEdgePolish
    {
        private const string MatPath = "Assets/Members/HJH/09.JungleWater/Generated/Materials/M_JungleWater.mat";
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

        internal static float Wob(Pond p, float th)
            => 1f + 0.22f * Mathf.Sin(2f * th + p.p1) + 0.14f * Mathf.Sin(3f * th + p.p2) + 0.08f * Mathf.Sin(5f * th + p.p3);

        internal static float U(Pond p, Vector2 w)
        {
            var d = w - p.c;
            float cs = Mathf.Cos(-p.yaw), sn = Mathf.Sin(-p.yaw);
            var l = new Vector2(d.x * cs - d.y * sn, d.x * sn + d.y * cs);
            var q = new Vector2(l.x / p.ax, l.y / p.az);
            float th = Mathf.Atan2(q.y, q.x);
            return q.magnitude / Wob(p, th);
        }

        private static void Ponds()
        {
            if (!Guard()) return;
            var terrain = Terrain.activeTerrain;
            if (terrain == null) { Debug.LogWarning("[EdgePolish] Terrain 이 없습니다."); return; }
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);

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
                if (r.bounds.size.x >= 0.8f || r.bounds.size.z >= 0.8f) avoidBounds.Add(r.bounds);
            }
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
                if (edge < 3.0f || edge > 12f) continue;
                var pond = new Pond
                {
                    c = c, ax = L(1.2f, 2.8f), az = L(1.0f, 2.0f), yaw = R() * Mathf.PI,
                    p1 = R() * 6.28f, p2 = R() * 6.28f, p3 = R() * 6.28f,
                };
                float rr = pond.R + 0.8f;
                if (DistPath(c) < 3.5f + pond.R * 0.5f) continue;
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
