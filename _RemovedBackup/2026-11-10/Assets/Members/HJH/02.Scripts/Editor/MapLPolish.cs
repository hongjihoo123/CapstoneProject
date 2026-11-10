using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HJH.EditorTools
{
    // 열려 있는 Map_L_Normal 씬 다듬기 도구 (메뉴: HJH/맵/다듬기/...)
    //  1) 바위 변주: 크기·회전을 흩뜨리고, 큰 바위 주변에 작은 바위(콜라이더 없음)를 채운다.
    //  2) 흙길: 시작 포탈 → 종료 포탈 경로를 터레인 진흙 레이어로 칠해 가야 할 길을 보여 준다.
    //  4) 안개: 전역 안개와 바닥 안개를 옅게 한다.
    // 모든 단계는 Undo 가능하고, 플레이 중에는 돌지 않는다.
    public static class MapLPolish
    {
        private const string RockPrefabA = "Assets/Members/HJH/05.Data/MapProps/Prefabs/Rock_A.prefab";
        private const string RockPrefabB = "Assets/Members/HJH/05.Data/MapProps/Prefabs/Rock_B.prefab";
        private const string DetailRoot = "Rock_Detail";

        // 시작 포탈 → 종료 포탈 (맵 빌더의 ㄴ자 길과 같은 경로, 웅덩이 북쪽으로 돌아감)
        private static readonly Vector2[] PathPoints =
        {
            new Vector2(15, 56), new Vector2(15, 44), new Vector2(13, 32), new Vector2(15, 22),
            new Vector2(24, 17), new Vector2(34, 18), new Vector2(46, 19), new Vector2(57, 15),
        };

        private static readonly Regex RockName = new Regex(@"^Rock_[AB]( \(\d+\))?$");

        private static bool Guard()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[MapLPolish] 플레이 중에는 못 합니다."); return false; }
            return true;
        }

        // ───────────────────────── 1. 바위 변주 ─────────────────────────
        [MenuItem("HJH/맵/다듬기/1 바위 크기·회전 변주 + 작은 바위 채우기")]
        private static void VaryRocks()
        {
            if (!Guard()) return;

            var rocks = new List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!RockName.IsMatch(t.name)) continue;
                if (t.parent != null && t.parent.name == DetailRoot) continue; // 이미 채운 작은 바위는 제외
                rocks.Add(t);
            }
            if (rocks.Count == 0) { Debug.LogWarning("[MapLPolish] Rock_A/Rock_B 를 못 찾았습니다."); return; }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            // 이전 작은 바위 지움 (다시 실행해도 쌓이지 않게)
            var oldDetail = GameObject.Find(DetailRoot);
            if (oldDetail != null) Undo.DestroyObjectImmediate(oldDetail);

            // 큰 바위 판별은 변주 전 크기 기준: 상위 25 %
            var sizes = new List<float>();
            foreach (var r in rocks) sizes.Add(r.lossyScale.x);
            sizes.Sort();
            float bigThreshold = sizes[Mathf.Clamp((int)(sizes.Count * 0.75f), 0, sizes.Count - 1)];

            var rng = new System.Random(20261109);
            float R() => (float)rng.NextDouble();

            int varied = 0;
            var bigRocks = new List<Transform>();
            foreach (var r in rocks)
            {
                if (r.GetComponent<MapPropVaried>() != null) { if (r.lossyScale.x >= bigThreshold) bigRocks.Add(r); continue; }
                Undo.RecordObject(r, "Rock variation");
                bool big = r.lossyScale.x >= bigThreshold;
                float k = big ? Mathf.Lerp(1.0f, 1.55f, R()) : Mathf.Lerp(0.7f, 1.25f, R());
                // 비균일 스케일을 조금 섞어 같은 모양 반복을 깬다
                var s = r.localScale * k;
                s.x *= Mathf.Lerp(0.88f, 1.15f, R());
                s.z *= Mathf.Lerp(0.88f, 1.15f, R());
                r.localScale = s;
                // 완전 랜덤 Y 회전 + 살짝 기울임
                var e = r.eulerAngles;
                r.rotation = Quaternion.Euler(Mathf.Lerp(-6f, 6f, R()), R() * 360f, Mathf.Lerp(-6f, 6f, R()));
                Undo.AddComponent<MapPropVaried>(r.gameObject);
                if (big) bigRocks.Add(r);
                varied++;
            }

            // 큰 바위 주변에 작은 바위
            var prefabA = AssetDatabase.LoadAssetAtPath<GameObject>(RockPrefabA);
            var prefabB = AssetDatabase.LoadAssetAtPath<GameObject>(RockPrefabB);
            var terrain = Terrain.activeTerrain;
            int placed = 0;
            if (prefabA != null && terrain != null)
            {
                var root = new GameObject(DetailRoot);
                Undo.RegisterCreatedObjectUndo(root, "Rock detail");
                var propsParent = GameObject.Find("Props");
                if (propsParent != null) root.transform.SetParent(propsParent.transform, false);

                foreach (var big in bigRocks)
                {
                    int n = 3 + rng.Next(0, 3); // 3~5개
                    float baseR = Mathf.Max(big.lossyScale.x, 1f) * 1.4f;
                    for (int i = 0; i < n; i++)
                    {
                        float ang = R() * Mathf.PI * 2f;
                        float dist = baseR * Mathf.Lerp(0.8f, 2.2f, R());
                        var p = big.position + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                        if (OnPath(new Vector2(p.x, p.z), 2.2f)) continue; // 길 위는 비움
                        float gy = terrain.SampleHeight(p) + terrain.transform.position.y;
                        if (Mathf.Abs(gy - big.position.y) > 1.2f) continue; // 비탈로 튀는 것 방지
                        p.y = gy - 0.03f;

                        var prefab = R() < 0.65f ? prefabA : prefabB != null ? prefabB : prefabA;
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                        Undo.RegisterCreatedObjectUndo(go, "Rock detail");
                        go.name = "RockSmall";
                        go.transform.position = p;
                        go.transform.rotation = Quaternion.Euler(Mathf.Lerp(-8f, 8f, R()), R() * 360f, Mathf.Lerp(-8f, 8f, R()));
                        float sc = Mathf.Lerp(0.22f, 0.5f, R());
                        go.transform.localScale = new Vector3(sc * Mathf.Lerp(0.9f, 1.25f, R()), sc * Mathf.Lerp(0.8f, 1.1f, R()), sc * Mathf.Lerp(0.9f, 1.25f, R()));
                        foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false; // 시각용만
                        placed++;
                    }
                }
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[MapLPolish] 바위 {rocks.Count}개 중 새로 변주 {varied}개, 큰 바위 {bigRocks.Count}개 주변에 작은 바위 {placed}개 추가 (콜라이더 없음).");
        }

        // ───────────────────────── 2. 흙길 ─────────────────────────
        [MenuItem("HJH/맵/다듬기/2 흙길 칠하기 (시작→종료 포탈)")]
        private static void PaintPath()
        {
            if (!Guard()) return;
            var terrain = Terrain.activeTerrain;
            if (terrain == null) { Debug.LogWarning("[MapLPolish] Terrain 이 없습니다."); return; }
            var data = terrain.terrainData;
            if (data.alphamapLayers < 2) { Debug.LogWarning("[MapLPolish] 터레인 레이어가 부족합니다 (진흙 = 1번 필요)."); return; }

            const int mud = 1;
            const float halfWidth = 1.7f, edge = 1.3f; // 중심 폭 3.4m + 부드러운 가장자리
            int res = data.alphamapResolution, layers = data.alphamapLayers;
            Undo.RegisterCompleteObjectUndo(data, "Paint path");
            var map = data.GetAlphamaps(0, 0, res, res);
            var origin = terrain.transform.position;
            var size = data.size;

            var line = Smooth(PathPoints, 8);
            int painted = 0;
            for (int zi = 0; zi < res; zi++)
            {
                for (int xi = 0; xi < res; xi++)
                {
                    var p = new Vector2(origin.x + (xi + 0.5f) / res * size.x, origin.z + (zi + 0.5f) / res * size.z);
                    float d = DistToLine(p, line);
                    // 가장자리를 노이즈로 울퉁불퉁하게
                    float wob = (Mathf.PerlinNoise(p.x * 0.6f, p.y * 0.6f) - 0.5f) * 0.9f;
                    float w = 1f - Mathf.Clamp01((d - halfWidth + wob) / edge);
                    if (w <= 0.001f) continue;
                    w = w * w * (3f - 2f * w);
                    float sum = 0f;
                    for (int l = 0; l < layers; l++)
                    {
                        map[zi, xi, l] = l == mud ? Mathf.Lerp(map[zi, xi, l], 1f, w) : map[zi, xi, l] * (1f - w);
                        sum += map[zi, xi, l];
                    }
                    if (sum > 0f) for (int l = 0; l < layers; l++) map[zi, xi, l] /= sum;
                    painted++;
                }
            }
            data.SetAlphamaps(0, 0, map);
            EditorUtility.SetDirty(data);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[MapLPolish] 흙길 칠함: {painted}칸 (폭 약 {halfWidth * 2:0.0}m, 진흙 레이어). 질감이 약하면 TL_Mud 타일링/색을 조정하세요.");
        }

        private static bool OnPath(Vector2 p, float margin)
            => DistToLine(p, Smooth(PathPoints, 8)) < margin;

        private static List<Vector2> Smooth(Vector2[] pts, int steps)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var p0 = pts[Mathf.Max(i - 1, 0)];
                var p1 = pts[i];
                var p2 = pts[i + 1];
                var p3 = pts[Mathf.Min(i + 2, pts.Length - 1)];
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps, t2 = t * t, t3 = t2 * t;
                    result.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            result.Add(pts[pts.Length - 1]);
            return result;
        }

        private static float DistToLine(Vector2 p, List<Vector2> line)
        {
            float best = float.MaxValue;
            for (int i = 0; i < line.Count - 1; i++)
            {
                var a = line[i]; var ab = line[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        // ───────────────────────── 4. 안개 ─────────────────────────
        [MenuItem("HJH/맵/다듬기/4 안개 옅게")]
        private static void ThinFog()
        {
            if (!Guard()) return;
            RenderSettings.fogDensity = 0.005f; // 전역 안개 (기존 0.012)

            // 바닥 안개 판: 공용 머티리얼은 건드리지 않고 이 맵 전용 복사본을 만들어 절반 이하로 옅게
            var go = GameObject.Find("GroundFog");
            bool done = false;
            if (go != null && go.TryGetComponent<MeshRenderer>(out var mr))
            {
                const string copyPath = "Assets/Members/HJH/05.Data/Map/MapL_GroundFog.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(copyPath);
                if (mat == null && mr.sharedMaterial != null)
                {
                    mat = new Material(mr.sharedMaterial) { name = "MapL_GroundFog" };
                    AssetDatabase.CreateAsset(mat, copyPath);
                }
                if (mat != null)
                {
                    if (mat.HasProperty("_Density")) mat.SetFloat("_Density", 0.08f); // 원본 0.18
                    EditorUtility.SetDirty(mat);
                    Undo.RecordObject(mr, "Thin fog");
                    mr.sharedMaterial = mat;
                    done = true;
                }
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[MapLPolish] 안개: 전역 0.012→0.005, 바닥 안개 판 {(done ? "전용 머티리얼로 교체 (밀도 0.18→0.08)" : "못 찾음")}.");
        }
    }
}
