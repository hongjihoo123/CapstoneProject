using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // 메뉴 HJH > 건슬링어 v2 > 세팅 (모델·애니·컨트롤러·프리팹 + 캐릭터 연결)
    //
    // Tripo 로 새로 뽑은 건슬링어(v2, 믹사모 리깅) 를 게임에 꽂는 도구.
    //  1. GunnerV2/Model/GunnerV2.fbx  -> 휴머노이드, 툰 재질(몸 = 새 텍스처, 총 = M_Gun_Toon)
    //  2. GunnerV2/Mixamo/*.fbx         -> 휴머노이드 클립 (모델 아바타 복사), 반복/자르기/애니 이벤트(Anim_Fire 등)
    //  3. GunnerV2.controller           -> 기존 Gunner.controller 와 같은 상태 이름 (CharacterAnimator 가 이름으로 틂)
    //                                       Base: Idle / Run(8방향) / Dash / Attack / Skill / WeaponSkill / Ultimate / PassiveShot / Hit / Dead / Cast_*
    //                                       Upper(상체 마스크): Idle / Run(권총 겨누고 뜀) / Attack / Hit
    //  4. GunnerV2.prefab               -> 모델 + Animator(항상 갱신)
    //  5. Char1_Gunner 의 모델/컨트롤러/크기/방향을 v2 로 바꿈 (예전 값은 GunnerV2/OldGunnerRefs.txt 에 적어둠, "되돌리기" 메뉴)
    // 컨트롤러가 아직 없으면 스크립트 컴파일 직후 한 번 자동으로 돎. (컨트롤러만 지우면 컴파일이 안 일어나서 안 돎 -> 메뉴로 직접 실행)
    [InitializeOnLoad]
    public static class GunnerV2Setup
    {
        private const string Root = "Assets/Members/HJH/05.Data/Character/GunnerV2";
        private const string ModelPath = Root + "/Model/GunnerV2.fbx";
        private const string GenericFolder = Root + "/Generic";
        private const string TexturePath = Root + "/Textures/GunnerV2_BaseColor.jpg";
        private const string BodyMatPath = Root + "/GunnerV2_Toon.mat";
        private const string MaskPath = Root + "/GunnerV2_Upper.mask";
        private const string ControllerPath = Root + "/GunnerV2.controller";
        private const string PrefabPath = Root + "/GunnerV2.prefab";
        private const string BackupPath = Root + "/OldGunnerRefs.txt";
        private const string ClothToon = "Assets/Members/HJH/05.Data/Character/ToonMaterials/M_Cloth_Toon.mat";
        private const string GunToon = "Assets/Members/HJH/05.Data/Character/ToonMaterials/M_Gun_Toon.mat";
        private const string OldCastFolder = "Assets/Members/HJH/05.Data/Character/Gunner/Mixamo";
        private const string CharacterAsset = "Assets/Members/HJH/05.Data/Character/Char1_Gunner.asset";
        private const string VersionKey = "HJH.GunnerV2Setup.Version";
        private const string SetupVersion = "v3.9";   // 클립 설정·이벤트 바꾸면 이 값을 올림 -> 다음 컴파일 때 자동으로 다시 세팅

        // 클립 설정: 이름, 시작/끝 프레임(-1 = 전체), 반복, 이벤트(프레임, 함수)
        private sealed class ClipSpec
        {
            public string File; public int First = -1, Last = -1; public bool Loop, Mirror;
            public (int frame, string fn)[] Events = System.Array.Empty<(int, string)>();
        }

        private static readonly ClipSpec[] Clips =
        {
            // v3 (Generic): 모든 클립을 Blender 에서 모델과 똑같은 뼈대로 구워둠 (Generic/GunnerV2_G_*.fbx, 프레임 0부터 -> 번호 +1 해서 적음)
            //   휴머노이드 변환은 이 캐릭터(팔 짧음 + 허리 비틀기)에서 팔이 내려가고 허리가 과하게 꺾여서 안 씀
            new() { File = "IdleBreath", Loop = true },
            new() { File = "FRun_F", Loop = true }, new() { File = "FRun_B", Loop = true },   // 여자 달리기 (다리 모으고 상체 고정)
            new() { File = "JogL", Loop = true }, new() { File = "JogR", Loop = true },
            new() { File = "C_AimIdle", Loop = true }, new() { File = "C_AimRun", Loop = true },
            new() { File = "C_CarryIdle", Loop = true }, new() { File = "C_CarryRun", Loop = true },   // 평소: 총 내리고 듦
            new() { File = "C_Attack", Events = new[] { (4, "Anim_Fire") } },   // 4프레임(0.1초): 총 내린 자세 -> 견착 전환(0.08초)이 끝난 뒤에 총알이 나가야 총구 위치가 맞음
            new() { File = "C_PassiveShot", Events = new[] { (5, "Anim_Fire") } },
            new() { File = "C_Dash", Events = new[] { (1, "PlaySkillSound") } },
            new() { File = "C_WeaponSkill", Events = new[] { (1, "PlaySkillSound") } },
            new() { File = "C_Ultimate" },
            new() { File = "Hit" }, new() { File = "Dead" },
            new() { File = "Cast_Forward" }, new() { File = "Cast_Sweep" }, new() { File = "Cast_Ground" },
            new() { File = "Cast_Sky" }, new() { File = "Cast_Self" },
        };

        static GunnerV2Setup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(ModelPath))
                    return;
                // 컨트롤러가 없거나, 있는데 동작 빠진 상태가 있으면(지난번에 덜 됨) 한 번 더
                var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                bool complete = existing != null && existing.layers.All(l => l.stateMachine.states.All(s => s.state.motion != null))
                                && EditorPrefs.GetString(VersionKey) == SetupVersion;   // 설정 바뀌면 버전 올려서 다시 돌게
                if (!complete)
                    Run();
            };
        }

        [MenuItem("HJH/건슬링어 v2/세팅 (모델·애니·컨트롤러·연결)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[GunnerV2] 플레이 중엔 안 함"); return; }
            var log = new List<string>();
            Avatar avatar = SetupModel(log);
            if (avatar == null) { Debug.LogError("[GunnerV2] 모델 아바타 없음 -> 중단"); return; }
            Dictionary<string, AnimationClip> clips = SetupClips(avatar, log);
            AvatarMask mask = BuildMask();
            AnimatorController controller = BuildController(clips, mask, log);
            GameObject prefab = BuildPrefab(log);
            ApplyToCharacter(prefab, controller, log);
            AssetDatabase.SaveAssets();
            Preview(prefab, controller, clips);
            EditorPrefs.SetString(VersionKey, SetupVersion);
            Debug.Log("[GunnerV2] 세팅 끝 " + SetupVersion + "\n" + string.Join("\n", log));
            GunnerV2Diag.Run();   // 진단 사진도 같이 (Temp/GunnerV2Diag)
        }

        // ---------- 1. 모델 ----------
        private static Avatar SetupModel(List<string> log)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.importNormals = ModelImporterNormals.Import;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.SaveAndReimport();

            Material body = BodyMaterial();
            Material gun = RifleMaterial(body);
            foreach (Material m in AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Material>())
            {
                Material target = m.name.ToLower().Contains("gun") ? gun : body;
                if (target != null)
                    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), target);
            }
            mi.SaveAndReimport();
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            log.Add($"모델: Generic 아바타={(avatar != null && avatar.isValid)}");
            return avatar;
        }

        // 새 돌격소총(Tripo) 전용 툰 재질: 몸 재질을 복사해서 소총 텍스처만 바꿈 (없으면 예전 M_Gun_Toon)
        private static Material RifleMaterial(Material body)
        {
            const string rifleTex = Root + "/Textures/GunnerV2_Rifle_BaseColor.png";
            const string rifleMat = Root + "/GunnerV2_Rifle_Toon.mat";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(rifleTex);
            if (tex == null) return AssetDatabase.LoadAssetAtPath<Material>(GunToon);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(rifleMat);
            if (mat == null)
            {
                mat = new Material(body);
                AssetDatabase.CreateAsset(mat, rifleMat);
            }
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material BodyMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(BodyMatPath);
            if (mat == null)
            {
                var src = AssetDatabase.LoadAssetAtPath<Material>(ClothToon);
                mat = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, BodyMatPath);
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- 2. 클립 ----------
        private static Dictionary<string, AnimationClip> SetupClips(Avatar avatar, List<string> log)
        {
            var result = new Dictionary<string, AnimationClip>();
            foreach (ClipSpec spec in Clips)
            {
                string path = $"{GenericFolder}/GunnerV2_G_{spec.File}.fbx";
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null) { log.Add("없음: " + path); continue; }
                // 모델과 같은 뼈대로 구운 파일 -> Generic + 모델 아바타 복사 (뼈 회전 그대로 재생)
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                mi.sourceAvatar = avatar;
                mi.importAnimation = true;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.clipAnimations = System.Array.Empty<ModelImporterClipAnimation>();
                mi.SaveAndReimport();
                mi = (ModelImporter)AssetImporter.GetAtPath(path);   // 다시 가져온 뒤엔 새로 받아야 테이크 정보가 보임

                ModelImporterClipAnimation c = mi.defaultClipAnimations.FirstOrDefault();
                if (c == null && mi.importedTakeInfos.Length > 0)
                {
                    TakeInfo take = mi.importedTakeInfos[0];
                    c = new ModelImporterClipAnimation
                    {
                        takeName = take.name,
                        firstFrame = Mathf.Round(take.startTime * take.sampleRate),
                        lastFrame = Mathf.Round(take.stopTime * take.sampleRate),
                    };
                }
                if (c == null) { log.Add("테이크 없음: " + path); continue; }
                c.name = "GunnerV2_" + spec.File;
                // spec 프레임은 Blender/Mixamo 번호(1부터) -> Unity 테이크 프레임(0부터)
                int first = spec.First >= 0 ? spec.First - 1 : (int)c.firstFrame;
                int last = spec.Last >= 0 ? spec.Last - 1 : (int)c.lastFrame;
                c.firstFrame = first; c.lastFrame = last;
                c.loopTime = spec.Loop; c.loopPose = spec.Loop; c.mirror = spec.Mirror;
                // 이동은 코드가 함 -> 루트 회전/높이/수평 이동은 몸에 구움
                c.lockRootRotation = true; c.keepOriginalOrientation = true;
                c.lockRootHeightY = true; c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true;
                float span = Mathf.Max(1, last - first);
                c.events = spec.Events.Select(e => new AnimationEvent
                {
                    // 임포터 이벤트 시간은 0~1 (클립 비율)
                    time = Mathf.Clamp01((e.frame - 1 - first) / span),
                    functionName = e.fn,
                }).ToArray();
                mi.clipAnimations = new[] { c };
                mi.SaveAndReimport();
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(a => !a.name.StartsWith("__preview"));
                if (clip != null) result[spec.File] = clip;
                log.Add($"클립 {spec.File}: {(clip != null ? clip.length.ToString("0.00") + "초" : "실패")}{(spec.Events.Length > 0 ? " 이벤트 " + string.Join(",", spec.Events.Select(e => e.fn)) : "")}");
                // 검증: 설정이 중간에 끊겨 비어 있거나 Loop 가 어긋나면 바로 경고 (예전에 FRun_F 가 이렇게 빠져 달리기가 안 이어졌음)
                var check = (ModelImporter)AssetImporter.GetAtPath(path);
                if (clip == null || check.clipAnimations.Length != 1 || check.clipAnimations[0].loopTime != spec.Loop)
                    Debug.LogWarning($"[GunnerV2] 클립 {spec.File} 설정 확인 필요 (Loop 기대값 {spec.Loop}). 메뉴로 다시 세팅하세요.");
            }
            return result;
        }

        // ---------- 3. 컨트롤러 ----------
        private static AvatarMask BuildMask()
        {
            // Generic 이라 뼈 경로로 마스크: 척추(Spine) 와 그 아래 전부(가슴·목·머리·양팔·총) = 상체, 나머지(골반·다리) = 하체
            if (File.Exists(MaskPath)) AssetDatabase.DeleteAsset(MaskPath);
            var mask = new AvatarMask();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            mask.AddTransformPath(model.transform, true);
            for (int i = 0; i < mask.transformCount; i++)
            {
                string p = mask.GetTransformPath(i);
                mask.SetTransformActive(i, p.Contains("mixamorig:Spine"));
            }
            AssetDatabase.CreateAsset(mask, MaskPath);
            return mask;
        }

        private static AnimationClip OldCast(string name)
        {
            string path = $"{OldCastFolder}/{name}.fbx";
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(a => !a.name.StartsWith("__preview"));
        }

        private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips, AvatarMask mask, List<string> log)
        {
            if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ac.AddParameter(new AnimatorControllerParameter { name = "AttackSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            ac.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            ac.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            AnimationClip C(string k) => clips.TryGetValue(k, out AnimationClip c) ? c : null;

            AnimatorStateMachine sm = ac.layers[0].stateMachine;
            AnimatorState Add(AnimatorStateMachine m, string name, Motion motion, float speed = 1f, bool attackSpeed = false)
            {
                AnimatorState s = m.AddState(name);
                s.motion = motion; s.speed = speed;
                if (attackSpeed) { s.speedParameter = "AttackSpeed"; s.speedParameterActive = true; }
                return s;
            }

            sm.defaultState = Add(sm, "Idle", C("C_CarryIdle"));
            BlendTree run = new BlendTree { name = "Run", blendType = BlendTreeType.FreeformDirectional2D, blendParameter = "MoveX", blendParameterY = "MoveY", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(run, ac);
            const float d = 0.7071f;
            foreach (var (k, x, y) in new[] { ("FRun_F", 0f, 1f), ("FRun_B", 0f, -1f), ("JogL", -1f, 0f), ("JogR", 1f, 0f) })
                if (C(k) != null) run.AddChild(C(k), new Vector2(x, y));
            Add(sm, "Run", run);
            Add(sm, "Dash", C("C_Dash"));
            Add(sm, "Attack", C("C_Attack"), 1f, true);
            Add(sm, "PassiveShot", C("C_PassiveShot"));
            Add(sm, "WeaponSkill", C("C_WeaponSkill"));
            Add(sm, "Ultimate", C("C_Ultimate"));
            Add(sm, "Hit", C("Hit"), 2f);
            Add(sm, "Dead", C("Dead"), 1.3f);
            Add(sm, "Skill", C("Cast_Forward"), 1.25f);
            foreach (string cast in new[] { "Cast_Forward", "Cast_Sweep", "Cast_Ground", "Cast_Sky", "Cast_Self" })
                Add(sm, cast, C(cast), 1.25f);

            // 상체 레이어: 뛰면서 쏘기 / 맞기
            ac.AddLayer("Upper");
            AnimatorControllerLayer[] layers = ac.layers;
            layers[1].avatarMask = mask;
            layers[1].defaultWeight = 1f;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            ac.layers = layers;
            AnimatorStateMachine up = ac.layers[1].stateMachine;
            // 평소엔 총 내린 자세, 쏠 때(Attack)만 견착 -> 상체 분리
            // v3.7: 몸통은 힘 뺀 Carry 자세(견착처럼 비틀지 않음). 총·팔은 GunHoldIK 가 덮어씀(몸 흔들림 따라 총이 딸려오고 양손이 총을 잡음)
            up.defaultState = Add(up, "Idle", C("C_CarryIdle"));
            Add(up, "Run", C("C_CarryRun"));
            // 재생은 안 함. GunHoldIK 가 견착 자세(기준 파지)를 읽으려고 컨트롤러에서 이 클립을 찾으니 넣어 둠 (빠지면 IK 가 통째로 꺼짐)
            Add(up, "AimRef", C("C_AimIdle"));
            Add(up, "Attack", C("C_Attack"), 1f, true);
            Add(up, "Hit", C("Hit"), 2f);
            EditorUtility.SetDirty(ac);
            log.Add($"컨트롤러: {ControllerPath} (Base {sm.states.Length}개, Upper {up.states.Length}개)");
            return ac;
        }

        // ---------- 4. 프리팹 ----------
        private static GameObject BuildPrefab(List<string> log)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = "GunnerV2";
            Animator anim = inst.GetComponentInChildren<Animator>();
            if (anim != null) { anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; anim.applyRootMotion = false; }
            // 몸 메시는 재질 슬롯 없이 들어옴(믹사모용으로 뺐음) -> 렌더러에 직접 툰 재질
            Material body = AssetDatabase.LoadAssetAtPath<Material>(BodyMatPath);
            Material gun = RifleMaterial(body);
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
            {
                Material m = r.name == "Gunner_Gun" ? gun : body;   // "GunnerV2_Body" 에도 gun 글자가 있어서 정확히 비교
                if (m != null) r.sharedMaterials = Enumerable.Repeat(m, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
            }
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath);
            Object.DestroyImmediate(inst);
            log.Add("프리팹: " + PrefabPath + (FindDeep(prefab.transform, "AimOrigin") != null ? " (AimOrigin 있음)" : " (AimOrigin 없음!)"));
            return prefab;
        }

        // ---------- 5. 캐릭터 데이터 ----------
        private static void ApplyToCharacter(GameObject prefab, AnimatorController controller, List<string> log)
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CharacterAsset);
            if (data == null) { log.Add("캐릭터 데이터 없음"); return; }
            var so = new SerializedObject(data);
            SerializedProperty pModel = so.FindProperty("model"), pCtrl = so.FindProperty("animatorController"),
                pScale = so.FindProperty("modelScale"), pRot = so.FindProperty("modelRotation");
            var oldModel = pModel.objectReferenceValue as GameObject;
            if (oldModel != prefab && !File.Exists(BackupPath))
                File.WriteAllText(BackupPath,
                    $"model={AssetDatabase.GetAssetPath(oldModel)}\ncontroller={AssetDatabase.GetAssetPath(pCtrl.objectReferenceValue)}\nscale={pScale.floatValue}\nrotation={pRot.vector3Value}\n");

            // 키는 예전 모델과 맞춤 (다시 돌려도 백업에 적힌 예전 모델 기준), 방향은 왼손이 -X 쪽이면 +Z 를 보는 것
            GameObject reference = oldModel != prefab ? oldModel : AssetDatabase.LoadAssetAtPath<GameObject>(ReadBackup("model"));
            float oldH = reference != null ? Height(reference) * (oldModel != prefab ? pScale.floatValue : ReadOldScale()) : 0f;
            float newH = Height(prefab);
            float scale = oldH > 0.01f && newH > 0.01f ? oldH / newH : 1f;
            bool facesForward = FacesForward(prefab);
            pModel.objectReferenceValue = prefab;
            pCtrl.objectReferenceValue = controller;
            pScale.floatValue = scale;
            pRot.vector3Value = facesForward ? Vector3.zero : new Vector3(0, 180, 0);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            log.Add($"Char1_Gunner 연결: 키 예전 {oldH:0.00} / 새 {newH:0.00} -> 크기 {scale:0.00}, 회전 {(facesForward ? 0 : 180)}");
        }

        private static string ReadBackup(string key)
        {
            if (!File.Exists(BackupPath)) return "";
            foreach (string line in File.ReadAllLines(BackupPath))
                if (line.StartsWith(key + "=")) return line.Substring(key.Length + 1);
            return "";
        }

        private static float ReadOldScale() => float.TryParse(ReadBackup("scale"), out float s) ? s : 1f;

        private static float Height(GameObject asset)
        {
            var inst = Object.Instantiate(asset);
            Renderer[] rs = inst.GetComponentsInChildren<Renderer>();
            float h = 0f;
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                h = b.size.y;
            }
            Object.DestroyImmediate(inst);
            return h;
        }

        private static bool FacesForward(GameObject asset)
        {
            var inst = Object.Instantiate(asset);
            bool ok = true;
            Transform l = FindDeep(inst.transform, "mixamorig:LeftHand"), r = FindDeep(inst.transform, "mixamorig:RightHand");
            if (l != null && r != null) ok = l.position.x < r.position.x;
            Object.DestroyImmediate(inst);
            return ok;
        }

        private static Transform FindDeep(Transform root, string n) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);

        // ---------- 확인용 사진 (Temp/GunnerV2_*.png) ----------
        private static void Preview(GameObject prefab, AnimatorController controller, Dictionary<string, AnimationClip> clips)
        {
            var go = Object.Instantiate(prefab);
            go.transform.position = new Vector3(0, -500, 0);
            var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CharacterAsset);
            var so = new SerializedObject(data);
            go.transform.localScale = Vector3.one * so.FindProperty("modelScale").floatValue;
            go.transform.rotation = Quaternion.Euler(so.FindProperty("modelRotation").vector3Value);
            var camGo = new GameObject("GunnerV2PreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.35f, 0.35f, 0.38f);
            var lightGo = new GameObject("GunnerV2PreviewLight"); var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);
            var rt = new RenderTexture(512, 512, 24);
            cam.targetTexture = rt;
            foreach (string k in new[] { "C_CarryIdle", "C_Attack", "C_Dash", "C_CarryRun" })
            {
                if (clips.TryGetValue(k, out AnimationClip clip)) clip.SampleAnimation(go, clip.length * 0.4f);
                Bounds b = new Bounds(go.transform.position, Vector3.zero);
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                cam.orthographicSize = b.extents.y * 1.15f;
                cam.transform.position = b.center + new Vector3(0.6f, 0.3f, 1f).normalized * 10f;
                cam.transform.LookAt(b.center);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(512, 512, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); tex.Apply();
                File.WriteAllBytes($"Temp/GunnerV2_{k}.png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            RenderTexture.active = null; cam.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo); Object.DestroyImmediate(lightGo); Object.DestroyImmediate(go);
        }

        [MenuItem("HJH/건슬링어 v2/되돌리기 (예전 KM 건슬링어)")]
        public static void Revert()
        {
            if (!File.Exists(BackupPath)) { Debug.LogWarning("[GunnerV2] 백업 없음"); return; }
            var map = File.ReadAllLines(BackupPath).Select(l => l.Split('=')).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1]);
            var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CharacterAsset);
            var so = new SerializedObject(data);
            so.FindProperty("model").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(map["model"]);
            so.FindProperty("animatorController").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(map["controller"]);
            so.FindProperty("modelScale").floatValue = float.Parse(map["scale"]);
            so.FindProperty("modelRotation").vector3Value = Vector3.zero;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[GunnerV2] 예전 건슬링어로 되돌림");
        }
    }
}
