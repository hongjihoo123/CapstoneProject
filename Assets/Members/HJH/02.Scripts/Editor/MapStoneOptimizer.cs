using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HJH.EditorTools
{
    // 어느 맵 씬에서나 쓰는 돌 최적화 도구 (맵 코드의 좌표를 쓰지 않음 -> 새로 만든 맵에도 그대로 적용)
    //
    // 원리
    //  1) 플레이어가 실제로 걸어갈 수 있는 구역을 씬에서 직접 구함: 플레이어 위치에서 격자로 퍼지며, 지면 높이 차가 0.6m 이하인 칸만 이동 가능 (벽·바위·가파른 언덕은 막힘)
    //  2) 그 구역 곳곳(4m 간격)에 플레이어가 서 있다고 보고 카메라 시야(PlayerRig 의 카메라 오프셋 그대로)를 만들어,
    //     돌이 한 번도 시야에 안 들어오거나, 들어와도 지형·다른 돌에 항상 가려지면 '안 보이는 돌'로 판정
    //  3) 안 보이는 돌: 구역에서 12m 넘게 멀면 꺼 둠(이름 뒤에 ~opt), 가까우면 그림자만 남기고 안 그림 (긴 그림자 보존)
    //  4) 보이는 돌: 카메라에서 본 화면 크기로 저폴리 교체 (화면 높이의 40% 이상 원본, 12% 이상 35% 폴리, 그보다 작으면 12% 폴리), 구역에서 8m 넘게 멀면 그림자 끔
    // 저폴리 메시는 ArtSource/StonePack/lod_gen.py 가 Stones/Models 폴더에 Stone_*_L1/_L2.fbx 로 만들어 둠.
    // 되돌리기: 이름의 ~opt 제거 + 켜기, 메시 원본으로, 그림자는 Library 에 적어 둔 원래 값으로.
    // 전제: 플레이어 세트(PlayerRig)가 씬에 있어야 함. 레이어 정리(돌/7)를 먼저 해 두면 가림 판정이 정확함.
    public static class MapStoneOptimizer
    {
        private const string ModelDir = "Assets/Members/HJH/05.Data/MapProps/Stones/Models/";
        private const string SceneDir = "Assets/Members/HJH/01.Scene/";
        private const string Suffix = "~opt";

        private const float WalkStep = 0.6f;          // 이웃 칸과 지면 높이 차가 이 이하면 걸어갈 수 있음
        private const float SampleSpacing = 4f;       // 플레이어가 서 있다고 가정할 자리 간격
        private const int MaxGrid = 520;
        // 저폴리 단계는 카메라에서 본 화면 크기로 정함 (돌의 가장 큰 변 / 화면 높이). 가장 가깝게 보이는 순간 기준이라 품질이 모자라지 않음
        private const float Lod0Frac = 0.40f;   // 화면 높이의 40% 이상 차지하면 원본
        private const float Lod1Frac = 0.12f;   // 12% 이상이면 35% 폴리, 그보다 작으면 12% 폴리
        private const float ShadowOffDist = 8f;
        private const float DeactivateDist = 12f;
        private const float MinLodSize = 1f;          // 이보다 작은 돌은 LOD 안 씀

        private static readonly Regex LodName = new Regex(@"_L[12]\.fbx$");
        private static readonly Dictionary<string, Mesh> MeshCache = new Dictionary<string, Mesh>();
        private static readonly HashSet<string> Warned = new HashSet<string>();
        private static readonly RaycastHit[] Buf = new RaycastHit[24];
        private static readonly Comparer<RaycastHit> ByDistance = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        private class Item
        {
            public MeshFilter mf;
            public MeshRenderer r;
            public Mesh baseMesh;
            public Bounds b;
            public float dist;
            public bool visible;
            public float camDist;   // 이 돌을 볼 수 있는 자리 중 카메라와 가장 가까운 거리
            public Mesh lodMesh;
            public int lod;
        }

        private class Grid
        {
            public int nx, nz;
            public float x0, z0, cell;
            public float[] h;
            public bool[] play;
            public float[] dist;
            public int Idx(int i, int j) => j * nx + i;
            public bool InRange(int i, int j) => i >= 0 && j >= 0 && i < nx && j < nz;
            public float DistAt(float x, float z)
            {
                int i = Mathf.FloorToInt((x - x0) / cell), j = Mathf.FloorToInt((z - z0) / cell);
                return InRange(i, j) ? dist[Idx(i, j)] : 999f;
            }
        }

        [Serializable] private class SavedEntry { public string id; public int shadow; }
        [Serializable] private class SavedFile { public List<SavedEntry> entries = new List<SavedEntry>(); }

        // ───────────────────────── 메뉴 ─────────────────────────
        [MenuItem("HJH/맵/최적화/돌 최적화 - 분석만 (지금 열린 씬)")]
        private static void MenuAnalyze() { if (!EditorApplication.isPlaying) Debug.Log(Run(false)); }

        [MenuItem("HJH/맵/최적화/돌 최적화 - 적용 (지금 열린 씬)")]
        private static void MenuApply()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Debug.Log(Run(true));
        }

        [MenuItem("HJH/맵/최적화/돌 최적화 - 되돌리기 (지금 열린 씬)")]
        private static void MenuRestore()
        {
            if (EditorApplication.isPlaying) return;
            Debug.Log($"[StoneOpt] {SceneManager.GetActiveScene().name}: {RestoreScene()}");
        }

        [MenuItem("HJH/맵/최적화/돌 최적화 - 적용 (HJH 폴더의 모든 맵 씬)")]
        private static void MenuApplyAll()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string original = SceneManager.GetActiveScene().path;
            var paths = Directory.GetFiles(SceneDir, "*.unity").Select(p => p.Replace('\\', '/'))
                .Where(p => !Path.GetFileName(p).StartsWith("Shader") && !Path.GetFileName(p).StartsWith("_Shader")).ToList();

            string backup = "_RemovedBackup/" + DateTime.Now.ToString("yyyy-MM-dd") + "/" + SceneDir;
            Directory.CreateDirectory(backup);
            foreach (string p in paths) File.Copy(p, backup + Path.GetFileName(p), true);

            var log = new List<string>();
            foreach (string p in paths)
            {
                Scene sc = EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                string result = Run(true);
                log.Add(result);
                if (!result.Contains("건너뜀")) EditorSceneManager.SaveScene(sc);
            }
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            Debug.Log("[StoneOpt] 모든 맵 씬 적용 결과 (씬 백업: " + backup + ")\n" + string.Join("\n", log));
        }

        // ───────────────────────── 본체 ─────────────────────────
        private static string Run(bool apply)
        {
            Scene scene = SceneManager.GetActiveScene();
            string head = $"[StoneOpt] {scene.name}: ";
            if (apply && string.IsNullOrEmpty(scene.path)) return head + "저장되지 않은 씬이라 건너뜀. 먼저 저장하세요.";

            if (apply) RestoreScene();   // 항상 원본 상태에서 계산

            if (!FindRig(out Vector3 player, out Vector3 camOffset, out Quaternion camRot, out Camera cam))
                return head + "플레이어 세트(PlayerRig)와 카메라를 못 찾아 건너뜀. 플레이어 → 3 메뉴로 먼저 적용하세요.";

            Physics.SyncTransforms();
            Grid g = BuildGrid(player);
            if (g == null) return head + "지면을 못 찾아 건너뜀 (지형/바닥 콜라이더가 없음).";

            // 플레이어가 설 자리들과 그 시야
            var samples = new List<Vector3>();
            int stride = Mathf.Max(1, Mathf.RoundToInt(SampleSpacing / g.cell));
            for (int j = 0; j < g.nz; j += stride)
                for (int i = 0; i < g.nx; i += stride)
                {
                    int k = g.Idx(i, j);
                    if (g.play[k]) samples.Add(new Vector3(g.x0 + (i + 0.5f) * g.cell, g.h[k], g.z0 + (j + 0.5f) * g.cell));
                }
            if (samples.Count == 0) samples.Add(player);

            var planes = new Plane[samples.Count][];
            var camPos = new Vector3[samples.Count];
            for (int s = 0; s < samples.Count; s++)
            {
                camPos[s] = samples[s] + camOffset;
                planes[s] = new Plane[6];
                GeometryUtility.CalculateFrustumPlanes(Projection(cam) * WorldToCamera(camPos[s], camRot), planes[s]);
            }

            // 돌 모으기
            var items = new List<Item>();
            foreach (var mf in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var r = mf.GetComponent<MeshRenderer>();
                if (r == null || !r.enabled || mf.sharedMesh == null) continue;
                string path = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (!path.StartsWith(ModelDir) || r.gameObject.name.EndsWith(Suffix)) continue;
                items.Add(new Item { mf = mf, r = r, baseMesh = BaseOf(mf.sharedMesh), b = r.bounds });
            }
            if (items.Count == 0) return head + "최적화할 돌(Stones/Models 메시)이 없음.";

            EnsureLodImports(items.Select(i => AssetDatabase.GetAssetPath(i.baseMesh)).Distinct());

            int occMask = BulletLayerMask();
            for (int n = 0; n < items.Count; n++)
            {
                Item it = items[n];
                it.dist = FootprintDist(g, it.b);
                it.visible = IsVisible(it, samples.Count, planes, camPos, occMask, out it.camDist);
                if (n % 40 == 0 && EditorUtility.DisplayCancelableProgressBar("돌 최적화 분석", $"{n}/{items.Count}", (float)n / items.Count))
                    break;
            }
            EditorUtility.ClearProgressBar();

            // 안전장치: 시야 계산이 틀리면 돌이 전부 '안 보임'으로 나옴 -> 적용하지 않고 진단 정보를 보여줌
            int visibleCount = items.Count(i => i.visible);
            string diag = $"카메라 오프셋 {camOffset:0.0}, 회전 {camRot.eulerAngles:0}, 시야각 {cam.fieldOfView:0}, 이동 가능 칸 {g.play.Count(p => p)}개";
            if (visibleCount == 0 || visibleCount < items.Count * 0.02f)
                return head + $"보이는 돌이 {visibleCount}/{items.Count}개뿐이라 시야 계산이 맞지 않는 것으로 보고 {(apply ? "적용하지 않았습니다" : "분석을 중단합니다")}. ({diag})";

            // 계획
            int hiddenOff = 0, hiddenShadow = 0, lod1 = 0, lod2 = 0, shadowOff = 0, missingLod = 0;
            long trisBefore = 0, trisAfter = 0;
            foreach (var it in items)
            {
                int t0 = Tris(it.baseMesh);
                trisBefore += t0;
                if (!it.visible)
                {
                    if (it.dist > DeactivateDist) hiddenOff++; else hiddenShadow++;
                    continue;
                }
                // 화면에서 차지하는 비율: 돌의 가장 큰 변 / (카메라 거리에서 보이는 화면 높이)
                float fov = cam.orthographic ? 0f : cam.fieldOfView;
                float screenH = cam.orthographic ? cam.orthographicSize * 2f : 2f * Mathf.Max(0.5f, it.camDist) * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
                float frac = Mathf.Max(it.b.size.x, it.b.size.y, it.b.size.z) / Mathf.Max(0.01f, screenH);
                int want = it.b.size.magnitude < MinLodSize ? 0 : frac >= Lod0Frac ? 0 : frac >= Lod1Frac ? 1 : 2;
                it.lod = want;
                if (want > 0)
                {
                    it.lodMesh = LodMesh(it.baseMesh, want);
                    if (it.lodMesh == null) { it.lod = 0; missingLod++; }
                }
                if (it.lod == 1) lod1++; else if (it.lod == 2) lod2++;
                if (it.dist > ShadowOffDist && it.r.shadowCastingMode == ShadowCastingMode.On) shadowOff++;
                trisAfter += it.lodMesh != null ? Tris(it.lodMesh) : t0;
            }

            string summary = $"({diag})\n돌 {items.Count}개, 플레이 구역 {samples.Count}곳 기준 시야 분석, 보이는 돌 {visibleCount}개. 삼각형 약 {trisBefore / 1000}k → {trisAfter / 1000}k ({(trisBefore > 0 ? 100 - 100 * trisAfter / trisBefore : 0)}% 감소).\n" +
                $"  안 보이는 돌: 끄기 {hiddenOff}, 그림자만 남기기 {hiddenShadow} / 저폴리 교체: 35% {lod1}, 12% {lod2} / 그림자 끄기 {shadowOff}" +
                (missingLod > 0 ? $"\n  저폴리 메시를 못 찾은 돌 {missingLod}개 (ArtSource/StonePack/lod_gen.py 로 만든 _L1/_L2 가 필요)" : "");

            if (!apply)
                return head + "분석만 했습니다 (바뀐 것 없음).\n" + summary;

            // 적용
            Undo.IncrementCurrentGroup(); int grp = Undo.GetCurrentGroup();
            var saved = new SavedFile();
            foreach (var it in items)
            {
                if (!it.visible)
                {
                    if (it.dist > DeactivateDist)
                    {
                        var go = it.r.gameObject;
                        Undo.RecordObject(go, "Stone opt");
                        go.name += Suffix;
                        go.SetActive(false);
                    }
                    else
                    {
                        Remember(saved, it.r);
                        Undo.RecordObject(it.r, "Stone opt");
                        it.r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;   // 안 그리되 그림자는 남김
                    }
                    continue;
                }
                if (it.lodMesh != null)
                {
                    Undo.RecordObject(it.mf, "Stone opt");
                    it.mf.sharedMesh = it.lodMesh;
                }
                if (it.dist > ShadowOffDist && it.r.shadowCastingMode == ShadowCastingMode.On)
                {
                    Remember(saved, it.r);
                    Undo.RecordObject(it.r, "Stone opt");
                    it.r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            Undo.CollapseUndoOperations(grp);
            File.WriteAllText(SidecarPath(scene), JsonUtility.ToJson(saved));
            EditorSceneManager.MarkSceneDirty(scene);
            return head + "적용 완료 (Ctrl+Z 또는 '되돌리기' 메뉴로 복구).\n" + summary;
        }

        // ───────────────────────── 되돌리기 ─────────────────────────
        private static string RestoreScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            int unhidden = 0, meshes = 0, shadows = 0;

            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!t.name.EndsWith(Suffix)) continue;
                Undo.RecordObject(t.gameObject, "Stone opt restore");
                t.name = t.name.Substring(0, t.name.Length - Suffix.Length);
                t.gameObject.SetActive(true);
                unhidden++;
            }
            foreach (var mf in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mf.sharedMesh == null) continue;
                string path = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (!path.StartsWith(ModelDir) || !LodName.IsMatch(path)) continue;
                Undo.RecordObject(mf, "Stone opt restore");
                mf.sharedMesh = BaseOf(mf.sharedMesh);
                meshes++;
            }

            string side = SidecarPath(scene);
            if (File.Exists(side))
            {
                var saved = JsonUtility.FromJson<SavedFile>(File.ReadAllText(side));
                foreach (var e in saved.entries)
                {
                    if (!GlobalObjectId.TryParse(e.id, out GlobalObjectId gid)) continue;
                    var r = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as Renderer;
                    if (r == null) continue;
                    Undo.RecordObject(r, "Stone opt restore");
                    r.shadowCastingMode = (ShadowCastingMode)e.shadow;
                    shadows++;
                }
                File.Delete(side);
            }
            if (unhidden + meshes + shadows > 0) EditorSceneManager.MarkSceneDirty(scene);
            return $"되돌림: 켠 돌 {unhidden}, 원본 메시 {meshes}, 그림자 {shadows}";
        }

        private static string SidecarPath(Scene scene) => "Library/StoneOptimizer_" + AssetDatabase.AssetPathToGUID(scene.path) + ".json";

        private static void Remember(SavedFile f, Renderer r)
        {
            string id = GlobalObjectId.GetGlobalObjectIdSlow(r).ToString();
            if (f.entries.All(e => e.id != id)) f.entries.Add(new SavedEntry { id = id, shadow = (int)r.shadowCastingMode });
        }

        // ───────────────────────── 플레이어·카메라 ─────────────────────────
        private static bool FindRig(out Vector3 player, out Vector3 camOffset, out Quaternion camRot, out Camera cam)
        {
            player = Vector3.zero; camOffset = Vector3.zero; camRot = Quaternion.identity; cam = null;
            GameObject rig = GameObject.Find("PlayerRig");
            Transform pt = rig != null ? rig.transform.Find("Player") : null;
            if (pt == null) { var go = GameObject.Find("Player"); pt = go != null ? go.transform : null; }
            // 미니맵 카메라 같은 다른 카메라가 아니라 게임 화면 카메라(Main Camera)를 골라야 함
            cam = rig != null ? rig.GetComponentsInChildren<Camera>(true).FirstOrDefault(c => c.name == "Main Camera" || c.CompareTag("MainCamera")) : null;
            if (cam == null) cam = Camera.main;
            if (pt == null || cam == null) return false;
            player = pt.position;
            camOffset = cam.transform.position - pt.position;
            camRot = cam.transform.rotation;

            // 에디터에 저장된 카메라 위치는 실제 게임과 다름: TopDownCameraFollow 가 실행 중에 거리·기울기·방향으로 따로 배치함 -> 그 설정으로 계산
            var follow = cam.GetComponents<MonoBehaviour>().FirstOrDefault(c => c != null && c.GetType().Name == "TopDownCameraFollow");
            if (follow != null)
            {
                var so = new SerializedObject(follow);
                var pDist = so.FindProperty("distance"); var pPitch = so.FindProperty("pitch"); var pYaw = so.FindProperty("yaw");
                if (pDist != null && pPitch != null && pYaw != null)
                {
                    camRot = Quaternion.Euler(pPitch.floatValue, pYaw.floatValue, 0f);
                    camOffset = Vector3.up - camRot * Vector3.forward * pDist.floatValue;   // TopDownCameraFollow 의 FollowTarget 식 그대로
                }
            }
            return true;
        }

        private static Matrix4x4 WorldToCamera(Vector3 pos, Quaternion rot) =>
            Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(pos, rot, Vector3.one).inverse;

        private static Matrix4x4 Projection(Camera cam)
        {
            const float aspect = 2.4f;   // 21:9 까지 넉넉히 (더 넓은 화면에서도 보이는 돌을 안 지우게)
            float far = Mathf.Max(cam.farClipPlane, 200f);
            if (cam.orthographic)
            {
                float h = cam.orthographicSize;
                return Matrix4x4.Ortho(-h * aspect, h * aspect, -h, h, cam.nearClipPlane, far);
            }
            return Matrix4x4.Perspective(cam.fieldOfView, aspect, cam.nearClipPlane, far);
        }

        // ───────────────────────── 플레이 구역 격자 ─────────────────────────
        private static Grid BuildGrid(Vector3 start)
        {
            Terrain terrain = Terrain.activeTerrain;
            Bounds area;
            if (terrain != null)
                area = new Bounds(terrain.transform.position + terrain.terrainData.size * 0.5f, terrain.terrainData.size);
            else
            {
                var rs = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                if (rs.Length == 0) return null;
                area = rs[0].bounds;
                foreach (var r in rs) area.Encapsulate(r.bounds);
            }

            float cell = Mathf.Max(0.75f, Mathf.Max(area.size.x, area.size.z) / MaxGrid);
            var g = new Grid { cell = cell, x0 = area.min.x, z0 = area.min.z };
            g.nx = Mathf.Max(1, Mathf.CeilToInt(area.size.x / cell));
            g.nz = Mathf.Max(1, Mathf.CeilToInt(area.size.z / cell));
            g.h = new float[g.nx * g.nz]; g.play = new bool[g.h.Length]; g.dist = new float[g.h.Length];

            int playerLayer = LayerMask.NameToLayer("Player");
            int mask = ~((playerLayer >= 0 ? 1 << playerLayer : 0) | (1 << 2));
            float topY = area.max.y + 30f, depth = area.size.y + 80f;
            for (int j = 0; j < g.nz; j++)
                for (int i = 0; i < g.nx; i++)
                {
                    var o = new Vector3(g.x0 + (i + 0.5f) * cell, topY, g.z0 + (j + 0.5f) * cell);
                    g.h[g.Idx(i, j)] = Physics.Raycast(o, Vector3.down, out RaycastHit hit, depth, mask, QueryTriggerInteraction.Ignore) ? hit.point.y : float.NaN;
                }

            // 플레이어 칸에서 퍼져 나가며 걸어갈 수 있는 칸 표시
            int si = Mathf.Clamp(Mathf.FloorToInt((start.x - g.x0) / cell), 0, g.nx - 1);
            int sj = Mathf.Clamp(Mathf.FloorToInt((start.z - g.z0) / cell), 0, g.nz - 1);
            if (float.IsNaN(g.h[g.Idx(si, sj)])) return null;
            var queue = new Queue<int>();
            g.play[g.Idx(si, sj)] = true; queue.Enqueue(g.Idx(si, sj));
            int[] di = { 1, -1, 0, 0 }, dj = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                int k = queue.Dequeue(); int ci = k % g.nx, cj = k / g.nx;
                for (int d = 0; d < 4; d++)
                {
                    int ni = ci + di[d], nj = cj + dj[d];
                    if (!g.InRange(ni, nj)) continue;
                    int nk = g.Idx(ni, nj);
                    if (g.play[nk] || float.IsNaN(g.h[nk]) || Mathf.Abs(g.h[nk] - g.h[k]) > WalkStep) continue;
                    g.play[nk] = true; queue.Enqueue(nk);
                }
            }

            // 구역에서 바깥으로의 거리 (2패스 체임퍼)
            const float inf = 1e6f; float diag = cell * 1.4142f;
            for (int k = 0; k < g.dist.Length; k++) g.dist[k] = g.play[k] ? 0f : inf;
            for (int j = 0; j < g.nz; j++)
                for (int i = 0; i < g.nx; i++)
                {
                    int k = g.Idx(i, j); float v = g.dist[k];
                    if (i > 0) v = Mathf.Min(v, g.dist[g.Idx(i - 1, j)] + cell);
                    if (j > 0) v = Mathf.Min(v, g.dist[g.Idx(i, j - 1)] + cell);
                    if (i > 0 && j > 0) v = Mathf.Min(v, g.dist[g.Idx(i - 1, j - 1)] + diag);
                    if (i < g.nx - 1 && j > 0) v = Mathf.Min(v, g.dist[g.Idx(i + 1, j - 1)] + diag);
                    g.dist[k] = v;
                }
            for (int j = g.nz - 1; j >= 0; j--)
                for (int i = g.nx - 1; i >= 0; i--)
                {
                    int k = g.Idx(i, j); float v = g.dist[k];
                    if (i < g.nx - 1) v = Mathf.Min(v, g.dist[g.Idx(i + 1, j)] + cell);
                    if (j < g.nz - 1) v = Mathf.Min(v, g.dist[g.Idx(i, j + 1)] + cell);
                    if (i < g.nx - 1 && j < g.nz - 1) v = Mathf.Min(v, g.dist[g.Idx(i + 1, j + 1)] + diag);
                    if (i > 0 && j < g.nz - 1) v = Mathf.Min(v, g.dist[g.Idx(i - 1, j + 1)] + diag);
                    g.dist[k] = v;
                }
            return g;
        }

        private static float FootprintDist(Grid g, Bounds b)
        {
            float best = float.MaxValue;
            for (int ix = -1; ix <= 1; ix++)
                for (int iz = -1; iz <= 1; iz++)
                    best = Mathf.Min(best, g.DistAt(b.center.x + ix * b.extents.x, b.center.z + iz * b.extents.z));
            return best;
        }

        // ───────────────────────── 가시성 ─────────────────────────
        private static int BulletLayerMask() => LayerMask.GetMask("Obstacle", "Ground");   // 보이지 않는 벽(Default)은 시야를 안 가림

        private static bool IsVisible(Item it, int sampleCount, Plane[][] planes, Vector3[] camPos, int occMask, out float minCamDist)
        {
            Bounds b = it.b;
            Vector3 c = b.center, e = b.extents;
            Vector3[] pts =
            {
                new Vector3(c.x, b.max.y, c.z),
                new Vector3(c.x + e.x * 0.7f, c.y + e.y * 0.5f, c.z + e.z * 0.7f),
                new Vector3(c.x - e.x * 0.7f, c.y + e.y * 0.5f, c.z + e.z * 0.7f),
                new Vector3(c.x + e.x * 0.7f, c.y + e.y * 0.5f, c.z - e.z * 0.7f),
                new Vector3(c.x - e.x * 0.7f, c.y + e.y * 0.5f, c.z - e.z * 0.7f),
            };
            bool visible = false;
            minCamDist = float.MaxValue;
            for (int s = 0; s < sampleCount; s++)
            {
                if (!GeometryUtility.TestPlanesAABB(planes[s], b)) continue;
                float d = Vector3.Distance(camPos[s], c);
                if (d >= minCamDist) continue;   // 이미 더 가까운 자리에서 보임 -> 광선 검사 생략
                foreach (var p in pts)
                    if (Unoccluded(camPos[s], p, it.r.gameObject, occMask)) { visible = true; minCamDist = d; break; }
            }
            return visible;
        }

        private static bool Unoccluded(Vector3 from, Vector3 to, GameObject self, int mask)
        {
            Vector3 d = to - from; float len = d.magnitude;
            if (len < 0.05f) return true;
            int n = Physics.RaycastNonAlloc(from, d / len, Buf, len - 0.05f, mask, QueryTriggerInteraction.Ignore);
            Array.Sort(Buf, 0, n, ByDistance);
            for (int i = 0; i < n; i++)
            {
                if (Buf[i].collider.gameObject == self) continue;   // 자기 자신은 가리개가 아님
                return false;
            }
            return true;
        }

        // ───────────────────────── 메시 ─────────────────────────
        private static int Tris(Mesh m)
        {
            int t = 0;
            for (int i = 0; i < m.subMeshCount; i++) t += (int)m.GetIndexCount(i) / 3;
            return t;
        }

        private static Mesh BaseOf(Mesh m)
        {
            string path = AssetDatabase.GetAssetPath(m);
            if (!LodName.IsMatch(path)) return m;
            string basePath = LodName.Replace(path, ".fbx");
            return LoadMesh(basePath) ?? m;
        }

        private static Mesh LodMesh(Mesh baseMesh, int level)
        {
            string path = AssetDatabase.GetAssetPath(baseMesh);
            string lodPath = path.Replace(".fbx", "_L" + level + ".fbx");
            if (!File.Exists(lodPath)) return null;
            Mesh lod = LoadMesh(lodPath);
            if (lod == null) return null;
            // 크기가 어긋난 메시(임포트 스케일 문제)는 쓰지 않음
            float ratio = lod.bounds.size.magnitude / Mathf.Max(0.0001f, baseMesh.bounds.size.magnitude);
            if (Mathf.Abs(ratio - 1f) > 0.08f)
            {
                if (Warned.Add(lodPath)) Debug.LogWarning($"[StoneOpt] {lodPath} 크기가 원본과 {ratio:0.00}배로 달라 건너뜁니다 (FBX 임포트 설정 확인).");
                return null;
            }
            return lod;
        }

        private static Mesh LoadMesh(string path)
        {
            if (MeshCache.TryGetValue(path, out Mesh cached) && cached != null) return cached;
            Mesh m = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault();
            MeshCache[path] = m;
            return m;
        }

        // 저폴리 FBX 임포트 설정을 원본 돌과 같게 (재질 없음, 노말 가져오기, 압축 끔)
        private static void EnsureLodImports(IEnumerable<string> basePaths)
        {
            foreach (string bp in basePaths)
                foreach (int level in new[] { 1, 2 })
                {
                    string lp = bp.Replace(".fbx", "_L" + level + ".fbx");
                    if (!File.Exists(lp)) continue;
                    var imp = AssetImporter.GetAtPath(lp) as ModelImporter;
                    if (imp == null) { AssetDatabase.ImportAsset(lp); imp = AssetImporter.GetAtPath(lp) as ModelImporter; }
                    if (imp == null) continue;
                    bool ch = false;
                    if (imp.materialImportMode != ModelImporterMaterialImportMode.None) { imp.materialImportMode = ModelImporterMaterialImportMode.None; ch = true; }
                    if (imp.importNormals != ModelImporterNormals.Import) { imp.importNormals = ModelImporterNormals.Import; ch = true; }
                    if (imp.importTangents != ModelImporterTangents.CalculateMikk) { imp.importTangents = ModelImporterTangents.CalculateMikk; ch = true; }
                    if (imp.meshCompression != ModelImporterMeshCompression.Off) { imp.meshCompression = ModelImporterMeshCompression.Off; ch = true; }
                    if (imp.importAnimation) { imp.importAnimation = false; ch = true; }
                    if (imp.importBlendShapes) { imp.importBlendShapes = false; ch = true; }
                    if (ch) { imp.SaveAndReimport(); MeshCache.Remove(lp); }
                }
        }
    }
}
