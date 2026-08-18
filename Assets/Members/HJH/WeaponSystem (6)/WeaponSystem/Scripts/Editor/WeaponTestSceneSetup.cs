using UnityEditor;
using UnityEngine;

namespace RobotWeapons.Editor
{
    public class WeaponTestSceneSetup : EditorWindow
    {
        private enum Champion { Tanker, MainDealer_Laser, MainDealer_Gun, SubDealer_Melee, Healer }

        private const string GeneratedFolder = "Assets/RobotWeapons_Generated";
        private const string TestObjectPrefix = "[TEST] ";

        private Champion selectedChampion = Champion.Tanker;
        private int dummyCount = 3;

        [MenuItem("Tools/Robot Weapons/Test Scene Setup")]
        public static void Open() => GetWindow<WeaponTestSceneSetup>("Weapon Test Setup");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("테스트 씬 배치", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "버튼을 누르면 임시 Box 모델로 플레이어 + 무기 + 테스트 더미를 씬에 자동 배치합니다.\n" +
                "무기 데이터/프리팹은 " + GeneratedFolder + " 폴더에 자동 생성됩니다.",
                MessageType.Info);

            EditorGUILayout.Space();
            selectedChampion = (Champion)EditorGUILayout.EnumPopup("테스트할 챔피언", selectedChampion);
            dummyCount = EditorGUILayout.IntSlider("더미 타겟 개수", dummyCount, 1, 8);

            EditorGUILayout.Space();
            if (GUILayout.Button("테스트 씬 한번에 배치", GUILayout.Height(32)))
                SetupScene();

            EditorGUILayout.Space();
            if (GUILayout.Button("모든 더미 체력 리셋"))
                ResetAllDummies();

            if (GUILayout.Button("배치된 테스트 오브젝트 정리 (씬에서만 제거)"))
                CleanupTestObjects();
        }

        private void SetupScene()
        {
            EnsureFolder();
            WeaponData data = GetOrCreateChampionData(selectedChampion);

            CleanupTestObjects();

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = TestObjectPrefix + "Ground";
            ground.transform.localScale = new Vector3(3f, 1f, 3f);

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Cube);
            player.name = TestObjectPrefix + "Player";
            player.transform.position = new Vector3(0f, 1f, 0f);
            player.transform.localScale = new Vector3(1f, 2f, 1f);
            Object.DestroyImmediate(player.GetComponent<Collider>());

            GameObject attackOrigin = new GameObject("AttackOrigin");
            attackOrigin.transform.SetParent(player.transform);
            attackOrigin.transform.localPosition = new Vector3(0f, 0f, 1f);

            GameObject weaponModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            weaponModel.name = "WeaponModel";
            weaponModel.transform.SetParent(player.transform);
            weaponModel.transform.localScale = new Vector3(0.15f, 0.15f, 1.2f);
            weaponModel.transform.localPosition = new Vector3(0.6f, 0.3f, 0.8f);
            weaponModel.GetComponent<Collider>().isTrigger = true;
            WeaponHitbox hitbox = weaponModel.AddComponent<WeaponHitbox>();

            var owner = player.AddComponent<Sample.SampleWeaponOwner>();
            var so = new SerializedObject(owner);
            so.FindProperty("attackOrigin").objectReferenceValue = attackOrigin.transform;
            so.FindProperty("weaponHitbox").objectReferenceValue = hitbox;
            so.FindProperty("equippedWeaponData").objectReferenceValue = data;

            if (selectedChampion == Champion.MainDealer_Laser)
            {
                GameObject beamGO = new GameObject("LaserBeam");
                beamGO.transform.SetParent(player.transform);
                var line = beamGO.AddComponent<LineRenderer>();
                line.startWidth = 0.05f;
                line.endWidth = 0.05f;
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.startColor = Color.cyan;
                line.endColor = Color.cyan;
                var visual = beamGO.AddComponent<LaserBeamVisual>();
                so.FindProperty("laserBeamVisual").objectReferenceValue = visual;
            }

