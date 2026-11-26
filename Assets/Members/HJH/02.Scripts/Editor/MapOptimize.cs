using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using E = HJH.EditorTools.MapEdgePolish;

namespace HJH.EditorTools
{
    public static class MapOptimize
    {
        [MenuItem("HJH/맵/최적화/2 씬 최적화 (그림자·GI·라이트·프로브)")]
        private static void OptimizeScene()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Optimize] 플레이 중에는 못 합니다."); return; }

            int shadowOff = 0, gi = 0, lightsBaked = 0;
            var skipName = new System.Text.RegularExpressions.Regex(@"Water|Fog|Boundary|Forest_Fill|Canopy|Trees_Near|Bloom|Flora_|Player|Wall_|Spawn|Marker|Portal");

            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                bool skip = false;
                for (var t = r.transform; t != null; t = t.parent) if (skipName.IsMatch(t.name)) { skip = true; break; }
                if (skip) continue;
                var b = r.bounds;
                float ext = Mathf.Max(b.size.x, b.size.y, b.size.z);
                if (ext < 0.9f && r.shadowCastingMode != ShadowCastingMode.Off)
                {
                    Undo.RecordObject(r, "Optimize");
                    r.shadowCastingMode = ShadowCastingMode.Off; shadowOff++;
                }
                if (ext >= 1.6f)
                {
                    var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                    var want = flags | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic;
                    if (want != flags) { GameObjectUtility.SetStaticEditorFlags(r.gameObject, want); gi++; }
                    r.receiveGI = ReceiveGI.Lightmaps;
                }
            }

            var terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                var f = GameObjectUtility.GetStaticEditorFlags(terrain.gameObject);
                GameObjectUtility.SetStaticEditorFlags(terrain.gameObject, f | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
                var so = new SerializedObject(terrain);
                var sc = so.FindProperty("m_ScaleInLightmap");
                if (sc != null) { sc.floatValue = 0.3f; so.ApplyModifiedPropertiesWithoutUndo(); }
            }

            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.GetComponentInParent<Animator>() != null) continue;
                Undo.RecordObject(l, "Optimize");
                if (l.type == LightType.Directional) l.lightmapBakeType = LightmapBakeType.Mixed;
                else { l.lightmapBakeType = LightmapBakeType.Mixed; lightsBaked++; }
            }

            var pts = new List<Vector3>();
            if (EditorSceneManager.GetActiveScene().name == "Map_L_Normal")
            {
            var old = GameObject.Find("LightProbes_Map");
            if (old != null) Undo.DestroyObjectImmediate(old);
            var pg = new GameObject("LightProbes_Map");
            Undo.RegisterCreatedObjectUndo(pg, "Probes");
            var group = pg.AddComponent<LightProbeGroup>();
            for (float x = 0f; x <= 60f; x += 5f)
                for (float z = 0f; z <= 60f; z += 5f)
                {
                    var p = new Vector2(x, z);
                    if (!E.Inside(p) || E.DistEdge(p) < 0.5f) continue;
                    float gy = terrain != null ? terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y : 0f;
                    pts.Add(new Vector3(x, gy + 1.0f, z)); pts.Add(new Vector3(x, gy + 3.0f, z));
                }
            group.probePositions = pts.ToArray();
            }

            const string lsPath = "Assets/Members/HJH/05.Data/Map/MapL_Lighting.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(lsPath);
            if (ls == null) { ls = new LightingSettings { name = "MapL_Lighting" }; AssetDatabase.CreateAsset(ls, lsPath); }
            ls.bakedGI = true;
            ls.realtimeGI = false;
            ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            ls.mixedBakeMode = MixedLightingMode.IndirectOnly;
            ls.lightmapResolution = 8f;
            ls.lightmapMaxSize = 2048;
            ls.directSampleCount = 32;
            ls.indirectSampleCount = 128;
            ls.environmentSampleCount = 128;
            ls.autoGenerate = false;
            EditorUtility.SetDirty(ls);
            Lightmapping.lightingSettings = ls;

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Optimize] 작은 프랍 그림자 끔 {shadowOff}개, GI 참여(큰 프랍) {gi}개, 점광원 Mixed {lightsBaked}개, 라이트 프로브 {pts.Count}개, 라이팅 설정 적용(GPU, 8texel/m, 간접광만 베이크). 이제 3번으로 베이크하세요.");
        }

        [MenuItem("HJH/맵/최적화/진단 보고서 (그룹별 렌더러·그림자·폴리)")]
        private static void Report()
        {
            var rows = new List<(string name, int rend, int casters, long tris)>();
            var roots = new List<Transform>();
            foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == "Map_L_Normal" || go.name == "Props")
                    foreach (Transform c in go.transform) roots.Add(c);
                else roots.Add(go.transform);
            }
            foreach (var t in roots)
            {
                int rend = 0, casters = 0; long tris = 0;
                foreach (var r in t.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    var mf = r.GetComponent<MeshFilter>();
                    rend++;
                    if (r.shadowCastingMode != ShadowCastingMode.Off) casters++;
                    if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                }
                if (rend > 0) rows.Add((t.name, rend, casters, tris));
            }
            var sb = new System.Text.StringBuilder("[Optimize] 그룹별 보고 (LOD 모든 단계 포함이라 실제 화면 폴리는 이보다 적음)\n");
            foreach (var r in rows.OrderByDescending(x => x.casters).Take(25))
                sb.AppendLine($"  {r.name,-28} 렌더러 {r.rend,5}  그림자 {r.casters,5}  폴리합 {r.tris / 1000}k");
            Debug.Log(sb.ToString());
        }

        [MenuItem("HJH/맵/최적화/선택한 오브젝트 그림자 끄기")]
        private static void ShadowsOff() => SetShadows(ShadowCastingMode.Off);

        [MenuItem("HJH/맵/최적화/선택한 오브젝트 그림자 켜기")]
        private static void ShadowsOn() => SetShadows(ShadowCastingMode.On);

        private static void SetShadows(ShadowCastingMode mode)
        {
            int n = 0;
            foreach (var go in Selection.gameObjects)
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                {
                    if (r.shadowCastingMode == mode) continue;
                    Undo.RecordObject(r, "Shadows"); r.shadowCastingMode = mode; n++;
                }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Optimize] 렌더러 {n}개의 그림자를 {mode} 으로 바꿨습니다.");
        }

        [MenuItem("HJH/맵/최적화/3 베이크 시작")]
        private static void Bake()
        {
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.SaveOpenScenes();
            bool ok = Lightmapping.BakeAsync();
            Debug.Log(ok ? "[Optimize] 베이크 시작. 진행은 Unity 오른쪽 아래 막대와 Window > Rendering > Lighting 에서 보입니다." : "[Optimize] 베이크를 시작하지 못했습니다. Lighting 창 설정을 확인하세요.");
        }
    }
}
