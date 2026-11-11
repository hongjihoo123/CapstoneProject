using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Item = HJH.EditorTools.MapAudit.Item;

namespace HJH.EditorTools
{
    // 다리 맵에서 어색하게 놓인 작은 프랍만 가까운 빈자리로 옮긴다 (지우지 않음, Ctrl+Z 가능, 씬 파일은 먼저 백업).
    //   옮기는 이유: ROCK 큰 바위 속/밑에 묻힘, OVERLAP 다른 종류 프랍과 박힘, ROUTE 길 한가운데, PORTAL 포탈 바로 앞, FLOAT 떠 있음
    //   옮기는 대상: 꽃·버섯·작은 돌·뿌리·통나무·구역 안 나무. 큰 바위·암벽·등불·벽 나무는 그대로 둔다.
    //   꽃·버섯 옆의 점광원(GlowLight 등)은 같이 따라간다. 결과 목록: Captures/_tidy.txt
    public static class MapPropTidy
    {
        private const string OutPath = "Captures/_tidy.txt";

        internal static void BackupActiveScene()
        {
            string path = SceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            string dst = Path.Combine("_RemovedBackup", DateTime.Now.ToString("yyyy-MM-dd"), Path.GetDirectoryName(path),
                Path.GetFileNameWithoutExtension(path) + DateTime.Now.ToString("_HHmmss") + ".unity");
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(path, dst, true);
            Debug.Log("[Backup] 씬 백업(마지막 저장 상태): " + dst);
        }

        private static int Rank(Item i)
        {
            switch (i.cat)
            {
                case "flower": return 0;
                case "mushroom": return 1;
                case "rock": return (i.name.StartsWith("Rock_") && Mathf.Max(i.b.size.x, i.b.size.z) < 2.6f) ? 2 : -1;
                case "roots": return i.b.size.y > 0.05f ? 3 : -1;     // 크기 0 인 것(렌더러 없음)은 건드리지 않음
                case "log": return 4;
                case "tree": return i.name == "PF_JungleTree" ? 5 : -1;
                default: return -1;
            }
        }

