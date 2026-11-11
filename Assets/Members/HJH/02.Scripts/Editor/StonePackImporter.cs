using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using E = HJH.EditorTools.MapEdgePolish;

namespace HJH.EditorTools
{
    // 새 돌 팩(stone-pack): Big 5 / Mid 6 / Small 8 / Runic 7 (발광 룬) 을 프리팹으로 만들고, 기존 바위를 크기 유지한 채 바꾼다.
    //  1) 반입 + 프리팹 26종: FBX 설정, 머티리얼(색·노멀·AO, Runic 은 발광), 프리팹(루트 > Model: 볼록 메시 콜라이더), 반투명 복사본(페이더용).
    //     기존 Rock_A / Rock_B 프리팹도 새 돌(Mid_1 / Big_1)로 바꿔서 어디서 쓰든 새 모양으로 보인다.
    //  2) 씬의 바위 바꾸기: Rock_A/Rock_B 인스턴스를 같은 크기·위치·회전·콜라이더 켜짐 상태로 새 돌 변종(랜덤)으로 교체.
    //  3) 암반 덩어리 3종 다시 만들기: 큰 돌(Big)을 베이스로 층층이 + 룬 돌 하나.
    //  4) 가려진 콜라이더 빼기: 플레이 공간에서 어느 방향으로도 닿지 않는 돌 콜라이더(바깥 돌 뒤에 숨은 것)를 끈다.
    public static class StonePackImporter
    {
        private const string Root = "Assets/Members/HJH/05.Data/MapProps/Stones/";
        private const string LegacyDir = "Assets/Members/HJH/05.Data/MapProps/Prefabs/";
        private const string FadeFolder = "Assets/Members/HJH/05.Data/Resources/HJH/RockFade";

        private static readonly Dictionary<string, int> Counts = new Dictionary<string, int> { { "Big", 5 }, { "Mid", 6 }, { "Small", 8 }, { "Runic", 7 } };
        private static readonly Dictionary<string, float> Nominal = new Dictionary<string, float> { { "Big", 3.2f }, { "Mid", 2.4f }, { "Small", 1.3f }, { "Runic", 0.9f } };

        private static GameObject Stone(string cls, int n) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}Prefabs/Stone_{cls}_{n}.prefab");
        private static GameObject RandStone(string cls, System.Random rng) => Stone(cls, 1 + rng.Next(Counts[cls]));

