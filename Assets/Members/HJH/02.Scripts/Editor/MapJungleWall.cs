using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HJH.EditorTools
{
    // 다리 건너편(정글/늪지대) 구역의 지형 벽 앞을 나무로 빽빽하게 막음.
    // 기존 숲(KM 나무)과 다른 나무를 씀: PF_JungleTree_0~2 세 가지만 씀.
    // 윤곽선 바깥으로 4겹, 1.9m 간격 + 지터. 구역 안·물속·다리 입구는 피함. 콜라이더 없음(보이지 않는 벽이 막고 있음).
    // 뒤 겹(3~4)은 그림자 끄고 정적 배칭. Map_Bridge_Normal 전용, 다시 돌리면 JungleWall 을 지우고 새로 만듦.
    public static class MapJungleWall
    {
        private const string JwDir = "Assets/Members/HJH/09.JungleWater/Generated/Prefabs/";
        private const string GroupName = "JungleWall";
        private const float Spacing = 1.9f;
        private static readonly float[] RowOut = { 1.3f, 2.8f, 4.3f, 5.8f };

        [MenuItem("HJH/맵/나무/정글 쪽 벽을 나무로 빽빽하게")]
        private static void Build()
        {
            if (EditorApplication.isPlaying) return;
            if (EditorSceneManager.GetActiveScene().name != "Map_Bridge_Normal")
            {
                Debug.LogWarning("[JungleWall] Map_Bridge_Normal 씬에서만 동작합니다.");
                return;
            }
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) { Debug.LogWarning("[JungleWall] 지형이 없습니다."); return; }

            var trees = new List<GameObject>();
            for (int i = 0; i < 3; i++)
            {
                var t = AssetDatabase.LoadAssetAtPath<GameObject>(JwDir + $"Props/PF_JungleTree_{i}.prefab");
                if (t != null) trees.Add(t);
            }
            if (trees.Count == 0) { Debug.LogWarning("[JungleWall] PF_JungleTree_0~2 프리팹을 찾지 못했습니다."); return; }

            MapSpecBuilder.UseBridgeSpec();
            float waterY = MapSpecBuilder.SpecWaterY;
            Vector2[] poly = MapSpecBuilder.SpecBlob(1);
            float Ground(Vector2 p) => terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;

            var old = GameObject.Find(GroupName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var group = new GameObject(GroupName);
            Undo.RegisterCreatedObjectUndo(group, "Jungle wall");

            var rng = new System.Random(23);
            float R(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
            int placed = 0;
            float carry = 0f;

            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                float len = Vector2.Distance(a, b);
                if (len < 0.01f) continue;
                Vector2 dir = (b - a) / len;
                Vector2 normal = new Vector2(dir.y, -dir.x);
                if (MapSpecBuilder.SpecInBlob(1, (a + b) * 0.5f + normal * 0.6f)) normal = -normal;   // 바깥쪽으로

                float t = carry;
                for (; t < len; t += Spacing)
                {
                    Vector2 edge = a + dir * t;
                    if (MapSpecBuilder.SpecBridgeDist(edge) < 5f) continue;
                    for (int row = 0; row < RowOut.Length; row++)
                    {
                        Vector2 p = edge + normal * (RowOut[row] + R(-0.5f, 0.5f)) + dir * R(-0.6f, 0.6f);
                        if (MapSpecBuilder.SpecInBlob(0, p) || MapSpecBuilder.SpecInBlob(1, p) || MapSpecBuilder.SpecInWater(p)) continue;
                        if (Ground(p) < waterY + 0.1f) continue;

                        GameObject prefab = trees[rng.Next(trees.Count)];
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
                        Undo.RegisterCreatedObjectUndo(go, "Jungle wall tree");
                        go.name = $"WallTree_{row}_{placed}";
                        go.transform.position = new Vector3(p.x, Ground(p) - 0.05f, p.y);
                        go.transform.rotation = Quaternion.Euler(R(-2f, 2f), R(0f, 360f), R(-2f, 2f));
                        go.transform.localScale = Vector3.one * R(1.15f, 1.7f);
                        foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                        if (row >= 2)
                        {
                            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
                            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                        }
                        placed++;
                    }
                }
                carry = t - len;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[JungleWall] 정글 쪽 벽 나무 {placed}그루 (정글 나무 {trees.Count}종만 사용). 너무 무거우면 JungleWall 의 뒤 겹(WallTree_3_*)부터 지우세요.");
        }
    }
}
