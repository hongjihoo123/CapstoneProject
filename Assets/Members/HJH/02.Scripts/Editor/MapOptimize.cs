using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using E = HJH.EditorTools.MapEdgePolish;

namespace HJH.EditorTools
{
    // 폴리 줄이기 + 베이크 준비. 메뉴: HJH/맵/최적화/...
    //  1) LOD 만들기: 프랍 FBX 를 Blender 에서 줄여 둔 3단(60% / 22% / 7%)을 프리팹에 LODGroup 으로 붙인다 → 씬 인스턴스에 자동 반영.
    //  2) 씬 최적화: 작은 프랍 그림자 끄기, 큰 프랍·지형만 GI(라이트맵) 참여, 라이트 Baked/Mixed 로 전환, 라이트 프로브, 라이팅 설정.
    //  3) 베이크 시작.
    public static class MapOptimize
    {
        private const string ModelDir = "Assets/Members/HJH/05.Data/MapProps/Models/";
        private const string PrefabDir = "Assets/Members/HJH/05.Data/MapProps/Prefabs/";
        private static readonly string[] Names = { "Rock_A", "Rock_B", "Roots_A", "Roots_B", "Lantern", "GlowFlowers", "GlowVine", "FallenLog" };
        private static readonly HashSet<string> NoShadowProps = new HashSet<string> { "GlowFlowers", "GlowVine" };

