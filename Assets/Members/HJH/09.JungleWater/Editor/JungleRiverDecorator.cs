using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using Unity.AI.Navigation.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Members.HJH.Environment.EditorTools
{
    // 메뉴
    //  HJH > Jungle Water > 나무에 잎 달기           : 씬의 Tree 프랍마다 가지 끝을 찾아 잎 카드를 단다
    //  HJH > Jungle Water > 강가 꾸미기 (다시 누르면 새로 배치) : Props 씬의 바위·나무·맹그로브 뿌리를 물가에 배치
    //
    // Props 모델은 한 파일에 메시가 여러 개 떨어져 들어 있다 (Rock.fbx = 바위 5개).
    // → 떨어진 덩어리마다 프리팹을 따로 만들고(바닥 가운데 = 기준점), Props 씬과 같은 머티리얼을 씌운다.
    public static class JungleRiverDecorator
    {
        private const string PropsDir = "Assets/Members/KM/Assets/Props/Natural";
        private const string TreeModel = PropsDir + "/Tree/Tree.fbx";
        private const string RockModel = PropsDir + "/Rock/Rock.fbx";
        private const string TreeMaterial = PropsDir + "/Tree/M_Tree.mat";
        private const string RockMaterial = PropsDir + "/Rock/M_Rock.mat";
        // MagrobeWood = 1m 납작한 뿌리 바닥 판 → 놓을 때 지형 굴곡에 맞춰 휘게 만든다
        private const string RootsModel = PropsDir + "/Floors/Wood/MagrobeWood.fbx";
        private const string RootsMaterial = PropsDir + "/Floors/Wood/M_Roots.mat";

        private const string Generated = "Assets/Members/HJH/09.JungleWater/Generated";
        private const string PrefabDir = Generated + "/Prefabs/Props";
        private const string LeafMaterialPath = Generated + "/Materials/M_JungleLeaf.mat";
        private const string LeafGraphMenu = "HJH/Jungle Water/잎 셰이더 그래프 만들기";

        private const string LeavesName = "Leaves";
        private const string DecorRootName = "RiverDecor";

        // 크기 (m)
        private const float TreeHeight = 5f;
        private const string RootsBaseName = "RootsBase";   // 나무 밑동에 까는 뿌리 둔덕
        private const float RockMin = 0.8f, RockMax = 1.8f;
        private const int MaxRocks = 25;

        // =====================================================================
        // 잎 달기
        // =====================================================================

        [MenuItem("HJH/Jungle Water/나무에 잎 달기")]
        public static void AddLeavesToSceneTrees()
        {
            Material leaf = LoadLeafMaterial();
            if (leaf == null)
                return;

            int count = LeafSceneTrees(leaf);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[JungleWater] 나무 {count}그루에 잎을 달았습니다. (다시 누르면 새로 달림)");
        }

        // 씬에 직접 놓은 원본 Tree 모델에 나무 머티리얼 + 잎
        private static int LeafSceneTrees(Material leaf)
        {
            var bark = AssetDatabase.LoadAssetAtPath<Material>(TreeMaterial);
            var rng = new System.Random();
            int count = 0;
            foreach (GameObject tree in FindModelInstances(TreeModel).ToList())
            {
                if (bark != null)
                    ApplyMaterial(tree, bark, undo: true);
                BuildLeaves(tree.transform, leaf, rng);
                count++;
            }
            return count;
        }

        // 원본 머티리얼의 베이스 텍스처를 어둡게·탁하게 바꾼 복사본 머티리얼을 만든다 (원본 파일은 그대로)
        private static string MakeTintedMaterial(string sourcePath, string name, Color multiply, float desaturate)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (source == null || !source.HasProperty("_BaseColor") || source.GetTexture("_BaseColor") is not Texture2D baseMap)
                return sourcePath;

            EnsureFolder(Generated + "/Textures");
            EnsureFolder(Generated + "/Materials");

            // 읽기 불가 텍스처도 GPU 로 복사해서 픽셀을 읽는다
            RenderTexture rt = RenderTexture.GetTemporary(baseMap.width, baseMap.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(baseMap, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(baseMap.width, baseMap.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, baseMap.width, baseMap.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            Color[] pixels = copy.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                float gray = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                c = Color.Lerp(c, new Color(gray, gray, gray, c.a), desaturate);
                pixels[i] = new Color(c.r * multiply.r, c.g * multiply.g, c.b * multiply.b, c.a);
            }
            copy.SetPixels(pixels);

            string texturePath = $"{Generated}/Textures/T_{name}.png";
            System.IO.File.WriteAllBytes(texturePath, copy.EncodeToPNG());
            Object.DestroyImmediate(copy);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);

            string materialPath = $"{Generated}/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(source);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.CopyPropertiesFromMaterial(source);
            }
            material.SetTexture("_BaseColor", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            EditorUtility.SetDirty(material);
            return materialPath;
        }

        // 나무 메시 윗부분 꼭짓점을 칸으로 묶어 '가지 끝'을 찾고, 거기에 잎 카드(잘게 나눈 평면)를 단다.
        public static void BuildLeaves(Transform tree, Material leaf, System.Random rng)
        {
            Transform old = tree.Find(LeavesName);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);

            List<Vector3> points = CollectWorldVertices(tree);
            Bounds bounds = WorldBounds(tree);
            if (points.Count == 0)
                points.Add(bounds.center + Vector3.up * bounds.extents.y);   // 메시를 못 읽으면 꼭대기 한 장

            float minY = bounds.min.y, maxY = bounds.max.y;
            float h = Mathf.Max(0.01f, maxY - minY);
            var trunk = new Vector3(bounds.center.x, 0f, bounds.center.z);

            List<Vector3> tips = FindBranchTips(points, minY, h);
            var crown = new Vector3(trunk.x, maxY, trunk.z);

            var leavesRoot = new GameObject(LeavesName);
            Undo.RegisterCreatedObjectUndo(leavesRoot, "Add Leaves");
            leavesRoot.transform.SetPositionAndRotation(tree.position, tree.rotation);
            leavesRoot.transform.SetParent(tree, true);

            CreateCard(leavesRoot.transform, crown + Vector3.up * h * 0.04f, h * 0.7f, trunk, h, leaf, rng);
            foreach (Vector3 tip in tips)
                CreateCard(leavesRoot.transform, tip + Vector3.up * h * 0.03f, h * 0.45f, trunk, h, leaf, rng);
        }

        private static List<Vector3> FindBranchTips(List<Vector3> points, float minY, float h)
        {
            float cell = h * 0.22f;
            var cells = new Dictionary<Vector3Int, (Vector3 sum, int count, float top)>();
            foreach (Vector3 p in points)
            {
                if (p.y < minY + h * 0.45f)
                    continue;
                var key = Vector3Int.FloorToInt(p / cell);
                cells.TryGetValue(key, out var c);
                cells[key] = (c.sum + p, c.count + 1, c.count == 0 ? p.y : Mathf.Max(c.top, p.y));
            }

            var candidates = cells.Values
                .Where(c => c.count >= 3)
                .Select(c => { Vector3 m = c.sum / c.count; m.y = c.top; return m; })
                .OrderByDescending(p => p.y);

            var tips = new List<Vector3>();
            foreach (Vector3 p in candidates)
            {
                if (tips.Any(t => Vector3.Distance(t, p) < cell * 0.9f))
                    continue;
                tips.Add(p);
                if (tips.Count >= 12)
                    break;
            }
            return tips;
        }

        private static void CreateCard(Transform parent, Vector3 position, float size, Vector3 trunk, float treeHeight, Material leaf, System.Random rng)
        {
            var card = GameObject.CreatePrimitive(PrimitiveType.Plane);   // 10x10 칸 → 꼭짓점이 많아 휘어짐이 부드럽다
            card.name = "LeafCard";
            Object.DestroyImmediate(card.GetComponent<Collider>());

            // 바깥쪽 카드일수록 끝이 아래로 처지게 기울임
            var outward = new Vector3(position.x - trunk.x, 0f, position.z - trunk.z);
            float outwardness = Mathf.Clamp01(outward.magnitude / (treeHeight * 0.5f));
            Quaternion droop = outward.sqrMagnitude > 1e-4f
                ? Quaternion.AngleAxis(25f * outwardness, Vector3.Cross(Vector3.up, outward.normalized))
                : Quaternion.identity;
            Quaternion jitter = Quaternion.Euler(Range(rng, -8f, 8f), Range(rng, 0f, 360f), Range(rng, -8f, 8f));

            card.transform.SetPositionAndRotation(position, droop * jitter);
            card.transform.localScale = Vector3.one * (size * Range(rng, 0.85f, 1.15f) / 10f);
            card.transform.SetParent(parent, true);

            var renderer = card.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = leaf;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private static List<Vector3> CollectWorldVertices(Transform root)
        {
            var points = new List<Vector3>();
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.name == "LeafCard" || InRootsBase(mf.transform))
                    continue;
                try
                {
                    Matrix4x4 m = mf.transform.localToWorldMatrix;
                    foreach (Vector3 v in mf.sharedMesh.vertices)
                        points.Add(m.MultiplyPoint3x4(v));
                }
                catch (System.Exception)
                {
                    // 읽을 수 없는 메시는 건너뜀 (꼭대기 카드만 붙는다)
                }
            }
            return points;
        }

        private static bool InRootsBase(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name == RootsBaseName)
                    return true;
            return false;
        }

        private static Bounds WorldBounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>()
                .Where(r => r.name != "LeafCard" && !InRootsBase(r.transform)).ToArray();
            if (renderers.Length == 0)
                return new Bounds(root.position, Vector3.one);
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers)
                b.Encapsulate(r.bounds);
            return b;
        }

        // =====================================================================
        // 프랍 프리팹 만들기 (모델 → 떨어진 덩어리마다 하나)
        // =====================================================================

        private sealed class PropVariant
        {
            public GameObject Prefab;
            public Vector3 Size;   // 기준 크기 (scale 1)
        }

        private static List<PropVariant> BuildVariants(string modelPath, string materialPath, string prefix, System.Action<GameObject> finish)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
                return new List<PropVariant>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            EnsureFolder(PrefabDir);
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(model);
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            temp.transform.localScale = Vector3.one;
            PrefabUtility.UnpackPrefabInstance(temp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            MeshRenderer[] renderers = temp.GetComponentsInChildren<MeshRenderer>();
            if (material != null)
                foreach (MeshRenderer r in renderers)
                    r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();

            var result = new List<PropVariant>();
            List<List<MeshRenderer>> groups = GroupTouching(renderers);
            for (int i = 0; i < groups.Count; i++)
            {
                Bounds b = groups[i][0].bounds;
                foreach (MeshRenderer r in groups[i])
                    b.Encapsulate(r.bounds);

                // 바닥 가운데가 기준점이 되게 옮겨 담는다
                var root = new GameObject($"{prefix}_{i}");
                var pivot = new Vector3(b.center.x, b.min.y, b.center.z);
                foreach (MeshRenderer r in groups[i])
                    r.transform.SetParent(root.transform, true);
                foreach (Transform child in root.transform)
                    child.position -= pivot;

                finish?.Invoke(root);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{root.name}.prefab");
                Object.DestroyImmediate(root);
                result.Add(new PropVariant { Prefab = prefab, Size = b.size });
            }

            Object.DestroyImmediate(temp);
            return result;
        }

        // 바운딩 박스가 닿는 메시끼리 한 덩어리 (나무 줄기+가지는 하나, 따로 떨어진 바위는 각각)
        private static List<List<MeshRenderer>> GroupTouching(MeshRenderer[] renderers)
        {
            var groups = new List<List<MeshRenderer>>();
            foreach (MeshRenderer r in renderers)
            {
                Bounds rb = r.bounds;
                rb.Expand(rb.size.magnitude * 0.05f);
                List<List<MeshRenderer>> touching = groups.Where(g => g.Any(o => o.bounds.Intersects(rb))).ToList();

                var merged = new List<MeshRenderer> { r };
                foreach (List<MeshRenderer> g in touching)
                {
                    merged.AddRange(g);
                    groups.Remove(g);
                }
                groups.Add(merged);
            }
            return groups;
        }

        // =====================================================================
        // 강가 꾸미기
        // =====================================================================

        private enum Zone { None, DeepRock, Shore, Bank }

        [MenuItem("HJH/Jungle Water/강가 꾸미기 (다시 누르면 새로 배치)")]
        public static void DecorateRiver()
        {
            GameObject water = GameObject.Find("Water");
            if (water == null || !water.TryGetComponent(out Renderer waterRenderer))
            {
                Debug.LogError("[JungleWater] 열린 씬에 'Water' 오브젝트가 없습니다.");
                return;
            }

            Bounds waterBounds = waterRenderer.bounds;
            float waterY = waterBounds.center.y;
            Terrain terrain = Terrain.activeTerrains.FirstOrDefault(t =>
                new Bounds(t.transform.position + t.terrainData.size * 0.5f, t.terrainData.size).Intersects(waterBounds));
            if (terrain == null)
            {
                Debug.LogError("[JungleWater] 물 아래에 터레인을 찾지 못했습니다.");
                return;
            }

            Material leaf = LoadLeafMaterial();
            if (leaf == null)
                return;

            // 바위: 원본(M_Rock)은 두고, 어둡고 탁하게 바꾼 복사본 머티리얼로 늪 색에 맞춘다
            string rockMaterial = MakeTintedMaterial(RockMaterial, "M_JungleRock", new Color(0.62f, 0.66f, 0.58f), 0.45f);
            List<PropVariant> rocks = BuildVariants(RockModel, rockMaterial, "PF_Rock", root => AddBoxColliders(root));
            // 얇은 판처럼 생긴 바위는 세워 놓으면 종이처럼 보여서 뺀다 (전부 얇으면 그대로 사용)
            List<PropVariant> chunky = rocks.Where(v => Mathf.Min(v.Size.x, v.Size.y, v.Size.z) / MaxExtent(v.Size) >= 0.3f).ToList();
            if (chunky.Count > 0)
                rocks = chunky;
            List<PropVariant> roots = BuildVariants(RootsModel, RootsMaterial, "PF_MangroveRoots", null);
            List<PropVariant> trees = BuildVariants(TreeModel, TreeMaterial, "PF_JungleTree", root =>
            {
                BuildLeaves(root.transform, leaf, new System.Random(root.name.GetHashCode()));
                AddTrunkCollider(root);
            });
            if (rocks.Count == 0 || roots.Count == 0 || trees.Count == 0)
            {
                Debug.LogError("[JungleWater] Props 모델(Rock / MagrobeWood / Tree)을 찾지 못했습니다.");
                return;
            }

            // 이전 배치 지우기 (Undo 가능)
            GameObject previous = GameObject.Find(DecorRootName);
            if (previous != null)
                Undo.DestroyObjectImmediate(previous);

            // 처음 씬 만들 때 넣은 임시 맹그로브는 끈다 (지우진 않음)
            GameObject placeholders = GameObject.Find("Mangroves");
            if (placeholders != null && placeholders.activeSelf)
            {
                Undo.RecordObject(placeholders, "Hide placeholder mangroves");
                placeholders.SetActive(false);
            }

            var decor = new GameObject(DecorRootName);
            Undo.RegisterCreatedObjectUndo(decor, "Decorate River");
            Transform treeGroup = Child(decor.transform, "Trees");
            Transform rockGroup = Child(decor.transform, "Rocks");
            RemovePaintedRoots(terrain, waterY);   // 예전 버전이 지형에 칠한 뿌리가 남아 있으면 바닥 복구

            int seed = System.Environment.TickCount;
            var rng = new System.Random(seed);
            var placed = new List<(Vector2 pos, float radius)>();
            int treeCount = 0, rockCount = 0, rootsBases = 0;

            Vector3 tp = terrain.transform.position;
            Vector3 ts = terrain.terrainData.size;
            float minX = Mathf.Max(tp.x, waterBounds.min.x) + 3f, maxX = Mathf.Min(tp.x + ts.x, waterBounds.max.x) - 3f;
            float minZ = Mathf.Max(tp.z, waterBounds.min.z) + 3f, maxZ = Mathf.Min(tp.z + ts.z, waterBounds.max.z) - 3f;

            for (int attempt = 0; attempt < 4000; attempt++)
            {
                var p = new Vector3(Range(rng, minX, maxX), 0f, Range(rng, minZ, maxZ));
                p.y = terrain.SampleHeight(p) + tp.y;
                float above = p.y - waterY;
                float steep = terrain.terrainData.GetSteepness((p.x - tp.x) / ts.x, (p.z - tp.z) / ts.z);

                Zone zone = Classify(above);
                // 나무: 둑 + 물가(살짝 잠긴 곳까지, 맹그로브처럼 뿌리 둔덕 위에 선다)
                bool treeSpot = zone == Zone.Bank || (zone == Zone.Shore && above >= -0.15f && rng.NextDouble() < 0.35);

                switch (zone)
                {
                    case Zone.Bank or Zone.Shore when treeSpot && treeCount < 20 && steep < 30f && NearWater(terrain, p, waterY) && Free(placed, p, 4.5f):
                    {
                        PropVariant v = Pick(trees, rng);
                        float scale = TreeHeight * Range(rng, 0.85f, 1.15f) / Mathf.Max(0.01f, v.Size.y);
                        GameObject tree = Place(v, treeGroup, p, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), scale, sink: 0.1f);
                        if (AddRootsBase(tree, roots, terrain, rng))
                            rootsBases++;
                        placed.Add((Flat(p), 4.5f));
                        treeCount++;
                        break;
                    }


                    case Zone.Shore when rockCount < MaxRocks && rng.NextDouble() < 0.5 && Free(placed, p, 1.4f):
                        rockCount += PlaceRockCluster(rocks, rockGroup, terrain, p, rng, placed);
                        break;

                    case Zone.DeepRock when rockCount < MaxRocks && rng.NextDouble() < 0.15 && Free(placed, p, 3f):
                    {
                        // 물속 바위: 바닥에 박고 윗부분만 수면 위로
                        PropVariant v = Pick(rocks, rng);
                        float size = Range(rng, RockMin, RockMax);
                        float scale = size / MaxExtent(v.Size);
                        float height = v.Size.y * scale;
                        float bottom = waterY + height * 0.3f - height;
                        Place(v, rockGroup, new Vector3(p.x, bottom, p.z), RandomYaw(rng), scale, sink: 0f);
                        placed.Add((Flat(p), 3f));
                        rockCount++;
                        break;
                    }

                }
            }

            int leafed = LeafSceneTrees(leaf);   // 직접 배치한 원본 Tree 모델에도 잎
            foreach (GameObject tree in FindModelInstances(TreeModel).ToList())   // 그 나무들 밑동에도 뿌리 둔덕
                if (AddRootsBase(tree, roots, terrain, rng))
                    rootsBases++;

            RebakeNavMesh();
            EditorSceneManager.MarkSceneDirty(decor.scene);
            Debug.Log($"[JungleWater] 강가 꾸미기 완료 (seed {seed}): 나무 {treeCount}, 바위 {rockCount}, 나무 밑 뿌리 둔덕 {rootsBases}, 잎 단 기존 나무 {leafed}. " +
                      $"프랍 종류: 바위 {rocks.Count} · 나무 {trees.Count}. 다시 누르면 새로 배치 (Ctrl+Z 로 되돌리기).");
        }

        // 나무 밑동에 뿌리 판을 깔아 '뿌리 둔덕 위에 선 나무'로 만든다 (나무 자식으로 붙여서 같이 움직임)
        private static bool AddRootsBase(GameObject tree, List<PropVariant> roots, Terrain terrain, System.Random rng)
        {
            if (tree == null || roots.Count == 0)
                return false;

            Transform old = tree.transform.Find(RootsBaseName);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);

            Vector3 basePos = tree.transform.position;
            Vector3 tp = terrain.transform.position, ts = terrain.terrainData.size;
            if (basePos.x < tp.x || basePos.x > tp.x + ts.x || basePos.z < tp.z || basePos.z > tp.z + ts.z)
                return false;   // 이 터레인 위의 나무가 아님
            basePos.y = terrain.SampleHeight(basePos) + tp.y;

            // 나무 크기에 맞춰 (키의 75%, 최소 2m)
            Bounds b = WorldBounds(tree.transform);
            float size = Mathf.Max(b.size.y * 0.75f, 2f) * Range(rng, 0.9f, 1.15f);
            PropVariant v = Pick(roots, rng);

            GameObject plate = PlaceOnGround(v, null, terrain, basePos, size / MaxExtent(v.Size), Range(rng, 0f, 360f), lift: 0.02f);
            plate.name = RootsBaseName;
            Undo.RegisterCreatedObjectUndo(plate, "Add Roots Base");
            plate.transform.SetParent(tree.transform, true);
            return true;
        }

        // 납작한 판을 지형에 붙인다.
        //  - 메시 꼭짓점이 충분하면: 꼭짓점마다 그 자리 지형 높이로 옮겨서 굴곡대로 휘게 (판의 원래 두께는 유지)
        //  - 꼭짓점이 너무 적으면(사각형 하나 등): 판 아래 지형에 가장 잘 맞는 기울기로 눕힌다
        private static GameObject PlaceOnGround(PropVariant v, Transform parent, Terrain terrain, Vector3 ground, float scale, float yaw, float lift)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(v.Prefab, parent);
            go.transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;

            bool conformed = false;
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
                conformed |= ConformToTerrain(mf, terrain, ground.y, lift);

            if (!conformed)
                TiltToTerrain(go, terrain, yaw, lift);
            return go;
        }

        private static bool ConformToTerrain(MeshFilter mf, Terrain terrain, float baseY, float lift)
        {
            Mesh source = mf.sharedMesh;
            if (source == null)
                return false;

            Vector3[] vertices;
            try
            {
                vertices = source.vertices;
            }
            catch (System.Exception)
            {
                return false;   // 읽을 수 없는 메시
            }
            if (vertices.Length < 30)
                return false;

            Matrix4x4 toWorld = mf.transform.localToWorldMatrix;
            Matrix4x4 toLocal = mf.transform.worldToLocalMatrix;
            float groundY = terrain.transform.position.y;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 w = toWorld.MultiplyPoint3x4(vertices[i]);
                float relief = w.y - baseY;   // 판 자체의 두께·울퉁불퉁함
                w.y = terrain.SampleHeight(w) + groundY + relief + lift;
                vertices[i] = toLocal.MultiplyPoint3x4(w);
            }

            Mesh mesh = Object.Instantiate(source);
            mesh.name = source.name + "_OnGround";
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;   // 씬에 저장되는 이 배치 전용 메시
            return true;
        }

        private static void TiltToTerrain(GameObject go, Terrain terrain, float yaw, float lift)
        {
            Bounds b = WorldBounds(go.transform);
            float groundY = terrain.transform.position.y;

            // 판 아래 3x3 지점에서 지형 높이를 재서 평균 기울기를 구한다
            var samples = new List<Vector3>();
            for (int ix = -1; ix <= 1; ix++)
            for (int iz = -1; iz <= 1; iz++)
            {
                var q = new Vector3(b.center.x + ix * b.extents.x, 0f, b.center.z + iz * b.extents.z);
                q.y = terrain.SampleHeight(q) + groundY;
                samples.Add(q);
            }

            Vector3 center = samples[4];
            float dx = ((samples[6].y + samples[7].y + samples[8].y) - (samples[0].y + samples[1].y + samples[2].y)) / (6f * Mathf.Max(0.01f, b.extents.x));
            float dz = ((samples[2].y + samples[5].y + samples[8].y) - (samples[0].y + samples[3].y + samples[6].y)) / (6f * Mathf.Max(0.01f, b.extents.z));
            var normal = new Vector3(-dx, 1f, -dz).normalized;
            // 너무 가파르게 세워지지 않게 최대 20도까지만 기울임
            normal = Vector3.RotateTowards(Vector3.up, normal, 20f * Mathf.Deg2Rad, 0f);

            float averageY = samples.Average(s => s.y);
            go.transform.SetPositionAndRotation(
                new Vector3(center.x, averageY + lift, center.z),
                Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, yaw, 0f));
        }

        // 예전 버전(지형에 뿌리 칠하기)이 남긴 레이어가 있으면 바닥을 원래대로 다시 칠한다
        private static void RemovePaintedRoots(Terrain terrain, float waterY)
        {
            if (terrain.terrainData.terrainLayers.Any(l => l != null && l.name == "TL_MangroveRoots"))
                RestoreFloor(terrain, waterY);
        }

        // =====================================================================
        // 지형 바닥 텍스처 복구
        // =====================================================================

        [MenuItem("HJH/Jungle Water/지형 바닥 텍스처 복구")]
        public static void RestoreTerrainFloor()
        {
            GameObject water = GameObject.Find("Water");
            if (water == null || !water.TryGetComponent(out Renderer waterRenderer))
            {
                Debug.LogError("[JungleWater] 열린 씬에 'Water' 오브젝트가 없습니다.");
                return;
            }

            Bounds waterBounds = waterRenderer.bounds;
            Terrain terrain = Terrain.activeTerrains.FirstOrDefault(t =>
                new Bounds(t.transform.position + t.terrainData.size * 0.5f, t.terrainData.size).Intersects(waterBounds));
            if (terrain == null)
            {
                Debug.LogError("[JungleWater] 물 아래에 터레인을 찾지 못했습니다.");
                return;
            }

            if (RestoreFloor(terrain, waterBounds.center.y))
                Debug.Log("[JungleWater] 지형 바닥 텍스처를 처음 만들 때 규칙(진흙·젖은 이끼·이끼)으로 다시 칠했습니다. (Ctrl+Z 로 되돌리기)");
        }

        // =====================================================================
        // 시작 섬 테두리 다듬기
        // =====================================================================

        // 처음 만든 시작 섬은 원뿔이라 물가 선이 완벽한 원 → 둘레 높이에 노이즈를 더해 들쭉날쭉하게.
        // 누를 때마다 노이즈가 더해지니 한 번만 누르면 된다 (Ctrl+Z 로 되돌리기 가능).
        [MenuItem("HJH/Jungle Water/시작 섬 테두리 다듬기")]
        public static void RoughenStartIsland()
        {
            GameObject water = GameObject.Find("Water");
            if (water == null || !water.TryGetComponent(out Renderer waterRenderer))
            {
                Debug.LogError("[JungleWater] 열린 씬에 'Water' 오브젝트가 없습니다.");
                return;
            }

            Bounds waterBounds = waterRenderer.bounds;
            Terrain terrain = Terrain.activeTerrains.FirstOrDefault(t =>
                new Bounds(t.transform.position + t.terrainData.size * 0.5f, t.terrainData.size).Intersects(waterBounds));
            if (terrain == null)
            {
                Debug.LogError("[JungleWater] 물 아래에 터레인을 찾지 못했습니다.");
                return;
            }

            TerrainData data = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(data, "Roughen Start Island");

            int res = data.heightmapResolution;
            float[,] heights = data.GetHeights(0, 0, res, res);
            Vector3 size = data.size;
            var center = new Vector2(size.x * 0.5f, size.z * 0.5f);   // 시작 섬 = 맵 가운데
            var rng = new System.Random();
            float ox = Range(rng, 0f, 100f), oz = Range(rng, 0f, 100f);

            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                var p = new Vector2((float)x / (res - 1) * size.x, (float)z / (res - 1) * size.z);
                Vector2 d = p - center;
                float dist = d.magnitude;
                // 섬 가장자리 고리(1.5~9m)에서만
                float ring = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.5f, 3f, dist)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(6f, 9f, dist)));
                if (ring <= 0f)
                    continue;

                float angle = Mathf.Atan2(d.y, d.x);
                float around = Mathf.PerlinNoise(Mathf.Cos(angle) * 1.3f + ox, Mathf.Sin(angle) * 1.3f + oz);   // 둘레 방향 굴곡
                float local = Mathf.PerlinNoise(p.x * 0.35f + oz, p.y * 0.35f + ox);                             // 잔 굴곡
                float n = around * 0.6f + local * 0.4f - 0.5f;
                heights[z, x] += n * 0.35f * ring / size.y;   // ±약 18cm → 물가 선이 ±1m 정도 들쭉날쭉
            }

            data.SetHeights(0, 0, heights);
            RestoreFloor(terrain, waterBounds.center.y);   // 높이가 바뀌었으니 바닥 색도 다시
            RebakeNavMesh();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            Debug.Log("[JungleWater] 시작 섬 테두리를 불규칙하게 다듬었습니다. (바닥 텍스처·NavMesh 도 다시 계산)");
        }

        // 레이어를 진흙·젖은 이끼·이끼 3장으로 되돌리고, 높이·경사로 다시 칠한다 (씬 처음 만들 때와 같은 규칙)
        private static bool RestoreFloor(Terrain terrain, float waterY)
        {
            var mud = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Generated + "/Terrain/TL_Mud.terrainlayer");
            var wet = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Generated + "/Terrain/TL_WetMoss.terrainlayer");
            var moss = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Generated + "/Terrain/TL_Moss.terrainlayer");
            if (mud == null || wet == null || moss == null)
            {
                Debug.LogError("[JungleWater] 바닥 레이어(TL_Mud / TL_WetMoss / TL_Moss)를 찾지 못했습니다.");
                return false;
            }

            TerrainData data = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(data, "Restore Terrain Floor");
            data.terrainLayers = new[] { mud, wet, moss };
            JungleWaterSceneBuilder.PaintSplat(data, waterY - terrain.transform.position.y);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            return true;
        }

        // 물 높이 기준 구역
        private static Zone Classify(float above)
        {
            if (above >= -0.6f && above < -0.2f) return Zone.DeepRock;
            if (above >= -0.2f && above < 0.3f) return Zone.Shore;
            if (above >= 0.3f && above < 1.6f) return Zone.Bank;
            return Zone.None;
        }

        // 주변 4m 안에 물이 있어야 '강가' 나무
        private static bool NearWater(Terrain terrain, Vector3 p, float waterY)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var q = p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4f;
                if (terrain.SampleHeight(q) + terrain.transform.position.y < waterY)
                    return true;
            }
            return false;
        }

        // 큰 바위 하나 + 옆에 작은 바위 1~2개
        private static int PlaceRockCluster(List<PropVariant> rocks, Transform parent, Terrain terrain, Vector3 p,
            System.Random rng, List<(Vector2, float)> placed)
        {
            PropVariant big = Pick(rocks, rng);
            float size = Range(rng, RockMin, RockMax);
            float scale = size / MaxExtent(big.Size);
            Place(big, parent, p, RandomYaw(rng), scale, sink: big.Size.y * scale * 0.35f);
            placed.Add((Flat(p), 1.4f));
            int count = 1;

            int small = rng.Next(0, 2);
            for (int i = 0; i < small; i++)
            {
                float a = Range(rng, 0f, Mathf.PI * 2f);
                var q = p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * size * Range(rng, 0.7f, 1.1f);
                q.y = terrain.SampleHeight(q) + terrain.transform.position.y;
                PropVariant v = Pick(rocks, rng);
                float s = size * Range(rng, 0.3f, 0.5f) / MaxExtent(v.Size);
                Place(v, parent, q, RandomYaw(rng), s, sink: v.Size.y * s * 0.2f);
                count++;
            }
            return count;
        }

        // 프리팹 기준점 = 바닥 가운데 → 땅 높이에 그대로 놓고 sink 만큼 묻는다
        private static GameObject Place(PropVariant v, Transform parent, Vector3 ground, Quaternion rotation, float scale, float sink)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(v.Prefab, parent);
            go.transform.SetPositionAndRotation(ground + Vector3.down * sink, rotation);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        private static bool Free(List<(Vector2 pos, float radius)> placed, Vector3 p, float radius)
        {
            Vector2 q = Flat(p);
            if (placed.Any(o => Vector2.Distance(o.pos, q) < Mathf.Max(o.radius, radius)))
                return false;

            // 이미 씬에 있는 물체(지형 제외)와 겹치면 건너뜀
            foreach (Collider c in Physics.OverlapSphere(p + Vector3.up * 1.2f, radius * 0.5f))
                if (c is not TerrainCollider)
                    return false;
            return true;
        }

        private static void AddBoxColliders(GameObject root)
        {
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>())
                if (mf.GetComponent<Collider>() == null)
                    mf.gameObject.AddComponent<BoxCollider>();   // 메시 크기에 자동으로 맞춰짐
        }

        private static void AddTrunkCollider(GameObject root)
        {
            Bounds b = WorldBounds(root.transform);
            float h = b.size.y;
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(b.center.x, b.min.y + h * 0.3f, b.center.z) - root.transform.position;
            capsule.height = h * 0.6f;
            capsule.radius = Mathf.Max(0.05f, h * 0.05f);
        }

        private static void RebakeNavMesh()
        {
            Object[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None)
                .Where(s => s.isActiveAndEnabled).Cast<Object>().ToArray();
            if (surfaces.Length > 0)
                NavMeshAssetManager.instance.StartBakingSurfaces(surfaces);
        }

        // =====================================================================
        // 도우미
        // =====================================================================

        private static Material LoadLeafMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(LeafMaterialPath);
            if (mat == null)
            {
                EditorApplication.ExecuteMenuItem(LeafGraphMenu);   // 잎 셰이더가 아직 없으면 먼저 만든다
                mat = AssetDatabase.LoadAssetAtPath<Material>(LeafMaterialPath);
            }
            if (mat == null)
                Debug.LogError($"[JungleWater] 잎 머티리얼이 없습니다. '{LeafGraphMenu}' 를 먼저 실행해 주세요.");
            return mat;
        }

        private static void ApplyMaterial(GameObject go, Material mat, bool undo)
        {
            foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.name == "LeafCard")
                    continue;
                if (undo)
                    Undo.RecordObject(r, "Apply Prop Material");
                r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            }
        }

        private static IEnumerable<GameObject> FindModelInstances(string modelPath)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                GameObject go = t.gameObject;
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go))
                    continue;
                Object source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
                if (source != null && AssetDatabase.GetAssetPath(source) == modelPath)
                    yield return go;
            }
        }

        private static PropVariant Pick(List<PropVariant> list, System.Random rng) => list[rng.Next(list.Count)];
        private static float MaxExtent(Vector3 size) => Mathf.Max(0.01f, Mathf.Max(size.x, size.y, size.z));
        private static Vector2 Flat(Vector3 p) => new(p.x, p.z);

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static Quaternion RandomYaw(System.Random rng) => Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);

        private static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
