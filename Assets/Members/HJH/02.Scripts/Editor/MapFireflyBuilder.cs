using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using E = HJH.EditorTools.MapEdgePolish;
using HJH.MapFx;

namespace HJH.EditorTools
{
    // 반딧불이 구역을 만들고 가장자리 나무 쪽에 놓는다. 메뉴: HJH/맵/반딧불이/...
    //  1) 에셋 만들기: 부드러운 원형 텍스처, 덧셈 머티리얼 2개, FireflyZone 프리팹 (반딧불이 파티클 + 큰 번짐 + 점광원)
    //  2) 가장자리에 놓기: 외곽선 안쪽 2~5.5m(나무 앞)에 ~14곳. 라이트는 가장자리 나무 쪽으로 비껴 놓아 잎에 빛이 닿게 한다.
    //  3) 라이트 켜기/끄기: 성능이 부족하면 파티클만 쓴다.
    public static class MapFireflyBuilder
    {
        private const string Dir = "Assets/Members/HJH/05.Data/Fx/Firefly/";
        private const string Group = "Fireflies";

        [MenuItem("HJH/맵/반딧불이/1 에셋 만들기 (텍스처+머티리얼+프리팹)")]
        private static void BuildAssets()
        {
            if (EditorApplication.isPlaying) return;
            Directory.CreateDirectory(Dir);

            // 부드러운 원형 글로우 텍스처
            string texPath = Dir + "T_FireflyGlow.png";
            if (!File.Exists(texPath))
            {
                const int N = 64;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N / 2f, N / 2f)) / (N / 2f);
                        float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                tex.Apply();
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);
                var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
                ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
            var glow = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            var matFly = MakeMat(Dir + "M_Firefly.mat", glow, new Color(3.2f, 2.9f, 0.9f, 1f));   // HDR → Bloom 에 걸려 반짝임
            var matHalo = MakeMat(Dir + "M_FireflyHalo.mat", glow, new Color(0.55f, 0.9f, 0.55f, 1f));

            // 프리팹
            var root = new GameObject("FireflyZone");
            var zone = root.AddComponent<FireflyZone>();