        // ───────────────────────── 1. LOD ─────────────────────────
        [MenuItem("HJH/맵/최적화/1 프랍 LOD 만들기 (폴리 줄이기)")]
        private static void MakeLods()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Optimize] 플레이 중에는 못 합니다."); return; }
            AssetDatabase.Refresh();
            int done = 0;
            foreach (var n in Names)
            {
                foreach (var suffix in new[] { "", "_LOD1", "_LOD2" }) ConfigureModel(ModelDir + n + suffix + ".fbx");

                string prefabPath = PrefabDir + n + ".prefab";
                var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    if (contents.GetComponent<LODGroup>() != null) { Debug.Log($"[Optimize] {n}: 이미 LOD 있음 (건너뜀)"); continue; }
                    var model = contents.transform.Find("Model");
                    if (model == null) { Debug.LogWarning($"[Optimize] {n}: Model 자식이 없어 건너뜀"); continue; }
                    var r0 = model.GetComponent<MeshRenderer>();
                    var lodRenderers = new List<Renderer[]> { new Renderer[] { r0 } };
                    bool noShadow = NoShadowProps.Contains(n);
                    if (noShadow) r0.shadowCastingMode = ShadowCastingMode.Off;

                    for (int lv = 1; lv <= 2; lv++)
                    {
                        var mesh = AssetDatabase.LoadAllAssetsAtPath(ModelDir + n + "_LOD" + lv + ".fbx").OfType<Mesh>().FirstOrDefault();
                        if (mesh == null) { Debug.LogWarning($"[Optimize] {n}_LOD{lv} 메시를 못 찾음"); continue; }
                        var go = new GameObject("Model_LOD" + lv);
                        go.transform.SetParent(contents.transform, false);
                        go.transform.localPosition = model.localPosition;
                        go.transform.localRotation = model.localRotation;
                        go.transform.localScale = model.localScale;
                        go.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var mr = go.AddComponent<MeshRenderer>();
                        mr.sharedMaterials = r0.sharedMaterials;
                        mr.shadowCastingMode = (noShadow || lv == 2) ? ShadowCastingMode.Off : r0.shadowCastingMode;
                        lodRenderers.Add(new Renderer[] { mr });
                    }
                    var lg = contents.AddComponent<LODGroup>();
                    var lods = new List<LOD>();
                    float[] heights = { 0.06f, 0.02f, 0.006f }; // 화면 높이 비율: 이보다 작아지면 다음 단계, 마지막 아래는 컬링
                    for (int i = 0; i < lodRenderers.Count; i++) lods.Add(new LOD(heights[i], lodRenderers[i]));
                    lg.SetLODs(lods.ToArray());
                    lg.fadeMode = LODFadeMode.None;
                    lg.RecalculateBounds();
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                    done++;
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Optimize] 프랍 LOD 적용 {done}종: LOD0 약 60%, LOD1 22%, LOD2 7% 폴리, 아주 작아지면 컬링. 씬 인스턴스는 자동 반영됩니다.");
        }

        private static void ConfigureModel(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) return;
            bool ch = false;
            if (imp.materialImportMode != ModelImporterMaterialImportMode.None) { imp.materialImportMode = ModelImporterMaterialImportMode.None; ch = true; }
            if (imp.importNormals != ModelImporterNormals.Import) { imp.importNormals = ModelImporterNormals.Import; ch = true; }
            if (imp.importTangents != ModelImporterTangents.CalculateMikk) { imp.importTangents = ModelImporterTangents.CalculateMikk; ch = true; }
            if (imp.importCameras || imp.importLights) { imp.importCameras = false; imp.importLights = false; ch = true; }
            if (imp.meshCompression != ModelImporterMeshCompression.Off) { imp.meshCompression = ModelImporterMeshCompression.Off; ch = true; }
            if (!imp.generateSecondaryUV) { imp.generateSecondaryUV = true; ch = true; } // 라이트맵용 UV2
            if (ch) imp.SaveAndReimport();
        }

        // ───────────────────────── 2. 씬 최적화 ─────────────────────────
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
                if (sc != null) { sc.floatValue = 0.3f; so.ApplyModifiedPropertiesWithoutUndo(); } // 바깥 언덕까지 큰 지형이라 라이트맵 밀도를 낮춤
            }

            // 라이트: 태양 Mixed(직접광·그림자 실시간 + 간접광 베이크), 점광원은 Mixed
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.GetComponentInParent<Animator>() != null) continue;
                Undo.RecordObject(l, "Optimize");
                if (l.type == LightType.Directional) l.lightmapBakeType = LightmapBakeType.Mixed;
                else { l.lightmapBakeType = LightmapBakeType.Mixed; lightsBaked++; } // Baked 로 하면 움직이는 것·작은 프랍에 빛이 거의 안 닿아 어두워져서 Mixed 로 둠
            }

            // 라이트 프로브: 플레이어·적처럼 움직이는 것이 베이크 빛을 받게 5m 격자
            var old = GameObject.Find("LightProbes_Map");
            if (old != null) Undo.DestroyObjectImmediate(old);
            var pg = new GameObject("LightProbes_Map");
            Undo.RegisterCreatedObjectUndo(pg, "Probes");
            var group = pg.AddComponent<LightProbeGroup>();
            var pts = new List<Vector3>();
            for (float x = 0f; x <= 60f; x += 5f)
                for (float z = 0f; z <= 60f; z += 5f)
                {
                    var p = new Vector2(x, z);
                    if (!E.Inside(p) || E.DistEdge(p) < 0.5f) continue;
                    float gy = terrain != null ? terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y : 0f;
                    pts.Add(new Vector3(x, gy + 1.0f, z)); pts.Add(new Vector3(x, gy + 3.0f, z));
                }
            group.probePositions = pts.ToArray();

            // 라이팅 설정 에셋
            const string lsPath = "Assets/Members/HJH/05.Data/Map/MapL_Lighting.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(lsPath);
            if (ls == null) { ls = new LightingSettings { name = "MapL_Lighting" }; AssetDatabase.CreateAsset(ls, lsPath); }
            ls.bakedGI = true;
            ls.realtimeGI = false;
            ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU; // GPU 가 안 맞으면 Lighting 창에서 CPU 로
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

        // ───────────────────────── 밝기 복구 ─────────────────────────
        // 점광원을 Baked 로 바꿨더니 베이크 후 어두워진 경우: Mixed(직접광은 실시간 그대로 + 간접광만 베이크)로 되돌린다. 다시 베이크 필요.
        [MenuItem("HJH/맵/최적화/밝기 복구 (점광원 Mixed + 지형 라이트맵 밀도 올림)")]
        private static void RestoreBrightness()
        {
            if (EditorApplication.isPlaying) return;
            int n = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) continue;
                if (l.lightmapBakeType == LightmapBakeType.Mixed) continue;
                Undo.RecordObject(l, "Restore light"); l.lightmapBakeType = LightmapBakeType.Mixed; n++;
            }
            var terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                var so = new SerializedObject(terrain);
                var sc = so.FindProperty("m_ScaleInLightmap");
                if (sc != null) { sc.floatValue = 0.8f; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Optimize] 점광원 {n}개를 Mixed 로 복구, 지형 라이트맵 밀도 0.8. 다시 베이크(3번)하세요.");
        }

        // ───────────────────────── 진단: 어디서 배치가 나오는지 ─────────────────────────
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

        // 진단 결과 그림자 캐스터의 대부분(Forest_Fill 732개)이 바깥 숲 채움. 플레이 공간에서 먼 것만 그림자를 끈다 (가까운 것은 바닥의 어른거리는 그림자용으로 유지).
        [MenuItem("HJH/맵/최적화/숲 채움 그림자 줄이기 (플레이 공간에서 먼 것만 끔)")]
        private static void ThinForestShadows()
        {
            if (EditorApplication.isPlaying) return;
            const float keepWithin = 9f; // 외곽선에서 이 거리 안의 나무는 그림자 유지
            var fill = GameObject.Find("Forest_Fill");
            if (fill == null) { Debug.LogWarning("[Optimize] Forest_Fill 이 없습니다."); return; }
            int off = 0, kept = 0;
            foreach (var r in fill.GetComponentsInChildren<MeshRenderer>())
            {
                var p = new Vector2(r.bounds.center.x, r.bounds.center.z);
                if (E.DistEdge(p) > keepWithin)
                {
                    if (r.shadowCastingMode != ShadowCastingMode.Off) { Undo.RecordObject(r, "Forest shadows"); r.shadowCastingMode = ShadowCastingMode.Off; off++; }
                }
                else kept++;
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Optimize] Forest_Fill: 먼 나무 {off}개 그림자 끔, 가까운 {kept}개 유지 (외곽선 {keepWithin}m 안). 더 줄이려면 keepWithin 값을 낮추세요.");
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

        // ───────────────────────── 3. 베이크 ─────────────────────────
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
