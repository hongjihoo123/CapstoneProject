using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using M = HJH.EditorTools.MapSpecBuilder;

namespace HJH.EditorTools
{
    // 늪지대 소품: 야광 버섯(코드로 만든 저폴리 모델 3종, 발광 머티리얼) + 쓰러진 통나무(기존 FallenLog 프리팹 재사용). 메뉴: HJH/맵/늪지대/...
    public static class MapSwampProps
    {
        private const string Dir = "Assets/Members/HJH/05.Data/MapProps/Mushrooms/";
        private const string LogPrefab = "Assets/Members/HJH/05.Data/MapProps/Prefabs/FallenLog.prefab";
        private const string Group = "Swamp_Props2";

        // ───────────────────────── 버섯 모델 생성 ─────────────────────────
        private static void AddLathe(List<Vector3> v, List<int> tris, Vector3 basePos, Quaternion rot, float[] rs, float[] ys, int seg, bool closeBottom)
        {
            int start = v.Count;
            for (int i = 0; i < rs.Length; i++)
                for (int s = 0; s < seg; s++)
                {
                    float a = s / (float)seg * Mathf.PI * 2f;
                    v.Add(basePos + rot * new Vector3(Mathf.Cos(a) * rs[i], ys[i], Mathf.Sin(a) * rs[i]));
                }
            for (int i = 0; i < rs.Length - 1; i++)
                for (int s = 0; s < seg; s++)
                {
                    int a0 = start + i * seg + s, a1 = start + i * seg + (s + 1) % seg, b0 = start + (i + 1) * seg + s, b1 = start + (i + 1) * seg + (s + 1) % seg;
                    tris.Add(a0); tris.Add(b0); tris.Add(a1); tris.Add(a1); tris.Add(b0); tris.Add(b1);
                }
            if (closeBottom)   // 맨 위(마지막 링)를 중심점으로 닫음
            {
                int c = v.Count; v.Add(basePos + rot * new Vector3(0f, ys[ys.Length - 1], 0f));
                int last = start + (rs.Length - 1) * seg;
                for (int s = 0; s < seg; s++) { tris.Add(last + s); tris.Add(c); tris.Add(last + (s + 1) % seg); }
            }
        }

