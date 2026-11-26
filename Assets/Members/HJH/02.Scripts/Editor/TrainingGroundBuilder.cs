using System.Collections.Generic;
using System.IO;
using Assets.Members.HJH._02.Scripts.Tutorial;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HJH.EditorTools
{
    // 왕국 훈련장 (튜토리얼 맵): 낡은 벽돌 + 덩굴 성벽으로 둘러싸인 모래 연무장. 가운데 연무 원, 둘레에 더미·과녁·무기대·화로.
    //  - 메뉴 한 번으로 텍스처·머티리얼·메시·훈련 더미 프리팹·씬(Map_Training)까지 만들고 빌드 설정에 넣음
    //  - 다시 실행하면 덮어씀 (기존 씬은 _RemovedBackup 에 복사). 손으로 다듬은 뒤에는 다시 돌리지 말 것
    //  - 배치 좌표는 "화면 기준": +z = 화면 위, +x = 화면 오른쪽. 카메라 yaw 45 에 맞춰 Root 를 45도 돌려 놓음
    //  - 카메라 쪽(아래) 벽은 낮게, 먼 쪽(위) 벽은 높게
    public static class TrainingGroundBuilder
    {
        private const string SceneDir = "Assets/Members/HJH/01.Scene/";
        private const string ScenePath = SceneDir + "Map_Training.unity";
        private const string DataDir = "Assets/Members/HJH/05.Data/Training";
        private const string MeshPath = DataDir + "/Training_Meshes.asset";
        private const string DummyPath = DataDir + "/TrainingDummy.prefab";
        private const string NavPath = DataDir + "/Training_NavMesh.asset";
        private const string SlimePath = "Assets/Members/JJH/03_Datas/02_Prefabs/Enemies/Slime_Enemy.prefab";
        private const string StationPath = "Assets/Members/HJH/05.Data/Prefab/UI/ComboBindStation.prefab";
        private const float CameraYaw = 45f;
        private const float WallThick = 1.2f;

        private static readonly Dictionary<string, Material> Mats = new();
        private static System.Random _rng;

        private static float R() => (float)_rng.NextDouble();
        private static float L(float a, float b) => Mathf.Lerp(a, b, R());

        [MenuItem("HJH/맵/새 맵/왕국 훈련장 만들기 (Map_Training, 튜토리얼)")]
        private static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[Training] 플레이 중에는 못 만듭니다."); return; }
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) { Debug.LogError("[Training] 저장 안 된 씬이 있습니다. 저장한 뒤 다시 실행하세요."); return; }

            _rng = new System.Random(20261126);
            Directory.CreateDirectory(DataDir);
            AssetDatabase.Refresh();
            BuildMaterials();
            AssetDatabase.DeleteAsset(MeshPath);
            GameObject dummy = BuildDummyPrefab();
            BuildScene();
            TutorialAssetSync.Sync(dummy);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Training] 완료: " + ScenePath);
        }

        // ───────────────────────── 씬 ─────────────────────────
        private static void BuildScene()
        {
            if (File.Exists(ScenePath))
            {
                string backup = "_RemovedBackup/" + System.DateTime.Now.ToString("yyyy-MM-dd") + "/" + SceneDir;
                Directory.CreateDirectory(backup);
                File.Copy(ScenePath, backup + "Map_Training" + System.DateTime.Now.ToString("_HHmmss") + ".unity", true);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Map_Training").transform;
            root.rotation = Quaternion.Euler(0f, CameraYaw, 0f);

            var visual = Child(root, "Visual");
            var colliders = Child(root, "Colliders");
            var lights = Child(root, "Lights");

            var kit = new Kit();
            BuildGround(kit, colliders);
            BuildWalls(kit, colliders);
            BuildCastle(kit);
            BuildProps(kit, colliders, lights);
            kit.Emit(visual, Mats, MeshPath);
            BuildMiniMap(root);

            // 마무리 훈련에서 더미가 설 자리
            Transform spots = Child(root, "DummySpots");
            Vector2[] spotXz = { new(-5.5f, 2f), new(5.5f, 2f), new(0f, 5.5f), new(-3.2f, -2f), new(3.2f, -2f) };
            for (int i = 0; i < spotXz.Length; i++)
            {
                Transform spot = Child(spots, "DummySpot_" + (i + 1));
                spot.localPosition = new Vector3(spotXz[i].x, 0f, spotXz[i].y);
                spot.gameObject.AddComponent<TrainingDummySpot>();
            }

            var station = AssetDatabase.LoadAssetAtPath<GameObject>(StationPath);
            if (station != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(station, scene);
                go.transform.position = root.TransformPoint(new Vector3(11f, 0f, 6.5f));
            }
            else Debug.LogWarning("[Training] 콤보 상점 프리팹이 없습니다: " + StationPath);

            BuildLighting(lights);

            var nav = new GameObject("NavMesh").AddComponent<NavMeshSurface>();
            nav.collectObjects = CollectObjects.All;
            nav.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            nav.layerMask = LayerMask.GetMask("Default", "Ground", "Obstacle");
            Physics.SyncTransforms();
            nav.BuildNavMesh();
            AssetDatabase.DeleteAsset(NavPath);
            AssetDatabase.CreateAsset(nav.navMeshData, NavPath);

            if (!PlayerRigBuilder.PlaceRigAt(root.TransformPoint(new Vector3(0f, 0.1f, -8f))))
                Debug.LogError("[Training] PlayerRig 프리팹을 못 놓았습니다.");

            TrainingGroundDawn.Apply(scene);   // 새벽 조명·후처리, 가림 투명화 끄기

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void BuildLighting(Transform parent)
        {
            var sun = Child(parent, "Directional Light").gameObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.8f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.transform.rotation = Quaternion.Euler(48f, CameraYaw + 38f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.76f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.66f, 0.6f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.25f, 0.20f);
            RenderSettings.fog = false;
        }

        private static void BuildGround(Kit kit, Transform colliders)
        {
            // 성 바닥(바깥) → 모래 연무장(안)
            kit.Floor("OuterStone", new[] { new Vector2(-60, -60), new Vector2(-60, 60), new Vector2(60, 60), new Vector2(60, -60) }, -0.04f, 5f);
            Vector2[] sand = Outline(0.4f);
            kit.Floor("Sand", sand, 0f, 6f);

            BoxCol(colliders, "Floor", new Vector3(0f, -0.5f, 0f), Quaternion.identity, new Vector3(37f, 1f, 31f), "Ground");

            // 가운데 연무 원: 낡은 돌 테두리 (바닥에 박힌 모양이라 걸리지 않음)
            Ring(kit, 8.2f, 24, 0.55f);
            Ring(kit, 2.1f, 12, 0.35f);

            // 잔돌
            for (int i = 0; i < 170; i++)
            {
                var p = new Vector3(L(-16f, 16f), 0f, L(-13f, 13f));
                float s = L(0.05f, 0.14f) * (R() < 0.05f ? 2f : 1f);
                kit.Box(R() < 0.6f ? "Stone" : "SandDark", Matrix4x4.TRS(p, Quaternion.Euler(0f, L(0f, 360f), 0f), Vector3.one),
                    new Vector3(0f, s * 0.2f, 0f), new Vector3(s * L(1f, 1.8f), s * 0.5f, s));
            }
        }

        private static void Ring(Kit kit, float radius, int count, float width)
        {
            float length = 2f * Mathf.PI * radius / count;
            for (int i = 0; i < count; i++)
            {
                float a = i * 360f / count;
                var m = Matrix4x4.TRS(Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, radius), Quaternion.Euler(0f, a + L(-1.5f, 1.5f), 0f), Vector3.one);
                kit.Box("Stone", m, new Vector3(0f, L(-0.025f, -0.01f), 0f), new Vector3(length * L(0.9f, 0.97f), 0.08f, width * L(0.9f, 1.05f)));
            }
        }

        // 연무장 윤곽 (모서리를 깎은 팔각형), grow 만큼 바깥으로
        private static Vector2[] Outline(float grow)
        {
            float w = 17f + grow, d = 14f + grow, c = 5f;
            return new[]
            {
                new Vector2(-w + c, -d), new Vector2(-w, -d + c), new Vector2(-w, d - c), new Vector2(-w + c, d),
                new Vector2(w - c, d), new Vector2(w, d - c), new Vector2(w, -d + c), new Vector2(w - c, -d)
            };
        }

        // ───────────────────────── 성벽 ─────────────────────────
        private static void BuildWalls(Kit kit, Transform colliders)
        {
            Vector2[] v = Outline(0f);
            // v 순서: 남서 모서리부터 서 → 북서 → 북 → 북동 → 동 → 남동 → 남
            float[] heights = { 1.5f, 4.6f, 6.2f, 7f, 6.2f, 4.6f, 1.5f, 1f };
            for (int i = 0; i < v.Length; i++)
            {
                Vector2 a = v[i], b = v[(i + 1) % v.Length];
                var mid = new Vector3((a.x + b.x) * 0.5f, 0f, (a.y + b.y) * 0.5f);
                var along = new Vector3(b.x - a.x, 0f, b.y - a.y);
                float length = along.magnitude;
                Vector3 inward = Vector3.Cross(Vector3.up, along.normalized);
                if (Vector3.Dot(inward, -mid) < 0f) inward = -inward;
                var m = Matrix4x4.TRS(mid, Quaternion.LookRotation(inward), Vector3.one);

                WallSegment(kit, m, length, heights[i], gate: i == 3);

                // 보이는 벽(총알 막힘) + 낮은 벽 위로도 못 넘게 보이지 않는 벽
                Quaternion rot = Quaternion.LookRotation(inward);
                float h = heights[i];
                BoxCol(colliders, "Wall_" + i, mid + rot * new Vector3(0f, h * 0.5f, -WallThick * 0.5f + 0.1f), rot,
                    new Vector3(length + 0.8f, h, WallThick + 0.2f), "Obstacle");
                if (h < 3f)
                    BoxCol(colliders, "Boundary_" + i, mid + rot * new Vector3(0f, 2.5f, -0.5f), rot, new Vector3(length + 0.8f, 5f, 0.8f), "Default");
            }
        }

        // 벽 좌표: x = 벽을 따라, y = 높이, z = 0 이 안쪽 면 (+z 가 마당 쪽)
        private static void WallSegment(Kit kit, Matrix4x4 m, float length, float h, bool gate)
        {
            kit.Box("Brick", m, new Vector3(0f, h * 0.5f, -WallThick * 0.5f), new Vector3(length + 0.5f, h, WallThick));
            kit.Box("Stone", m, new Vector3(0f, 0.25f, 0.08f), new Vector3(length, 0.5f, 0.18f));

            // 위 갓돌: 조각조각 (낡아서 들쭉날쭉)
            for (float x = -length * 0.5f; x < length * 0.5f - 0.05f;)
            {
                float w = Mathf.Min(L(0.9f, 1.7f), length * 0.5f - x);
                kit.Box("Stone", m, new Vector3(x + w * 0.5f, h + 0.14f + L(-0.03f, 0.03f), -WallThick * 0.5f + 0.05f), new Vector3(w - 0.05f, 0.3f, WallThick + 0.35f));
                x += w;
            }

            if (h >= 4f)
            {
                // 성가퀴 (몇 개는 깨지거나 빠짐)
                for (float x = -length * 0.5f + 0.6f; x < length * 0.5f - 0.4f; x += 1.5f)
                {
                    if (R() < 0.12f) continue;
                    float mh = R() < 0.18f ? L(0.2f, 0.4f) : 0.72f;
                    kit.Box("Brick", m, new Vector3(x, h + 0.29f + mh * 0.5f, -0.25f), new Vector3(0.8f, mh, 0.5f));
                }
            }

            // 벽돌이 튀어나온 자국
            int bumps = Mathf.RoundToInt(length * h * 0.12f);
            for (int i = 0; i < bumps; i++)
                kit.Box("Brick", m, new Vector3(L(-0.47f, 0.47f) * length, L(0.6f, Mathf.Max(0.7f, h - 0.3f)), 0.02f), new Vector3(L(0.35f, 0.75f), 0.18f, L(0.05f, 0.11f)));

            // 기둥
            int bays = Mathf.Max(1, Mathf.RoundToInt(length / (gate ? 5.5f : 4.6f)));
            float bay = length / bays;
            for (int i = 0; i <= bays; i++)
            {
                float x = -length * 0.5f + i * bay;
                if (gate && Mathf.Abs(x) < 3.2f) continue;
                float ph = h >= 4f ? h + 0.35f : h + 0.45f;
                kit.Box("Stone", m, new Vector3(x, ph * 0.5f, 0.12f), new Vector3(h >= 4f ? 0.75f : 0.55f, ph, 0.35f));
                kit.Box("Stone", m, new Vector3(x, ph + 0.1f, 0.1f), new Vector3(h >= 4f ? 1f : 0.75f, 0.22f, 0.55f));
            }

            if (h >= 4f)
            {
                for (int i = 0; i < bays; i++)
                {
                    float x = -length * 0.5f + (i + 0.5f) * bay;
                    if (gate && Mathf.Abs(x) < 4f) continue;
                    Window(kit, m, x, h >= 6f ? 3.1f : 2.2f);
                }
            }

            if (gate)
            {
                Gate(kit, m);
                foreach (float x in new[] { -4.3f, 4.3f }) Banner(kit, m, x, h - 0.5f, 1.35f, 3.6f);
            }
            else if (h >= 6f) Banner(kit, m, 0f, h - 0.3f, 0.9f, 1.9f);

            Vines(kit, m, length, h, gate);
        }

        private static void Window(Kit kit, Matrix4x4 m, float x, float y)
        {
            const float halfW = 0.55f, halfH = 0.75f;
            kit.Box("Dark", m, new Vector3(x, y, 0.03f), new Vector3(halfW * 2f, halfH * 2f, 0.06f));
            kit.ArchFill("Dark", m, new Vector3(x, y + halfH, 0.065f), halfW);
            kit.Box("Stone", m, new Vector3(x, y - halfH - 0.1f, 0.1f), new Vector3(halfW * 2f + 0.5f, 0.2f, 0.28f));
            foreach (float s in new[] { -1f, 1f })
                kit.Box("Stone", m, new Vector3(x + s * (halfW + 0.1f), y, 0.07f), new Vector3(0.2f, halfH * 2f, 0.18f));
            Arch(kit, m, new Vector3(x, y + halfH, 0.07f), halfW + 0.1f, 7, 0.2f, 0.18f);
            kit.Box("Iron", m, new Vector3(x, y + 0.1f, 0.09f), new Vector3(0.06f, halfH * 2f + 0.5f, 0.03f));
            kit.Box("Iron", m, new Vector3(x, y, 0.09f), new Vector3(halfW * 2f, 0.06f, 0.03f));
        }

        private static void Arch(Kit kit, Matrix4x4 m, Vector3 center, float radius, int count, float thick, float depth)
        {
            float step = 180f / count;
            float length = Mathf.PI * radius / count * 1.08f;
            for (int i = 0; i < count; i++)
            {
                float angle = step * (i + 0.5f);
                Quaternion rot = Quaternion.Euler(0f, 0f, angle - 90f);
                Matrix4x4 stone = m * Matrix4x4.TRS(center + rot * new Vector3(0f, radius, 0f), rot, Vector3.one);
                kit.Box("Stone", stone, Vector3.zero, new Vector3(length, thick, depth));
            }
        }

        private static void Gate(Kit kit, Matrix4x4 m)
        {
            kit.Box("DoorWood", m, new Vector3(0f, 1.7f, 0.06f), new Vector3(4f, 3.4f, 0.12f));
            kit.ArchFill("DoorWood", m, new Vector3(0f, 3.4f, 0.13f), 2f);
            for (float x = -1.6f; x <= 1.61f; x += 0.4f)
                kit.Box("Dark", m, new Vector3(x, 1.7f, 0.125f), new Vector3(0.025f, 3.4f, 0.01f));
            foreach (float y in new[] { 0.7f, 2f, 3.2f })
                kit.Box("Iron", m, new Vector3(0f, y, 0.15f), new Vector3(3.9f, 0.14f, 0.05f));
            kit.Box("Dark", m, new Vector3(0f, 2.2f, 0.16f), new Vector3(0.07f, 4.1f, 0.03f));
            foreach (float s in new[] { -1f, 1f })
            {
                kit.Box("Gold", m, new Vector3(s * 0.3f, 1.75f, 0.2f), new Vector3(0.18f, 0.18f, 0.08f));
                kit.Box("Stone", m, new Vector3(s * 2.35f, 1.7f, 0.2f), new Vector3(0.7f, 3.4f, 0.5f));
            }
            Arch(kit, m, new Vector3(0f, 3.4f, 0.2f), 2.35f, 11, 0.7f, 0.5f);
            // 문 앞 돌계단 (낮아서 올라설 수 있어 보이지만 벽 앞 장식)
            for (int i = 0; i < 3; i++)
                kit.Box("Stone", m, new Vector3(0f, 0.04f + (2 - i) * 0.05f, 0.35f + i * 0.45f), new Vector3(5.2f + i * 0.6f, 0.08f + (2 - i) * 0.1f, 0.5f));
        }

        private static void Banner(Kit kit, Matrix4x4 m, float x, float top, float width, float height)
        {
            kit.Box("Iron", m, new Vector3(x, top + 0.08f, 0.36f), new Vector3(width + 0.35f, 0.09f, 0.09f));
            kit.Box("Cloth", m, new Vector3(x, top - height * 0.5f, 0.34f), new Vector3(width, height, 0.04f));
            foreach (float s in new[] { -1f, 1f })
                kit.Box("Cloth", m, new Vector3(x + s * width * 0.28f, top - height - 0.18f, 0.34f), new Vector3(width * 0.44f, 0.38f, 0.04f));
            kit.Box("Gold", m, new Vector3(x, top - height * 0.42f, 0.37f), new Vector3(width * 0.14f, height * 0.5f, 0.02f));
            kit.Box("Gold", m, new Vector3(x, top - height * 0.33f, 0.371f), new Vector3(width * 0.55f, height * 0.07f, 0.02f));
            kit.Box("Gold", m, new Vector3(x, top - 0.16f, 0.37f), new Vector3(width, 0.08f, 0.02f));
        }

        // 덩굴: 벽 위에서 아래로 늘어지는 줄기 + 잎 뭉치, 바닥에서 타고 오르는 것 조금
        private static void Vines(Kit kit, Matrix4x4 m, float length, float h, bool gate)
        {
            int strands = Mathf.RoundToInt(length * (h >= 4f ? 1.05f : 0.6f));
            for (int s = 0; s < strands; s++)
            {
                bool climbing = R() < 0.22f;
                float x = L(-0.48f, 0.48f) * length;
                if (gate && Mathf.Abs(x) < 5.4f) continue;
                float y = climbing ? 0.1f : h + 0.28f;
                float reach = climbing ? L(0.8f, h * 0.55f) : L(h * 0.2f, h * 0.85f);
                float travelled = 0f;
                while (travelled < reach)
                {
                    const float step = 0.32f;
                    float nx = Mathf.Clamp(x + L(-0.2f, 0.2f), -length * 0.5f, length * 0.5f);
                    float ny = y + (climbing ? step : -step);
                    if (ny < 0.05f) break;
                    var from = new Vector3(x, y, 0.2f);
                    var to = new Vector3(nx, ny, 0.2f);
                    Matrix4x4 stem = m * Matrix4x4.TRS((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, to - from), Vector3.one);
                    kit.Box("VineStem", stem, Vector3.zero, new Vector3(0.05f, Vector3.Distance(from, to) + 0.04f, 0.04f));

                    float fullness = 1f - travelled / reach;
                    int leaves = 2 + (R() < 0.35f + fullness * 0.6f ? 2 : 0);
                    for (int i = 0; i < leaves; i++)
                    {
                        float size = L(0.45f, 0.75f) * (0.65f + fullness * 0.45f);
                        var p = new Vector3(nx + L(-0.24f, 0.24f), ny + L(-0.2f, 0.2f), L(0.23f, 0.38f));
                        Matrix4x4 leaf = m * Matrix4x4.TRS(p, Quaternion.Euler(L(-35f, 10f), L(-30f, 30f), L(0f, 360f)), Vector3.one);
                        kit.Card(R() < 0.5f ? "Leaf" : "LeafDark", leaf, size);
                    }
                    x = nx; y = ny; travelled += step;
                }
            }

            // 갓돌 위에 덮인 잎
            int tops = Mathf.RoundToInt(length * 1.4f);
            for (int i = 0; i < tops; i++)
            {
                float x = L(-0.5f, 0.5f) * length;
                if (Mathf.PerlinNoise(x * 0.35f + h, length) < 0.45f) continue;
                Matrix4x4 leaf = m * Matrix4x4.TRS(new Vector3(x, h + L(0.3f, 0.42f), L(-0.7f, 0.2f)), Quaternion.Euler(L(65f, 115f), L(0f, 360f), 0f), Vector3.one);
                kit.Card(R() < 0.5f ? "Leaf" : "LeafDark", leaf, L(0.4f, 0.7f));
            }
        }

        // ───────────────────────── 바깥 성 건물 ─────────────────────────
        private static void BuildCastle(Kit kit)
        {
            // 북쪽 본관
            Matrix4x4 keep = Matrix4x4.TRS(new Vector3(0f, 0f, 20.5f), Quaternion.Euler(0f, 180f, 0f), Vector3.one);
            kit.Box("Brick", keep, new Vector3(0f, 5f, -2.5f), new Vector3(26f, 10f, 5f));
            kit.Box("Stone", keep, new Vector3(0f, 10.2f, -2.5f), new Vector3(27f, 0.4f, 6f));
            for (float x = -12.5f; x <= 12.51f; x += 1.7f)
                kit.Box("Brick", keep, new Vector3(x, 10.8f, 0.2f), new Vector3(0.9f, 0.8f, 0.5f));
            for (float x = -10f; x <= 10.1f; x += 4f)
                Window(kit, keep, x, 8f);

            foreach (float s in new[] { -1f, 1f })
            {
                Tower(kit, new Vector3(s * 15.5f, 0f, 18.5f), 2.8f, 11f);
                Tower(kit, new Vector3(s * 21.5f, 0f, 9f), 2.4f, 8.5f);
                Tower(kit, new Vector3(s * 21.5f, 0f, -6f), 2.1f, 6f);
                Tower(kit, new Vector3(s * 20.5f, 0f, -16f), 2f, 2.4f, roof: false);

                // 옆 회랑 지붕
                var side = Matrix4x4.TRS(new Vector3(s * 21f, 0f, 1.5f), Quaternion.Euler(0f, -s * 90f, 0f), Vector3.one);
                kit.Box("Brick", side, new Vector3(0f, 1.8f, 0f), new Vector3(13f, 3.6f, 3f));
                kit.Box("Roof", side * Matrix4x4.TRS(new Vector3(0f, 4.05f, 0.7f), Quaternion.Euler(26f, 0f, 0f), Vector3.one), Vector3.zero, new Vector3(13.4f, 0.22f, 2.3f));
                kit.Box("Roof", side * Matrix4x4.TRS(new Vector3(0f, 4.05f, -0.7f), Quaternion.Euler(-26f, 0f, 0f), Vector3.one), Vector3.zero, new Vector3(13.4f, 0.22f, 2.3f));
            }
        }

        private static void Tower(Kit kit, Vector3 at, float radius, float height, bool roof = true)
        {
            var m = Matrix4x4.TRS(at, Quaternion.identity, Vector3.one);
            kit.Cylinder("Brick", m, Vector3.zero, radius, radius * 0.94f, height, 14);
            kit.Cylinder("Stone", m, new Vector3(0f, height, 0f), radius * 1.12f, radius * 1.12f, 0.4f, 14);
            if (roof)
                kit.Cylinder("Roof", m, new Vector3(0f, height + 0.4f, 0f), radius * 1.2f, 0.02f, radius * 1.6f, 14);
            else
                for (int i = 0; i < 8; i++)
                {
                    Matrix4x4 merlon = m * Matrix4x4.Rotate(Quaternion.Euler(0f, i * 45f, 0f));
                    kit.Box("Brick", merlon, new Vector3(0f, height + 0.65f, radius * 0.9f), new Vector3(0.8f, 0.55f, 0.45f));
                }
            // 벽을 타고 오른 덩굴
            for (int i = 0; i < Mathf.RoundToInt(radius * height * 3f); i++)
            {
                float a = L(0f, 360f), y = Mathf.Pow(R(), 1.6f) * height * 0.8f;
                Matrix4x4 leaf = m * Matrix4x4.Rotate(Quaternion.Euler(0f, a, 0f)) *
                                 Matrix4x4.TRS(new Vector3(0f, y + 0.2f, radius + 0.08f), Quaternion.Euler(L(-25f, 10f), L(-20f, 20f), L(0f, 360f)), Vector3.one);
                if (Mathf.PerlinNoise(a * 0.035f + radius, y * 0.4f) > 0.48f)
                    kit.Card(R() < 0.5f ? "Leaf" : "LeafDark", leaf, L(0.45f, 0.75f));
            }
        }

        // ───────────────────────── 소품 ─────────────────────────
        private static void BuildProps(Kit kit, Transform colliders, Transform lights)
        {
            // 벽 쪽 나무 더미 (장식, 맞으면 총알이 막힘)
            foreach (var (x, z, yaw) in new[] { (-13.8f, 3.5f, 70f), (-14.2f, -3f, 100f), (13.8f, -2.5f, -80f), (13.2f, -7.5f, -50f) })
            {
                var m = Matrix4x4.TRS(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f), Vector3.one);
                kit.Cylinder("Wood", m, Vector3.zero, 0.5f, 0.45f, 0.14f, 10);
                kit.Cylinder("Wood", m, Vector3.zero, 0.14f, 0.13f, 1.9f, 8);
                kit.Cylinder("Straw", m, new Vector3(0f, 0.75f, 0f), 0.34f, 0.3f, 0.75f, 10);
                kit.Cylinder("Straw", m, new Vector3(0f, 1.68f, 0f), 0.24f, 0.22f, 0.38f, 10);
                kit.Box("Wood", m, new Vector3(0f, 1.38f, 0f), new Vector3(1.5f, 0.13f, 0.13f));
                kit.Cylinder("DoorWood", m, new Vector3(0f, 0.95f, 0f), 0.345f, 0.34f, 0.09f, 10);
                BoxCol(colliders, "Post", new Vector3(x, 1f, z), Quaternion.identity, new Vector3(0.7f, 2f, 0.7f), "Obstacle");
            }

            // 과녁
            foreach (var (x, z, yaw) in new[] { (-14.3f, 8.2f, 125f), (-11.4f, 11.2f, 145f) })
            {
                var m = Matrix4x4.TRS(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f), Vector3.one);
                foreach (float s in new[] { -1f, 1f })
                    kit.Box("Wood", m * Matrix4x4.TRS(new Vector3(s * 0.55f, 0.75f, -0.1f), Quaternion.Euler(0f, 0f, s * 14f), Vector3.one), Vector3.zero, new Vector3(0.12f, 1.6f, 0.12f));
                kit.Box("Wood", m * Matrix4x4.TRS(new Vector3(0f, 0.7f, -0.55f), Quaternion.Euler(32f, 0f, 0f), Vector3.one), Vector3.zero, new Vector3(0.12f, 1.65f, 0.12f));
                Matrix4x4 face = m * Matrix4x4.TRS(new Vector3(0f, 1.35f, 0f), Quaternion.Euler(80f, 0f, 0f), Vector3.one);
                string[] ring = { "Straw", "Cream", "Cloth", "Cream", "Cloth" };
                float[] radius = { 0.82f, 0.7f, 0.5f, 0.32f, 0.14f };
                for (int i = 0; i < ring.Length; i++)
                    kit.Cylinder(ring[i], face, new Vector3(0f, i * 0.015f, 0f), radius[i], radius[i], 0.1f, 20);
                BoxCol(colliders, "Target", new Vector3(x, 1.1f, z), Quaternion.Euler(0f, yaw, 0f), new Vector3(1.5f, 2.2f, 0.7f), "Obstacle");
            }

            // 무기대
            foreach (var (x, z, yaw) in new[] { (15.9f, 2.2f, -90f), (15.9f, -3.4f, -90f), (-7.5f, 13.1f, 180f), (-15.9f, -6.5f, 90f) })
            {
                var m = Matrix4x4.TRS(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f), Vector3.one);
                foreach (float s in new[] { -1.5f, 1.5f })
                    kit.Box("Wood", m, new Vector3(s, 0.75f, 0f), new Vector3(0.18f, 1.5f, 0.5f));
                kit.Box("Wood", m, new Vector3(0f, 0.2f, 0f), new Vector3(3.1f, 0.18f, 0.6f));
                kit.Box("Wood", m, new Vector3(0f, 1.3f, 0f), new Vector3(3.1f, 0.12f, 0.2f));
                for (int i = 0; i < 7; i++)
                {
                    float px = -1.2f + i * 0.4f;
                    Matrix4x4 w = m * Matrix4x4.TRS(new Vector3(px, 0.25f, 0.14f), Quaternion.Euler(-5f, 0f, L(-4f, 4f)), Vector3.one);
                    if (i % 2 == 0)
                    {
                        kit.Cylinder("Wood", w, Vector3.zero, 0.035f, 0.035f, 2.1f, 6);
                        kit.Cylinder("Steel", w, new Vector3(0f, 2.1f, 0f), 0.09f, 0.005f, 0.38f, 4);
                    }
                    else
                    {
                        kit.Box("Steel", w, new Vector3(0f, 0.55f, 0f), new Vector3(0.12f, 1.1f, 0.03f));
                        kit.Box("Gold", w, new Vector3(0f, 1.13f, 0f), new Vector3(0.36f, 0.07f, 0.08f));
                        kit.Box("DoorWood", w, new Vector3(0f, 1.32f, 0f), new Vector3(0.07f, 0.32f, 0.07f));
                    }
                }
                BoxCol(colliders, "Rack", new Vector3(x, 0.9f, z), Quaternion.Euler(0f, yaw, 0f), new Vector3(3.3f, 1.8f, 0.8f), "Obstacle");
            }

            // 통·상자·건초
            foreach (var (x, z) in new[] { (7.4f, 12.8f), (8.4f, 12.9f), (7.9f, 12f), (-15.8f, -1f), (15.8f, 6.2f), (10.8f, -11.3f) })
                Barrel(kit, colliders, new Vector3(x, 0f, z), L(0.85f, 1.05f));
            foreach (var (x, z, s) in new[] { (9.7f, 12.7f, 1f), (10.6f, 12.4f, 0.75f), (-11.2f, -11.1f, 1.1f), (-12.3f, -10.4f, 0.8f), (15.5f, 7.4f, 0.9f) })
            {
                Quaternion rot = Quaternion.Euler(0f, L(-25f, 25f), 0f);
                var m = Matrix4x4.TRS(new Vector3(x, 0f, z), rot, Vector3.one);
                kit.Box("Wood", m, new Vector3(0f, s * 0.5f, 0f), Vector3.one * s);
                foreach (float y in new[] { 0.08f, 0.92f })
                    kit.Box("DoorWood", m, new Vector3(0f, s * y, 0f), new Vector3(s * 1.04f, s * 0.13f, s * 1.04f));
                BoxCol(colliders, "Crate", new Vector3(x, s * 0.5f, z), rot, Vector3.one * s, "Obstacle");
            }
            foreach (var (x, z, yaw) in new[] { (-9f, -12.4f, 8f), (-7.2f, -12.6f, -14f), (13.4f, 9.4f, 55f) })
            {
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                var m = Matrix4x4.TRS(new Vector3(x, 0f, z), rot, Vector3.one);
                kit.Box("Straw", m, new Vector3(0f, 0.38f, 0f), new Vector3(1.5f, 0.76f, 0.85f));
                foreach (float s in new[] { -0.4f, 0.4f })
                    kit.Box("DoorWood", m, new Vector3(s, 0.38f, 0f), new Vector3(0.07f, 0.8f, 0.89f));
                BoxCol(colliders, "Hay", new Vector3(x, 0.4f, z), rot, new Vector3(1.5f, 0.8f, 0.85f), "Obstacle");
            }

            // 화로
            foreach (var (x, z) in new[] { (-3.9f, 11.9f), (3.9f, 11.9f), (-13.4f, -8.4f), (13.6f, 4.9f) })
            {
                var m = Matrix4x4.TRS(new Vector3(x, 0f, z), Quaternion.identity, Vector3.one);
                kit.Cylinder("Stone", m, Vector3.zero, 0.5f, 0.38f, 0.25f, 8);
                kit.Cylinder("Iron", m, new Vector3(0f, 0.25f, 0f), 0.11f, 0.09f, 0.85f, 6);
                kit.Cylinder("Iron", m, new Vector3(0f, 1.1f, 0f), 0.28f, 0.52f, 0.38f, 10);
                kit.Cylinder("Flame", m, new Vector3(0f, 1.42f, 0f), 0.34f, 0.01f, 0.7f, 6);
                kit.Cylinder("FlameCore", m, new Vector3(0.12f, 1.44f, -0.08f), 0.2f, 0.01f, 0.52f, 5);
                kit.Cylinder("Flame", m, new Vector3(-0.17f, 1.44f, 0.09f), 0.17f, 0.01f, 0.45f, 5);
                BoxCol(colliders, "Brazier", new Vector3(x, 0.8f, z), Quaternion.identity, new Vector3(0.9f, 1.6f, 0.9f), "Obstacle");
                var light = Child(lights, "BrazierLight").gameObject.AddComponent<Light>();
                light.transform.localPosition = new Vector3(x, 2.2f, z);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.55f, 0.22f);
                light.range = 7f;
                light.intensity = 3f;
                light.shadows = LightShadows.None;
            }

            // 벽 밑 풀
            Vector2[] edge = Outline(-0.35f);
            for (int i = 0; i < edge.Length; i++)
            {
                Vector2 a = edge[i], b = edge[(i + 1) % edge.Length];
                int count = Mathf.RoundToInt(Vector2.Distance(a, b) * 2.2f);
                for (int k = 0; k < count; k++)
                {
                    float t = R();
                    if (Mathf.PerlinNoise(i * 3.1f, t * 6f) < 0.5f) continue;
                    Vector2 p = Vector2.Lerp(a, b, t) * L(0.965f, 1.005f);
                    Matrix4x4 leaf = Matrix4x4.TRS(new Vector3(p.x, L(0.05f, 0.2f), p.y), Quaternion.Euler(L(45f, 110f), L(0f, 360f), 0f), Vector3.one);
                    kit.Card(R() < 0.5f ? "Leaf" : "LeafDark", leaf, L(0.25f, 0.55f));
                }
            }
        }

        private static void Barrel(Kit kit, Transform colliders, Vector3 at, float s)
        {
            var m = Matrix4x4.TRS(at, Quaternion.identity, Vector3.one * s);
            kit.Cylinder("Wood", m, Vector3.zero, 0.36f, 0.46f, 0.55f, 12);
            kit.Cylinder("Wood", m, new Vector3(0f, 0.55f, 0f), 0.46f, 0.36f, 0.55f, 12);
            foreach (float y in new[] { 0.14f, 0.52f, 0.9f })
            {
                float r = Mathf.Lerp(0.47f, 0.4f, Mathf.Abs(y - 0.55f) / 0.55f) + 0.012f;
                kit.Cylinder("Iron", m, new Vector3(0f, y, 0f), r, r, 0.06f, 12);
            }
            BoxCol(colliders, "Barrel", at + new Vector3(0f, 0.55f * s, 0f), Quaternion.identity, new Vector3(0.85f, 1.1f, 0.85f) * s, "Obstacle");
        }

        // 미니맵에만 보이는 바닥과 벽 윤곽
        private static void BuildMiniMap(Transform root)
        {
            int layer = LayerMask.NameToLayer("MiniMap");
            if (layer < 0) return;
            var kit = new Kit();
            kit.Floor("MapFloor", Outline(0f), 0.02f, 6f);
            Vector2[] v = Outline(0f);
            for (int i = 0; i < v.Length; i++)
            {
                Vector2 a = v[i], b = v[(i + 1) % v.Length];
                var along = new Vector3(b.x - a.x, 0f, b.y - a.y);
                var m = Matrix4x4.TRS(new Vector3((a.x + b.x) * 0.5f, 0.05f, (a.y + b.y) * 0.5f), Quaternion.LookRotation(along), Vector3.one);
                kit.Box("MapWall", m, Vector3.zero, new Vector3(0.7f, 0.05f, along.magnitude + 0.5f));
            }
            Transform parent = Child(root, "MiniMap");
            kit.Emit(parent, Mats, MeshPath);
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
                if (t.TryGetComponent(out MeshRenderer renderer))
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // ───────────────────────── 훈련 더미 ─────────────────────────
        // 슬라임 적을 복사해 겉모습만 허수아비로 바꿈 (체력·피격·CC·미니맵 등 적 기능은 그대로)
        private static GameObject BuildDummyPrefab()
        {
            GameObject source = PrefabUtility.LoadPrefabContents(SlimePath);
            try
            {
                source.name = "TrainingDummy";
                int miniMap = LayerMask.NameToLayer("MiniMap");
                foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true))
                    if ((renderer is SkinnedMeshRenderer || renderer is MeshRenderer) && renderer.gameObject.layer != miniMap
                        && renderer.GetComponent<TMPro.TMP_Text>() == null)
                        renderer.enabled = false;
                foreach (Animator animator in source.GetComponentsInChildren<Animator>(true))
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                if (source.TryGetComponent(out BoxCollider box))
                {
                    box.center = new Vector3(0f, 0.95f, 0f);
                    box.size = new Vector3(1f, 1.9f, 1f);
                }
                foreach (Canvas canvas in source.GetComponentsInChildren<Canvas>(true))
                {
                    Vector3 p = canvas.transform.position;
                    canvas.transform.position = new Vector3(p.x, source.transform.position.y + 2.45f, p.z);
                }

                var kit = new Kit();
                Matrix4x4 m = Matrix4x4.identity;
                kit.Cylinder("Wood", m, Vector3.zero, 0.5f, 0.42f, 0.14f, 10);
                kit.Cylinder("Wood", m, Vector3.zero, 0.12f, 0.12f, 1.5f, 8);
                kit.Cylinder("Straw", m, new Vector3(0f, 0.6f, 0f), 0.3f, 0.42f, 0.78f, 12);
                kit.Cylinder("Straw", m, new Vector3(0f, 1.45f, 0f), 0.3f, 0.27f, 0.45f, 12);
                kit.Cylinder("Cloth", m, new Vector3(0f, 1.36f, 0f), 0.33f, 0.31f, 0.11f, 12);
                kit.Cylinder("Steel", m, new Vector3(0f, 1.86f, 0f), 0.31f, 0.12f, 0.2f, 12);
                kit.Box("Wood", m, new Vector3(0f, 1.2f, 0f), new Vector3(1.45f, 0.14f, 0.14f));
                foreach (float z in new[] { -0.42f, 0.42f })
                {
                    kit.Box("Cloth", m, new Vector3(0f, 1.05f, z * 0.97f), new Vector3(0.3f, 0.3f, 0.05f));
                    kit.Box("Cream", m, new Vector3(0f, 1.05f, z), new Vector3(0.13f, 0.13f, 0.05f));
                }

                Transform visual = Child(source.transform, "DummyVisual");
                kit.Emit(visual, Mats, MeshPath);
                var dummy = source.AddComponent<TrainingDummy>();
                var so = new SerializedObject(dummy);
                so.FindProperty("visual").objectReferenceValue = visual;
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(source, DummyPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(source);
            }
        }

        // ───────────────────────── 머티리얼·텍스처 ─────────────────────────
        private static void BuildMaterials()
        {
            Mats.Clear();
            Texture2D brick = Save("T_OldBrick", 512, BrickPixel, false);
            Texture2D sand = Save("T_Sand", 512, SandPixel, false);
            Texture2D stone = Save("T_Stone", 256, StonePixel, false);
            Texture2D wood = Save("T_Wood", 256, WoodPixel, false);
            Texture2D leaf = Save("T_IvyLeaf", 256, LeafPixel, true);

            Toon("Brick", brick, new Color(0.97f, 0.94f, 0.9f), 3f);
            Toon("Sand", sand, Color.white, 1f);
            Toon("SandDark", sand, new Color(0.72f, 0.66f, 0.58f), 1f);
            Toon("Stone", stone, new Color(0.86f, 0.82f, 0.75f), 2f);
            Toon("OuterStone", stone, new Color(0.52f, 0.48f, 0.44f), 1f);
            Toon("Wood", wood, new Color(0.82f, 0.62f, 0.42f), 1.2f);
            Toon("DoorWood", wood, new Color(0.45f, 0.31f, 0.22f), 1.2f);
            Toon("VineStem", wood, new Color(0.35f, 0.32f, 0.18f), 1f);
            Toon("Straw", wood, new Color(1.12f, 0.98f, 0.66f), 0.7f);
            Toon("Roof", stone, new Color(0.33f, 0.37f, 0.48f), 1.4f);
            Toon("Cloth", null, new Color(0.62f, 0.09f, 0.08f), 1f);
            Toon("Cream", null, new Color(0.9f, 0.85f, 0.72f), 1f);
            Toon("Gold", null, new Color(0.87f, 0.66f, 0.22f), 1f);
            Toon("Iron", null, new Color(0.2f, 0.2f, 0.23f), 1f);
            Toon("Steel", null, new Color(0.66f, 0.69f, 0.74f), 1f);
            Toon("Dark", null, new Color(0.09f, 0.08f, 0.1f), 1f);
            foreach (var (name, tint) in new[] { ("Leaf", new Color(1f, 1f, 1f)), ("LeafDark", new Color(0.8f, 0.9f, 0.76f)) })
            {
                Material m = Toon(name, leaf, tint, 1f);
                m.SetFloat("_Cull", 0f);
                m.SetFloat("_AlphaClip", 1f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cutoff", 0.5f);
                m.SetFloat("_Simplify", 0f);
                m.SetFloat("_ReceiveShadow", 0.35f);
                m.SetColor("_BaseColor", tint * 1.15f);
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            Unlit("Flame", new Color(1f, 0.36f, 0.04f));
            Unlit("FlameCore", new Color(1f, 0.74f, 0.2f));
            Unlit("MapFloor", new Color(0.36f, 0.3f, 0.2f));
            Unlit("MapWall", new Color(0.78f, 0.62f, 0.5f));
        }

        // metersPerTile = 텍스처 한 장이 덮는 길이는 Kit 의 UV(1 = 1m)를 나눠서 맞춤
        private static Material Toon(string name, Texture2D texture, Color color, float metersPerTile)
        {
            Material m = LoadOrCreate(name, "HJH/Map/Toon");
            m.SetTexture("_BaseMap", texture);
            // 바닥(Sand/OuterStone)은 Floor 에서 UV 를 이미 타일 길이로 나눔
            float scale = name is "Sand" or "OuterStone" or "Leaf" or "LeafDark" ? 1f : 1f / metersPerTile;
            m.SetTextureScale("_BaseMap", new Vector2(scale, scale));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Saturation", 1.05f);
            m.SetFloat("_Brightness", 1f);
            m.SetFloat("_Simplify", 0.3f);
            m.SetFloat("_NormalStrength", 0f);
            m.SetColor("_ShadowColor", new Color(0.72f, 0.68f, 0.76f, 1f));
            m.SetFloat("_ShadowSoftness", 0.12f);
            m.SetFloat("_RimStrength", 0.05f);
            m.SetFloat("_OutlineWidth", 0f);
            m.SetShaderPassEnabled("SRPDefaultUnlit", false);
            EditorUtility.SetDirty(m);
            Mats[name] = m;
            return m;
        }

        private static void Unlit(string name, Color color)
        {
            Material m = LoadOrCreate(name, "Universal Render Pipeline/Unlit");
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            Mats[name] = m;
        }

        private static Material LoadOrCreate(string name, string shaderName)
        {
            string path = $"{DataDir}/M_Training_{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = shader;
            return m;
        }

        private static Texture2D Save(string name, int size, System.Func<float, float, Color> pixel, bool alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
            texture.SetPixels(pixels);
            string path = $"{DataDir}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = alpha ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.alphaIsTransparency = alpha;
            importer.mipMapsPreserveCoverage = alpha;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // 이어 붙여도 티가 안 나는 값 노이즈 (period = 한 장에 들어가는 칸 수)
        private static float Noise(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(xi, yi, period, seed), b = Hash(xi + 1, yi, period, seed);
            float c = Hash(xi, yi + 1, period, seed), d = Hash(xi + 1, yi + 1, period, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Hash(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        private static float Fbm(float u, float v, int period, int seed) =>
            Noise(u, v, period, seed) * 0.55f + Noise(u, v, period * 2, seed + 1) * 0.3f + Noise(u, v, period * 4, seed + 2) * 0.15f;

        // 낡은 벽돌: 3m 한 장에 8줄 x 4장. 색이 제각각, 모서리가 깨지고 줄눈에 이끼
        private static Color BrickPixel(float u, float v)
        {
            const int rows = 8, cols = 4;
            int row = Mathf.FloorToInt(v * rows);
            float shifted = u * cols + (row % 2) * 0.5f;
            int col = Mathf.FloorToInt(shifted);
            float bx = shifted - col, by = v * rows - row;
            col %= cols;

            float wobble = (Noise(u, v, 48, 5) - 0.5f) * 0.14f + (Noise(u, v, 17, 9) - 0.5f) * 0.1f;
            float edge = Mathf.Min(Mathf.Min(bx, 1f - bx) * 2f, Mathf.Min(by, 1f - by)) + wobble;
            float tone = Hash(col, row, 64, 11), pick = Hash(col, row, 64, 23);

            Color brick = pick < 0.5f ? new Color(0.57f, 0.33f, 0.25f)
                : pick < 0.78f ? new Color(0.62f, 0.45f, 0.34f)
                : pick < 0.92f ? new Color(0.47f, 0.40f, 0.37f)
                : new Color(0.40f, 0.24f, 0.21f);
            brick *= 0.86f + tone * 0.26f + (Fbm(u, v, 16, 3) - 0.5f) * 0.35f;
            if (by > 0.8f) brick *= 1.12f;
            if (by < 0.22f) brick *= 0.82f;
            if (Mathf.Abs(Noise(u, v, 9, 31) - 0.5f) < 0.012f && pick > 0.55f) brick *= 0.62f;   // 금

            Color mortar = new Color(0.60f, 0.55f, 0.47f) * (0.82f + Noise(u, v, 90, 7) * 0.3f);
            Color c = edge < 0.09f ? mortar : edge < 0.13f ? brick * 0.72f : brick;

            float moss = Mathf.Clamp01((Fbm(u, v, 5, 41) - 0.58f) * 4f) * (edge < 0.2f ? 1f : 0.45f);
            c = Color.Lerp(c, new Color(0.29f, 0.37f, 0.18f) * (0.8f + Noise(u, v, 64, 43) * 0.4f), moss);
            c.a = 1f;
            return c;
        }

        private static Color SandPixel(float u, float v)
        {
            Color c = new Color(0.79f, 0.64f, 0.43f);
            c *= 0.9f + (Fbm(u, v, 5, 51) - 0.5f) * 0.32f + (Noise(u, v, 128, 53) - 0.5f) * 0.14f + 0.06f;
            c = Color.Lerp(c, new Color(0.62f, 0.47f, 0.31f), Mathf.Clamp01((Fbm(u, v, 3, 57) - 0.55f) * 2.4f) * 0.7f);   // 눌린 자국
            float grain = Noise(u, v, 170, 59);
            if (grain > 0.8f) c *= 1.13f;
            else if (grain < 0.16f) c *= 0.84f;
            c.a = 1f;
            return c;
        }

        private static Color StonePixel(float u, float v)
        {
            Color c = new Color(0.66f, 0.63f, 0.57f);
            c *= 0.9f + (Fbm(u, v, 6, 61) - 0.5f) * 0.55f + (Noise(u, v, 96, 63) - 0.5f) * 0.14f + 0.05f;
            if (Mathf.Abs(Noise(u, v, 7, 65) - 0.5f) < 0.013f) c *= 0.68f;
            c = Color.Lerp(c, new Color(0.36f, 0.42f, 0.24f), Mathf.Clamp01((Fbm(u, v, 4, 67) - 0.62f) * 3f));
            c.a = 1f;
            return c;
        }

        private static Color WoodPixel(float u, float v)
        {
            float grain = Noise(u + Noise(u, v, 4, 71) * 0.08f, v * 0.12f, 24, 73);
            float shade = 0.62f + grain * 0.38f + (Noise(u, v, 64, 75) - 0.5f) * 0.12f;
            if (Mathf.Repeat(u * 4f, 1f) < 0.045f) shade *= 0.62f;
            return new Color(shade * 0.92f, shade * 0.78f, shade * 0.62f, 1f);
        }

        // 담쟁이 잎 뭉치 (잎 7장)
        private static readonly Vector4[] Leaves =
        {
            // x, y, 크기, 각도
            new(0.50f, 0.70f, 0.24f, 0f), new(0.27f, 0.58f, 0.22f, 55f), new(0.74f, 0.57f, 0.22f, -50f), new(0.50f, 0.45f, 0.25f, 180f),
            new(0.26f, 0.29f, 0.20f, 130f), new(0.72f, 0.27f, 0.21f, -135f), new(0.51f, 0.19f, 0.16f, 175f)
        };

        private static Color LeafPixel(float u, float v)
        {
            Color c = new Color(0.28f, 0.42f, 0.17f, 0f);
            for (int i = 0; i < Leaves.Length; i++)
            {
                Vector4 l = Leaves[i];
                float a = l.w * Mathf.Deg2Rad;
                float dx = u - l.x, dy = v - l.y;
                float x = (dx * Mathf.Cos(a) + dy * Mathf.Sin(a)) / l.z;
                float y = (-dx * Mathf.Sin(a) + dy * Mathf.Cos(a)) / l.z;
                if (y < -0.75f || y > 0.95f) continue;
                float t = Mathf.InverseLerp(-0.75f, 0.95f, y);
                float half = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.7f)) * 0.68f * (1f + 0.14f * Mathf.Sin(t * 17f));
                if (Mathf.Abs(x) > half) continue;
                float tone = Hash(i, 3, 16, 81);
                Color leaf = Color.Lerp(new Color(0.23f, 0.38f, 0.14f), new Color(0.47f, 0.58f, 0.22f), tone);
                leaf *= x > 0f ? 1.12f : 0.9f;
                if (Mathf.Abs(x) < 0.035f || Mathf.Abs(x) > half - 0.07f) leaf *= Mathf.Abs(x) < 0.035f ? 1.3f : 0.75f;
                leaf.a = 1f;
                c = leaf;
            }
            return c;
        }

        // ───────────────────────── 도우미 ─────────────────────────
        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // position/rotation 은 parent 기준
        private static void BoxCol(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 size, string layer)
        {
            Transform t = Child(parent, name);
            t.localPosition = position;
            t.localRotation = rotation;
            t.gameObject.layer = Mathf.Max(0, LayerMask.NameToLayer(layer));
            t.gameObject.AddComponent<BoxCollider>().size = size;
        }

        private static void AddToBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (list.Exists(s => s.path == ScenePath)) return;
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // 머티리얼별로 모아서 메시 하나씩 만드는 조립기. UV 는 1 = 1m (면 방향에 맞춘 평면 투영)
        private sealed class Kit
        {
            private sealed class Buffer
            {
                public readonly List<Vector3> V = new(), N = new();
                public readonly List<Vector2> U = new();
                public readonly List<int> T = new();
            }

            private readonly Dictionary<string, Buffer> _buffers = new();

            private Buffer Of(string material)
            {
                if (!_buffers.TryGetValue(material, out Buffer b))
                    _buffers[material] = b = new Buffer();
                return b;
            }

            private static Vector2 Project(Vector3 p, Vector3 n)
            {
                if (Mathf.Abs(n.y) > 0.7f) return new Vector2(p.x, p.z);
                Vector3 tangent = Vector3.Cross(Vector3.up, n).normalized;
                return new Vector2(Vector3.Dot(p, tangent), p.y);
            }

            // a b c d 는 앞에서 볼 때 시계 방향
            private void Quad(string material, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-10f) n = Vector3.Cross(c - a, d - a);
                n.Normalize();
                Buffer buf = Of(material);
                int start = buf.V.Count;
                foreach (Vector3 p in new[] { a, b, c, d })
                {
                    buf.V.Add(p);
                    buf.N.Add(n);
                    buf.U.Add(Project(p, n));
                }
                buf.T.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            private void Tri(string material, Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                Buffer buf = Of(material);
                int start = buf.V.Count;
                foreach (Vector3 p in new[] { a, b, c })
                {
                    buf.V.Add(p);
                    buf.N.Add(n);
                    buf.U.Add(Project(p, n));
                }
                buf.T.AddRange(new[] { start, start + 1, start + 2 });
            }

            public void Box(string material, Matrix4x4 m, Vector3 center, Vector3 size)
            {
                Vector3 h = size * 0.5f;
                Vector3 P(int x, int y, int z) => m.MultiplyPoint3x4(center + new Vector3(h.x * x, h.y * y, h.z * z));
                Quad(material, P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1));
                Quad(material, P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1));
                Quad(material, P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1));
                Quad(material, P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1));
                Quad(material, P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1));
                Quad(material, P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1));
            }

            public void Cylinder(string material, Matrix4x4 m, Vector3 bottom, float r0, float r1, float height, int sides)
            {
                Vector3 P(float angle, float r, float y) => m.MultiplyPoint3x4(bottom + new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r));
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides, b = (i + 1) * Mathf.PI * 2f / sides;
                    Quad(material, P(a, r0, 0f), P(a, r1, height), P(b, r1, height), P(b, r0, 0f));
                    Tri(material, P(a, 0f, height), P(b, r1, height), P(a, r1, height));
                    Tri(material, P(a, 0f, 0f), P(a, r0, 0f), P(b, r0, 0f));
                }
            }

            // 반원 채움 (아치 창·문). center = 반원 중심, +z 를 보는 면
            public void ArchFill(string material, Matrix4x4 m, Vector3 center, float radius)
            {
                const int steps = 10;
                for (int i = 0; i < steps; i++)
                {
                    float a = i * Mathf.PI / steps, b = (i + 1) * Mathf.PI / steps;
                    Vector3 pa = m.MultiplyPoint3x4(center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
                    Vector3 pb = m.MultiplyPoint3x4(center + new Vector3(Mathf.Cos(b) * radius, Mathf.Sin(b) * radius, 0f));
                    Tri(material, m.MultiplyPoint3x4(center), pa, pb);
                }
            }

            // 잎 한 장 (텍스처 전체가 올라가는 사각형, 양면 머티리얼)
            public void Card(string material, Matrix4x4 m, float size)
            {
                float h = size * 0.5f;
                Buffer buf = Of(material);
                int start = buf.V.Count;
                Vector3 normal = m.MultiplyVector(Vector3.forward).normalized;
                normal = Vector3.Slerp(normal, Vector3.up, 0.45f).normalized;
                Vector2[] uv = { new(0f, 0f), new(0f, 1f), new(1f, 1f), new(1f, 0f) };
                for (int i = 0; i < 4; i++)
                {
                    buf.V.Add(m.MultiplyPoint3x4(new Vector3((uv[i].x - 0.5f) * 2f * h, (uv[i].y - 0.5f) * 2f * h, 0f)));
                    buf.N.Add(normal);
                    buf.U.Add(uv[i]);
                }
                buf.T.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            // 평평한 볼록 다각형 바닥. UV 는 tile(m) 로 나눔
            public void Floor(string material, Vector2[] outline, float y, float tile)
            {
                Buffer buf = Of(material);
                int start = buf.V.Count;
                float area = 0f;
                for (int i = 0; i < outline.Length; i++)
                {
                    Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                    area += a.x * b.y - b.x * a.y;
                    buf.V.Add(new Vector3(a.x, y, a.y));
                    buf.N.Add(Vector3.up);
                    buf.U.Add(a / tile);
                }
                bool clockwise = area < 0f;   // 위에서 볼 때 시계 방향이면 그대로가 앞면
                for (int i = 1; i < outline.Length - 1; i++)
                    buf.T.AddRange(clockwise ? new[] { start, start + i, start + i + 1 } : new[] { start, start + i + 1, start + i });
            }

            public void Emit(Transform parent, Dictionary<string, Material> materials, string meshAssetPath)
            {
                foreach (KeyValuePair<string, Buffer> pair in _buffers)
                {
                    Buffer b = pair.Value;
                    var mesh = new Mesh { name = parent.name + "_" + pair.Key };
                    if (b.V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                    mesh.SetVertices(b.V);
                    mesh.SetNormals(b.N);
                    mesh.SetUVs(0, b.U);
                    mesh.SetTriangles(b.T, 0);
                    mesh.RecalculateTangents();
                    mesh.RecalculateBounds();
                    if (AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath) == null)
                        AssetDatabase.CreateAsset(mesh, meshAssetPath);
                    else
                        AssetDatabase.AddObjectToAsset(mesh, meshAssetPath);

                    Transform t = Child(parent, pair.Key);
                    t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = materials[pair.Key];
                }
                AssetDatabase.SaveAssets();
            }
        }
    }
}