        [MenuItem("HJH/맵/정리/어색한 배치 다시 놓기 (바위 속·겹침·길 위)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "Map_Bridge_Normal") { Debug.LogWarning("[Tidy] 지금은 Map_Bridge_Normal 씬에서만 됩니다 (길·물 정보가 필요)."); return; }
            BackupActiveScene();
            MapSpecBuilder.UseBridgeSpec();
            Terrain terrain = Terrain.activeTerrain;
            float Ground(Vector2 p) => terrain != null ? terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y : 0f;

            List<Item> items = MapAudit.Collect(true).Where(i => i.cat != "fx" && i.cat != "light" && i.cat != "other").ToList();
            // 큰 바위의 낱개 렌더러마다 타원(바운드의 70%)으로 "바위 속" 판정
            var rockBounds = new List<(Item owner, Bounds b)>();
            foreach (Item i in items)
                if (i.cat == "rock" && i.b.size.y > 2.5f)
                    foreach (Renderer r in i.go.GetComponentsInChildren<Renderer>()) rockBounds.Add((i, r.bounds));
            bool InRock(Vector2 p, Item self, float ground)
            {
                foreach (var (owner, b) in rockBounds)
                {
                    if (owner == self || b.max.y < ground + 0.5f) continue;
                    float dx = (p.x - b.center.x) / (b.extents.x * 0.7f + 0.01f), dz = (p.y - b.center.z) / (b.extents.z * 0.7f + 0.01f);
                    if (dx * dx + dz * dz < 1f) return true;
                }
                return false;
            }
            var portals = new[] { "Portal_Start", "Portal_End" }.Select(GameObject.Find).Where(g => g != null).Select(g => new Vector2(g.transform.position.x, g.transform.position.z)).ToList();
            float RouteNeed(Item i) => i.cat == "flower" ? 0f : i.ext * 0.5f + 1.3f;
            float Gap(Item a, Item b) => Mathf.Max(0.3f, 0.35f * (a.ext + b.ext));

            string Problem(Item it)
            {
                Transform t = it.go.transform;
                bool water = MapSpecBuilder.SpecInWater(it.xz);
                if (it.cat != "rock" && InRock(it.xz, it, it.ground)) return "ROCK";   // 작은 돌이 큰 바위 발치에 붙은 건 자연스러워서 그대로
                if (it.play <= 0f && it.b.size.y > 0.4f && it.route < RouteNeed(it) && MapSpecBuilder.SpecBridgeDist(it.xz) > 3f) return "ROUTE";
                if (portals.Any(p => Vector2.Distance(p, it.xz) < it.ext + 1.6f)) return "PORTAL";
                foreach (Item o in items)
                {
                    if (o == it || o.cat == it.cat || o.cat == "lantern" && it.cat == "flower") continue;
                    int ro = Rank(o);
                    if (ro >= 0 && ro < Rank(it)) continue;   // 더 작은 쪽이 비킨다
                    if (o.cat == "rock" && o.b.size.y > 2.5f) continue;   // 큰 바위는 위의 타원 판정으로
                    if (Vector2.Distance(o.xz, it.xz) < Gap(it, o) && o.b.Intersects(it.b)) return "OVERLAP " + o.name;
                }
                float floor = water ? Mathf.Min(it.ground, MapSpecBuilder.SpecWaterY) : it.ground;
                if (it.cat != "flower" && it.b.min.y - floor > 0.3f && MapSpecBuilder.SpecBridgeDist(it.xz) > 3f) return "FLOAT";
                return null;
            }

            bool Valid(Item it, Vector2 p, bool wasWater, bool wasPlay)
            {
                if (MapSpecBuilder.SpecInWater(p) != wasWater) return false;
                if ((MapSpecBuilder.PlayDist(p) <= 0f) != wasPlay) return false;
                if (MapSpecBuilder.SpecBridgeDist(p) < 3f) return false;
                if (wasPlay && MapSpecBuilder.RouteDist(p) < RouteNeed(it) + 0.3f) return false;
                if (portals.Any(q => Vector2.Distance(q, p) < it.ext + 3f)) return false;
                if (InRock(p, it, Ground(p))) return false;
                foreach (Item o in items)
                    if (o != it && Vector2.Distance(o.xz, p) < Mathf.Max(0.5f, 0.55f * (it.ext + o.ext))) return false;
                return true;
            }

            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Point).ToArray();
            var movedLights = new HashSet<Light>();
            var sb = new StringBuilder();
            int moved = 0, stuck = 0;
            foreach (Item it in items.Where(i => Rank(i) >= 0 && i.play < 2f).OrderBy(Rank).ThenBy(i => i.xz.x).ToList())
            {
                string why = Problem(it);
                if (why == null) continue;
                bool wasWater = MapSpecBuilder.SpecInWater(it.xz), wasPlay = it.play <= 0f;
                Vector2? spot = null;
                if (why == "FLOAT") spot = it.xz;   // 제자리에서 바닥에 붙이기만
                float phase = Mathf.Repeat(it.xz.x * 12.9898f + it.xz.y * 78.233f, Mathf.PI * 2f);   // 물체마다 다른 방향부터 (실행할 때마다 같은 결과)
                for (float r = 1f; r <= 8f && spot == null; r += 0.5f)
                    for (int k = 0; k < 12 && spot == null; k++)
                    {
                        float a = phase + k * Mathf.PI / 6f;
                        Vector2 p = it.xz + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                        if (Valid(it, p, wasWater, wasPlay)) spot = p;
                    }
                if (spot == null) { stuck++; sb.AppendLine($"STUCK [{why}] {it.name} {V(it.go.transform.position)} 8m 안에 빈자리 없음"); continue; }

                Transform t = it.go.transform;
                Vector3 old = t.position;
                float g = Ground(spot.Value);
                float newY = why == "FLOAT" ? old.y - (it.b.min.y - (wasWater ? Mathf.Min(g, MapSpecBuilder.SpecWaterY) : g)) + 0.02f : old.y - it.ground + g;
                Vector3 delta = new Vector3(spot.Value.x, newY, spot.Value.y) - old;
                Undo.RecordObject(t, "Tidy props");
                t.position = old + delta;
                if (it.cat == "flower" || it.cat == "mushroom")
                    foreach (Light l in lights)
                    {
                        if (movedLights.Contains(l) || l.transform.IsChildOf(t)) continue;
                        Vector3 lp = l.transform.position;
                        if (Vector2.Distance(new Vector2(lp.x, lp.z), it.xz) > 1.2f || Mathf.Abs(lp.y - old.y) > 2.5f) continue;
                        Undo.RecordObject(l.transform, "Tidy props");
                        l.transform.position += delta; movedLights.Add(l);
                    }
                it.xz = spot.Value; it.ground = g; it.b.center += delta;
                it.play = MapSpecBuilder.PlayDist(it.xz); it.route = MapSpecBuilder.RouteDist(it.xz);
                moved++;
                sb.AppendLine($"MOVE [{why}] {it.name} {V(old)} -> {V(t.position)} ({delta.magnitude:0.0}m)");
            }
            if (moved > 0) EditorSceneManager.MarkSceneDirty(scene);
            Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
            File.WriteAllText(OutPath, $"moved {moved} stuck {stuck} lights {movedLights.Count}\n" + sb);
            Debug.Log($"[Tidy] {moved}개 옮김, {stuck}개는 빈자리 없어 그대로, 따라간 조명 {movedLights.Count}개. 씬은 저장하지 않았습니다 (Ctrl+Z 가능). 목록: {Path.GetFullPath(OutPath)}");
        }

        [MenuItem("HJH/맵/정리/방금 한 작업 되돌리기 (Undo 한 번)")]
        public static void UndoOnce() { if (!EditorApplication.isPlaying) Undo.PerformUndo(); }

        private static string V(Vector3 v) => $"({v.x:0.0},{v.y:0.0},{v.z:0.0})";
    }
}
