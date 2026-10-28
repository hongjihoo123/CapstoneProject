using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // 메뉴 HJH > 애니 비교 > 믹사모 세팅 + 비교 씬 만들기
    //
    // Gunner/Mixamo 폴더의 믹사모 FBX 들을 휴머노이드로 읽고, 컨트롤러 2개 + 비교 씬 1개를 만듦.
    //   Pistol_Idle.fbx  : 몸(스킨) 들어있는 파일. 이걸로 아바타 만들고 나머지는 이 아바타 빌려씀
    //   Mixamo_Pistol    : 권총 팩. 진짜 방향은 앞/뒤/왼/오 4개뿐이고 대각선은 블렌드트리가 섞어서 만듦
    //   Mixamo_Rifle     : 라이플 8방향 팩. 대각선까지 전부 진짜 클립
    // 두 컨트롤러 다 상태 이름(Idle/Run)이랑 파라미터(MoveX/MoveY)가 KM 거랑 같아서 게임(CharacterAnimator)에도 바로 꽂힘.
    //
    // 비교 씬(01.Scene/AnimCompare.unity) : 현재 KM 캐릭터 / 믹사모 권총 / 믹사모 라이플 을 나란히 세우고
    // AnimCompareDriver 가 셋 다 똑같은 이동 입력을 넣음. 다시 누르면 씬이랑 컨트롤러는 새로 만듦.
    public static class MixamoCompareSetup
    {
        private const string Folder = "Assets/Members/HJH/05.Data/Character/Gunner/Mixamo";
        private const string SkinFile = Folder + "/Pistol_Idle.fbx";
        private const string KmMaterials = "Assets/Members/KM/Assets/Character/Rusian/Materials";
        private const string GunnerPrefab = "Assets/Members/HJH/05.Data/Character/Gunner/Gunner_Rusian.prefab";
        private const string ScenePath = "Assets/Members/HJH/01.Scene/AnimCompare.unity";

        [MenuItem("HJH/애니 비교/믹사모 세팅 + 비교 씬 만들기")]
        public static void Run()
        {
            // 플레이 중엔 씬을 못 만들어서, 플레이 끄고 에디터로 돌아오면 이어서 함
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.Log("[애니 비교] 플레이 중이라 끄고 이어서 함");
                // 플레이 끌 때 도메인 리로드되면 static 구독이 날아가서 SessionState 에 적어둠
                SessionState.SetBool(ResumeKey, true);
                EditorApplication.isPlaying = false;
                return;
            }
            if (!File.Exists(SkinFile))
            {
                Debug.LogError($"[애니 비교] {SkinFile} 없음. 믹사모 파일부터 넣어야 함");
                return;
            }

            Avatar avatar = ImportSkin();
            if (avatar == null || !avatar.isHuman)
            {
                Debug.LogError("[애니 비교] 믹사모 몸으로 휴머노이드 아바타를 못 만듦");
                return;
            }

            var clips = new Dictionary<string, AnimationClip>();
            foreach (string path in Directory.GetFiles(Folder, "*.fbx").Select(p => p.Replace('\\', '/')))
            {
                AnimationClip clip = ImportClip(path, avatar);
                if (clip != null)
                    clips[Path.GetFileNameWithoutExtension(path)] = clip;
            }

            // 권총 팩 옆걸음 2개는 믹사모에서 이름이 똑같이 "pistol strafe" 임.
            // Blender 로 발 움직임 재서 확인함: A = 왼쪽, B = 오른쪽
            AnimationClip strafeLeft = Get(clips, "Pistol_StrafeA");
            AnimationClip strafeRight = Get(clips, "Pistol_StrafeB");

            AnimatorController pistol = BuildController("Mixamo_Pistol", Get(clips, "Pistol_Idle"), new[]
            {
                (Get(clips, "Pistol_Run"), new Vector2(0f, 1f)),
                (Get(clips, "Pistol_RunBackward"), new Vector2(0f, -1f)),
                (strafeRight, new Vector2(1f, 0f)),
                (strafeLeft, new Vector2(-1f, 0f)),
            });

            const float d = 0.70710677f;
            AnimatorController rifle = BuildController("Mixamo_Rifle", Get(clips, "Rifle_Idle"), new[]
            {
                (Get(clips, "Rifle_RunForward"), new Vector2(0f, 1f)),
                (Get(clips, "Rifle_RunForwardRight"), new Vector2(d, d)),
                (Get(clips, "Rifle_RunRight"), new Vector2(1f, 0f)),
                (Get(clips, "Rifle_RunBackwardRight"), new Vector2(d, -d)),
                (Get(clips, "Rifle_RunBackward"), new Vector2(0f, -1f)),
                (Get(clips, "Rifle_RunBackwardLeft"), new Vector2(-d, -d)),
                (Get(clips, "Rifle_RunLeft"), new Vector2(-1f, 0f)),
                (Get(clips, "Rifle_RunForwardLeft"), new Vector2(-d, d)),
            });

            BuildScene(pistol, rifle);
            Debug.Log($"[애니 비교] 끝. 클립 {clips.Count}개, 씬 {ScenePath}");
        }

        // 확인용: 비교 씬의 세 캐릭터를 방향별로 멈춰 세워서 사진 한 장으로 뽑음 (Temp/FxBridge/AnimCompare.jpg)
        [MenuItem("HJH/애니 비교/확인용 사진 찍기")]
        public static void Snapshot()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !(scene.IsValid() && scene.isLoaded);
            if (opened)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                AnimCompareDriver driver = roots.Select(r => r.GetComponent<AnimCompareDriver>()).FirstOrDefault(d => d != null);
                Camera camera = roots.Select(r => r.GetComponent<Camera>()).FirstOrDefault(c => c != null);
                if (driver == null || camera == null)
                {
                    Debug.LogError("[애니 비교] 씬에 드라이버/카메라 없음");
                    return;
                }
                SerializedProperty list = new SerializedObject(driver).FindProperty("actors");
                var animators = new List<Animator>();
                var pivots = new List<Transform>();
                for (int i = 0; i < list.arraySize; i++)
                {
                    SerializedProperty e = list.GetArrayElementAtIndex(i);
                    animators.Add(e.FindPropertyRelative("animator").objectReferenceValue as Animator);
                    pivots.Add(e.FindPropertyRelative("root").objectReferenceValue as Transform);
                }

                (string name, Vector2 dir, float time)[] shots =
                {
                    ("idle", Vector2.zero, 0.3f), ("front", new Vector2(0f, 1f), 0.15f), ("front-right", new Vector2(0.7071f, 0.7071f), 0.15f),
                    ("right", new Vector2(1f, 0f), 0.15f), ("back", new Vector2(0f, -1f), 0.15f), ("back-left", new Vector2(-0.7071f, -0.7071f), 0.15f),
                };
                const int w = 640, h = 360;
                var target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                var read = new Texture2D(w, h, TextureFormat.RGB24, false);
                var sheet = new Texture2D(w * 2, h * 3, TextureFormat.RGB24, false);
                camera.targetTexture = target;
                camera.aspect = (float)w / h;

                for (int s = 0; s < shots.Length; s++)
                {
                    for (int i = 0; i < animators.Count; i++)
                    {
                        Animator a = animators[i];
                        if (a == null)
                            continue;
                        if (pivots[i] != null)
                            pivots[i].rotation = Quaternion.identity;
                        // 에디터에선 클립을 새로 구워도 애니메이터가 예전 걸 들고 있어서 컨트롤러를 다시 꽂음
                        RuntimeAnimatorController rac = a.runtimeAnimatorController;
                        a.runtimeAnimatorController = null;
                        a.runtimeAnimatorController = rac;
                        a.cullingMode = AnimatorCullingMode.AlwaysAnimate; // KM 프리팹은 안 보이면 안 움직이는 설정이라 사진에서 멈춰 있었음
                        a.Rebind();
                        a.SetFloat("MoveX", shots[s].dir.x);
                        a.SetFloat("MoveY", shots[s].dir.y);
                        string state = shots[s].dir == Vector2.zero ? "Idle" : "Run";
                        for (int layer = 0; layer < a.layerCount; layer++)
                        {
                            if (layer > 0)
                                a.SetLayerWeight(layer, 1f);
                            if (a.HasState(layer, Animator.StringToHash(state)))
                                a.Play(state, layer, 0f);
                        }
                        a.Update(0f);
                        a.Update(shots[s].time);
                    }

                    var request = new RenderPipeline.StandardRequest { destination = target };
                    if (RenderPipeline.SupportsRenderRequest(camera, request))
                        RenderPipeline.SubmitRenderRequest(camera, request);
                    else
                        camera.Render();
                    RenderTexture.active = target;
                    read.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    read.Apply();
                    RenderTexture.active = null;
                    sheet.SetPixels((s % 2) * w, (2 - s / 2) * h, w, h, read.GetPixels());
                }
                camera.targetTexture = null;
                camera.ResetAspect();
                sheet.Apply();
                Directory.CreateDirectory("Temp/FxBridge");
                File.WriteAllBytes("Temp/FxBridge/AnimCompare.jpg", sheet.EncodeToJPG(85));
                foreach (Animator a in animators)
                {
                    if (a != null)
                        a.Rebind();
                }
                Object.DestroyImmediate(sheet);
                Object.DestroyImmediate(read);
                target.Release();
                Debug.Log("[애니 비교] 사진 -> Temp/FxBridge/AnimCompare.jpg (idle, front / front-right, right / back, back-left)");
            }
            finally
            {
                if (opened)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private const string ResumeKey = "HJH.MixamoCompare.Resume";

        [InitializeOnLoadMethod]
        private static void HookResume()
        {
            EditorApplication.playModeStateChanged += ResumeAfterPlay;
            if (SessionState.GetBool(ResumeKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                ResumeAfterPlay(PlayModeStateChange.EnteredEditMode);
        }

        private static void ResumeAfterPlay(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(ResumeKey, false))
                return;
            SessionState.EraseBool(ResumeKey);
            EditorApplication.delayCall += Run;
        }

        private static AnimationClip Get(Dictionary<string, AnimationClip> clips, string key)
        {
            if (clips.TryGetValue(key, out AnimationClip clip))
                return clip;
            Debug.LogWarning($"[애니 비교] {key}.fbx 없음");
            return null;
        }

        // ---------- 임포트 ----------

        private static Avatar ImportSkin()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(SkinFile);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            SetClipOptions(importer, "Pistol_Idle");
            importer.SaveAndReimport();

            RemapMaterials(importer);
            return AssetDatabase.LoadAllAssetsAtPath(SkinFile).OfType<Avatar>().FirstOrDefault();
        }

        // 믹사모 몸은 텍스처 없이 올렸어서 KM 머티리얼을 이름으로 찾아 붙임 (M_Armer -> M_Arm 처럼 앞부분만 맞아도 됨)
        private static void RemapMaterials(ModelImporter importer)
        {
            Material[] km = AssetDatabase.FindAssets("t:Material", new[] { KmMaterials })
                .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(m => m != null)
                .ToArray();

            bool changed = false;
            foreach (Material embedded in AssetDatabase.LoadAllAssetsAtPath(SkinFile).OfType<Material>())
            {
                string name = embedded.name.ToLowerInvariant();
                Material match = km.FirstOrDefault(m => m.name.ToLowerInvariant() == name)
                                 ?? km.FirstOrDefault(m => name.StartsWith(m.name.ToLowerInvariant()) || m.name.ToLowerInvariant().StartsWith(name));
                if (match == null)
                    continue;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), match);
                changed = true;
            }
            if (changed)
                importer.SaveAndReimport();
        }

        private static AnimationClip ImportClip(string path, Avatar avatar)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                return null;

            string name = Path.GetFileNameWithoutExtension(path);
            if (path != SkinFile)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                SetClipOptions(importer, name);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        // 반복 재생. 회전/높이는 클립에 구워서(제자리), 앞뒤 이동은 안 구움 -> 루트모션 끄면 어차피 제자리고,
        // 옆걸음 방향 판별용으로 averageSpeed 를 읽을 수 있음
        private static void SetClipOptions(ModelImporter importer, string name)
        {
            ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = name;
                clip.loopTime = true;
                clip.loopPose = true;
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
                clip.heightFromFeet = false;
                clip.lockRootPositionXZ = false;
            }
            importer.clipAnimations = clips;
        }

        // ---------- 컨트롤러 ----------

        private static AnimatorController BuildController(string name, AnimationClip idle, (AnimationClip clip, Vector2 dir)[] run)
        {
            string path = $"{Folder}/{name}.controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idleState = machine.AddState("Idle", new Vector3(300f, 0f));
            idleState.motion = idle;
            machine.defaultState = idleState;

            AnimatorState runState = controller.CreateBlendTreeInController("Run", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveY";
            foreach ((AnimationClip clip, Vector2 dir) in run)
            {
                if (clip != null)
                    tree.AddChild(clip, dir);
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        // ---------- 씬 ----------

        private static void BuildScene(AnimatorController pistol, AnimatorController rifle)
        {
            // 비교 씬이 이미 열려 있으면 그 자리에서 새로 만듦 (같은 경로로 두 개 열려 있으면 저장이 안 됨)
            Scene existing = SceneManager.GetSceneByPath(ScenePath);
            bool onlyCompareOpen = existing.IsValid() && existing.isLoaded && SceneManager.sceneCount == 1;
            if (existing.IsValid() && existing.isLoaded && !onlyCompareOpen)
                EditorSceneManager.CloseScene(existing, true);

            // 아니면 지금 열린 씬은 안 건드리려고 옆에 새로 만들고 저장한 뒤 닫음
            Scene previous = onlyCompareOpen ? default : SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, onlyCompareOpen ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.6f);

            // 게임 카메라랑 비슷하게 위에서 내려봄
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.17f, 0.18f);
            cameraGo.transform.position = new Vector3(0f, 5.8f, -7f);
            cameraGo.transform.LookAt(new Vector3(0f, 0.9f, 0f));

            Renderer ground = MakeGround();

            GameObject gunnerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GunnerPrefab);
            GameObject skin = AssetDatabase.LoadAssetAtPath<GameObject>(SkinFile);

            var current = (GameObject)PrefabUtility.InstantiatePrefab(gunnerPrefab);
            current.name = "게임_Gunner";
            float height = Height(current);

            GameObject pistolGo = SpawnMixamo(skin, pistol, "믹사모_권총", height);
            GameObject rifleGo = SpawnMixamo(skin, rifle, "믹사모_라이플", height);

            var list0 = new List<AnimCompareDriver.Actor>();
            // 라이플 다리 굽기(MixamoRetargetBaker) 한 뒤면 원래 KM 컨트롤러 복사본이 있음 -> 원래 vs 지금 게임 둘 다 세움
            var original = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Members/HJH/05.Data/Character/Gunner/Gunner_Original.controller");
            if (original != null)
            {
                var originalGo = (GameObject)PrefabUtility.InstantiatePrefab(gunnerPrefab);
                originalGo.name = "KM_원래";
                originalGo.GetComponentInChildren<Animator>().runtimeAnimatorController = original;
                list0.Add(Actor(originalGo, "KM 원래", Vector3.zero));
                list0.Add(Actor(current, "게임 Gunner\n(다리 = 믹사모 라이플)", Vector3.zero));
            }
            else
            {
                list0.Add(Actor(current, "현재 (KM 애니)", Vector3.zero));
            }
            list0.Add(Actor(pistolGo, "믹사모 권총\n(4방향 + 사이 섞기)", Vector3.zero));
            list0.Add(Actor(rifleGo, "믹사모 라이플\n(진짜 8방향)", Vector3.zero));
            const float gap = 2.4f;
            for (int i = 0; i < list0.Count; i++)
                list0[i].root.position = new Vector3((i - (list0.Count - 1) * 0.5f) * gap, 0f, 0f);
            AnimCompareDriver.Actor[] actors = list0.ToArray();

            var driverGo = new GameObject("AnimCompare");
            var driver = driverGo.AddComponent<AnimCompareDriver>();
            var so = new SerializedObject(driver);
            SerializedProperty list = so.FindProperty("actors");
            list.arraySize = actors.Length;
            for (int i = 0; i < actors.Length; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("label").stringValue = actors[i].label;
                element.FindPropertyRelative("animator").objectReferenceValue = actors[i].animator;
                element.FindPropertyRelative("root").objectReferenceValue = actors[i].root;
            }
            so.FindProperty("ground").objectReferenceValue = ground;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (onlyCompareOpen)
                return;
            if (previous.IsValid())
                SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);

            // 저장 안 된 씬이 없으면 바로 열어줌
            bool dirty = Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty);
            if (!dirty && !Application.isPlaying)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static AnimCompareDriver.Actor Actor(GameObject go, string label, Vector3 position)
        {
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            Animator animator = go.GetComponentInChildren<Animator>();
            if (animator != null)
                animator.applyRootMotion = false;
            return new AnimCompareDriver.Actor { label = label, animator = animator, root = go.transform };
        }

        private static GameObject SpawnMixamo(GameObject skin, AnimatorController controller, string name, float height)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(skin);
            go.name = name;
            Animator animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(SkinFile).OfType<Avatar>().FirstOrDefault();

            // 현재 캐릭터랑 키 맞춤 (믹사모 쪽은 1.8m 로 올렸음)
            float mine = Height(go);
            if (height > 0.1f && mine > 0.1f)
                go.transform.localScale = Vector3.one * (height / mine);
            return go;
        }

        private static float Height(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return 0f;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
                bounds.Encapsulate(r.bounds);
            return bounds.size.y;
        }

        // 1m 격자 바닥 (AnimCompareDriver 가 텍스처를 흘려서 이동하는 것처럼 보이게 함)
        private static Renderer MakeGround()
        {
            string texturePath = Folder + "/Compare_Grid.asset";
            string materialPath = Folder + "/Compare_Ground.mat";

            var grid = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (grid == null)
            {
                grid = new Texture2D(64, 64, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    bool line = x < 2 || y < 2;
                    bool half = x == 32 || y == 32;
                    grid.SetPixel(x, y, line ? new Color(0.5f, 0.52f, 0.5f) : half ? new Color(0.33f, 0.35f, 0.34f) : new Color(0.27f, 0.29f, 0.28f));
                }
                grid.Apply();
                AssetDatabase.CreateAsset(grid, texturePath);
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            const float tiles = 40f; // 바닥 40m -> 1칸 1m
            material.mainTexture = grid;
            material.mainTextureScale = Vector2.one * tiles;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", grid);
                material.SetTextureScale("_BaseMap", Vector2.one * tiles);
            }
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(material);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = Vector3.one * (tiles / 10f);
            var renderer = ground.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }
    }
}
