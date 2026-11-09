using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HJH.EditorTools
{
    // 빈 바닥(기본: 가로 팔 x30~60, z3~27)을 이동에 방해되지 않게 채운다. 메뉴: HJH/맵/다듬기/빈 바닥 채우기 (이동 방해 없음)
    //  - 전부 콜라이더 없음 (대시·탄·이동에 안 걸림), 길 가운데 폭 2m 는 비워 둠
    //  - 돌무리(중간 돌 + 작은 돌 몇 개) / 흩어진 미세한 돌 / 꽃 무리(+가끔 파란 Baked 라이트) / 뿌리·덩굴 조금
    //  - 물(이름이 Water 인 오브젝트)마다 가장자리에 돌·꽃·뿌리를 둘러 물가를 채움
    //  - 이미 있는 프랍(바위·뿌리·랜턴·꽃·암반 덩어리 등)과 겹치지 않게 피한다
    //  - 씬에서 오브젝트를 하나 이상 선택하면 그 경계를 채울 영역으로 쓴다. 결과는 Props/ArmFill 아래, 다시 돌리면 교체.
    public static class MapArmFill
    {
        private const string Folder = "Assets/Members/HJH/05.Data/MapProps/Prefabs/";
        private const string Root = "ArmFill";

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

        private static readonly Regex WaterName = new Regex(@"^(Water( \(\d+\))?|WaterPond_\d+|WaterLobe_\d+)$");
        private static readonly Regex IgnoreName = new Regex(@"Canopy|Forest|Boundary|Terrain|Water|Fog|Wall|Directional|Main|Player|Light|Spawn|Marker|Flora_|Bloom");

        [MenuItem("HJH/맵/다듬기/빈 바닥 채우기 (이동 방해 없음)")]
        private static void Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ArmFill] 플레이 중에는 못 합니다."); return; }
            var terrain = Terrain.activeTerrain;
            var rockA = Load("Rock_A"); var rockB = Load("Rock_B"); var roots = Load("Roots_A");
            var flower = Load("GlowFlowers"); var vine = Load("GlowVine");
            if (terrain == null || rockA == null || flower == null) { Debug.LogWarning("[ArmFill] Terrain 이나 Rock_A/GlowFlowers 프리팹이 없습니다."); return; }

            // 영역
            Rect area = new Rect(30f, 3f, 30f, 24f);
            var sel = Selection.gameObjects;
            if (sel.Length > 0)
            {
                Bounds sb = default; bool has = false;
                foreach (var g in sel) foreach (var r in g.GetComponentsInChildren<Renderer>()) { if (!has) { sb = r.bounds; has = true; } else sb.Encapsulate(r.bounds); }
                if (has) area = Rect.MinMaxRect(sb.min.x, sb.min.z, sb.max.x, sb.max.z);
            }

            // 이전 결과 지움
            var old = GameObject.Find(Root);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var rootGo = new GameObject(Root);
            Undo.RegisterCreatedObjectUndo(rootGo, "Arm fill");
            var props = GameObject.Find("Props");
            if (props != null) rootGo.transform.SetParent(props.transform, false);

            // 기존 프랍 영역 + 물 영역 수집
            var taken = new List<Rect>();
            var ponds = new List<Bounds>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var t = r.transform;
                if (t.IsChildOf(rootGo.transform)) continue;
                var top = t; // 이름 판정은 렌더러 자신/부모 몇 단계로
                bool isWater = false, ignore = false;
                for (var x = t; x != null; x = x.parent)
                {
                    if (WaterName.IsMatch(x.name)) isWater = true;
                    if (IgnoreName.IsMatch(x.name)) ignore = true;
                }
                if (isWater && r is MeshRenderer) { ponds.Add(r.bounds); continue; }
                if (ignore) continue;
                var b = r.bounds;
                if (b.size.x > 25f || b.size.z > 25f) continue; // 터레인급
                taken.Add(Rect.MinMaxRect(b.min.x - 0.4f, b.min.z - 0.4f, b.max.x + 0.4f, b.max.z + 0.4f));
            }

            var rng = new System.Random(20261110);
            float R() => (float)rng.NextDouble();
            float L(float lo, float hi) => Mathf.Lerp(lo, hi, R());

            Vector3 Ground(Vector2 p, float sink = 0f)
            {
                var w = new Vector3(p.x, 0f, p.y);
                return new Vector3(p.x, terrain.SampleHeight(w) + terrain.transform.position.y - sink, p.y);
            }
            bool Free(Vector2 p)
            {
                if (!Inside(p) || DistEdge(p) < 0.8f) return false;
                foreach (var tk in taken) if (tk.Contains(p)) return false;
                foreach (var pb in ponds) if (new Rect(pb.min.x, pb.min.z, pb.size.x, pb.size.z).Contains(p)) return false; // 물 위는 비움
                return true;
            }
            bool NearLane(Vector2 p, float d) => DistPath(p) < d;
            GameObject Put(GameObject prefab, Vector2 p, float sink, Vector3 scale, float tilt)
            {
                var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rootGo.transform);
                Undo.RegisterCreatedObjectUndo(g, "Arm fill");
                g.transform.position = Ground(p, sink);
                g.transform.rotation = Quaternion.Euler(L(-tilt, tilt), R() * 360f, L(-tilt, tilt));
                g.transform.localScale = scale;
                foreach (var c in g.GetComponentsInChildren<Collider>()) c.enabled = false;
                taken.Add(new Rect(p.x - scale.x * 0.5f, p.y - scale.z * 0.5f, scale.x, scale.z));
                return g;
            }
            Vector2 RandIn() => new Vector2(L(area.xMin, area.xMax), L(area.yMin, area.yMax));

            int nRock = 0, nChip = 0, nFlower = 0, nRoot = 0, nLight = 0, nEdge = 0, clumps = 0;

            // 1) 돌무리: 중간 돌 1 + 작은 돌 2~4 (모여 있는 것이 자연스럽다). 길 가운데는 비움.
            for (int c = 0, tries = 0; c < 16 && tries < 600; tries++)
            {
                var p = RandIn();
                if (!Free(p) || NearLane(p, 2.2f)) continue;
                float s = L(0.45f, 0.9f);
                Put(R() < 0.5f ? rockA : (rockB ?? rockA), p, L(0.08f, 0.22f), new Vector3(s * L(0.9f, 1.3f), s * L(0.6f, 0.95f), s * L(0.9f, 1.3f)), 8f); nRock++;
                int k = 2 + rng.Next(0, 3);
                for (int i = 0; i < k; i++)
                {
                    float a = R() * Mathf.PI * 2f, d = L(0.6f, 1.5f);
                    var q = p + new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d);
                    if (!Free(q) || NearLane(q, 1.2f)) continue;
                    float cs = L(0.12f, 0.3f);
                    Put(rockA, q, 0.03f, new Vector3(cs * L(0.9f, 1.4f), cs * L(0.6f, 1f), cs * L(0.9f, 1.4f)), 18f); nChip++;
                }
                c++;
            }

            // 2) 흩어진 미세한 돌 (길 위에도 조금 허용: 콜라이더가 없어 걸리지 않음)
            for (int c = 0, tries = 0; c < 45 && tries < 900; tries++)
            {
                var p = RandIn();
                if (!Free(p)) continue;
                float s = L(0.06f, 0.16f);
                Put(rockA, p, 0.01f, new Vector3(s * L(0.9f, 1.5f), s * L(0.6f, 1f), s * L(0.9f, 1.5f)), 25f); nChip++; c++;
            }

            // 3) 꽃 무리 (3~5송이), 3 무리마다 파란 라이트 1개 (Baked)
            for (int c = 0, tries = 0; c < 10 && tries < 600; tries++)
            {
                var p = RandIn();
                if (!Free(p) || NearLane(p, 2.0f)) continue;
                int k = 3 + rng.Next(0, 3); var sum = Vector3.zero; int ok = 0;
                for (int i = 0; i < k; i++)
                {
                    var q = p + new Vector2(L(-0.8f, 0.8f), L(-0.8f, 0.8f));
                    if (!Free(q)) continue;
                    float s = L(0.28f, 0.5f);
                    var g = Put(flower, q, 0.03f, new Vector3(s, s * L(0.9f, 1.2f), s), 6f); sum += g.transform.position; ok++; nFlower++;
                }
                if (ok == 0) continue;
                if (c % 3 == 0)
                    {
                    var lgo = new GameObject("FlowerLight"); Undo.RegisterCreatedObjectUndo(lgo, "Arm fill");
                    lgo.transform.SetParent(rootGo.transform, true);
                    lgo.transform.position = sum / ok + Vector3.up * 0.55f;
                    var li = lgo.AddComponent<Light>();
                    li.type = LightType.Point; li.color = new Color(0.25f, 0.65f, 1f); li.intensity = 2.0f; li.range = 4.5f;
                    li.shadows = LightShadows.None; li.lightmapBakeType = LightmapBakeType.Baked; nLight++;
                }
                clumps++; c++;
            }

            // 4) 뿌리 / 덩굴 조금 (낮게, 길 가운데 제외)
            for (int c = 0, tries = 0; c < 7 && tries < 400; tries++)
            {
                var p = RandIn();
                if (!Free(p) || NearLane(p, 2.6f)) continue;
                var prefab = (c % 2 == 0 && roots != null) ? roots : (vine != null ? vine : rockA);
                float s = L(0.35f, 0.6f);
                Put(prefab, p, 0.05f, new Vector3(s, s * L(0.8f, 1.1f), s), 5f); nRoot++; c++;
            }

            // 5) 물가 채우기: 각 물 둘레에 돌·꽃·뿌리를 반쯤 걸쳐 둔다
            foreach (var pb in ponds)
            {
                if (!area.Overlaps(new Rect(pb.min.x, pb.min.z, pb.size.x, pb.size.z))) continue;
                var ctr = new Vector2(pb.center.x, pb.center.z);
                var ex = new Vector2(pb.extents.x, pb.extents.z);
                int n = Mathf.Clamp(Mathf.RoundToInt((ex.x + ex.y) * 1.1f), 6, 22);
                for (int i = 0; i < n; i++)
                {
                    float a = (i + L(-0.3f, 0.3f)) / n * Mathf.PI * 2f;
                    var p = ctr + new Vector2(Mathf.Cos(a) * (ex.x + L(0.1f, 0.9f)), Mathf.Sin(a) * (ex.y + L(0.1f, 0.9f)));
                    if (!Inside(p) || DistEdge(p) < 0.8f) continue;
                    float roll = R();
                    if (roll < 0.45f)
                    { float s = L(0.2f, 0.55f); Put(rockA, p, L(0.05f, 0.15f), new Vector3(s * L(0.9f, 1.4f), s * L(0.6f, 0.95f), s * L(0.9f, 1.4f)), 10f); nEdge++; }
                    else if (roll < 0.75f)
                    { float s = L(0.3f, 0.5f); Put(flower, p, 0.03f, new Vector3(s, s, s), 6f); nEdge++; nFlower++; }
                    else if (roots != null)
                    { float s = L(0.3f, 0.5f); Put(roots, p, 0.05f, new Vector3(s, s, s), 5f); nEdge++; }
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[ArmFill] 영역 {area}: 돌무리 {nRock}개(+작은 돌 {nChip}), 꽃 {nFlower}송이/무리 {clumps}, 뿌리·덩굴 {nRoot}, 물가 {nEdge}개 ({ponds.Count}곳), 파란 라이트 {nLight}개. 전부 콜라이더 없음.");
        }

        private static GameObject Load(string n) => AssetDatabase.LoadAssetAtPath<GameObject>(Folder + n + ".prefab");

        private static bool Inside(Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = Outline.Length - 1; i < Outline.Length; j = i++)
                if ((Outline[i].y > p.y) != (Outline[j].y > p.y) &&
                    p.x < (Outline[j].x - Outline[i].x) * (p.y - Outline[i].y) / (Outline[j].y - Outline[i].y) + Outline[i].x)
                    c = !c;
            return c;
        }

        private static float DistEdge(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Outline.Length; i++) best = Mathf.Min(best, Seg(p, Outline[i], Outline[(i + 1) % Outline.Length]));
            return best;
        }

        private static float DistPath(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < PathPts.Length - 1; i++) best = Mathf.Min(best, Seg(p, PathPts[i], PathPts[i + 1]));
            return best;
        }

        private static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
