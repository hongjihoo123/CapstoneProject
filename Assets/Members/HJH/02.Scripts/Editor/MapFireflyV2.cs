using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using HJH.MapFx;

namespace HJH.EditorTools
{
    // 반딧불이 v2 (11-13). 메뉴: HJH/맵/반딧불이/...
    //  4) v2 업그레이드: 노란 점 → 작은 빛.
    //     - T_FireflyGlow2: 밝은 심(core) + 부드러운 번짐(halo) 텍스처
    //     - M_Firefly: 덧셈 + HDR(BaseColor ×3.5) → Bloom(threshold 0.8) 에 걸림. 색은 입자 색(#FFE066~#C8FF5A)이 정함
    //     - Flies: 크기/높이/밝기 다양, 느린 Noise, Drag(흩어진 뒤 다시 느려짐), 15% 입자에 작은 라이트(최대 4개)
    //     - 깜빡임·흩어짐은 FireflyZone 스크립트가 입자마다 다른 박자로 처리
    //     프리팹을 직접 고쳐서(LoadPrefabContents) 씬에 놓인 구역들의 위치/라이트 위치 오버라이드는 그대로 유지된다.
    //  5) 덤불·물가 쪽으로 모으기: 지금 씬의 구역들을 8m 안의 꽃·버섯·뿌리·쓰러진 나무·물웅덩이 가장자리 중
    //     식생이 가장 빽빽한 곳으로 옮긴다. 구역끼리 3.5m 까지 붙을 수 있어 무리를 짓는다. 근처에 아무것도 없으면 그대로.
    public static class MapFireflyV2
    {
        private const string Dir = "Assets/Members/HJH/05.Data/Fx/Firefly/";
        private const string TexPath = Dir + "T_FireflyGlow2.png";
        private const string LightPath = Dir + "P_FireflyParticleLight.prefab";
        private const string PrefabPath = Dir + "FireflyZone.prefab";

        [MenuItem("HJH/맵/반딧불이/4 v2 업그레이드 (빛나는 반딧불이)")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Firefly] 플레이 중에는 실행하지 않습니다."); return; }
            if (!File.Exists(PrefabPath)) { Debug.LogWarning("[Firefly] FireflyZone.prefab 이 없습니다. 1번을 먼저."); return; }