        // ───────────────────────── 1. 반입 ─────────────────────────
        [MenuItem("HJH/맵/돌/1 새 돌 반입 + 프리팹 26종 만들기 (Rock_A/B도 교체)")]
        private static void ImportAll()
        {
            if (EditorApplication.isPlaying) return;
            AssetDatabase.Refresh();
            var mats = new Dictionary<string, Material>();
            foreach (var cls in Counts.Keys)
            {
                ConfigureTextures(cls);
                mats[cls] = MakeMaterial(cls);
                MakeFade(mats[cls], AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}Textures/Stone_{cls}_Color.png"));
            }
            int made = 0;
            foreach (var kv in Counts)
                for (int n = 1; n <= kv.Value; n++)
                {
                    string fbx = $"{Root}Models/Stone_{kv.Key}_{n}.fbx";
                    ConfigureModel(fbx);
                    var mesh = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().FirstOrDefault();
                    if (mesh == null) { Debug.LogWarning("[Stones] 메시를 못 찾음: " + fbx); continue; }
                    var root = new GameObject($"Stone_{kv.Key}_{n}");
                    var model = new GameObject("Model"); model.transform.SetParent(root.transform, false);
                    model.AddComponent<MeshFilter>().sharedMesh = mesh;
                    model.AddComponent<MeshRenderer>().sharedMaterial = mats[kv.Key];
                    var mc = model.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.convex = true;
                    PrefabUtility.SaveAsPrefabAsset(root, $"{Root}Prefabs/Stone_{kv.Key}_{n}.prefab");
                    Object.DestroyImmediate(root);
                    made++;
                }

            // 기존 Rock_A / Rock_B 프리팹을 새 돌로 (LOD 는 제거: 새 돌은 이미 저폴리)
            Repoint(LegacyDir + "Rock_A.prefab", "Mid", 1, mats["Mid"]);
            Repoint(LegacyDir + "Rock_B.prefab", "Big", 1, mats["Big"]);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Stones] 프리팹 {made}종 생성 ({Root}Prefabs). Rock_A→Stone_Mid_1, Rock_B→Stone_Big_1 로 교체. 큰 돌 약 3.2m / 중간 2.4m / 작은 돌 1.3m / 룬 0.9m (가장 긴 변). 다음: 2번(씬 바위 변주), 3번(암반 덩어리), 4번(콜라이더 정리).");
        }

        private static void Repoint(string prefabPath, string cls, int n, Material mat)
        {
            var mesh = AssetDatabase.LoadAllAssetsAtPath($"{Root}Models/Stone_{cls}_{n}.fbx").OfType<Mesh>().FirstOrDefault();
            if (mesh == null) return;
            var c = PrefabUtility.LoadPrefabContents(prefabPath);   // 루트는 그대로 두므로 씬 인스턴스의 위치·크기 오버라이드는 유지됨
            try
            {
                foreach (var lg in c.GetComponents<LODGroup>()) Object.DestroyImmediate(lg);
                for (int i = c.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(c.transform.GetChild(i).gameObject);   // 옛 FBX 인스턴스·LOD 자식 전부 제거
                var model = new GameObject("Model"); model.transform.SetParent(c.transform, false);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                model.AddComponent<MeshRenderer>().sharedMaterial = mat;
                var mc = model.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.convex = true;
                PrefabUtility.SaveAsPrefabAsset(c, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(c); }
        }

        private static void ConfigureModel(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter; if (imp == null) return;
            bool ch = false;
            if (imp.materialImportMode != ModelImporterMaterialImportMode.None) { imp.materialImportMode = ModelImporterMaterialImportMode.None; ch = true; }
            if (imp.importNormals != ModelImporterNormals.Import) { imp.importNormals = ModelImporterNormals.Import; ch = true; }
            if (imp.importTangents != ModelImporterTangents.CalculateMikk) { imp.importTangents = ModelImporterTangents.CalculateMikk; ch = true; }
            if (imp.importCameras || imp.importLights) { imp.importCameras = false; imp.importLights = false; ch = true; }
            if (imp.meshCompression != ModelImporterMeshCompression.Off) { imp.meshCompression = ModelImporterMeshCompression.Off; ch = true; }
            if (!imp.generateSecondaryUV) { imp.generateSecondaryUV = true; ch = true; }
            if (ch) imp.SaveAndReimport();
        }

        private static void ConfigureTextures(string cls)
        {
            int size = (cls == "Big" || cls == "Runic") ? 2048 : 1024;
            foreach (var kind in new[] { "Color", "Normal", "AO", "Emission" })
            {
                string p = $"{Root}Textures/Stone_{cls}_{kind}.png";
                var imp = AssetImporter.GetAtPath(p) as TextureImporter; if (imp == null) continue;
                bool ch = false;
                var type = kind == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (imp.textureType != type) { imp.textureType = type; ch = true; }
                bool srgb = kind == "Color" || kind == "Emission";
                if (kind != "Normal" && imp.sRGBTexture != srgb) { imp.sRGBTexture = srgb; ch = true; }
                if (!imp.mipmapEnabled) { imp.mipmapEnabled = true; ch = true; }
                if (imp.maxTextureSize != size) { imp.maxTextureSize = size; ch = true; }
                if (ch) imp.SaveAndReimport();
            }
        }

        private static Material MakeMaterial(string cls)
        {
            string path = $"{Root}Materials/Stone_{cls}_Mat.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            Texture2D T(string k) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}Textures/Stone_{cls}_{k}.png");
            m.SetTexture("_BaseMap", T("Color")); m.SetColor("_BaseColor", Color.white);
            var n = T("Normal"); if (n != null) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f); }
            var ao = T("AO"); if (ao != null) { m.SetTexture("_OcclusionMap", ao); m.SetFloat("_OcclusionStrength", 1f); }
            m.SetFloat("_Smoothness", 0.12f);
            var em = T("Emission");
            if (cls == "Runic" && em != null)
            {
                m.SetTexture("_EmissionMap", em); m.SetColor("_EmissionColor", new Color(2.2f, 2.2f, 2.2f));
                m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // JungleOcclusionFader 는 Resources/HJH/RockFade/<머티리얼 이름>_Fade 를 찾는다.
        private static void MakeFade(Material orig, Texture2D tex)
        {
            string dest = $"{FadeFolder}/{orig.name}_Fade.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(dest) == null && !AssetDatabase.CopyAsset(FadeFolder + "/M_Rock_Fade.mat", dest))
            { Debug.LogWarning("[Stones] 반투명 복사본을 못 만듦: " + dest); return; }
            var fade = AssetDatabase.LoadAssetAtPath<Material>(dest);
            if (fade == null) return;
            fade.SetTexture("_BaseMap", tex); fade.SetTexture("_BumpMap", null); fade.DisableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(fade);
        }

        // ───────────────────────── 2. 씬 바위 바꾸기 ─────────────────────────
        private static void SwapSceneRocks()
        {
            if (EditorApplication.isPlaying) return;
            var rockA = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyDir + "Rock_A.prefab");
            var rockB = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyDir + "Rock_B.prefab");
            if (Stone("Big", 1) == null) { Debug.LogWarning("[Stones] 먼저 1번을 실행하세요."); return; }

            var targets = new List<(GameObject go, bool isB)>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                var go = t.gameObject;
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;
                var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (src == rockA) targets.Add((go, false)); else if (src == rockB) targets.Add((go, true));
            }
            Undo.IncrementCurrentGroup(); int grp = Undo.GetCurrentGroup();
            int n = 0;
            foreach (var (go, isB) in targets)
            {
                var t = go.transform;
                float baseN = isB ? 3.0f : 2.4f;
                float T = baseN * (t.lossyScale.x + t.lossyScale.z) * 0.5f;       // 지금 크기(최장변 근사)
                string cls = isB ? (T >= 2.2f ? "Big" : "Mid") : (T >= 1.8f ? "Mid" : "Small");
                var rng = new System.Random(Mathf.RoundToInt(t.position.x * 131f + t.position.z * 17f + t.position.y * 7f));
                var prefab = RandStone(cls, rng);
                if (prefab == null) continue;

                var oldCol = go.GetComponentInChildren<Collider>(true);
                bool colOn = oldCol == null || oldCol.enabled;
                var oldR = go.GetComponentInChildren<MeshRenderer>(true);
                var shadow = oldR != null ? oldR.shadowCastingMode : UnityEngine.Rendering.ShadowCastingMode.On;

                var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, t.parent);
                Undo.RegisterCreatedObjectUndo(g, "Swap rock");
                g.transform.position = t.position; g.transform.rotation = t.rotation;
                g.transform.localScale = t.localScale * (baseN / Nominal[cls]);      // 보이는 크기 유지
                g.transform.SetSiblingIndex(t.GetSiblingIndex());
                g.name = prefab.name;
                if (!colOn) foreach (var c in g.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                if (shadow != UnityEngine.Rendering.ShadowCastingMode.On) foreach (var r in g.GetComponentsInChildren<MeshRenderer>(true)) r.shadowCastingMode = shadow;
                Undo.DestroyObjectImmediate(go);
                n++;
            }
            Undo.CollapseUndoOperations(grp);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Stones] 씬의 바위 {n}개를 새 돌로 교체 (크기·위치·회전·콜라이더 켜짐 유지). 암반 덩어리 안의 돌은 3번으로 다시 만드세요.");
        }

        // ───────────────────────── 3. 암반 덩어리 ─────────────────────────
        private static void RebuildMasses()
        {
            if (EditorApplication.isPlaying) return;
            if (Stone("Big", 1) == null) { Debug.LogWarning("[Stones] 먼저 1번을 실행하세요."); return; }
            string[] names = { "RockMass_A", "RockMass_B", "RockMass_C" };
            var shapes = new[] { new Vector3(1f, 1f, 1f), new Vector3(1.3f, 0.9f, 0.8f), new Vector3(0.85f, 1.25f, 0.85f) };   // 넓은 판 / 길쭉 / 높은 단
            for (int v = 0; v < 3; v++) BuildMass(names[v], 7001 + v * 97, shapes[v]);
            AssetDatabase.SaveAssets();
            Debug.Log("[Stones] RockMass_A/B/C 를 새 돌로 다시 만들었습니다 (씬의 인스턴스는 자동 반영). 베이스·옆 큰 돌·2단 중간 돌에만 콜라이더, 나머지는 시각용.");
        }

        private static void BuildMass(string name, int seed, Vector3 shape)
        {
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            float L(float lo, float hi) => Mathf.Lerp(lo, hi, R());
            var root = new GameObject(name);
            try
            {
                float u = L(1.7f, 2.2f);
                Vector3 bs = new Vector3(u * shape.x, u * shape.y, u * shape.z);
                Add(root.transform, RandStone("Big", rng), "Base", new Vector3(0f, -0.25f, 0f), bs, R() * 360f, true);
                float W = 1.5f * u;                                  // 베이스 반폭 근사
                float topY = 2.0f * u * shape.y;                     // 베이스 윗면 높이 근사
                for (int i = 0; i < 2; i++)                          // 옆구리 큰 돌 2개 (콜라이더)
                {
                    float ang = (i == 0 ? 0.2f : 0.7f) * Mathf.PI * 2f + L(-0.2f, 0.2f);
                    float s = L(1.1f, 1.5f);
                    Add(root.transform, RandStone("Big", rng), "Flank" + i, new Vector3(Mathf.Cos(ang) * W * 0.95f, -0.2f, Mathf.Sin(ang) * W * 0.8f), Vector3.one * s, R() * 360f, true);
                }
                Add(root.transform, RandStone("Mid", rng), "Tier2", new Vector3(L(-0.25f, 0.25f) * W, topY * 0.75f, -0.2f * W), Vector3.one * L(1.1f, 1.5f), R() * 360f, true);

                int n = 4 + rng.Next(0, 2);                          // 옆 중간·작은 돌 (시각용)
                for (int i = 0; i < n; i++)
                {
                    float ang = (i / (float)n + L(-0.08f, 0.08f)) * Mathf.PI * 2f;
                    var pos = new Vector3(Mathf.Cos(ang) * W * L(1.15f, 1.45f), L(-0.3f, -0.1f), Mathf.Sin(ang) * W * L(1.0f, 1.3f));
                    bool mid = R() < 0.5f;
                    Add(root.transform, RandStone(mid ? "Mid" : "Small", rng), "Side" + i, pos, Vector3.one * (mid ? L(0.5f, 0.9f) : L(0.9f, 1.5f)), R() * 360f, false);
                }
                int m = 3 + rng.Next(0, 2);                          // 윗면 작은 돌
                for (int i = 0; i < m; i++)
                {
                    float ang = R() * Mathf.PI * 2f, rad = L(0.1f, 0.6f);
                    Add(root.transform, RandStone("Small", rng), "Chip" + i, new Vector3(Mathf.Cos(ang) * W * rad, topY * L(0.8f, 1.0f), Mathf.Sin(ang) * W * rad), Vector3.one * L(0.25f, 0.45f), R() * 360f, false);
                }
                var rune = RandStone("Runic", rng);                  // 룬 돌 하나 (발광 포인트)
                if (rune != null) Add(root.transform, rune, "Rune", new Vector3(W * 1.1f, -0.05f, -W * 0.5f), Vector3.one * L(0.9f, 1.2f), R() * 360f, false);

                PrefabUtility.SaveAsPrefabAsset(root, LegacyDir + name + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Add(Transform parent, GameObject prefab, string name, Vector3 pos, Vector3 scale, float yaw, bool collider)
        {
            if (prefab == null) return;
            var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            g.name = name; g.transform.localPosition = pos; g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f); g.transform.localScale = scale;
            if (!collider) foreach (var c in g.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        // ───────────────────────── 5. 콜라이더 심기 ─────────────────────────
        [MenuItem("HJH/맵/돌/5 돌·버섯·통나무 콜라이더 심기 (가려진 것은 자동으로 뺌)")]
        private static void AddCollidersAll()
        {
            if (EditorApplication.isPlaying) return;
            // 1) 버섯 프리팹에 캡슐 콜라이더 (무리 전체를 감싸는 크기)
            foreach (var n in new[] { "Mushroom_A", "Mushroom_B", "Mushroom_C" })
            {
                string path = $"Assets/Members/HJH/05.Data/MapProps/Mushrooms/{n}.prefab";
                if (!File.Exists(path)) continue;
                var c = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var model = c.transform.Find("Model"); if (model == null) continue;
                    var mf = model.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null && model.GetComponent<Collider>() == null)
                    {
                        var b = mf.sharedMesh.bounds;
                        var cap = model.gameObject.AddComponent<CapsuleCollider>();
                        cap.direction = 1; cap.center = new Vector3(0f, b.center.y, 0f);
                        cap.radius = Mathf.Min(b.extents.x, b.extents.z) * 0.8f; cap.height = Mathf.Max(b.size.y, cap.radius * 2f);
                    }
                    PrefabUtility.SaveAsPrefabAsset(c, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(c); }
            }
            // 2) 통나무 프리팹: 볼록 메시 콜라이더가 없으면 추가
            string logPath = LegacyDir + "FallenLog.prefab";
            if (File.Exists(logPath))
            {
                var c = PrefabUtility.LoadPrefabContents(logPath);
                try
                {
                    var model = c.transform.Find("Model");
                    if (model != null && model.GetComponent<Collider>() == null && model.GetComponent<MeshFilter>() != null)
                    { var mc = model.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = model.GetComponent<MeshFilter>().sharedMesh; mc.convex = true; PrefabUtility.SaveAsPrefabAsset(c, logPath); }
                }
                finally { PrefabUtility.UnloadPrefabContents(c); }
            }

            // 2b) 암반 덩어리 프리팹: 조약돌(Chip*)만 끄고 나머지(Base/Flank/Tier2/Side*/Rune)는 콜라이더 켬 → 새 맵에 놓아도 처음부터 적용
            foreach (var n in new[] { "RockMass_A", "RockMass_B", "RockMass_C" })
            {
                string mp = LegacyDir + n + ".prefab";
                if (!File.Exists(mp)) continue;
                var c = PrefabUtility.LoadPrefabContents(mp);
                try
                {
                    foreach (var mc in c.GetComponentsInChildren<MeshCollider>(true))
                    {
                        // 이 콜라이더를 가진 돌 조각(바로 위 프리팹 인스턴스 루트 또는 이 오브젝트)의 이름
                        var piece = PrefabUtility.GetNearestPrefabInstanceRoot(mc.gameObject);
                        string pn = piece != null && piece != c ? piece.name : mc.transform.parent != null ? mc.transform.parent.name : mc.name;
                        mc.enabled = true;   // Chip 도 0.3m 이상이라 조약돌 아님 -> 켬
                    }
                    PrefabUtility.SaveAsPrefabAsset(c, mp);
                }
                finally { PrefabUtility.UnloadPrefabContents(c); }
            }

            // 3) 씬: 통나무·버섯은 전부, 돌은 가로 0.8m 이상인 것만 콜라이더 켬 (조약돌 = 0.8m 미만은 끔: 대시·탄이 걸리지 않게)
            Undo.IncrementCurrentGroup(); int grp = Undo.GetCurrentGroup();
            int logsN = 0, mushN = 0, rocksOn = 0, rocksOff = 0;
            foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (col is TerrainCollider) continue;
                bool skip = false;
                for (var t = col.transform; t != null; t = t.parent) if (t.name == "Boundary_Walls" || t.name == "Bridge" || t.name.StartsWith("Wall_")) { skip = true; break; }
                if (skip) continue;
                var rootGo = PrefabUtility.GetOutermostPrefabInstanceRoot(col.gameObject);
                string rn = rootGo != null ? rootGo.name : col.transform.root.name;
                // 프리팹 풀거나 그룹 밑에 넣은 돌도 잡도록: 위로 올라가며 이름이 돌/통나무/버섯인 첫 오브젝트를 씀
                for (var t = col.transform; t != null; t = t.parent)
                    if (t.name.StartsWith("Stone_") || t.name.StartsWith("Rock") || t.name.StartsWith("FallenLog") || t.name.StartsWith("Mushroom_")) { rn = t.name; break; }
                bool want;
                if (rn.StartsWith("FallenLog")) { want = true; logsN++; }
                else if (rn.StartsWith("Mushroom_")) { want = true; mushN++; }
                else if (rn.StartsWith("Stone_") || rn.StartsWith("Rock"))
                {
                    want = Assets.Members.HJH.Environment.StoneScale.WantsCollider(col.transform);   // Scale 0.2 이하 = 조약돌
                    if (want) rocksOn++; else rocksOff++;
                }
                else continue;
                if (col.enabled != want) { Undo.RecordObject(col, "Colliders"); col.enabled = want; }
            }
            Undo.CollapseUndoOperations(grp);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Stones] 콜라이더 켬: 통나무 {logsN}, 버섯 {mushN}, 돌 {rocksOn}개 (Scale 0.2 이하 {rocksOff}개는 끔). 이어서 맵 밖·닿지 않는 콜라이더를 정리합니다.");
            PruneHiddenColliders();
            MapLayers.Assign();   // 총알이 닿는 레이어(Obstacle/Ground)로 정리
        }

        // ───────────────────────── 4. 가려진 콜라이더 빼기 ─────────────────────────
        internal static System.Func<Vector2, float> PlayDistFn()
        {
            string sn = EditorSceneManager.GetActiveScene().name;
            if (sn == "Map_L_Normal") return p => E.Inside(p) ? 0f : E.DistEdge(p);
            if (sn == "Map_Bridge_Normal") { MapSpecBuilder.UseBridgeSpec(); return MapSpecBuilder.PlayDist; }
            return null;
        }

        [MenuItem("HJH/맵/돌/4 가려진 콜라이더 빼기 (플레이 공간에서 안 닿는 것)")]
        private static void PruneHiddenColliders()
        {
            if (EditorApplication.isPlaying) return;
            var playDist = PlayDistFn();
            if (playDist == null) { Debug.LogWarning("[Stones] 이 씬은 플레이 영역 정보가 없어 건너뜁니다 (Map_L_Normal / Map_Bridge_Normal 만 지원)."); return; }
            var terrain = Terrain.activeTerrain;
            Physics.SyncTransforms();

            // 대상: 맵에 놓인 모든 콜라이더 (돌·통나무·버섯·나무 등). 지형·벽·다리·플레이어·트리거·물리 오브젝트는 건드리지 않음
            var cands = new List<Collider>();
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (!c.enabled || c.isTrigger || c is TerrainCollider) continue;
                if (c is MeshCollider mcol && mcol.sharedMesh == null) continue;
                if (c.GetComponentInParent<Rigidbody>() != null || c.GetComponentInParent<CharacterController>() != null) continue;
                bool skip = false;
                for (var t = c.transform; t != null; t = t.parent)
                {
                    string n = t.name;
                    if (n == "Boundary_Walls" || n == "Bridge" || n.StartsWith("Terrain") || n.StartsWith("Wall_") || n == "PlayerRig" || n == "Player"
                        || n.Contains("Portal") || n.Contains("Spawn") || n.Contains("Shop") || n.Contains("Door")) { skip = true; break; }
                }
                if (!skip) cands.Add(c);
            }

            Undo.IncrementCurrentGroup(); int grp = Undo.GetCurrentGroup();
            int off = 0, kept = 0, farOff = 0;
            foreach (var c in cands)
            {
                var b = c.bounds; var center = b.center;
                // 맵 밖: 바닥 면적 어느 점도 플레이 공간에서 1.5m 넘게 떨어져 있으면 플레이어·탄이 닿을 수 없음 -> 끔
                bool near = false;
                for (int ix = -1; ix <= 1 && !near; ix++)
                    for (int iz = -1; iz <= 1 && !near; iz++)
                    {
                        var q = new Vector2(center.x + ix * b.extents.x, center.z + iz * b.extents.z);
                        if (playDist(q) <= 1.5f) near = true;
                    }
                if (!near) { Undo.RecordObject(c, "Prune collider"); c.enabled = false; farOff++; off++; continue; }

                float rad = Mathf.Max(b.extents.x, b.extents.z);
                bool exposed = false;
                for (int k = 0; k < 16 && !exposed; k++)
                {
                    float ang = k / 16f * Mathf.PI * 2f; var dir2 = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    foreach (float r in new[] { rad + 0.5f, rad + 2.0f })
                    {
                        var o2 = new Vector2(center.x, center.z) + dir2 * r;
                        if (playDist(o2) > 0.01f) continue;                       // 플레이 공간 안에서만 쏜다
                        float gy = terrain != null ? terrain.SampleHeight(new Vector3(o2.x, 0, o2.y)) + terrain.transform.position.y : b.min.y;
                        foreach (float h in new[] { 0.5f, 1.4f })
                        {
                            var origin = new Vector3(o2.x, gy + h, o2.y);
                            var target = new Vector3(center.x, Mathf.Clamp(gy + h, b.min.y + 0.1f, b.max.y), center.z);
                            var d = target - origin; float dist = d.magnitude; if (dist < 0.01f) continue;
                            if (Physics.Raycast(origin, d / dist, out var hit, dist + 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider == c) { exposed = true; break; }
                        }
                        if (exposed) break;
                    }
                }
                if (exposed) { kept++; continue; }
                Undo.RecordObject(c, "Prune collider"); c.enabled = false; off++;
            }
            Undo.CollapseUndoOperations(grp);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Stones] 콜라이더 {cands.Count}개 검사: 닿는 {kept}개 유지, {off}개 끔 (그중 맵 밖 {farOff}개) (Ctrl+Z 로 되돌릴 수 있음). 플레이 공간 어디에서도 레이가 닿지 않는 것(다른 돌·벽 뒤)이 대상입니다.");
        }
    }
}
