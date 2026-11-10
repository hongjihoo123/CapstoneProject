using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.TopDown;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // 메뉴 HJH > 맵 > ㄴ자 일반맵 테스트 씬 만들기
    //
    // 새 씬 Map_L_Normal 을 만든다. 적은 안 나오고 걸어다니며 디자인/라이팅만 확인용.
    //   좌표 단위 m, 원점 = 코너 광장 남서쪽 모서리. X 동쪽, Z 북쪽.
    //   코너 광장 [0,30]x[0,30], 세로 팔 [5,25]x[30,60] (폭 20), 가로 팔 [30,60]x[5,25] (폭 20)
    //   시작 포탈 (15,56) 북서 끝 -> 종료 포탈 (57,15) 남동 끝
    //
    // 바닥은 Terrain (Start 씬과 같은 방식): ㄴ자 안은 평평하고 약간 울퉁불퉁, 바깥은 가파르게 솟은 언덕(= 벽),
    // 웅덩이는 푹 꺼지고, 이끼/진흙/젖은 이끼/뿌리 레이어를 거리·노이즈로 칠한다. 지형 데이터는 05.Data/Map 에 저장.
    // 프랍은 Start 씬(JJH/01_Scene/Map_Test/Start.unity)을 읽기 전용으로 열어 실제로 쓰는 것을 가져온다:
    //   나무 = KM "Plane.002 (1) 1" 프리팹 (줄기 + 잎 카드),
    //   바위 = Start 씬의 Rock.fbx 메시(머티리얼·회전 그대로 복제),
    //   뿌리 = MangrovePlaceholder / PF_MangroveRoots_0.
    // 다시 실행하면 같은 이름 씬을 처음부터 다시 만든다. 플레이 중에는 안 돈다.
    public static class MapLNormalBuilder
    {
        private const string ScenePath = "Assets/Members/HJH/01.Scene/Map_L_Normal.unity";
        private const string MatFolder = "Assets/Members/HJH/05.Data/Map";
        private const string PlayerPath = "Assets/Members/HJH/05.Data/Prefab/Player.prefab";
        private const string TerrainDataPath = MatFolder + "/MapL_TerrainData.asset";
        private const string LayerFolder = "Assets/Members/HJH/09.JungleWater/Generated/Terrain/";

        private const string StartScenePath = "Assets/Members/JJH/01_Scene/Map_Test/Start.unity";
        private const string TreeAsset = "Assets/Members/KM/Assets/Props/Natural/Rock/Plane.002 (1) 1.prefab";
        private const string CanopyAsset = "Assets/Members/KM/Assets/Props/Natural/Group (13).prefab";
        private const string RockFbx = "Assets/Members/KM/Assets/Props/Natural/Rock/Rock.fbx";
        private const string MangroveAsset = "Assets/Members/HJH/09.JungleWater/Generated/Prefabs/MangrovePlaceholder.prefab";
        private const string RootsAsset = "Assets/Members/HJH/09.JungleWater/Generated/Prefabs/Props/PF_MangroveRoots_0.prefab";

        // ㄴ자 외곽선
        private static readonly Vector2[] Outline =
        {
            new Vector2(5, 60), new Vector2(25, 60), new Vector2(25, 30), new Vector2(30, 30),
            new Vector2(30, 25), new Vector2(60, 25), new Vector2(60, 5), new Vector2(30, 5),
            new Vector2(30, 0), new Vector2(0, 0), new Vector2(0, 30), new Vector2(5, 30),
        };

        private static readonly Vector2 StartPortal = new Vector2(15, 56);
        private static readonly Vector2 EndPortal = new Vector2(57, 15);

        private static readonly (string name, Vector2 pos, string role)[] Spawns =
        {
            ("Spawn_1_세로팔_좌", new Vector2(10, 44), "근접"),
            ("Spawn_2_세로팔_우", new Vector2(20, 38), "근접"),
            ("Spawn_3_코너광장_뒤", new Vector2(8, 8), "주력/원거리"),
            ("Spawn_4_가로팔_북쪽", new Vector2(42, 22), "원거리"),
            ("Spawn_5_종료포탈_앞", new Vector2(51, 17), "엘리트"),
        };

        // 물웅덩이 (가로 팔 남쪽). 지형이 이만큼 푹 꺼진다.
        private static readonly Vector2 PondMin = new Vector2(36, 7);
        private static readonly Vector2 PondMax = new Vector2(54, 14);
        private const float PondDepth = 0.4f, WaterY = -0.12f;

        // 지형 크기: 128m 정사각, 원점 (-34, -2, -34), 높이 폭 16m (바닥 y=0 이 정규화 값 2/16)
        private const float TerrainSize = 128f, TerrainHeight = 16f, TerrainBaseY = -2f, TerrainOrigin = -34f;
        private const int HeightRes = 257, AlphaRes = 256;

        private static Terrain _terrain;

        private sealed class Template
        {
            public GameObject Prefab;       // 프리팹 인스턴스로 놓는 경우
            public Mesh Mesh;               // 아니면 메시 + 머티리얼로 새로 만든다 (Start 씬의 바위)
            public Material[] Materials;
            public Quaternion Rotation;
            public Vector3 Scale = Vector3.one;
        }

        private static readonly List<Template> TreeT = new List<Template>();
        private static readonly List<Template> RockT = new List<Template>();
        private static readonly List<Template> RootT = new List<Template>();
        private static readonly List<Template> CanopyT = new List<Template>();

        [MenuItem("HJH/맵/ㄴ자 일반맵 테스트 씬 만들기")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[MapL] 플레이 중에는 만들지 않습니다. 플레이를 끄고 다시 실행하세요.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var rng = new System.Random(20261109);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            HarvestStartProps();
            SceneManager.SetActiveScene(scene);

            EnsureFolder(MatFolder);
            EnsureRockFadeCopy();
            var startMat = MakeEmissiveMat("MapL_PortalStart", new Color(0.2f, 0.9f, 1f));
            var endMat = MakeEmissiveMat("MapL_PortalEnd", new Color(1f, 0.55f, 0.15f));
            var spawnMat = MakeEmissiveMat("MapL_SpawnMarker", new Color(1f, 0.15f, 0.2f));

            var root = new GameObject("Map_L_Normal").transform;

            BuildLighting(root);
            BuildTerrain(root);
            BuildWaterAndFog(root);
            BuildBoundary(root, rng);
            BuildPortals(root, startMat, endMat);
            BuildSpawns(root, spawnMat);
            BuildProps(root, rng);
            BuildPlayerAndCamera();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[MapL] 만들었습니다: " + ScenePath + " (플레이해서 돌아다니며 확인)");
            CaptureShots();
        }

        // 확인용 사진: 위에서 본 전체 + 게임 카메라 각도로 주요 지점 (Temp/MapLShots/*.png). 만든 직후 자동으로 찍는다.
        private static void CaptureShots()
        {
            try
            {
                string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Temp/MapLShots"));
                System.IO.Directory.CreateDirectory(dir);
                var camGo = new GameObject("~ShotCam");
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 30f;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 300f;
                var rt = new RenderTexture(1280, 800, 24, RenderTextureFormat.ARGB32);

                // 전체 위에서 (정사영)
                cam.orthographic = true;
                cam.orthographicSize = 42f;
                camGo.transform.position = new Vector3(30f, 120f, 30f);
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                Shoot(cam, rt, dir + "/1_overview.png");

                // 게임 카메라 각도(피치 55°, 요 45°, 거리 16m)로 주요 지점
                cam.orthographic = false;
                cam.fieldOfView = 30f;
                var spots = new[]
                {
                    ("2_start", new Vector2(15, 52)),
                    ("3_arm_ramp", new Vector2(15, 38)),
                    ("4_corner", new Vector2(14, 22)),
                    ("5_opening", new Vector2(30, 15)),
                    ("6_pond", new Vector2(45, 16)),
                    ("7_end", new Vector2(54, 15)),
                };
                foreach (var s in spots)
                {
                    Vector3 target = OnGround(s.Item2) + Vector3.up;
                    var rot = Quaternion.Euler(55f, 45f, 0f);
                    camGo.transform.SetPositionAndRotation(target - rot * Vector3.forward * 16f, rot);
                    Shoot(cam, rt, dir + "/" + s.Item1 + ".png");
                }

                Object.DestroyImmediate(camGo);
                rt.Release();
                Object.DestroyImmediate(rt);
                Debug.Log("[MapL] 확인용 사진: " + dir);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[MapL] 사진 찍기 실패: " + e.Message);
            }
        }

        private static void Shoot(Camera cam, RenderTexture rt, string path)
        {
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = prev;
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
        }

        // ------------------------------------------------------------ 라이팅
        private static void BuildLighting(Transform root)
        {
            var sunGo = new GameObject("Directional Light");
            sunGo.transform.SetParent(root, false);
            sunGo.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.8f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.62f, 0.6f);
            RenderSettings.ambientEquatorColor = new Color(0.28f, 0.4f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.17f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.55f, 0.74f, 0.68f);
            RenderSettings.fogDensity = 0.012f;
        }

        // ------------------------------------------------------------ 지형
        // 높이: ㄴ자 안(거리 0)은 거의 평평 + 살짝 굴곡, 밖으로 나가면 가파르게 솟은 뒤 완만한 언덕으로 이어짐.
        // 가장자리 경사는 CharacterController 의 경사 한계(45°)보다 가팔라서 그대로 벽이 된다.
        private static float HeightAt(Vector2 p)
        {
            float d = WaveD(p);
            float roll = Mathf.PerlinNoise(p.x * 0.11f + 40f, p.y * 0.11f + 70f) - 0.5f;      // -0.5 ~ 0.5
            float fine = Mathf.PerlinNoise(p.x * 0.4f + 5f, p.y * 0.4f + 9f) - 0.5f;
            float h = 0.07f + roll * 0.16f + fine * 0.05f;                                     // 약 -0.05 ~ 0.19 (물보다 항상 높음)

            // 단차 (SoD 처럼 바닥이 계단식): 세로 팔 위쪽(시작 구역)은 2m 높은 턱 위, 가로 팔 끝(종료 구역)은 0.9m 높음.
            // 경사 한계(45°) 안이라 걸어서 올라간다 (턱 2m / 4m 구간 = 최대 약 37°)
            h += 2.0f * Smooth(34f, 38f, p.y) + 0.9f * Smooth(48f, 56f, p.x);

            if (d > 0f)
            {
                float hills = Mathf.PerlinNoise(p.x * 0.06f + 11f, p.y * 0.06f + 23f);        // 0~1
                h += Smooth(0f, 2.2f, d) * (3.2f + hills * 1.2f);                              // 가장자리 급경사 벽
                h += Smooth(2f, 18f, d) * (2.5f + hills * 5f);                                 // 바깥 언덕
            }

            // 웅덩이: 가장자리에서 부드럽게 꺼짐
            float pr = PondInside(p);
            if (pr > 0f)
                h = Mathf.Lerp(h, -PondDepth + fine * 0.05f, Smooth(0f, 2.5f, pr));
            return h;
        }

        // 웅덩이 사각형 안쪽으로 얼마나 들어갔는지 (밖이면 0 이하)
        private static float PondInside(Vector2 p) =>
            Mathf.Min(Mathf.Min(p.x - PondMin.x, PondMax.x - p.x), Mathf.Min(p.y - PondMin.y, PondMax.y - p.y));

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static void BuildTerrain(Transform root)
        {
            var data = new TerrainData
            {
                heightmapResolution = HeightRes,
                size = new Vector3(TerrainSize, TerrainHeight, TerrainSize),
                alphamapResolution = AlphaRes,
            };

            var heights = new float[HeightRes, HeightRes];
            for (int zi = 0; zi < HeightRes; zi++)
            {
                for (int xi = 0; xi < HeightRes; xi++)
                {
                    var p = new Vector2(TerrainOrigin + xi * TerrainSize / (HeightRes - 1), TerrainOrigin + zi * TerrainSize / (HeightRes - 1));
                    heights[zi, xi] = Mathf.Clamp01((HeightAt(p) - TerrainBaseY) / TerrainHeight);
                }
            }
            data.SetHeights(0, 0, heights);

            // 레이어: 0 이끼(길) / 1 진흙 / 2 젖은 이끼(웅덩이) / 3 맹그로브 뿌리(언덕 비탈)
            var layers = new[]
            {
                AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerFolder + "TL_Moss.terrainlayer"),
                AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerFolder + "TL_Mud.terrainlayer"),
                AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerFolder + "TL_WetMoss.terrainlayer"),
                AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerFolder + "TL_MangroveRoots.terrainlayer"),
            };
            data.terrainLayers = layers;

            var alpha = new float[AlphaRes, AlphaRes, 4];
            for (int zi = 0; zi < AlphaRes; zi++)
            {
                for (int xi = 0; xi < AlphaRes; xi++)
                {
                    var p = new Vector2(TerrainOrigin + (xi + 0.5f) * TerrainSize / AlphaRes, TerrainOrigin + (zi + 0.5f) * TerrainSize / AlphaRes);
                    float d = WaveD(p);
                    float n = Mathf.PerlinNoise(p.x * 0.13f + 3f, p.y * 0.13f + 91f);
                    float nFine = Mathf.PerlinNoise(p.x * 0.5f + 17f, p.y * 0.5f + 5f);

                    float pond = PondInside(p);
                    float wet = Smooth(-0.5f, 2.5f, pond);                          // 웅덩이 안쪽
                    float bank = Mathf.Clamp01(1f - Mathf.Abs(pond) / 3.5f);         // 웅덩이 가장자리 띠 → 진흙
                    float outside = Smooth(0.3f, 3.5f, d);                           // 길 밖 (비탈)

                    float mud = Mathf.Clamp01(Smooth(0.52f, 0.72f, n) * 0.85f + bank * 0.8f + Smooth(0.6f, 0.9f, nFine) * 0.25f);
                    float roots = outside * (0.55f + 0.45f * n);
                    float moss = 1f;

                    float wMud = mud * (1f - wet) * (1f - outside * 0.5f);
                    float wRoots = roots * (1f - wet);
                    float wWet = wet;
                    float wMoss = moss * (1f - wet);
                    float sum = wMud + wRoots + wWet + wMoss;
                    alpha[zi, xi, 0] = wMoss / sum;
                    alpha[zi, xi, 1] = wMud / sum;
                    alpha[zi, xi, 2] = wWet / sum;
                    alpha[zi, xi, 3] = wRoots / sum;
                }
            }
            data.SetAlphamaps(0, 0, alpha);

            if (AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath) != null)
                AssetDatabase.DeleteAsset(TerrainDataPath);
            AssetDatabase.CreateAsset(data, TerrainDataPath);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain_L";
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(TerrainOrigin, TerrainBaseY, TerrainOrigin);
            _terrain = go.GetComponent<Terrain>();
            _terrain.drawInstanced = true;
            _terrain.heightmapPixelError = 4f;
            _terrain.shadowCastingMode = ShadowCastingMode.On;
        }

        private static float GroundY(Vector2 p)
        {
            if (_terrain == null) return 0f;
            return _terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + _terrain.transform.position.y;
        }

        private static Vector3 OnGround(Vector2 p) => new Vector3(p.x, GroundY(p), p.y);

        // 윤곽을 구불구불하게: 직선 가장자리에서 안쪽으로 최대 4.5m 노이즈만큼 파고든다 (바깥으로는 안 나가서 안전 벽과 안 어긋남).
        // 포탈·스폰·광장 중심 근처는 파고들지 않게 눌러서 길이 막히지 않게 한다.
        private static float WaveD(Vector2 p)
        {
            float sd = SignedDistToL(p);
            float n = Mathf.PerlinNoise(p.x * 0.07f + 200f, p.y * 0.07f + 300f);
            float inset = Mathf.Clamp((n - 0.4f) * 10f, 0f, 4.5f) * Smooth(5f, 14f, DistToKeyPoints(p));
            return Mathf.Max(0f, sd + inset);
        }

        private static float SignedDistToL(Vector2 p)
        {
            float e = float.MaxValue;
            for (int i = 0; i < Outline.Length; i++)
                e = Mathf.Min(e, DistToSegment(p, Outline[i], Outline[(i + 1) % Outline.Length]));
            return InsideL(p, 0f) ? -e : e;
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        private static float DistToKeyPoints(Vector2 p)
        {
            float m = Vector2.Distance(p, StartPortal);
            m = Mathf.Min(m, Vector2.Distance(p, EndPortal));
            foreach (var s in Spawns) m = Mathf.Min(m, Vector2.Distance(p, s.pos));
            return Mathf.Min(m, Vector2.Distance(p, new Vector2(15f, 15f)) - 8f);
        }

        // ㄴ자 사각형 3개까지의 대략적인 거리 (안쪽이면 0)
        private static float DistanceToL(Vector2 p) =>
            Mathf.Min(RectDist(p, 0, 0, 30, 30), Mathf.Min(RectDist(p, 5, 30, 25, 60), RectDist(p, 30, 5, 60, 25)));

        private static float RectDist(Vector2 p, float x0, float z0, float x1, float z1)
        {
            float dx = Mathf.Max(x0 - p.x, 0f, p.x - x1);
            float dz = Mathf.Max(z0 - p.y, 0f, p.y - z1);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static bool InsideL(Vector2 p, float margin)
        {
            return Inside(p, 0, 0, 30, 30, margin) || Inside(p, 5, 30, 25, 60, margin) || Inside(p, 30, 5, 60, 25, margin);
        }

        private static bool Inside(Vector2 p, float x0, float z0, float x1, float z1, float m) =>
            p.x > x0 + m && p.x < x1 - m && p.y > z0 + m && p.y < z1 - m;

        // ------------------------------------------------------------ 물·안개
        // 물: 09 물 시스템 그대로 (M_JungleWater + WaterRippleSimulation). 안개: 판 하나 + JungleFogController 는 런타임에 자동으로 붙음.
        private static void BuildWaterAndFog(Transform root)
        {
            var group = new GameObject("Water_Fog").transform;
            group.SetParent(root, false);

            var waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Members/HJH/09.JungleWater/Generated/Materials/M_JungleWater.mat");
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Water";
            plane.transform.SetParent(group, false);
            Vector2 size = PondMax - PondMin;
            // 시뮬레이션 영역은 정사각형이라 판도 정사각. 웅덩이 밖은 지형이 수면보다 높아서 가려줌.
            float side = Mathf.Max(size.x, size.y) + 2f;
            plane.transform.position = new Vector3((PondMin.x + PondMax.x) * 0.5f, WaterY, (PondMin.y + PondMax.y) * 0.5f);
            plane.transform.localScale = new Vector3(side / 10f, 1f, side / 10f);
            Object.DestroyImmediate(plane.GetComponent<Collider>());
            var wr = plane.GetComponent<MeshRenderer>();
            if (waterMat != null) wr.sharedMaterial = waterMat;
            else Debug.LogWarning("[MapL] M_JungleWater 를 못 찾았습니다.");
            wr.shadowCastingMode = ShadowCastingMode.Off;

            var sim = new GameObject("WaterRippleSimulation").AddComponent<Assets.Members.HJH.Environment.WaterRippleSimulation>();
            sim.transform.SetParent(group, false);
            var so = new SerializedObject(sim);
            so.FindProperty("simShader").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Shader>("Assets/Members/HJH/09.JungleWater/Shaders/JungleRippleSim.shader");
            so.FindProperty("waterSurface").objectReferenceValue = wr;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 안개판: 씬 전체를 덮고 바닥에서 약 1m 위
            var fogMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Members/HJH/05.Data/Fx/M_JungleFog_Stylized.mat");
            var fog = GameObject.CreatePrimitive(PrimitiveType.Plane);
            fog.name = "GroundFog";
            fog.transform.SetParent(group, false);
            fog.transform.position = new Vector3(30f, 1.0f, 30f);
            fog.transform.localScale = new Vector3(16f, 1f, 16f);
            Object.DestroyImmediate(fog.GetComponent<Collider>());
            var fr = fog.GetComponent<MeshRenderer>();
            if (fogMat != null) fr.sharedMaterial = fogMat;
            else Debug.LogWarning("[MapL] M_JungleFog_Stylized 를 못 찾았습니다.");
            fr.shadowCastingMode = ShadowCastingMode.Off;
            fr.receiveShadows = false;
        }

        // ------------------------------------------------------------ 외곽 (보이지 않는 안전 벽 + 경계 나무 + 바깥 숲)
        private static void BuildBoundary(Transform root, System.Random rng)
        {
            var walls = new GameObject("Boundary_Walls").transform;
            walls.SetParent(root, false);
            var trees = new GameObject("Boundary_Trees").transform;
            trees.SetParent(root, false);
            var canopyRoot = new GameObject("Canopy_Frame").transform;
            canopyRoot.SetParent(root, false);

            for (int i = 0; i < Outline.Length; i++)
            {
                Vector2 a = Outline[i];
                Vector2 b = Outline[(i + 1) % Outline.Length];
                Vector2 dir = (b - a);
                float len = dir.magnitude;
                dir /= len;
                Vector2 outward = new Vector2(dir.y, -dir.x);
                if (SignedArea() < 0f) outward = -outward;

                // 지형 벽이 뚫릴 때를 대비한 안전 콜라이더 (폭 2m, 높이 12m)
                var wall = new GameObject("Wall_" + i);
                wall.transform.SetParent(walls, false);
                Vector2 mid = (a + b) * 0.5f + outward * 1f;
                wall.transform.position = new Vector3(mid.x, 6f, mid.y);
                wall.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.y));
                wall.AddComponent<BoxCollider>().size = new Vector3(2f, 12f, len);

                // 경계 나무: 가장자리 바로 바깥 줄 (비탈 위)
                int count = Mathf.Max(2, Mathf.RoundToInt(len / 4.5f));
                for (int k = 0; k < count; k++)
                {
                    float t = (k + 0.5f) / count;
                    Vector2 p = Vector2.Lerp(a, b, t);
                    Vector2 tp = PushOut(p + outward * (2.2f + Rand(rng, 0f, 1.8f)), outward, 2.5f);
                    PlaceTree(trees, tp, Rand(rng, 9f, 13f), rng);
                }

                // 수관 틀: 가장자리 위에 낮게 드리운 큰 잎 덩어리 (SoD 화면 모서리를 덮는 어두운 나뭇잎). 약 9m 간격
                int canopies = Mathf.Max(1, Mathf.RoundToInt(len / 9f));
                for (int k = 0; k < canopies; k++)
                {
                    Vector2 p = Vector2.Lerp(a, b, (k + 0.5f) / canopies);
                    Vector2 cp = PushOut(p + outward * Rand(rng, 0.5f, 2.5f), outward, 1.0f);
                    PlaceCanopy(canopyRoot, cp, Rand(rng, 4.5f, 6.5f), Rand(rng, 5.5f, 8f), rng);
                }
            }

            PlaceGlowFlora(root, rng);

            // 바깥 숲: 언덕 위에 5.5m 격자 + 흔들기로 나무를 채워 ㄴ자 밖이 숲으로 보이게
            var forest = new GameObject("Forest_Fill").transform;
            forest.SetParent(root, false);
            for (float x = -14f; x <= 74f; x += 5.5f)
            {
                for (float z = -14f; z <= 74f; z += 5.5f)
                {
                    var p = new Vector2(x + Rand(rng, -1.8f, 1.8f), z + Rand(rng, -1.8f, 1.8f));
                    if (WaveD(p) < 5f) continue;   // 길 + 급경사 + 경계 나무 줄과 겹치지 않게
                    PlaceTree(forest, p, Rand(rng, 9f, 14f), rng);
                }
            }
        }

        // 지점이 길(윤곽이 파고든 안쪽)에 들어와 있으면 바깥 방향으로 1m씩 밀어낸다 (최대 8번)
        private static Vector2 PushOut(Vector2 p, Vector2 outward, float minD)
        {
            for (int i = 0; i < 8 && WaveD(p) < minD; i++)
                p += outward;
            return p;
        }

        // 수관(잎 덩어리): Start 씬의 잎 카드 프리팹(Group (13))을 공중에 띄운다.
        private static void PlaceCanopy(Transform parent, Vector2 p, float height, float baseHeight, System.Random rng)
        {
            if (CanopyT.Count == 0) return;
            var inst = Spawn(CanopyT[rng.Next(CanopyT.Count)], parent, rng);
            inst.transform.position = new Vector3(p.x, 0f, p.y);
            FitHeight(inst, 9f, GroundY(p) + baseHeight, height);
            foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        }

        // 발광 꽃: 가장자리를 따라 작은 푸른 발광 구슬 무리 (SoD 의 푸른 발광 꽃/덩굴 느낌). 빛은 안 내고 머티리얼 발광만.
        private static void PlaceGlowFlora(Transform root, System.Random rng)
        {
            var mat = MakeEmissiveMat("MapL_GlowFlora", new Color(0.35f, 0.75f, 1f));
            var group = new GameObject("GlowFlora").transform;
            group.SetParent(root, false);
            int placed = 0;
            for (int tries = 0; tries < 400 && placed < 46; tries++)
            {
                var p = new Vector2(Rand(rng, -2f, 62f), Rand(rng, -2f, 62f));
                float d = WaveD(p);
                if (d < 0.2f || d > 1.8f) continue;          // 길 가장자리 비탈 아래쪽 띠
                if (PondInside(p) > -2f) continue;           // 웅덩이 안·바로 옆은 비움
                var cluster = new GameObject("Flora_" + placed).transform;
                cluster.SetParent(group, false);
                cluster.position = OnGround(p);
                int n = rng.Next(4, 8);
                for (int i = 0; i < n; i++)
                {
                    var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    s.name = "Bloom";
                    s.transform.SetParent(cluster, false);
                    s.transform.localPosition = new Vector3(Rand(rng, -0.5f, 0.5f), Rand(rng, 0.1f, 0.55f), Rand(rng, -0.5f, 0.5f));
                    s.transform.localScale = Vector3.one * Rand(rng, 0.1f, 0.22f);
                    s.GetComponent<MeshRenderer>().sharedMaterial = mat;
                    s.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                    Object.DestroyImmediate(s.GetComponent<Collider>());
                }
                placed++;
            }
        }

        private static float SignedArea()
        {
            float s = 0f;
            for (int i = 0; i < Outline.Length; i++)
            {
                Vector2 a = Outline[i];
                Vector2 b = Outline[(i + 1) % Outline.Length];
                s += (a.x * b.y - b.x * a.y);
            }
            return s * 0.5f;
        }

        // ------------------------------------------------------------ Start 씬에서 프랍 가져오기
        // 이 프리팹/메시는 블렌더 회전이 구워져 있어서 기본 회전으로는 눕거나 기울어짐.
        // Start 씬 인스턴스의 실제 회전을 읽어 와서 같은 자세로 세운다.
        private static void HarvestStartProps()
        {
            TreeT.Clear(); RockT.Clear(); RootT.Clear(); CanopyT.Clear();
            var start = EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Additive);
            try
            {
                var foundPrefabs = new Dictionary<string, List<KeyValuePair<Quaternion, float>>>();
                var rocks = new List<Template>();
                foreach (var rootGo in start.GetRootGameObjects())
                    Collect(rootGo.transform, foundPrefabs, rocks);

                AddPrefabTemplates(TreeT, TreeAsset, foundPrefabs, true, 12);
                AddPrefabTemplates(RootT, MangroveAsset, foundPrefabs, true, 6);
                AddPrefabTemplates(RootT, RootsAsset, foundPrefabs, false, 6);
                AddPrefabTemplates(CanopyT, CanopyAsset, foundPrefabs, false, 12);

                // 바위는 고르게 뽑음 (최대 24개)
                int step = Mathf.Max(1, rocks.Count / 24);
                for (int i = 0; i < rocks.Count; i += step) RockT.Add(rocks[i]);
            }
            finally
            {
                EditorSceneManager.CloseScene(start, true);
            }
            Debug.Log($"[MapL] Start 씬에서 가져온 프랍: 나무 {TreeT.Count}, 바위 {RockT.Count}, 뿌리/맹그로브 {RootT.Count}");
            if (TreeT.Count == 0) Debug.LogWarning("[MapL] 나무 프리팹을 못 가져왔습니다 (" + TreeAsset + ")");
            if (RockT.Count == 0) Debug.LogWarning("[MapL] Start 씬에서 Rock.fbx 바위를 못 찾았습니다.");
        }

        private static void Collect(Transform t, Dictionary<string, List<KeyValuePair<Quaternion, float>>> found, List<Template> rocks)
        {
            var go = t.gameObject;
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(go))
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                if (path == TreeAsset || path == MangroveAsset || path == RootsAsset || path == CanopyAsset)
                {
                    float upright = 0f;
                    var rs = go.GetComponentsInChildren<Renderer>();
                    if (rs.Length > 0)
                    {
                        var b = rs[0].bounds;
                        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                        upright = b.size.y / Mathf.Max(0.001f, Mathf.Max(b.size.x, b.size.z));
                    }
                    if (!found.TryGetValue(path, out var list))
                        found[path] = list = new List<KeyValuePair<Quaternion, float>>();
                    list.Add(new KeyValuePair<Quaternion, float>(t.rotation, upright));
                }
            }

            // Rock.fbx 메시를 쓰는 오브젝트 = 바위
            var mf = go.GetComponent<MeshFilter>();
            var mr = go.GetComponent<MeshRenderer>();
            if (mf != null && mr != null && mf.sharedMesh != null && AssetDatabase.GetAssetPath(mf.sharedMesh) == RockFbx)
            {
                rocks.Add(new Template
                {
                    Mesh = mf.sharedMesh,
                    Materials = mr.sharedMaterials,
                    Rotation = t.rotation,
                    Scale = t.lossyScale,
                });
            }

            for (int i = 0; i < t.childCount; i++)
                Collect(t.GetChild(i), found, rocks);
        }

        private static void AddPrefabTemplates(List<Template> target, string assetPath,
            Dictionary<string, List<KeyValuePair<Quaternion, float>>> found, bool preferUpright, int max)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null) { Debug.LogWarning("[MapL] 프리팹 없음: " + assetPath); return; }
            if (!found.TryGetValue(assetPath, out var list) || list.Count == 0)
            {
                target.Add(new Template { Prefab = prefab, Rotation = prefab.transform.rotation });
                return;
            }
            if (preferUpright) list.Sort((a, b) => b.Value.CompareTo(a.Value));
            int n = Mathf.Min(max, list.Count);
            for (int i = 0; i < n; i++)
                target.Add(new Template { Prefab = prefab, Rotation = list[i].Key });
        }

        // 템플릿으로 오브젝트를 만든다 (프리팹 인스턴스 또는 메시 복제)
        private static GameObject Spawn(Template tpl, Transform parent, System.Random rng)
        {
            GameObject inst;
            if (tpl.Prefab != null)
            {
                inst = (GameObject)PrefabUtility.InstantiatePrefab(tpl.Prefab);
            }
            else
            {
                inst = new GameObject("Rock");
                inst.AddComponent<MeshFilter>().sharedMesh = tpl.Mesh;
                var r = inst.AddComponent<MeshRenderer>();
                r.sharedMaterials = tpl.Materials;
                inst.transform.localScale = tpl.Scale;
            }
            inst.transform.SetParent(parent, false);
            inst.transform.rotation = Quaternion.Euler(0f, Rand(rng, 0f, 360f), 0f) * tpl.Rotation;
            return inst;
        }

        private static void PlaceTree(Transform parent, Vector2 p, float height, System.Random rng)
        {
            if (TreeT.Count == 0) return;
            var inst = Spawn(TreeT[rng.Next(TreeT.Count)], parent, rng);
            inst.transform.position = new Vector3(p.x, 0f, p.y);
            FitHeight(inst, height, GroundY(p) - 0.15f);
            // 나무에는 콜라이더를 두지 않는다 (대시 걸림 방지)
            foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        }

        private static void PlaceProp(Transform parent, List<Template> list, Vector2 p, float height, System.Random rng)
        {
            if (list.Count == 0) return;
            var inst = Spawn(list[rng.Next(list.Count)], parent, rng);
            inst.transform.position = new Vector3(p.x, 0f, p.y);
            FitHeight(inst, height, GroundY(p) - 0.05f, height * 3.5f);
            FitColliders(inst);
        }

        // 프리팹에 들어 있던 콜라이더는 지우고 실제 메시 모양을 따르는 볼록 메시 콜라이더로 바꿈
        // (렌더러와 같은 오브젝트 → 바위 투명 처리 광선도 맞음)
        private static void FitColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.GetComponent<MeshRenderer>() == null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = true;
            }
        }

        // 렌더러 바운딩 높이가 목표 높이가 되게 균등 스케일하고, 바닥(groundY)에 붙인다.
        private static void FitHeight(GameObject go, float targetHeight, float groundY, float maxWidth = 12f)
        {
            var b = GetBounds(go);
            if (b.size.y > 0.0001f)
                {
                // 높이 기준으로 맞추되, 납작한 메시(잎 카드 덩어리 등)가 가로로 어마어마하게 커지지 않게 폭 상한도 건다
                float s = targetHeight / b.size.y;
                float w = Mathf.Max(b.size.x, b.size.z);
                if (w * s > maxWidth) s = maxWidth / Mathf.Max(0.001f, w);
                go.transform.localScale *= s;
            }
            b = GetBounds(go);
            go.transform.position += new Vector3(0f, groundY - b.min.y, 0f);
        }

        private static Bounds GetBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // ------------------------------------------------------------ 포탈 / 스폰 (표시만)
        private static void BuildPortals(Transform root, Material startMat, Material endMat)
        {
            var group = new GameObject("Portals").transform;
            group.SetParent(root, false);
            MakePortal(group, "Portal_Start", OnGround(StartPortal), startMat, new Color(0.2f, 0.9f, 1f));
            MakePortal(group, "Portal_End", OnGround(EndPortal), endMat, new Color(1f, 0.55f, 0.15f));
        }

        private static void MakePortal(Transform parent, string name, Vector3 pos, Material mat, Color light)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            // 바닥에 은은한 원판 (포탈 자리) + 랜턴 기둥 (SoD 의 청록 랜턴처럼 멀리서도 보이는 랜드마크)
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";
            disc.transform.SetParent(go.transform, false);
            disc.transform.localPosition = new Vector3(0, 0.04f, 0);
            disc.transform.localScale = new Vector3(3.2f, 0.03f, 3.2f);
            disc.GetComponent<MeshRenderer>().sharedMaterial = mat;
            disc.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Object.DestroyImmediate(disc.GetComponent<Collider>());

            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "LanternPost";
            post.transform.SetParent(go.transform, false);
            post.transform.localPosition = new Vector3(2.2f, 1.2f, 0);
            post.transform.localScale = new Vector3(0.12f, 1.2f, 0.12f);
            Object.DestroyImmediate(post.GetComponent<Collider>());

            var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lamp.name = "Lantern";
            lamp.transform.SetParent(go.transform, false);
            lamp.transform.localPosition = new Vector3(2.2f, 2.65f, 0);
            lamp.transform.localScale = new Vector3(0.55f, 0.75f, 0.55f);
            lamp.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(lamp.GetComponent<Collider>());

            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = new Vector3(2.2f, 2.65f, 0);
            l.type = LightType.Point;
            l.color = light;
            l.range = 16f;
            l.intensity = 4f;
        }

        private static void BuildSpawns(Transform root, Material mat)
        {
            var group = new GameObject("EnemySpawns (markers only)").transform;
            group.SetParent(root, false);
            foreach (var s in Spawns)
            {
                var go = new GameObject(s.name + " [" + s.role + "]");
                go.transform.SetParent(group, false);
                go.transform.position = OnGround(s.pos);
                var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                m.name = "Marker";
                m.transform.SetParent(go.transform, false);
                m.transform.localPosition = new Vector3(0, 0.4f, 0);
                m.transform.localScale = Vector3.one * 0.8f;
                m.GetComponent<MeshRenderer>().sharedMaterial = mat;
                Object.DestroyImmediate(m.GetComponent<Collider>());
            }
        }

        // ------------------------------------------------------------ 프랍 (바위·뿌리)
        private static void BuildProps(Transform root, System.Random rng)
        {
            var group = new GameObject("Props").transform;
            group.SetParent(root, false);

            var rocks = RockT;
            var roots = RootT;

            // 코너 광장: SoD 처럼 가운데는 비우고 큰 바위 2개만 (엄폐용). 나머지 바위는 가장자리로
            PlaceProp(group, rocks, new Vector2(10.5f, 13f), 2.8f, rng);
            PlaceProp(group, rocks, new Vector2(21f, 19f), 3.2f, rng);
            PlaceProp(group, rocks, new Vector2(3.5f, 9f), 1.6f, rng);
            PlaceProp(group, rocks, new Vector2(27f, 27f), 1.8f, rng);
            // 광장 가장자리 뿌리 덩굴
            PlaceProp(group, roots, new Vector2(3.5f, 22f), 1.3f, rng);
            PlaceProp(group, roots, new Vector2(26f, 3.5f), 1.3f, rng);

            // 세로 팔: 바위 3개 + 뿌리 2개 (스폰 ① ② 의 엄폐)
            PlaceProp(group, rocks, new Vector2(7.5f, 50), 1.8f, rng);
            PlaceProp(group, rocks, new Vector2(23, 46), 2.2f, rng);
            PlaceProp(group, rocks, new Vector2(15, 42), 2.6f, rng);   // 시작 구역 가운데 큰 바위 (턱 위)
            PlaceProp(group, roots, new Vector2(8, 42), 1.2f, rng);
            PlaceProp(group, roots, new Vector2(22, 41), 1.2f, rng);

            // 가로 팔: 뿌리 더미 2곳 (스폰 ④ 뒤) + 바위 2개 (웅덩이 위쪽 둑)
            PlaceProp(group, roots, new Vector2(40, 22.5f), 1.4f, rng);
            PlaceProp(group, roots, new Vector2(33, 10f), 1.3f, rng);
            PlaceProp(group, rocks, new Vector2(38, 18), 1.6f, rng);
            PlaceProp(group, rocks, new Vector2(48, 19), 1.5f, rng);

            // 포탈 주변: 바위로 입구를 감쌈
            PlaceProp(group, rocks, new Vector2(11.5f, 58), 1.7f, rng);
            PlaceProp(group, rocks, new Vector2(18.5f, 58), 1.7f, rng);
            PlaceProp(group, rocks, new Vector2(58.5f, 11), 1.7f, rng);
            PlaceProp(group, rocks, new Vector2(58.5f, 19), 1.7f, rng);
        }

        // ------------------------------------------------------------ 플레이어·카메라
        private static void BuildPlayerAndCamera()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            GameObject player = null;
            Vector3 startPos = OnGround(new Vector2(15f, 53f)) + Vector3.up * 0.1f;
            if (playerPrefab != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.position = startPos;
                player.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // 남쪽(코너 쪽)을 봄
            }
            else Debug.LogWarning("[MapL] Player 프리팹을 못 찾았습니다: " + PlayerPath);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 150f;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<TopDownCameraFollow>();
            if (player != null)
            {
                var so = new SerializedObject(follow);
                so.FindProperty("target").objectReferenceValue = player.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // 나무·바위가 플레이어를 가리면 반투명하게
            var fader = camGo.AddComponent<Assets.Members.HJH.Environment.JungleOcclusionFader>();
            var fso = new SerializedObject(fader);
            if (player != null) fso.FindProperty("target").objectReferenceValue = player.transform;
            fso.FindProperty("rockMinSize").floatValue = 1.2f; // 이 맵 바위는 1.4~2m 라 기본값(3m)이면 투명 처리가 안 됨
            fso.ApplyModifiedPropertiesWithoutUndo();
            camGo.transform.position = startPos + new Vector3(-7f, 14f, -7f);
            camGo.transform.rotation = Quaternion.Euler(55f, 45f, 0f);
        }

        // ------------------------------------------------------------ 헬퍼
        private static float Rand(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            AssetDatabase.CreateFolder("Assets/Members/HJH/05.Data", "Map");
        }

        // 바위 투명 처리용 반투명 복사본: JungleOcclusionFader 가 Resources/HJH/RockFade/<머티리얼이름>_Fade 를 찾는다.
        // 바위 머티리얼마다 복사본이 있어야 해서, Start 씬 바위가 쓰는 머티리얼 이름 기준으로 없는 것만 만든다.
        private static void EnsureRockFadeCopy()
        {
            const string folder = "Assets/Members/HJH/05.Data/Resources/HJH/RockFade/";
            const string src = folder + "M_Rock_Fade.mat";
            var names = new HashSet<string> { "M_JungleRock" };
            foreach (var t in RockT)
                if (t.Materials != null)
                    foreach (var m in t.Materials)
                        if (m != null) names.Add(m.name);

            foreach (var n in names)
            {
                string dest = folder + n + "_Fade.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(dest) != null) continue;
                if (!AssetDatabase.CopyAsset(src, dest))
                {
                    Debug.LogWarning("[MapL] " + n + "_Fade 를 만들지 못했습니다 (원본 M_Rock_Fade 없음).");
                    continue;
                }
                var fade = AssetDatabase.LoadAssetAtPath<Material>(dest);
                var orig = FindMaterial(n);
                if (fade == null) continue;
                if (orig != null)
                {
                    // 원본 텍스처/색을 옮겨서 투명해질 때 모양이 안 바뀌게
                    foreach (var prop in new[] { "_BaseMap", "_BumpMap" })
                        if (orig.HasProperty(prop)) fade.SetTexture(prop, orig.GetTexture(prop));
                    if (orig.HasProperty("_BaseColor")) fade.SetColor("_BaseColor", orig.GetColor("_BaseColor"));
                    if (orig.HasProperty("_BumpMap") && orig.GetTexture("_BumpMap") != null) fade.EnableKeyword("_NORMALMAP");
                    else fade.DisableKeyword("_NORMALMAP");
                }
                EditorUtility.SetDirty(fade);
            }
        }

        private static Material FindMaterial(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Material"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m != null && m.name == name) return m;
            }
            return null;
        }

        private static Material MakeEmissiveMat(string name, Color color)
        {
            string path = MatFolder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.3f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 2.5f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