            var flies = MakeSystem(root.transform, "Flies", matFly);
            {
                var main = flies.main;
                main.loop = true; main.prewarm = true; main.playOnAwake = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.45f), new Color(0.55f, 1f, 0.7f));
                main.maxParticles = 28;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                var em = flies.emission; em.rateOverTime = 3.6f;
                var sh = flies.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 3.2f; sh.scale = new Vector3(1f, 0.45f, 1f); sh.position = new Vector3(0f, 1.5f, 0f);
                var nz = flies.noise; nz.enabled = true; nz.strength = 0.55f; nz.frequency = 0.4f; nz.scrollSpeed = 0.15f; nz.damping = true; nz.octaveCount = 1; nz.quality = ParticleSystemNoiseQuality.Low;
                var col = flies.colorOverLifetime; col.enabled = true;
                var g = new Gradient();   // 반짝이며 나타났다 사라짐
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.25f, 0.25f), new GradientAlphaKey(1f, 0.4f),
                                  new GradientAlphaKey(0.3f, 0.6f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(g);
            }

            var halo = MakeSystem(root.transform, "Halo", matHalo);   // 빛이 잎에 번지는 느낌의 큰 흐릿한 덩어리
            {
                var main = halo.main;
                main.loop = true; main.prewarm = true; main.playOnAwake = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(1.8f, 3.0f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 1f, 0.55f, 0.10f), new Color(0.5f, 1f, 0.8f, 0.07f));
                main.maxParticles = 6;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                var em = halo.emission; em.rateOverTime = 0.6f;
                var sh = halo.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 2.4f; sh.scale = new Vector3(1f, 0.6f, 1f); sh.position = new Vector3(0f, 2.4f, 0f);
                var nz = halo.noise; nz.enabled = true; nz.strength = 0.25f; nz.frequency = 0.2f; nz.scrollSpeed = 0.1f; nz.damping = true; nz.quality = ParticleSystemNoiseQuality.Low;
                var col = halo.colorOverLifetime; col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.35f), new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(g);
            }

            var lgo = new GameObject("GlowLight");
            lgo.transform.SetParent(root.transform, false);
            lgo.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            var li = lgo.AddComponent<Light>();
            li.type = LightType.Point; li.color = new Color(0.85f, 1f, 0.5f); li.intensity = 1.6f; li.range = 6f; li.shadows = LightShadows.None;
            li.lightmapBakeType = LightmapBakeType.Realtime; // 깜빡이므로 실시간

            var so = new SerializedObject(zone);
            so.FindProperty("glowLight").objectReferenceValue = li;
            var arr = so.FindProperty("systems"); arr.arraySize = 2;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = flies;
            arr.GetArrayElementAtIndex(1).objectReferenceValue = halo;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, Dir + "FireflyZone.prefab");
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[Firefly] 에셋 생성 완료: " + Dir + " (T_FireflyGlow, M_Firefly, M_FireflyHalo, FireflyZone.prefab). 이제 2번으로 놓으세요.");
        }

        private static Material MakeMat(string path, Texture2D tex, Color baseColor)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetFloat("_Surface", 1f);       // Transparent
            m.SetFloat("_Blend", 2f);         // Additive
            m.SetFloat("_SrcBlend", 5f);      // SrcAlpha
            m.SetFloat("_DstBlend", 1f);      // One
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static ParticleSystem MakeSystem(Transform parent, string name, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 0.6f;
            return ps;
        }

        // ───────────────────────── 2. 놓기 ─────────────────────────
        private static void Place()
        {
            if (EditorApplication.isPlaying) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "FireflyZone.prefab");
            var terrain = Terrain.activeTerrain;
            if (prefab == null || terrain == null) { Debug.LogWarning("[Firefly] 먼저 1번으로 에셋을 만드세요 (Terrain 도 필요)."); return; }

            var old = GameObject.Find(Group);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject(Group);
            Undo.RegisterCreatedObjectUndo(root, "Fireflies");
            var props = GameObject.Find("Props");
            if (props != null) root.transform.SetParent(props.transform, false);

            var rng = new System.Random(1109);
            float R() => (float)rng.NextDouble();
            var placed = new System.Collections.Generic.List<Vector2>();
            const int target = 14;
            for (int tries = 0; placed.Count < target && tries < 4000; tries++)
            {
                var p = new Vector2(Mathf.Lerp(0f, 60f, R()), Mathf.Lerp(0f, 60f, R()));
                if (!E.Inside(p)) continue;
                float d = E.DistEdge(p);
                if (d < 2.0f || d > 5.5f) continue;
                bool bad = false;
                foreach (var q in placed) if (Vector2.Distance(q, p) < 9f) { bad = true; break; }
                if (bad) continue;

                var edge = NearestEdgePoint(p);
                var dir = (edge - p).normalized;          // 가장자리(나무) 방향
                float gy = terrain.SampleHeight(new Vector3(p.x, 0, p.y)) + terrain.transform.position.y;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                Undo.RegisterCreatedObjectUndo(go, "Fireflies");
                go.transform.position = new Vector3(p.x, gy, p.y);
                // 라이트를 나무 쪽으로 비껴 놓아 잎에 빛이 닿게 (구역 중심보다 가장자리 쪽 1.6m)
                var light = go.GetComponentInChildren<Light>();
                if (light != null) light.transform.position = new Vector3(p.x + dir.x * 1.6f, gy + 2.3f, p.y + dir.y * 1.6f);
                placed.Add(p);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Firefly] 반딧불이 구역 {placed.Count}곳을 가장자리 나무 쪽에 놓았습니다. 플레이하면 움직입니다 (구역마다 라이트 1개, 카메라에서 42m 넘으면 멈춤).");
        }

        private static Vector2 NearestEdgePoint(Vector2 p)
        {
            Vector2 best = p; float bd = float.MaxValue;
            var o = OutlinePts;
            for (int i = 0; i < o.Length; i++)
            {
                var a = o[i]; var b = o[(i + 1) % o.Length]; var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                var c = a + ab * t; float dd = Vector2.Distance(p, c);
                if (dd < bd) { bd = dd; best = c; }
            }
            return best;
        }

        private static readonly Vector2[] OutlinePts =
        {
            new Vector2(5, 60), new Vector2(25, 60), new Vector2(25, 30), new Vector2(30, 30),
            new Vector2(30, 25), new Vector2(60, 25), new Vector2(60, 5), new Vector2(30, 5),
            new Vector2(30, 0), new Vector2(0, 0), new Vector2(0, 30), new Vector2(5, 30),
        };

        // ───────────────────────── 3. 라이트 켜기/끄기 ─────────────────────────
        [MenuItem("HJH/맵/반딧불이/라이트 끄기 (파티클만)")]
        private static void LightsOff() => SetLights(false);

        [MenuItem("HJH/맵/반딧불이/라이트 켜기")]
        private static void LightsOn() => SetLights(true);

        private static void SetLights(bool on)
        {
            int n = 0;
            foreach (var z in Object.FindObjectsByType<FireflyZone>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(z);
                so.FindProperty("lightEnabled").boolValue = on;
                so.ApplyModifiedProperties();
                var l = z.GetComponentInChildren<Light>(true);
                if (l != null) { Undo.RecordObject(l, "Firefly light"); l.enabled = on; }
                n++;
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Firefly] 구역 {n}곳의 라이트를 {(on ? "켰" : "껐")}습니다.");
        }
    }
}