            so.ApplyModifiedProperties();

            for (int i = 0; i < dummyCount; i++)
            {
                GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dummy.name = TestObjectPrefix + $"Dummy_{i}";
                dummy.transform.position = new Vector3((i - dummyCount / 2f) * 2f, 1f, 4f);
                dummy.AddComponent<TestDummy>();
            }

            Selection.activeGameObject = player;
            SceneView.lastActiveSceneView?.FrameSelected();

            Debug.Log($"[WeaponTestSceneSetup] 배치 완료: {selectedChampion} (좌클릭=Primary, 우클릭=Secondary)");
        }

        private void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
                AssetDatabase.CreateFolder("Assets", "RobotWeapons_Generated");
        }

        private WeaponData GetOrCreateChampionData(Champion champion)
        {
            switch (champion)
            {
                case Champion.Tanker:
                {
                    var d = GetOrCreateAsset<TankerWeaponData>("Test_Tanker");
                    d.type = WeaponType.Tanker;
                    d.mode = TankerWeaponData.Mode.Both;
                    d.projectilePrefab = GetOrCreateProjectilePrefab();
                    return d;
                }
                case Champion.MainDealer_Laser:
                {
                    var d = GetOrCreateAsset<LaserDealerData>("Test_LaserDealer");
                    d.type = WeaponType.MainDealer;
                    return d;
                }
                case Champion.MainDealer_Gun:
                {
                    var d = GetOrCreateAsset<GunDealerData>("Test_GunDealer");
                    d.type = WeaponType.MainDealer;
                    d.projectilePrefab = GetOrCreateProjectilePrefab();
                    return d;
                }
                case Champion.SubDealer_Melee:
                {
                    var d = GetOrCreateAsset<MeleeSubDealerData>("Test_SubDealer");
                    d.type = WeaponType.SubDealer;
                    return d;
                }
                case Champion.Healer:
                {
                    var d = GetOrCreateAsset<HealerData>("Test_Healer");
                    d.type = WeaponType.Healer;
                    d.healShotPrefab = GetOrCreateHealShotPrefab();
                    d.grenadePrefab = GetOrCreateGrenadePrefab();
                    return d;
                }
                default:
                    return null;
            }
        }

        private T GetOrCreateAsset<T>(string fileName) where T : ScriptableObject
        {
            string path = $"{GeneratedFolder}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private GameObject GetOrCreateProjectilePrefab()
        {
            string path = $"{GeneratedFolder}/Test_ProjectilePrefab.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            temp.transform.localScale = Vector3.one * 0.3f;
            temp.AddComponent<Projectile>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private GameObject GetOrCreateHealShotPrefab()
        {
            string path = $"{GeneratedFolder}/Test_HealShotPrefab.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            temp.transform.localScale = Vector3.one * 0.3f;
            temp.AddComponent<HealShotProjectile>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private GameObject GetOrCreateGrenadePrefab()
        {
            string path = $"{GeneratedFolder}/Test_GrenadePrefab.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            temp.transform.localScale = Vector3.one * 0.35f;
            temp.AddComponent<Rigidbody>();
            temp.AddComponent<HealGrenade>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private void ResetAllDummies()
        {
            var dummies = Object.FindObjectsByType<TestDummy>(FindObjectsSortMode.None);
            foreach (var d in dummies) d.ResetDummy();
            Debug.Log($"[WeaponTestSceneSetup] 더미 {dummies.Length}개 리셋 완료.");
        }

        private void CleanupTestObjects()
        {
            var all = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            int removed = 0;
            foreach (var go in all)
            {
                if (go != null && go.name.StartsWith(TestObjectPrefix))
                {
                    Object.DestroyImmediate(go);
                    removed++;
                }
            }
            if (removed > 0)
                Debug.Log($"[WeaponTestSceneSetup] 테스트 오브젝트 {removed}개 정리 완료.");
        }
    }
}
