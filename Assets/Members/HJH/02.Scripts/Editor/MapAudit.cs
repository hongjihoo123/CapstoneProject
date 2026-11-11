using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HJH.EditorTools
{
    // 읽기 전용 진단: 활성 씬의 프랍 배치를 숫자로 점검해 Captures/_audit.txt 에 적는다 (씬은 바꾸지 않음).
    //   FLOAT 떠 있음 / BURIED 묻힘 / TILT 기울어짐 / ROUTE 길 막음 / PORTAL 포탈 앞 / WATER 물속 / OVERLAP 서로 박힘 / TALL 플레이 구역의 큰 가림물
    // 메뉴: HJH/맵/진단/배치 점검
    public static class MapAudit
    {
        private const string OutPath = "Captures/_audit.txt";

        internal class Item
        {
            public GameObject go; public string name, cat, group; public Bounds b; public Vector2 xz; public float ground, ext, play, route;
            public List<string> flags = new List<string>();
        }

        internal static string Category(string n)
        {
            if (n.StartsWith("RockMass") || n.StartsWith("Rock") || n.StartsWith("Cliff") || n.StartsWith("Stone")) return "rock";
            if (n.StartsWith("Tree") || n.StartsWith("PF_JungleTree") || n.StartsWith("WallTree")) return "tree";
            if (n.StartsWith("PF_MangroveRoots") || n.StartsWith("Roots")) return "roots";
            if (n.StartsWith("Mushroom")) return "mushroom";
            if (n.StartsWith("LanternLight")) return "light";
            if (n.StartsWith("Lantern")) return "lantern";
            if (n.StartsWith("FallenLog")) return "log";
            if (n.StartsWith("GlowFlowers") || n.StartsWith("FlowerLight")) return "flower";
            if (n.StartsWith("FireflyZone")) return "fx";
            return "other";
        }

        // 활성 씬의 프랍(가장 바깥 프리팹 인스턴스, PlayerRig 제외)을 모아 측정한다. 다른 맵 도구도 쓴다.
        internal static List<Item> Collect(bool bridge)
        {
            Terrain terrain = Terrain.activeTerrain;
            var items = new List<Item>();
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                GameObject go = t.gameObject;
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;
                if (t.root.name == "PlayerRig") continue;
                var rs = go.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer) && !(r is TrailRenderer)).ToArray();
                if (rs.Length == 0) continue;
                Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                var it = new Item { go = go, name = src != null ? src.name : go.name, b = b, xz = new Vector2(t.position.x, t.position.z) };
                it.cat = Category(it.name);
                it.group = t.parent != null ? (t.parent.parent != null ? t.parent.parent.name + "/" : "") + t.parent.name : "(root)";
                it.ground = terrain != null ? terrain.SampleHeight(t.position) + terrain.transform.position.y : 0f;
                it.ext = it.cat == "tree" ? 0.5f : Mathf.Max(b.extents.x, b.extents.z);
                it.play = bridge ? MapSpecBuilder.PlayDist(it.xz) : 0f;
                it.route = bridge ? MapSpecBuilder.RouteDist(it.xz) : 99f;
                items.Add(it);
            }
            return items;
        }

        [MenuItem("HJH/맵/진단/배치 점검 (Captures\\_audit.txt)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            Scene scene = SceneManager.GetActiveScene();
            bool bridge = scene.name == "Map_Bridge_Normal";
            if (bridge) MapSpecBuilder.UseBridgeSpec();
            var sb = new StringBuilder();
            sb.AppendLine($"scene {scene.name} dirty={scene.isDirty}");
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null) sb.AppendLine($"terrain pos {V(terrain.transform.position)} size {V(terrain.terrainData.size)}");
            if (bridge) sb.AppendLine($"waterY {MapSpecBuilder.SpecWaterY:0.00}");

            sb.AppendLine("\n== hierarchy (root > child : descendants) ==");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                sb.AppendLine($"{root.name} [{root.GetComponentsInChildren<Transform>(true).Length}] pos {V(root.transform.position)} active={root.activeSelf}");
                if (root.name == "PlayerRig") continue;
                foreach (Transform c in root.transform)
                    if (root.transform.childCount <= 12) sb.AppendLine($"  {c.name} [{c.GetComponentsInChildren<Transform>(true).Length}] pos {V(c.position)}");
            }

            List<Item> items = Collect(bridge);
            Vector3? pS = Pos("Portal_Start"), pE = Pos("Portal_End");
            foreach (Item it in items)
            {
                Transform t = it.go.transform;
                bool grounded = it.cat != "fx" && it.cat != "light" && it.cat != "other";
                float gap = it.b.min.y - it.ground;
                bool onWater = bridge && MapSpecBuilder.SpecInWater(it.xz);
                bool nearBridge = bridge && MapSpecBuilder.SpecBridgeDist(it.xz) < 3f;
                if (grounded && !nearBridge)
                {
                    float floor = onWater ? Mathf.Min(it.ground, MapSpecBuilder.SpecWaterY) : it.ground;
                    if (it.b.min.y - floor > 0.3f && it.cat != "flower") it.flags.Add($"FLOAT {gap:0.00}");
                    if (it.b.max.y - it.ground < it.b.size.y * 0.25f) it.flags.Add($"BURIED top {it.b.max.y - it.ground:0.00}");
                }
                float tilt = Vector3.Angle(t.up, Vector3.up);
                if ((it.cat == "lantern" || it.cat == "mushroom" || it.cat == "tree") && tilt > 20f) it.flags.Add($"TILT {tilt:0}");
                if (bridge && it.play <= 0f)
                {
                    if (it.cat != "flower" && it.cat != "fx" && it.cat != "light" && it.route < it.ext + 1.0f && it.b.size.y > 0.5f) it.flags.Add($"ROUTE {it.route:0.0}");
                    if (it.b.size.y > 4.5f && it.cat != "tree") it.flags.Add($"TALL {it.b.size.y:0.0}");
                    if (onWater && !nearBridge && (it.cat == "lantern" || it.cat == "log")) it.flags.Add("WATER");
                }
                foreach (Vector3? p in new[] { pS, pE })
                    if (p.HasValue && it.cat != "fx" && it.cat != "light" && Vector2.Distance(it.xz, new Vector2(p.Value.x, p.Value.z)) < it.ext + 1.6f && it.name.IndexOf("Lantern") < 0)
                        it.flags.Add("PORTAL");
            }
            // 서로 박힘: 플레이 구역 근처에서만, 종류가 다른 것끼리 또는 거의 같은 자리의 같은 종류
            var near = items.Where(i => i.play < 3f && i.cat != "fx" && i.cat != "light" && i.cat != "other").ToList();
            for (int a = 0; a < near.Count; a++)
                for (int c = a + 1; c < near.Count; c++)
                {
                    Item A = near[a], B = near[c];
                    float d = Vector2.Distance(A.xz, B.xz);
                    bool same = A.cat == B.cat;
                    if (same && A.cat == "rock") continue;   // 돌끼리 뭉친 건 의도
                    float lim = same ? 0.25f * Mathf.Min(A.ext, B.ext) : 0.6f * Mathf.Max(A.ext, B.ext);
                    if (A.cat == "flower" || B.cat == "flower") lim = Mathf.Min(lim, 0.6f * (A.cat == "flower" ? B.ext : A.ext));
                    if (d < lim && A.b.Intersects(B.b)) { A.flags.Add($"OVERLAP {B.name}@{d:0.0}"); B.flags.Add($"OVERLAP {A.name}@{d:0.0}"); }
                }

            sb.AppendLine("\n== prefab summary: name cat count | height min/avg/max | in-play | flagged ==");
            foreach (var g in items.GroupBy(i => i.name).OrderByDescending(g => g.Count()))
                sb.AppendLine($"{g.Key} {g.First().cat} {g.Count()} | {g.Min(i => i.b.size.y):0.0}/{g.Average(i => i.b.size.y):0.0}/{g.Max(i => i.b.size.y):0.0} | {g.Count(i => i.play <= 0f)} | {g.Count(i => i.flags.Count > 0)}");

            sb.AppendLine("\n== flagged (play dist, route dist) ==");
            foreach (Item it in items.Where(i => i.flags.Count > 0).OrderBy(i => i.play).ThenBy(i => i.name).Take(260))
                sb.AppendLine($"[{string.Join(", ", it.flags)}] {it.name} <{it.group}/{it.go.name}> pos {V(it.go.transform.position)} size {V(it.b.size)} ground {it.ground:0.00} play {it.play:0.0} route {it.route:0.0}");

            sb.AppendLine("\n== waterfall ==");
            GameObject wf = GameObject.Find("폭포");
            if (wf != null)
                foreach (Transform c in wf.GetComponentsInChildren<Transform>(true))
                {
                    if (c.GetComponent<ParticleSystem>() != null) continue;
                    var mr = c.GetComponent<MeshRenderer>();
                    sb.AppendLine($"{c.name} pos {V(c.position)} euler {V(c.eulerAngles)} scale {V(c.lossyScale)}" + (mr != null ? $" bounds c{V(mr.bounds.center)} s{V(mr.bounds.size)}" : ""));
                }
            else sb.AppendLine("(없음)");

            sb.AppendLine("\n== lights ==");
            foreach (var g in Object.FindObjectsByType<Light>(FindObjectsSortMode.None).GroupBy(l => l.type + (l.shadows != LightShadows.None ? "+shadow" : "")))
                sb.AppendLine($"{g.Key} {g.Count()} intensity avg {g.Average(l => l.intensity):0.0} range avg {g.Average(l => l.range):0.0}");
            if (pS.HasValue) sb.AppendLine($"Portal_Start {V(pS.Value)}");
            if (pE.HasValue) sb.AppendLine($"Portal_End {V(pE.Value)}");

            Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
            File.WriteAllText(OutPath, sb.ToString());
            Debug.Log($"[Audit] 프랍 {items.Count}개 점검, 표시 {items.Count(i => i.flags.Count > 0)}개 → {Path.GetFullPath(OutPath)}");
        }

        private static Vector3? Pos(string name) { GameObject g = GameObject.Find(name); return g != null ? g.transform.position : (Vector3?)null; }
        private static string V(Vector3 v) => $"({v.x:0.0},{v.y:0.0},{v.z:0.0})";
    }
}
