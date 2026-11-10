using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HJH.EditorTools
{
    // 다리 건너기 전 땅(시작 구역) 가장자리의 지형 벽을 돌산으로 덮음.
    // 돌 팩의 노말맵 돌(Stone_Big / Stone_Mid)을 윤곽선 바깥쪽에 3겹으로 쌓아서, 지형이 벽처럼 솟은 곳이 돌 절벽으로 보이게 함.
    //   1겹(안쪽): 플레이어가 볼 수 있는 면 -> 콜라이더 켬 (Scale 0.7 이상이라 가리면 투명)
    //   2~3겹(뒤쪽): 안 닿는 산 -> 콜라이더 끔, 정적 배칭
    // 물가나 다리 입구는 건너뜀. 다시 돌리면 'StartCliff' 를 지우고 새로 만듦. Map_Bridge_Normal 전용.
    public static class MapStartCliff
    {
        private const string StonePrefabDir = "Assets/Members/HJH/05.Data/MapProps/Stones/Prefabs/";
        private const string GroupName = "StartCliff";

        // 겹별: 윤곽선에서 바깥으로 떨어진 거리(m), 돌 Scale 범위
        private static readonly float[] RowDist = { 0.5f, 2.4f, 4.6f };
        private static readonly Vector2[] RowScale = { new Vector2(0.95f, 1.35f), new Vector2(1.25f, 1.7f), new Vector2(1.6f, 2.1f) };
        private const float Spacing = 2.8f;

        [MenuItem("HJH/맵/돌/6 시작 구역 암벽 쌓기 (지형 벽을 돌산으로)")]
        private static void Build()
        {
            if (EditorApplication.isPlaying) return;
            if (EditorSceneManager.GetActiveScene().name != "Map_Bridge_Normal")
            {
                Debug.LogWarning("[Cliff] Map_Bridge_Normal 씬에서만 동작합니다.");
                return;
            }
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) { Debug.LogWarning("[Cliff] 지형이 없습니다."); return; }

            var bigs = LoadAll("Stone_Big_");
            var mids = LoadAll("Stone_Mid_");
            if (bigs.Count == 0) { Debug.LogWarning("[Cliff] Stone_Big 프리팹이 없습니다. 돌 → 1 메뉴를 먼저 돌려 주세요."); return; }

            MapSpecBuilder.UseBridgeSpec();
            Vector2[] poly = MapSpecBuilder.SpecBlob(0);

            // 기존 암벽 지우고 새로
            GameObject old = GameObject.Find(GroupName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var group = new GameObject(GroupName);
            Undo.RegisterCreatedObjectUndo(group, "Start cliff");

            var rng = new System.Random(11);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int placed = 0, skipped = 0;

            // 윤곽선을 따라 Spacing 간격으로 걸으며 바깥 방향에 돌을 쌓음
            float carry = 0f;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                float len = Vector2.Distance(a, b);
                if (len < 0.01f) continue;
                Vector2 dir = (b - a) / len;
                Vector2 normal = new Vector2(dir.y, -dir.x);
                // 바깥쪽을 향하게 (안쪽 점이 구역 안이어야 함)
                Vector2 probe = (a + b) * 0.5f + normal * 0.6f;
                if (MapSpecBuilder.SpecInBlob(0, probe)) normal = -normal;

                float t = carry;
                for (; t < len; t += Spacing)
                {
                    Vector2 edge = a + dir * t;
                    if (MapSpecBuilder.SpecBridgeDist(edge) < 5f || MapSpecBuilder.SpecInWater(edge + normal * 3f))
                    {
                        skipped++;
                        continue;
                    }
                    for (int row = 0; row < RowDist.Length; row++)
                    {
                        Vector2 p = edge + normal * (RowDist[row] + R(-0.3f, 0.3f)) + dir * R(-0.8f, 0.8f);
                        if (MapSpecBuilder.SpecInBlob(0, p) || MapSpecBuilder.SpecInWater(p)) continue;

                        bool useMid = row < 2 && mids.Count > 0 && rng.NextDouble() < 0.4;
                        List<GameObject> pool = useMid ? mids : bigs;
                        GameObject prefab = pool[rng.Next(pool.Count)];

                        float sc = R(RowScale[row].x, RowScale[row].y) * (useMid ? 1.3f : 1f);   // 중간 돌은 작아서 키워서 비슷한 덩치로
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
                        Undo.RegisterCreatedObjectUndo(go, "Cliff stone");
                        go.name = $"Cliff_{row}_{placed}";
                        go.transform.rotation = Quaternion.Euler(R(-7f, 7f), R(0f, 360f), R(-7f, 7f));
                        go.transform.localScale = Vector3.one * sc;

                        float ty = terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;
                        go.transform.position = new Vector3(p.x, ty, p.y);
                        // 돌 피벗이 어디든 상관없게: 돌 아래쪽 40% 를 땅에 묻음
                        Bounds bb = BoundsOf(go);
                        go.transform.position += Vector3.up * (ty - bb.min.y - bb.size.y * 0.4f);

                        bool front = row == 0;
                        foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.enabled = front;
                        if (!front) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                        placed++;
                    }
                }
                carry = t - len;   // 다음 변에서 간격 이어감
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Cliff] 시작 구역 암벽: 돌 {placed}개 (물가·다리 입구 {skipped}곳은 건너뜀). 1겹만 콜라이더 켬. 맘에 안 드는 부분은 StartCliff 아래에서 지우거나 옮기고, 돌 → 5 메뉴로 콜라이더를 정리하세요.");
        }

        private static List<GameObject> LoadAll(string prefix)
        {
            var list = new List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets(prefix + " t:Prefab", new[] { StonePrefabDir.TrimEnd('/') }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go != null && go.name.StartsWith(prefix)) list.Add(go);
            }
            return list;
        }

        private static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
