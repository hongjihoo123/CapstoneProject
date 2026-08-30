using UnityEditor;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace RobotWeapons.Editor
{
    public class WeaponTestSceneSetup : EditorWindow
    {
        private enum Champion { Tanker, MainDealer_Laser, MainDealer_Gun, MainDealer_Bow, SubDealer_MeleeSawedOff, SubDealer_SniperSawedOff, Healer }

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

            GameObject player = new GameObject(TestObjectPrefix + "Player");
            player.transform.position = new Vector3(0f, 1f, 0f);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(player.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(1f, 2f, 1f);

            GameObject aimOrigin = new GameObject("AimOrigin");
            aimOrigin.transform.SetParent(player.transform);
            aimOrigin.transform.localPosition = new Vector3(0.164f, 0.256f, 1.514f);

            GameObject weaponModel;
            if (selectedChampion == Champion.MainDealer_Bow)
            {
                // 대충 활 몸통 모양 - 세로로 긴 얇은 원기둥 (총처럼 가로로 긴 큐브랑 구분되게)
                weaponModel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                weaponModel.name = "WeaponModel";
                weaponModel.transform.SetParent(player.transform);
                weaponModel.transform.localScale = new Vector3(0.03f, 0.45f, 0.03f);
                weaponModel.transform.localPosition = new Vector3(0.25f, -0.1f, 0.5f);
            }
            else
            {
                weaponModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                weaponModel.name = "WeaponModel";
                weaponModel.transform.SetParent(player.transform);
                weaponModel.transform.localScale = new Vector3(0.15f, 0.15f, 1.2f);
                weaponModel.transform.localPosition = new Vector3(0.25f, -0.1f, 0.5f);
            }
            weaponModel.GetComponent<Collider>().isTrigger = true;
            WeaponHitbox hitbox = weaponModel.AddComponent<WeaponHitbox>();

            // WeaponModel(비균등 스케일)의 자식으로 두면 위치가 왜곡되므로,
            // MuzzleOrigin은 반드시 플레이어 루트(스케일 1) 직속으로 둔다.
            GameObject muzzleOrigin = new GameObject("MuzzleOrigin");
            muzzleOrigin.transform.SetParent(player.transform);
            muzzleOrigin.transform.localPosition = new Vector3(0.25f, -0.1f, 1.0f);

            var owner = player.AddComponent<Sample.SampleWeaponOwner>();
            var so = new SerializedObject(owner);
            so.FindProperty("aimOrigin").objectReferenceValue = aimOrigin.transform;
            so.FindProperty("muzzleOrigin").objectReferenceValue = muzzleOrigin.transform;
            so.FindProperty("weaponHitbox").objectReferenceValue = hitbox;
            so.FindProperty("equippedWeaponData").objectReferenceValue = data;
            so.FindProperty("weaponModelTransform").objectReferenceValue = weaponModel.transform;

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

            if (selectedChampion == Champion.Healer)
            {
                var profile = GetOrCreateHealVignetteProfile();
                var volume = player.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.profile = profile;
                var effect = player.AddComponent<HealScreenEffect>();
                so.FindProperty("healScreenEffect").objectReferenceValue = effect;
            }

            bool needsRangedUI = selectedChampion == Champion.MainDealer_Gun
                || selectedChampion == Champion.MainDealer_Laser
                || selectedChampion == Champion.MainDealer_Bow
                || selectedChampion == Champion.SubDealer_MeleeSawedOff
                || selectedChampion == Champion.SubDealer_SniperSawedOff;

            if (needsRangedUI)
            {
                Text ammoText = CreateAmmoUI();
                so.FindProperty("ammoText").objectReferenceValue = ammoText;

                Image hitMarker = CreateHitMarkerUI();
                var hitFeedback = player.AddComponent<HitFeedback>();
                var hfSo = new SerializedObject(hitFeedback);
                hfSo.FindProperty("hitMarker").objectReferenceValue = hitMarker;
                hfSo.ApplyModifiedProperties();
                so.FindProperty("hitFeedback").objectReferenceValue = hitFeedback;

                var impulseSource = player.AddComponent<CinemachineImpulseSource>();
                so.FindProperty("impulseSource").objectReferenceValue = impulseSource;

                CinemachineCamera cmCam = CreateTestCamera(aimOrigin);
                so.FindProperty("cinemachineCamera").objectReferenceValue = cmCam;

                if (selectedChampion == Champion.MainDealer_Laser)
                {
                    Slider gaugeSlider = CreateGaugeSliderUI();
                    so.FindProperty("laserGaugeSlider").objectReferenceValue = gaugeSlider;
                }

                if (selectedChampion == Champion.MainDealer_Bow)
                {
                    Slider chargeSlider = CreateGaugeSliderUI();
                    so.FindProperty("bowChargeSlider").objectReferenceValue = chargeSlider;
                }

                if (selectedChampion == Champion.SubDealer_SniperSawedOff)
                {
                    GameObject tracerGO = new GameObject("Tracer");
                    tracerGO.transform.SetParent(player.transform);
                    var tracerLine = tracerGO.AddComponent<LineRenderer>();
                    tracerLine.startWidth = 0.03f;
                    tracerLine.endWidth = 0.03f;
                    tracerLine.material = new Material(Shader.Find("Sprites/Default"));
                    tracerLine.startColor = Color.white;
                    tracerLine.endColor = Color.white;
                    var tracer = tracerGO.AddComponent<TracerVisual>();
                    so.FindProperty("tracerVisual").objectReferenceValue = tracer;

                    Image scope = CreateScopeOverlayUI();
                    so.FindProperty("scopeOverlay").objectReferenceValue = scope.gameObject;
                }
            }

            so.ApplyModifiedProperties();

            GameObject sparkPrefab = GetOrCreateSparkPrefab();
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            int weakpointLayer = LayerMask.NameToLayer("Weakpoint");
            if (weakpointLayer < 0)
                Debug.LogWarning("[WeaponTestSceneSetup] 'Weakpoint' 레이어가 없습니다. Edit > Project Settings > Tags and Layers에서 추가해주세요.");

            for (int i = 0; i < dummyCount; i++)
            {
                GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dummy.name = TestObjectPrefix + $"Dummy_{i}";
                dummy.transform.position = new Vector3((i - dummyCount / 2f) * 2f, 1f, 4f);
                if (enemyLayer >= 0) dummy.layer = enemyLayer;

                var td = dummy.AddComponent<TestDummy>();
                var tdSo = new SerializedObject(td);
                tdSo.FindProperty("hitEffectPrefab").objectReferenceValue = sparkPrefab;
                tdSo.ApplyModifiedProperties();

                GameObject weakpoint = new GameObject("Weakpoint");
                weakpoint.transform.SetParent(dummy.transform);
                weakpoint.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                var wpCollider = weakpoint.AddComponent<SphereCollider>();
                wpCollider.radius = 0.25f;
                if (weakpointLayer >= 0) weakpoint.layer = weakpointLayer;
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
                    d.resourceMax = 100f;
                    d.energyBallPrefab = GetOrCreateProjectilePrefab();
                    return d;
                }
                case Champion.MainDealer_Gun:
                {
                    var d = GetOrCreateAsset<GunDealerData>("Test_GunDealer");
                    d.type = WeaponType.MainDealer;
                    d.resourceMax = 30f;
                    d.projectilePrefab = GetOrCreateProjectilePrefab();
                    return d;
                }
                case Champion.MainDealer_Bow:
                {
                    var d = GetOrCreateAsset<BowData>("Test_Bow");
                    d.type = WeaponType.MainDealer;
                    d.arrowPrefab = GetOrCreateArrowPrefab();
                    d.defaultHitEffectPrefab = GetOrCreateSparkPrefab();
                    return d;
                }
                case Champion.SubDealer_MeleeSawedOff:
                {
                    var d = GetOrCreateAsset<MeleeSawedOffData>("Test_MeleeSawedOff");
                    d.type = WeaponType.SubDealer;
                    d.defaultHitEffectPrefab = GetOrCreateSparkPrefab();
                    d.bulletPrefab = GetOrCreateDumbBulletPrefab();
                    return d;
                }
                case Champion.SubDealer_SniperSawedOff:
                {
                    var d = GetOrCreateAsset<SniperSawedOffData>("Test_SniperSawedOff");
                    d.type = WeaponType.SubDealer;
                    d.defaultHitEffectPrefab = GetOrCreateSparkPrefab();
                    d.bulletPrefab = GetOrCreateDumbBulletPrefab();
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

        private VolumeProfile GetOrCreateHealVignetteProfile()
        {
            string path = $"{GeneratedFolder}/Test_HealVignetteProfile.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null) return existing;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.overrideState = true;
            vignette.color.overrideState = true;
            vignette.intensity.value = 0f;

            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private CinemachineCamera CreateTestCamera(GameObject aimOrigin)
        {
            GameObject camGO = new GameObject(TestObjectPrefix + "MainCamera");
            camGO.tag = "MainCamera";
            camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
            camGO.AddComponent<CinemachineBrain>();

            var cmCam = aimOrigin.AddComponent<CinemachineCamera>();
            aimOrigin.AddComponent<CinemachineImpulseListener>();
            return cmCam;
        }

        private Canvas CreateTestCanvas()
        {
            GameObject canvasGO = new GameObject(TestObjectPrefix + "Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private Text CreateAmmoUI()
        {
            var canvas = CreateTestCanvas();

            GameObject textGO = new GameObject("AmmoText");
            textGO.transform.SetParent(canvas.transform, false);
            var text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 32;
            text.alignment = TextAnchor.LowerRight;
            text.color = Color.white;

            var rect = textGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-30f, 30f);
            rect.sizeDelta = new Vector2(220f, 50f);

            return text;
        }

        private Slider CreateGaugeSliderUI()
        {
            var canvas = GameObject.Find(TestObjectPrefix + "Canvas")?.GetComponent<Canvas>() ?? CreateTestCanvas();

            GameObject sliderGO = new GameObject("GaugeSlider");
            sliderGO.transform.SetParent(canvas.transform, false);
            var rect = sliderGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 90f);
            rect.sizeDelta = new Vector2(300f, 20f);

            var bgSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            var fillSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            GameObject bgGO = new GameObject("Background");
            bgGO.transform.SetParent(sliderGO.transform, false);
            var bgImage = bgGO.AddComponent<Image>();
            bgImage.sprite = bgSprite;
            bgImage.type = Image.Type.Sliced;
            bgImage.color = new Color(0.15f, 0.15f, 0.15f);
            var bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            GameObject fillAreaGO = new GameObject("Fill Area");
            fillAreaGO.transform.SetParent(sliderGO.transform, false);
            var fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = Vector2.zero;

            GameObject fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fillImage = fillGO.AddComponent<Image>();
            fillImage.sprite = fillSprite;
            fillImage.type = Image.Type.Sliced;
            fillImage.color = new Color(0.3f, 0.7f, 1f);
            var fillRect = fillGO.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            var slider = sliderGO.AddComponent<Slider>();
            slider.targetGraphic = fillImage;
            slider.fillRect = fillRect;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.interactable = false;

            return slider;
        }

        private Image CreateScopeOverlayUI()
        {
            var canvas = GameObject.Find(TestObjectPrefix + "Canvas")?.GetComponent<Canvas>() ?? CreateTestCanvas();

            GameObject scopeGO = new GameObject("ScopeOverlay");
            scopeGO.transform.SetParent(canvas.transform, false);
            var image = scopeGO.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.15f); // placeholder - 나중에 실제 스코프 텍스처로 교체 권장

            var rect = scopeGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            scopeGO.SetActive(false);
            return image;
        }

        private Image CreateHitMarkerUI()
        {
            var canvas = GameObject.Find(TestObjectPrefix + "Canvas")?.GetComponent<Canvas>() ?? CreateTestCanvas();

            GameObject markerGO = new GameObject("HitMarker");
            markerGO.transform.SetParent(canvas.transform, false);
            var image = markerGO.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            image.color = Color.red;

            var rect = markerGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(24f, 24f);

            markerGO.SetActive(false);
            return image;
        }

        private GameObject GetOrCreateSparkPrefab()
        {
            string path = $"{GeneratedFolder}/Test_SparkPrefab.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject temp = new GameObject("Test_Spark");
            var ps = temp.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = 0.2f;
            main.startSpeed = 2f;
            main.startSize = 0.05f;
            main.startColor = Color.yellow;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private GameObject GetOrCreateArrowPrefab()
        {
            string path = $"{GeneratedFolder}/Test_ArrowPrefab.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            // 루트: 판정/물리 전용. 회전은 진행방향(LookRotation)을 그대로 따름.
            GameObject root = new GameObject("Test_Arrow");

            // 비주얼: 캡슐 기본 축(Y)을 로컬로 90도 틀어서 부모의 진행방향(Z)에 맞춤.
            // 부모 회전이 바뀌어도 이 로컬 보정은 안 깨짐 (LookRotation이 부모만 건드리니까).
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            visual.transform.localScale = new Vector3(0.05f, 0.4f, 0.05f);

            // 콜라이더는 촉(앞쪽 끝) 근처에만 작게
            var tipCollider = root.AddComponent<SphereCollider>();
            tipCollider.radius = 0.04f;
            tipCollider.center = new Vector3(0f, 0f, 0.38f);

            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = true;
            root.AddComponent<ArrowProjectile>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private GameObject GetOrCreateDumbBulletPrefab()
        {
            string path = $"{GeneratedFolder}/Test_DumbBulletPrefab.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            temp.name = "Test_DumbBullet";
            temp.transform.localScale = Vector3.one * 0.06f;
            Object.DestroyImmediate(temp.GetComponent<Collider>());
            temp.AddComponent<DumbBulletVisual>();

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
