using System.Collections.Generic;
using System.IO;
using Assets.Members.HJH._02.Scripts.Char.TopDown;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Members.HJH.Environment.EditorTools
{
    // 메뉴: HJH > Jungle Water > 테스트 씬 만들기
    // 울퉁불퉁한 늪 터레인 + 임시 맹그로브 + 얕은 물 + 안개 + 테스트 캐릭터를 한 번에 만든다.
    // 만든 에셋은 전부 09.JungleWater/Generated 에 저장된다.
    // 다시 실행하면 터레인/씬은 새로 만들고, 머티리얼은 기존 값(직접 조정한 것)을 그대로 쓴다.
    public static class JungleWaterSceneBuilder
    {
        private const string Root = "Assets/Members/HJH/09.JungleWater";
        private const string Generated = Root + "/Generated";
        private const string ScenePath = "Assets/Members/HJH/01.Scene/JungleWaterTest.unity";

        private const float MapSize = 48f;
        private const float MaxHeight = 6f;
        private const float WaterY = 1f;
        private const float FogTop = WaterY + 1.6f;
        private const int IgnoreRaycastLayer = 2;

        private struct Materials
        {
            public Material Water, Fog, Splash, Terrain, Bark, Leaf;
        }

        [MenuItem("HJH/Jungle Water/테스트 씬 만들기")]
        public static void Build()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Jungle Water", "씬과 터레인을 새로 만들어 덮어씁니다.\n(머티리얼 설정은 유지)", "만들기", "취소"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            foreach (string sub in new[] { "Materials", "Textures", "Terrain", "Prefabs" })
                EnsureFolder($"{Generated}/{sub}");

            Materials mats = CreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupLighting();
            Terrain terrain = BuildTerrain(mats.Terrain);
            ScatterMangroves(terrain, BuildMangrovePrefab(mats));
            Renderer water = BuildWaterAndFog(mats);
            GameObject walker = BuildWalker(terrain);
            BuildCamera(walker.transform);
            BuildWaterSystems(walker.transform, water, BuildSplashPrefab(mats.Splash));

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = walker;
            Debug.Log($"[JungleWater] 씬 생성 완료: {ScenePath}  (플레이: WASD 이동 / Space 물튀김 / 클릭 물튀김)");
        }

        // ---------------- 머티리얼 / 텍스처 ----------------

        private static Materials CreateMaterials() => new()
        {
            Water = LoadOrCreateMaterial("M_JungleWater", "HJH/JungleWater/ShallowWater", null),
            Fog = LoadOrCreateMaterial("M_JungleFog", "HJH/JungleWater/GroundFog", null),
            Splash = LoadOrCreateMaterial("M_WaterSplash", "HJH/JungleWater/SplashParticle", null),
            Terrain = LoadOrCreateMaterial("M_JungleTerrain", "Universal Render Pipeline/Terrain/Lit", null),
            Bark = LoadOrCreateMaterial("M_MangroveBark", "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", new Color(0.26f, 0.2f, 0.15f));
                m.SetFloat("_Smoothness", 0.1f);
            }),
            Leaf = LoadOrCreateMaterial("M_MangroveLeaf", "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", new Color(0.14f, 0.3f, 0.14f));
                m.SetFloat("_Smoothness", 0.15f);
            }),
        };

        private static Material LoadOrCreateMaterial(string name, string shaderName, System.Action<Material> init)
        {
            string path = $"{Generated}/Materials/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[JungleWater] 셰이더를 못 찾음: {shaderName} (컴파일 에러 확인)");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var mat = new Material(shader);
            init?.Invoke(mat);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // 이음새 없이 반복되는 노이즈 텍스처
        private static Texture2D MakeNoiseTexture(string name, Color dark, Color light, float frequency)
        {
            const int size = 256;
            string path = $"{Generated}/Textures/{name}.png";

            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = TileableNoise(x, y, size, frequency) * 0.7f + TileableNoise(x, y, size, frequency * 4f) * 0.3f;
                pixels[y * size + x] = Color.Lerp(dark, light, n);
            }
            tex.SetPixels(pixels);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float TileableNoise(int x, int y, int size, float frequency)
        {
            float s = frequency / size;
            float fx = (float)x / size, fy = (float)y / size;
            float a = Mathf.PerlinNoise(x * s, y * s);
            float b = Mathf.PerlinNoise((x - size) * s, y * s);
            float c = Mathf.PerlinNoise(x * s, (y - size) * s);
            float d = Mathf.PerlinNoise((x - size) * s, (y - size) * s);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static TerrainLayer MakeLayer(string name, Texture2D tex, float tile, float smoothness)
        {
            string path = $"{Generated}/Terrain/{name}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }

            layer.diffuseTexture = tex;
            layer.tileSize = new Vector2(tile, tile);
            layer.smoothness = smoothness;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        // ---------------- 터레인 ----------------

        private static Terrain BuildTerrain(Material terrainMaterial)
        {
            string dataPath = $"{Generated}/Terrain/JungleTerrainData.asset";
            AssetDatabase.DeleteAsset(dataPath);

            var data = new TerrainData { heightmapResolution = 129, alphamapResolution = 128, baseMapResolution = 256 };
            data.size = new Vector3(MapSize, MaxHeight, MapSize);

            int res = data.heightmapResolution;
            var heights = new float[res, res];
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float wx = (float)x / (res - 1) * MapSize;
                float wz = (float)z / (res - 1) * MapSize;
                heights[z, x] = HeightAt(wx, wz) / MaxHeight;
            }
            data.SetHeights(0, 0, heights);

            data.terrainLayers = new[]
            {
                MakeLayer("TL_Mud", MakeNoiseTexture("T_Mud", new Color(0.16f, 0.12f, 0.08f), new Color(0.3f, 0.23f, 0.15f), 6f), 3f, 0.35f),
                MakeLayer("TL_WetMoss", MakeNoiseTexture("T_WetMoss", new Color(0.15f, 0.17f, 0.08f), new Color(0.28f, 0.3f, 0.14f), 8f), 3f, 0.25f),
                MakeLayer("TL_Moss", MakeNoiseTexture("T_Moss", new Color(0.1f, 0.2f, 0.07f), new Color(0.22f, 0.36f, 0.12f), 10f), 4f, 0.05f),
            };
            PaintSplat(data);

            AssetDatabase.CreateAsset(data, dataPath);

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "JungleTerrain";
            var terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = terrainMaterial;
            return terrain;
        }

        // 기본은 물높이(1m) 근처를 오르내리는 울퉁불퉁한 늪, 가운데는 시작 섬, 가장자리는 둑
        private static float HeightAt(float x, float z)
        {
            float n = Mathf.PerlinNoise(x * 0.06f + 31.7f, z * 0.06f + 11.3f) * 0.6f
                    + Mathf.PerlinNoise(x * 0.15f + 5.1f, z * 0.15f + 71.9f) * 0.3f
                    + Mathf.PerlinNoise(x * 0.5f + 91f, z * 0.5f + 3f) * 0.1f;
            float h = 0.35f + n * 1.1f;

            float toCenter = Vector2.Distance(new Vector2(x, z), new Vector2(MapSize * 0.5f, MapSize * 0.5f));
            h = Mathf.Max(h, WaterY + 0.25f - Mathf.Max(0f, toCenter - 3f) * 0.15f);

            float edge = Mathf.Min(Mathf.Min(x, MapSize - x), Mathf.Min(z, MapSize - z));
            float wall = 1f - Smooth(0f, 6f, edge);
            return Mathf.Lerp(h, 3.2f + n * 0.8f, wall);
        }

        private static void PaintSplat(TerrainData data)
        {
            int res = data.alphamapResolution;
            var maps = new float[res, res, 3];
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float nx = (x + 0.5f) / res, nz = (z + 0.5f) / res;
                float h = data.GetInterpolatedHeight(nx, nz);
                float steep = data.GetSteepness(nx, nz);

                float mud = 1f - Smooth(WaterY - 0.02f, WaterY + 0.2f, h);
                float moss = Smooth(WaterY + 0.25f, WaterY + 0.8f, h);
                float wet = Mathf.Max(0f, 1f - mud - moss);
                mud += Smooth(25f, 45f, steep);

                float sum = mud + wet + moss;
                maps[z, x, 0] = mud / sum;
                maps[z, x, 1] = wet / sum;
                maps[z, x, 2] = moss / sum;
            }
            data.SetAlphamaps(0, 0, maps);
        }

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        // ---------------- 임시 맹그로브 ----------------

        private static GameObject BuildMangrovePrefab(Materials mats)
        {
            var root = new GameObject("MangrovePlaceholder");

            // 줄기: 땅 위 1m 부터 시작 (아래는 뿌리가 받침)
            var trunk = Primitive(PrimitiveType.Cylinder, root.transform, mats.Bark, keepCollider: true);
            trunk.name = "Trunk";
            trunk.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            trunk.transform.localScale = new Vector3(0.35f, 1.4f, 0.35f);

            // 버팀뿌리: 줄기 아래에서 바깥으로 휘어져 땅에 박힘
            const int rootCount = 7;
            var top = new Vector3(0f, 1.6f, 0f);
            for (int i = 0; i < rootCount; i++)
            {
                float angle = (i + 0.3f * (i % 2)) / rootCount * Mathf.PI * 2f;
                float reach = 1.1f + 0.3f * (i % 3);
                var bottom = new Vector3(Mathf.Cos(angle) * reach, -0.2f, Mathf.Sin(angle) * reach);
                Vector3 span = top - bottom;

                var rootPart = Primitive(PrimitiveType.Cylinder, root.transform, mats.Bark, keepCollider: false);
                rootPart.name = $"Root{i}";
                rootPart.transform.localPosition = (top + bottom) * 0.5f;
                rootPart.transform.localRotation = Quaternion.FromToRotation(Vector3.up, span);
                rootPart.transform.localScale = new Vector3(0.1f, span.magnitude * 0.5f, 0.1f);
            }

            // 잎: 납작한 구 몇 개 (탑뷰 시야를 너무 가리지 않게 작게)
            var canopyOffsets = new[] { new Vector3(0f, 4.1f, 0f), new Vector3(0.8f, 3.8f, 0.4f), new Vector3(-0.6f, 3.9f, -0.6f) };
            foreach (Vector3 offset in canopyOffsets)
            {
                var leaf = Primitive(PrimitiveType.Sphere, root.transform, mats.Leaf, keepCollider: false);
                leaf.name = "Canopy";
                leaf.transform.localPosition = offset;
                leaf.transform.localScale = new Vector3(2f, 0.9f, 2f);
            }

            string path = $"{Generated}/Prefabs/MangrovePlaceholder.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject Primitive(PrimitiveType type, Transform parent, Material mat, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!keepCollider)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // 물가(수면 살짝 아래 ~ 살짝 위)에만 심는다
        private static void ScatterMangroves(Terrain terrain, GameObject prefab)
        {
            var parent = new GameObject("Mangroves").transform;
            var placed = new List<Vector2>();
            var center = new Vector2(MapSize * 0.5f, MapSize * 0.5f);
            Random.State oldState = Random.state;
            Random.InitState(1234);

            for (int attempt = 0; attempt < 400 && placed.Count < 24; attempt++)
            {
                var p = new Vector2(Random.Range(5f, MapSize - 5f), Random.Range(5f, MapSize - 5f));
                if (Vector2.Distance(p, center) < 5f)
                    continue;

                float h = terrain.SampleHeight(new Vector3(p.x, 0f, p.y));
                if (h < WaterY - 0.3f || h > WaterY + 0.35f)
                    continue;

                bool tooClose = false;
                foreach (Vector2 other in placed)
                    if (Vector2.Distance(p, other) < 3.5f) { tooClose = true; break; }
                if (tooClose)
                    continue;

                placed.Add(p);
                var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                tree.transform.position = new Vector3(p.x, h, p.y);
                tree.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                tree.transform.localScale = Vector3.one * Random.Range(0.8f, 1.3f);
            }

            Random.state = oldState;
        }

        // ---------------- 물 / 안개 ----------------

        private static Renderer BuildWaterAndFog(Materials mats)
        {
            var water = Primitive(PrimitiveType.Plane, null, mats.Water, keepCollider: false);
            water.name = "Water";
            water.transform.position = new Vector3(MapSize * 0.5f, WaterY, MapSize * 0.5f);
            water.transform.localScale = Vector3.one * (MapSize / 10f);
            NoShadowCast(water);

            // 카메라가 맵 가장자리를 볼 때도 덮이도록 크게
            var fog = Primitive(PrimitiveType.Plane, null, mats.Fog, keepCollider: false);
            fog.name = "GroundFog";
            fog.transform.position = new Vector3(MapSize * 0.5f, FogTop, MapSize * 0.5f);
            fog.transform.localScale = Vector3.one * (MapSize * 1.6f / 10f);
            NoShadowCast(fog);
            fog.GetComponent<MeshRenderer>().receiveShadows = false;
            return water.GetComponent<Renderer>();
        }

        private static void NoShadowCast(GameObject go) =>
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

        // ---------------- 조명 / 카메라 / 캐릭터 ----------------

        private static void SetupLighting()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.94f, 0.8f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            RenderSettings.sun = light;
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.55f, 0.5f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.36f, 0.28f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.14f, 0.1f);
        }

        private static GameObject BuildWalker(Terrain terrain)
        {
            var center = new Vector3(MapSize * 0.5f, 0f, MapSize * 0.5f);
            center.y = terrain.SampleHeight(center) + 0.05f;

            var walker = new GameObject("TestWalker") { layer = IgnoreRaycastLayer };
            walker.transform.position = center;

            var controller = walker.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 1f, 0f);
            controller.height = 2f;
            controller.radius = 0.4f;

            walker.AddComponent<WaterTestWalker>();
            walker.AddComponent<WaterRippleEmitter>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.layer = IgnoreRaycastLayer;
            body.transform.SetParent(walker.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            Object.DestroyImmediate(body.GetComponent<Collider>());

            return walker;
        }

        private static void BuildCamera(Transform target)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.22f, 0.18f);
            cam.farClipPlane = 120f;
            camGo.AddComponent<AudioListener>();

            var follow = camGo.AddComponent<TopDownCameraFollow>();
            var so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = target;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 에디터에서도 바로 보이게 시작 위치 맞춰 두기
            Quaternion rot = Quaternion.Euler(55f, 45f, 0f);
            camGo.transform.SetPositionAndRotation(target.position + Vector3.up - rot * Vector3.forward * 16f, rot);
        }

        private static void BuildWaterSystems(Transform revealTarget, Renderer waterSurface, ParticleSystem splashPrefab)
        {
            var go = new GameObject("JungleWaterSystem");

            var sim = go.AddComponent<WaterRippleSimulation>();
            var simSo = new SerializedObject(sim);
            simSo.FindProperty("simShader").objectReferenceValue = Shader.Find("Hidden/HJH/JungleWater/RippleSim");
            simSo.FindProperty("waterSurface").objectReferenceValue = waterSurface;
            simSo.FindProperty("waterHeight").floatValue = WaterY;
            simSo.FindProperty("areaMin").vector2Value = Vector2.zero;
            simSo.FindProperty("areaSize").floatValue = MapSize;
            simSo.FindProperty("revealTarget").objectReferenceValue = revealTarget;
            simSo.ApplyModifiedPropertiesWithoutUndo();

            var pool = go.AddComponent<WaterSplashPool>();
            var poolSo = new SerializedObject(pool);
            poolSo.FindProperty("splashPrefab").objectReferenceValue = splashPrefab;
            poolSo.ApplyModifiedPropertiesWithoutUndo();

            go.AddComponent<WaterSkillSplashListener>();
        }

        // ---------------- 물보라 프리팹 ----------------

        private static ParticleSystem BuildSplashPrefab(Material mat)
        {
            var root = new GameObject("WaterSplash");
            ConfigureDroplets(root.AddComponent<ParticleSystem>(), mat);

            var mist = new GameObject("Mist");
            mist.transform.SetParent(root.transform, false);
            ConfigureMist(mist.AddComponent<ParticleSystem>(), mat);

            string path = $"{Generated}/Prefabs/WaterSplash.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<ParticleSystem>();
        }

        private static void ConfigureDroplets(ParticleSystem ps, Material mat)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.startColor = new Color(0.85f, 0.93f, 0.9f, 0.9f);
            main.gravityModifier = 1.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)45) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.35f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = FadeOut(0.6f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 1.2f;
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void ConfigureMist(ParticleSystem ps, Material mat)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startColor = new Color(0.85f, 0.92f, 0.9f, 0.35f);
            main.gravityModifier = -0.03f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)6) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.5f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));

            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = FadeOut(0.3f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static Gradient FadeOut(float holdUntil)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, holdUntil), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        // ---------------- 유틸 ----------------

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