        private static Mesh BuildCluster(string name, int seed, int count, float scale)
        {
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            float L(float lo, float hi) => Mathf.Lerp(lo, hi, R());
            var sv = new List<Vector3>(); var st = new List<int>();     // 줄기
            var cv = new List<Vector3>(); var ct = new List<int>();     // 갓
            for (int i = 0; i < count; i++)
            {
                float s = scale * (i == 0 ? L(1.0f, 1.25f) : L(0.45f, 0.95f));
                float ang = R() * 6.28f, dist = i == 0 ? 0f : L(0.12f, 0.38f) * scale;
                var pos = new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                var rot = Quaternion.Euler(L(-14f, 14f), R() * 360f, L(-14f, 14f));
                float h = 0.30f * s, r0 = 0.055f * s, r1 = 0.036f * s;
                // 줄기: 아래에서 위로 가늘어짐
                AddLathe(sv, st, pos, rot, new[] { r0 * 1.25f, r0, (r0 + r1) * 0.5f, r1 }, new[] { 0f, h * 0.12f, h * 0.6f, h }, 8, true);
                // 갓: 납작한 반구 (위로 볼록), 아랫면은 줄기 위에서 시작
                float Rc = 0.20f * s, capH = 0.11f * s;
                var rs = new List<float>(); var ys = new List<float>();
                rs.Add(r1); ys.Add(h * 0.98f);                           // 줄기 윗부분에서 시작 (아랫면)
                rs.Add(Rc * 0.92f); ys.Add(h + capH * 0.18f);
                rs.Add(Rc); ys.Add(h + capH * 0.42f);
                rs.Add(Rc * 0.88f); ys.Add(h + capH * 0.78f);
                rs.Add(Rc * 0.55f); ys.Add(h + capH * 0.97f);
                rs.Add(Rc * 0.22f); ys.Add(h + capH);
                AddLathe(cv, ct, pos, rot, rs.ToArray(), ys.ToArray(), 10, true);
            }
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt16 };
            var all = new List<Vector3>(sv); int cOff = all.Count; all.AddRange(cv);
            mesh.SetVertices(all);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(st, 0);
            var ct2 = new List<int>(); foreach (var t in ct) ct2.Add(t + cOff);
            mesh.SetTriangles(ct2, 1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static Material Mat(string name, Color baseCol, Color emission, float smooth)
        {
            string path = Dir + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", baseCol); m.SetFloat("_Smoothness", smooth);
            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        [MenuItem("HJH/맵/늪지대/야광 버섯 프리팹 만들기 (3종)")]
        private static void BuildMushrooms()
        {
            if (EditorApplication.isPlaying) return;
            Directory.CreateDirectory(Dir);
            var stem = Mat("M_Mushroom_Stem", new Color(0.52f, 0.60f, 0.58f), Color.black, 0.1f);
            var teal = Mat("M_Mushroom_CapTeal", new Color(0.08f, 0.30f, 0.32f), new Color(0.15f, 1.5f, 1.35f), 0.25f);
            var violet = Mat("M_Mushroom_CapViolet", new Color(0.20f, 0.10f, 0.30f), new Color(1.1f, 0.35f, 1.7f), 0.25f);
            var defs = new[] { ("Mushroom_A", 31, 5, 1.0f, teal), ("Mushroom_B", 57, 4, 0.8f, violet), ("Mushroom_C", 83, 6, 1.2f, teal) };
            foreach (var (name, seed, count, scale, cap) in defs)
            {
                var mesh = BuildCluster(name, seed, count, scale);
                string mp = Dir + name + "_Mesh.asset";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(mp) != null) AssetDatabase.DeleteAsset(mp);
                AssetDatabase.CreateAsset(mesh, mp);
                var root = new GameObject(name);
                var model = new GameObject("Model"); model.transform.SetParent(root.transform, false);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = model.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { stem, cap };
                mr.shadowCastingMode = ShadowCastingMode.Off;
                PrefabUtility.SaveAsPrefabAsset(root, Dir + name + ".prefab");
                Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Mushroom] 야광 버섯 프리팹 3종 생성: " + Dir + " (A 청록 5송이 / B 보라 4송이 / C 청록 6송이). 발광은 머티리얼 Emission 색으로 조정.");
        }

        // ───────────────────────── 놓기 ─────────────────────────
        [MenuItem("HJH/맵/늪지대/통나무 + 야광 버섯 놓기 (늪지대)")]
        private static void Place()
        {
            if (EditorApplication.isPlaying) return;
            var terrain = Terrain.activeTerrain;
            if (terrain == null || EditorSceneManager.GetActiveScene().name != "Map_Bridge_Normal") { Debug.LogWarning("[Mushroom] Map_Bridge_Normal 씬에서만 동작합니다."); return; }
            var log = AssetDatabase.LoadAssetAtPath<GameObject>(LogPrefab);
            var mush = new List<GameObject>();
            foreach (var n in new[] { "Mushroom_A", "Mushroom_B", "Mushroom_C" }) { var g = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + n + ".prefab"); if (g != null) mush.Add(g); }
            if (mush.Count == 0) { Debug.LogWarning("[Mushroom] 먼저 '야광 버섯 프리팹 만들기'를 실행하세요."); return; }
            M.UseBridgeSpec();
            float waterY = M.SpecWaterY;

            var old = GameObject.Find(Group); if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject(Group); Undo.RegisterCreatedObjectUndo(root, "Swamp props");
            var swamp = GameObject.Find("Swamp"); if (swamp != null) root.transform.SetParent(swamp.transform, false);

            // 웅덩이 정보: 물 판에서 중심과 대략적 반경
            var pools = new List<(Vector2 c, float r)>();
            var wg = GameObject.Find("Swamp_Water");
            if (wg != null)
                foreach (Transform w in wg.transform)
                { float side = w.localScale.x * 10f; pools.Add((new Vector2(w.position.x, w.position.z), Mathf.Max(1.5f, (side - 1f) / 3.1f / 1.45f * 1.15f))); }
            if (pools.Count == 0) { Debug.LogWarning("[Mushroom] 늪지대 웅덩이가 없습니다. 먼저 '다리 건너편 늪지대 꾸미기'를 실행하세요."); return; }

            var rng = new System.Random(20261112);
            float R() => (float)rng.NextDouble();
            float L(float lo, float hi) => Mathf.Lerp(lo, hi, R());
            float Ground(Vector2 p) => terrain.SampleHeight(new Vector3(p.x, 0, p.y)) + terrain.transform.position.y;
            bool Ok(Vector2 p, float lane) => M.InEnd(p) && M.EndDepth(p) > 0.8f && M.RouteDist(p) > lane && Ground(p) > waterY + 0.05f;

            int logs = 0, clusters = 0;
            void Cluster(Vector2 p)
            {
                if (!Ok(p, 1.6f)) return;
                var g = (GameObject)PrefabUtility.InstantiatePrefab(mush[rng.Next(mush.Count)], root.transform);
                g.transform.position = new Vector3(p.x, Ground(p) - 0.02f, p.y);
                g.transform.rotation = Quaternion.Euler(0f, R() * 360f, 0f);
                g.transform.localScale = Vector3.one * L(0.8f, 1.4f);
                clusters++;
            }

            foreach (var (c, r) in pools)
            {
                // 통나무: 웅덩이 둑에 접선 방향으로 (일부 물에 걸침)
                if (log != null && logs < 8 && r >= 2.2f && R() < 0.8f)
                {
                    float ang = R() * 6.28f; var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    var p = c + dir * r * L(1.0f, 1.2f);
                    if (Ok(p, 2.4f) || (M.InEnd(p) && M.RouteDist(p) > 2.4f))
                    {
                        var g = (GameObject)PrefabUtility.InstantiatePrefab(log, root.transform);
                        float yaw = Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;     // 로컬 길이 방향을 접선에 맞춤
                        g.transform.position = new Vector3(p.x, Ground(p) - 0.12f, p.y);
                        g.transform.rotation = Quaternion.Euler(L(-4f, 4f), yaw + L(-15f, 15f), L(-6f, 6f));
                        g.transform.localScale = Vector3.one * L(0.8f, 1.3f);
                        foreach (var col in g.GetComponentsInChildren<Collider>()) col.enabled = false;   // 이동 방해 없음
                        logs++;
                        var tan = new Vector2(-dir.y, dir.x);
                        Cluster(p + tan * L(1.4f, 2.2f) + dir * 0.6f); Cluster(p - tan * L(1.4f, 2.2f) + dir * 0.6f);   // 통나무 양끝에 버섯
                    }
                }
                // 둑 버섯
                int n = 2 + rng.Next(0, 3);
                for (int i = 0; i < n; i++)
                {
                    float a = R() * 6.28f; Cluster(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * L(0.95f, 1.35f));
                }
            }
            // 맹그로브 줄기 곁
            var trees = GameObject.Find("Swamp_Trees");
            if (trees != null)
                for (int i = 0, tries = 0; i < 16 && tries < 100 && trees.transform.childCount > 0; tries++)
                {
                    var t = trees.transform.GetChild(rng.Next(trees.transform.childCount));
                    float a = R() * 6.28f; var p = new Vector2(t.position.x, t.position.z) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * L(1.1f, 2.2f);
                    int before = clusters; Cluster(p); if (clusters > before) i++;
                }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Mushroom] 통나무 {logs}개 + 야광 버섯 무리 {clusters}곳을 늪지대에 놓았습니다 ({Group}). 통나무는 기존 FallenLog(Tripo) 재사용, 콜라이더 없음.");
        }
    }
}