            var tex = MakeTexture();
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Dir + "M_Firefly.mat");
            if (mat != null)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", new Color(3.5f, 3.5f, 3.5f, 1f));
                EditorUtility.SetDirty(mat);
            }
            var lightPrefab = MakeParticleLight();

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var zone = root.GetComponent<FireflyZone>();
                var fliesT = root.transform.Find("Flies");
                if (zone == null || fliesT == null) { Debug.LogWarning("[Firefly] 프리팹 구조가 예상과 다릅니다 (Flies 없음)."); return; }
                var ps = fliesT.GetComponent<ParticleSystem>();
                ConfigureFlies(ps, lightPrefab);
                var r = fliesT.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = mat;
                r.maxParticleSize = 0.25f;
                r.minParticleSize = 0f;

                var so = new SerializedObject(zone);
                so.FindProperty("flies").objectReferenceValue = ps;
                so.FindProperty("onSeconds").vector2Value = new Vector2(0.15f, 0.45f);   // 짧게 반짝
                so.FindProperty("offSeconds").vector2Value = new Vector2(1.5f, 4f);
                so.FindProperty("wanderSpeed").vector2Value = new Vector2(0.3f, 0.8f);
                so.FindProperty("wanderTurn").floatValue = 0.6f;
                so.FindProperty("leashRadius").floatValue = 3.2f;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Firefly] v2 업그레이드 완료: T_FireflyGlow2, M_Firefly(HDR ×3.5), P_FireflyParticleLight, FireflyZone.prefab/Flies. 씬 인스턴스 위치는 그대로.");
        }

        public static void ConfigureFlies(ParticleSystem ps, Light particleLight)
        {
            var main = ps.main;
            main.loop = true; main.prewarm = true; main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.5f);   // 텍스처의 번짐까지 포함한 크기 (심은 ~1/4)
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.878f, 0.4f), new Color(0.784f, 1f, 0.353f));
            main.maxParticles = 12;                 // 11-13 피드백: 너무 많음 → 30 에서 12
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0f;

            var em = ps.emission; em.enabled = true; em.rateOverTime = 1.2f;

            var sh = ps.shape; sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 2.6f; sh.radiusThickness = 1f;
            sh.scale = new Vector3(1f, 0.5f, 1f); sh.position = new Vector3(0f, 1.4f, 0f);   // 높이 ~0.1~2.7m

            var nz = ps.noise; nz.enabled = true;
            nz.strength = 0.15f; nz.frequency = 0.6f; nz.scrollSpeed = 0.3f; nz.damping = true;   // 큰 움직임은 FireflyZone 이, 노이즈는 잔떨림만
            nz.octaveCount = 1; nz.quality = ParticleSystemNoiseQuality.Low;
            nz.separateAxes = true;
            nz.strengthX = 0.15f; nz.strengthY = 0.08f; nz.strengthZ = 0.15f;

            var lv = ps.limitVelocityOverLifetime; lv.enabled = false;   // 속도는 FireflyZone 이 직접 조절 (날아다니기·흩어짐)
            lv.limit = 6f; lv.dampen = 0f; lv.drag = 1.4f; lv.multiplyDragByParticleSize = false; lv.multiplyDragByParticleVelocity = false;

            var col = ps.colorOverLifetime; col.enabled = true;   // 태어날 때/죽을 때만 부드럽게 (깜빡임은 FireflyZone)
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.06f), new GradientAlphaKey(1f, 0.92f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var sz = ps.sizeOverLifetime; sz.enabled = false;

            var li = ps.lights;
            li.enabled = particleLight != null;
            li.light = particleLight;
            li.ratio = 0.15f; li.useRandomDistribution = true;
            li.useParticleColor = true; li.sizeAffectsRange = false; li.alphaAffectsIntensity = true;
            li.rangeMultiplier = 1f; li.intensityMultiplier = 1f; li.maxLights = 2;
        }

        private static Texture2D MakeTexture()
        {
            if (!File.Exists(TexPath))
            {
                const int N = 128;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N / 2f, N / 2f)) / (N / 2f);
                        float core = Mathf.Exp(-(r * r) / (0.11f * 0.11f));          // 밝은 심
                        float halo = 0.38f * Mathf.Exp(-(r * r) / (0.38f * 0.38f));   // 부드러운 번짐
                        float a = Mathf.Clamp01(core + halo) * Mathf.Clamp01((1f - r) * 6f);   // 가장자리는 확실히 0
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                tex.Apply();
                File.WriteAllBytes(TexPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(TexPath);
                var ti = (TextureImporter)AssetImporter.GetAtPath(TexPath);
                ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true;
                ti.sRGBTexture = false; ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
        }

        private static Light MakeParticleLight()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(LightPath);
            if (existing != null) return existing.GetComponent<Light>();
            var go = new GameObject("P_FireflyParticleLight");
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.range = 1.6f; l.intensity = 1.2f; l.shadows = LightShadows.None;
            l.color = new Color(0.9f, 1f, 0.5f); l.lightmapBakeType = LightmapBakeType.Realtime;
            var saved = PrefabUtility.SaveAsPrefabAsset(go, LightPath);
            Object.DestroyImmediate(go);
            return saved.GetComponent<Light>();
        }

        // ───────────────────────── 5. 덤불·물가로 모으기 ─────────────────────────
        private struct Anchor { public Vector3 p; public float w; public string kind; }

        [MenuItem("HJH/맵/반딧불이/5 덤불·물가 쪽으로 모으기 (지금 씬)")]
        public static void Cluster()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Firefly] 플레이 중에는 실행하지 않습니다."); return; }
            var zones = Object.FindObjectsByType<FireflyZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
            if (zones.Length == 0) { Debug.LogWarning("[Firefly] 이 씬에 FireflyZone 이 없습니다."); return; }

            var anchors = CollectAnchors(out var counts);
            if (anchors.Count == 0) { Debug.LogWarning("[Firefly] 덤불/물가 기준점을 못 찾았습니다. 그대로 둡니다."); return; }

            // 기준점마다 주변(3m) 식생 밀도
            var score = new float[anchors.Count];
            for (int i = 0; i < anchors.Count; i++)
            {
                float s = 0f;
                for (int j = 0; j < anchors.Count; j++)
                {
                    float d = FlatDist(anchors[i].p, anchors[j].p);
                    if (d < 3f) s += anchors[j].w * (1f - d / 3f);
                }
                score[i] = s;
            }

            var terrain = Terrain.activeTerrain;
            var placed = new List<Vector3>();
            int moved = 0, kept = 0;
            var kinds = new Dictionary<string, int>();
            foreach (var z in zones)
            {
                var zp = z.transform.position;
                int best = -1; float bestS = 0f;
                for (int i = 0; i < anchors.Count; i++)
                {
                    float d = FlatDist(anchors[i].p, zp);
                    if (d > 8f) continue;
                    bool near = false;
                    foreach (var q in placed) if (FlatDist(q, anchors[i].p) < 3.5f) { near = true; break; }
                    if (near) continue;
                    float s = score[i] - d * 0.05f;   // 비슷하면 가까운 쪽
                    if (s > bestS) { bestS = s; best = i; }
                }
                if (best < 0 || bestS < 0.8f) { placed.Add(zp); kept++; continue; }

                var a = anchors[best].p;
                float gy = terrain != null ? terrain.SampleHeight(a) + terrain.transform.position.y : a.y;
                Undo.RecordObject(z.transform, "Firefly cluster");
                z.transform.position = new Vector3(a.x, gy, a.z);
                placed.Add(z.transform.position);
                moved++;
                kinds.TryGetValue(anchors[best].kind, out int c); kinds[anchors[best].kind] = c + 1;
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            var sb = new System.Text.StringBuilder();
            foreach (var kv in counts) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
            var sk = new System.Text.StringBuilder();
            foreach (var kv in kinds) sk.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
            Debug.Log($"[Firefly] 구역 {zones.Length}곳 중 {moved}곳을 덤불·물가 쪽으로 옮김, {kept}곳은 근처에 없어 그대로. 기준점: {sb} / 옮긴 곳: {sk}. 저장은 직접 (Ctrl+Z 가능).");
        }

        private static List<Anchor> CollectAnchors(out Dictionary<string, int> counts)
        {
            counts = new Dictionary<string, int>();
            var list = new List<Anchor>();
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var rootGo in scene.GetRootGameObjects())
                foreach (var t in rootGo.GetComponentsInChildren<Transform>(false))
                {
                    var go = t.gameObject;
                    if (go.GetComponentInParent<FireflyZone>() != null) continue;
                    string name;
                    if (PrefabUtility.IsAnyPrefabInstanceRoot(go))
                    {
                        var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                        name = src != null ? src.name : go.name;
                    }
                    else name = go.name;
                    string n = name.ToLowerInvariant();

                    // 물웅덩이: 가장자리(바운즈 타원) 위 점들
                    if ((n.StartsWith("waterswamp") || n == "swamp_water") && go.TryGetComponent<Renderer>(out var wr))
                    {
                        var b = wr.bounds;
                        if (b.extents.x > 30f || b.extents.z > 30f) continue;   // 맵 전체 물판은 제외
                        int k = Mathf.Clamp(Mathf.RoundToInt((b.extents.x + b.extents.z) * 1.6f), 6, 24);
                        for (int i = 0; i < k; i++)
                        {
                            float ang = i * Mathf.PI * 2f / k;
                            var p = new Vector3(b.center.x + Mathf.Cos(ang) * b.extents.x * 0.95f, b.center.y, b.center.z + Mathf.Sin(ang) * b.extents.z * 0.95f);
                            list.Add(new Anchor { p = p, w = 0.6f, kind = "water" });
                        }
                        Count(counts, "water");
                        continue;
                    }
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;

                    float w = 0f; string kind = null;
                    if (n.Contains("flower") || n.Contains("bush") || n.Contains("fern") || n.Contains("plant")) { w = 1f; kind = "flower"; }
                    else if (n.Contains("mushroom")) { w = 1f; kind = "mushroom"; }
                    else if (n.Contains("crystal") || n.Contains("lantern") || n.Contains("altar")) { w = 1.2f; kind = "crystal"; }
                    else if (n.Contains("roots") || n.Contains("fallenlog")) { w = 0.7f; kind = "roots"; }
                    else if (n.Contains("jungletree")) { w = 0.12f; kind = "tree"; }
                    if (kind == null) continue;
                    list.Add(new Anchor { p = t.position, w = w, kind = kind });
                    Count(counts, kind);
                }
            return list;
        }

        private static void Count(Dictionary<string, int> d, string k) { d.TryGetValue(k, out int c); d[k] = c + 1; }
        private static float FlatDist(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }
    }
}
