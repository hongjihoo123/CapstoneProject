using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using FSM_Skill_Module_CastMotion = Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill.CastMotion;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // 메뉴 HJH > 애니 비교 > 라이플 8방향을 KM 몸에 굽기 + Gunner 연결
    //
    // 믹사모 라이플 8방향 클립을 KM Rusian 뼈대(Bone.0xx)용 일반(Generic) 클립으로 바꿔서 저장하고,
    // Gunner.controller 하체(Base) Run 에 꽂음. 상체(Upper) Run 은 원래 KM 8방향 클립 그대로라
    // 결과적으로 "다리/골반 = 믹사모, 상체/팔/총 = KM" 이 됨. 손가락/총 뼈는 KM 상체 레이어가, 꼬리는 원래 KM 클립 흔들림을 그대로 따라감.
    //
    // 방법: 믹사모 몸은 KM 메쉬를 똑같은 자세로 올려서 리깅한 거라 바인드 자세가 같음. 그래서 프레임마다
    //   KM 뼈 월드회전 = 믹사모 뼈 월드회전(t) * 믹사모 바인드⁻¹ * KM 바인드
    // 로 그대로 옮기면 됨 (휴머노이드 T포즈 맞추기 같은 거 필요 없음). 골반 위아래 움직임은 키 비율로 줄임.
    //
    // 처음 한 번 Gunner.controller 를 Gunner_Original.controller 로 복사해둠 (원래 KM 다리로 돌아가고 싶으면 그거 쓰면 됨)
    public static class MixamoRetargetBaker
    {
        private const string MixamoFolder = "Assets/Members/HJH/05.Data/Character/Gunner/Mixamo";
        private const string SkinFile = MixamoFolder + "/Pistol_Idle.fbx";
        private const string GunnerFolder = "Assets/Members/HJH/05.Data/Character/Gunner";
        private const string GunnerPrefab = GunnerFolder + "/Gunner_Rusian.prefab";
        private const string Controller = GunnerFolder + "/Gunner.controller";
        private const string OriginalController = GunnerFolder + "/Gunner_Original.controller";
        private const string OutFolder = GunnerFolder + "/Animations/MixamoRun";
        private const float Fps = 30f;

        // KM 뼈 -> 믹사모 뼈 (Blender 로 뼈 위치/계층 보고 정함)
        private static readonly (string km, string mixamo)[] Map =
        {
            ("Bone", "Hips"),
            ("Bone.002", "Spine1"), ("Bone.001", "Spine2"), ("Bone.003", "Neck"), ("Bone.005", "Head"),
            ("Bone.004", "RightArm"), ("Bone.006", "RightForeArm"), ("Bone.007", "RightHand"),
            ("Bone.027", "LeftArm"), ("Bone.028", "LeftForeArm"), ("Bone.029", "LeftHand"),
            ("Bone.023", "RightUpLeg"), ("Bone.024", "RightLeg"), ("Bone.025", "RightFoot"), ("Bone.026", "RightToeBase"),
            ("Bone.045", "LeftUpLeg"), ("Bone.046", "LeftLeg"), ("Bone.047", "LeftFoot"), ("Bone.048", "LeftToeBase"),
        };

        // 믹사모 클립 -> 블렌드트리 방향
        private static readonly (string file, Vector2 dir)[] Runs =
        {
            ("Rifle_RunForward", new Vector2(0f, 1f)),
            ("Rifle_RunForwardRight", new Vector2(1f, 1f)),
            ("Rifle_RunRight", new Vector2(1f, 0f)),
            ("Rifle_RunBackwardRight", new Vector2(1f, -1f)),
            ("Rifle_RunBackward", new Vector2(0f, -1f)),
            ("Rifle_RunBackwardLeft", new Vector2(-1f, -1f)),
            ("Rifle_RunLeft", new Vector2(-1f, 0f)),
            ("Rifle_RunForwardLeft", new Vector2(-1f, 1f)),
        };

        [MenuItem("HJH/애니 비교/라이플 8방향을 KM 몸에 굽기 + Gunner 연결")]
        public static void Run()
        {
            // 원래 컨트롤러 복사본 먼저 (꼬리 흔들림을 원래 KM 클립에서 가져와야 해서)
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(OriginalController) == null)
                AssetDatabase.CopyAsset(Controller, OriginalController);
            Dictionary<Vector2, AnimationClip> originals = OriginalRunClips(0);
            Dictionary<Vector2, AnimationClip> originalUpper = OriginalRunClips(1);

            var baked = new Dictionary<Vector2, AnimationClip>();
            GameObject mixamo = null, km = null, kmOriginal = null;
            try
            {
                mixamo = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SkinFile));
                km = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GunnerPrefab));
                kmOriginal = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GunnerPrefab));
                mixamo.hideFlags = km.hideFlags = kmOriginal.hideFlags = HideFlags.HideAndDontSave;
                mixamo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                km.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                kmOriginal.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                var rig = new Rig(mixamo, km, kmOriginal);
                if (!rig.Valid(out string why))
                {
                    Debug.LogError("[라이플 굽기] " + why);
                    return;
                }

                Directory.CreateDirectory(OutFolder);
                foreach ((string file, Vector2 dir) in Runs)
                {
                    AnimationClip source = AssetDatabase.LoadAllAssetsAtPath($"{MixamoFolder}/{file}.fbx").OfType<AnimationClip>()
                        .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                    if (source == null)
                    {
                        Debug.LogWarning($"[라이플 굽기] {file} 클립 없음");
                        continue;
                    }
                    string name = "Gunner_Run_" + file.Substring("Rifle_Run".Length);
                    baked[dir] = rig.Bake(source, $"{OutFolder}/{name}.anim", name, Closest(originals, dir));
                }
            }
            finally
            {
                if (mixamo != null) Object.DestroyImmediate(mixamo);
                if (km != null) Object.DestroyImmediate(km);
                if (kmOriginal != null) Object.DestroyImmediate(kmOriginal);
            }

            AssetDatabase.SaveAssets();
            int lower = Hook(baked, 0);
            // 상체는 원래 KM 8방향 클립 그대로 (예전에 섞어봤다가 떨리고 총 자세가 달라져서 되돌림)
            int up = Hook(originalUpper, 1);
            Debug.Log($"[라이플 굽기] 클립 {baked.Count}개 구움, Gunner.controller 하체 Run {lower}칸 / 상체 Run {up}칸 교체. 원래 건 {OriginalController}");
        }

        // ------------------------------------------------ 원소 스킬 시전 동작
        // Mixamo/Cast_<이름>.fbx (Gunner 몸 그대로 받은 것) → Animations/Cast/Gunner_Cast_<이름>.anim
        // → Gunner.controller 0번 레이어에 Cast_<이름> 상태 추가 → 손 뻗는 순간을 CastMotions 에 기록
        private const string CastOutFolder = GunnerFolder + "/Animations/Cast";
        private const string CastSetPath = "Assets/Members/HJH/05.Data/Resources/HJH/CastMotions.asset";
        private const float CastSpeed = 1.25f;   // Mixamo 동작은 느긋해서 조금 빠르게

        private static readonly (string file, FSM_Skill_Module_CastMotion motion)[] Casts =
        {
            ("Cast_Forward", FSM_Skill_Module_CastMotion.Forward),
            ("Cast_Sweep", FSM_Skill_Module_CastMotion.Sweep),
            ("Cast_Ground", FSM_Skill_Module_CastMotion.Ground),
            ("Cast_Sky", FSM_Skill_Module_CastMotion.Sky),
            ("Cast_Self", FSM_Skill_Module_CastMotion.Self),
        };

        [MenuItem("HJH/애니/원소 스킬 시전 모션 굽기 + Gunner 연결")]
        public static string BakeCasts()
        {
            // 새로 받은 믹사모 파일은 기본(Generic)으로 들어와서, 휴머노이드인 스킨(Pistol_Idle)에 틀면 안 움직임.
            // 예전 라이플 파일과 같은 가져오기 설정(휴머노이드 + 스킨 아바타 복사)으로 맞춤
            var reference = (ModelImporter)AssetImporter.GetAtPath($"{MixamoFolder}/Rifle_RunForward.fbx");
            foreach ((string file, _) in Casts)
            {
                var importer = AssetImporter.GetAtPath($"{MixamoFolder}/{file}.fbx") as ModelImporter;
                if (importer == null || reference == null)
                    continue;
                if (importer.animationType == reference.animationType && importer.sourceAvatar == reference.sourceAvatar)
                    continue;
                importer.animationType = reference.animationType;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = reference.sourceAvatar;
                importer.SaveAndReimport();
            }

            var baked = new List<(string state, FSM_Skill_Module_CastMotion motion, AnimationClip clip, float release)>();
            GameObject mixamo = null, km = null, kmOriginal = null;
            try
            {
                mixamo = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SkinFile));
                km = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GunnerPrefab));
                kmOriginal = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GunnerPrefab));
                mixamo.hideFlags = km.hideFlags = kmOriginal.hideFlags = HideFlags.HideAndDontSave;
                foreach (GameObject go in new[] { mixamo, km, kmOriginal })
                    go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                var rig = new Rig(mixamo, km, kmOriginal);
                if (!rig.Valid(out string why))
                    return "cast bake: " + why;

                Directory.CreateDirectory(CastOutFolder);
                foreach ((string file, FSM_Skill_Module_CastMotion motion) in Casts)
                {
                    AnimationClip source = AssetDatabase.LoadAllAssetsAtPath($"{MixamoFolder}/{file}.fbx").OfType<AnimationClip>()
                        .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                    if (source == null)
                        continue;
                    float release = ReleaseTime(mixamo, source);
                    string name = "Gunner_" + file;
                    AnimationClip clip = rig.Bake(source, $"{CastOutFolder}/{name}.anim", name, null, lockHips: false, loop: false);
                    baked.Add((file, motion, clip, release));
                }
            }
            finally
            {
                if (mixamo != null) Object.DestroyImmediate(mixamo);
                if (km != null) Object.DestroyImmediate(km);
                if (kmOriginal != null) Object.DestroyImmediate(kmOriginal);
            }
            AssetDatabase.SaveAssets();

            // 컨트롤러 0번 레이어에 상태 추가 (있으면 클립만 교체)
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState skillState = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Skill");
            int row = 0;
            foreach ((string state, _, AnimationClip clip, _) in baked)
            {
                AnimatorState s = machine.states.Select(x => x.state).FirstOrDefault(x => x.name == state)
                                  ?? machine.AddState(state, new Vector3(650f, 40f + 60f * row, 0f));
                s.motion = clip;
                s.speed = CastSpeed;
                s.writeDefaultValues = skillState == null || skillState.writeDefaultValues;
                row++;
            }
            EditorUtility.SetDirty(controller);

            // 손 뻗는 순간 기록 (상태 속도만큼 빨라진 실제 시간으로)
            var set = AssetDatabase.LoadAssetAtPath<Char.Visual.CastMotionSet>(CastSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<Char.Visual.CastMotionSet>();
                AssetDatabase.CreateAsset(set, CastSetPath);
            }
            set.entries = baked.Select(b => new Char.Visual.CastMotionSet.Entry
            {
                motion = b.motion,
                state = b.state,
                release = b.release,      // 상태 시간(클립 초) 기준: CrossFadeInFixedTime 오프셋이 이 단위
                length = b.clip.length,
                lead = 0.15f,
            }).ToArray();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();

            return "cast bake: " + string.Join(", ", baked.Select(b => $"{b.state} {b.clip.length:0.00}s release {b.release:0.00}s"));
        }

        // ------------------------------------------------ 맞는 모션 / 죽는 모션
        // Mixamo/Hit.fbx, Death.fbx (Gunner 몸 그대로 받은 것) → Animations/Gunner_Hit.anim, Gunner_Death.anim
        //  - Hit  → "Upper"(상체) 레이어. 상체만 움찔해서 뛰면서 맞아도 다리는 계속 뜀
        //  - Dead → 0번(전신) 레이어. 쓰러지고 마지막 자세로 누워 있음 (반복 안 함)
        // 코드는 CharacterAnimator 가 상태 이름(Hit / Dead) 있으면 알아서 틂
        private static readonly (string file, string state, string layer, float speed)[] Reactions =
        {
            ("Hit", "Hit", "Upper", 2f),        // 받은 피격 모션이 2.4초라 2배 → 툭 움찔하고 빨리 돌아옴
            ("Death", "Dead", "Base Layer", 1.3f),
        };

        [MenuItem("HJH/애니/피격·사망 모션 굽기 + Gunner 연결")]
        public static void BakeReactions()
        {
            var reference = (ModelImporter)AssetImporter.GetAtPath($"{MixamoFolder}/Rifle_RunForward.fbx");
            var sources = new List<(AnimationClip clip, string file, string state, string layer, float speed)>();
            foreach ((string file, string state, string layer, float speed) in Reactions)
            {
                string fbx = $"{MixamoFolder}/{file}.fbx";
                if (AssetImporter.GetAtPath(fbx) is not ModelImporter importer || reference == null)
                {
                    Debug.LogWarning($"[피격 굽기] {fbx} 없음 → 건너뜀");
                    continue;
                }
                // 새로 받은 파일은 Generic 으로 들어와서 휴머노이드 스킨에 안 먹음 → 예전 파일이랑 같은 설정으로
                if (importer.animationType != reference.animationType || importer.sourceAvatar != reference.sourceAvatar)
                {
                    importer.animationType = reference.animationType;
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    importer.sourceAvatar = reference.sourceAvatar;
                    importer.SaveAndReimport();
                }
                AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (source != null)
                    sources.Add((source, file, state, layer, speed));
            }

            var baked = new List<(AnimationClip clip, string state, string layer, float speed)>();
            GameObject mixamo = null, km = null, kmOriginal = null;
            try
            {
                mixamo = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SkinFile));
                km = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GunnerPrefab));
                kmOriginal = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GunnerPrefab));
                mixamo.hideFlags = km.hideFlags = kmOriginal.hideFlags = HideFlags.HideAndDontSave;
                foreach (GameObject go in new[] { mixamo, km, kmOriginal })
                    go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                var rig = new Rig(mixamo, km, kmOriginal);
                if (!rig.Valid(out string why))
                {
                    Debug.LogError("[피격 굽기] " + why);
                    return;
                }
                foreach ((AnimationClip source, string file, string state, string layer, float speed) in sources)
                {
                    string name = "Gunner_" + file;
                    AnimationClip clip = rig.Bake(source, $"{GunnerFolder}/Animations/{name}.anim", name, null, lockHips: false, loop: false);
                    baked.Add((clip, state, layer, speed));
                }
            }
            finally
            {
                if (mixamo != null) Object.DestroyImmediate(mixamo);
                if (km != null) Object.DestroyImmediate(km);
                if (kmOriginal != null) Object.DestroyImmediate(kmOriginal);
            }
            AssetDatabase.SaveAssets();

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            var log = new List<string>();
            int row = 0;
            foreach ((AnimationClip clip, string state, string layerName, float speed) in baked)
            {
                int layer = System.Array.FindIndex(controller.layers, l => l.name == layerName);
                if (layer < 0)
                    layer = 0;
                AnimatorStateMachine machine = controller.layers[layer].stateMachine;
                AnimatorState idle = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle");
                AnimatorState s = machine.states.Select(x => x.state).FirstOrDefault(x => x.name == state)
                                  ?? machine.AddState(state, new Vector3(650f, -60f - 60f * row++, 0f));
                s.motion = clip;
                s.speed = speed;
                s.writeDefaultValues = idle == null || idle.writeDefaultValues;
                log.Add($"{state} {clip.length / speed:0.00}s ({controller.layers[layer].name})");
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log("[피격 굽기] Gunner.controller ← " + (log.Count > 0 ? string.Join(", ", log) : "구운 거 없음"));
        }

        // 손을 뻗는 순간 = 양손 중 골반 기준 손 속도가 가장 빠른 프레임 (클립 앞 85% 안에서)
        private static float ReleaseTime(GameObject mixamo, AnimationClip clip)
        {
            Transform[] all = mixamo.GetComponentsInChildren<Transform>();
            Transform Find(string n) => all.FirstOrDefault(t => t.name == "mixamorig:" + n || t.name.EndsWith(":" + n));
            Transform hips = Find("Hips"), right = Find("RightHand"), left = Find("LeftHand");
            var animator = mixamo.GetComponent<Animator>();
            if (hips == null || right == null || left == null || animator == null)
                return 0.3f;

            bool started = !AnimationMode.InAnimationMode();
            if (started)
                AnimationMode.StartAnimationMode();
            float best = 0f, bestTime = clip.length * 0.3f;
            Vector3 lastR = Vector3.zero, lastL = Vector3.zero;
            int frames = Mathf.Max(2, Mathf.RoundToInt(clip.length * Fps));
            for (int f = 0; f <= frames; f++)
            {
                float t = clip.length * f / frames;
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(animator.gameObject, clip, t);
                AnimationMode.EndSampling();
                Vector3 r = right.position - hips.position, l = left.position - hips.position;
                if (f > 0 && t <= clip.length * 0.85f)
                {
                    float speed = Mathf.Max((r - lastR).magnitude, (l - lastL).magnitude);
                    if (speed > best)
                    {
                        best = speed;
                        bestTime = t;
                    }
                }
                lastR = r;
                lastL = l;
            }
            if (started)
                AnimationMode.StopAnimationMode();
            return bestTime;
        }

        // Gunner.controller 의 layer 번 레이어 Run 블렌드트리 8방향 클립을 바꿈 (방향은 위치 각도로 맞춤). 바꾼 칸 수 돌려줌
        private static int Hook(Dictionary<Vector2, AnimationClip> baked, int layer)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            AnimatorState run = controller.layers[layer].stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Run");
            if (run?.motion is not BlendTree tree)
            {
                Debug.LogError($"[라이플 굽기] Gunner.controller {layer}번 레이어에 Run 블렌드트리 없음");
                return 0;
            }

            ChildMotion[] children = tree.children;
            int replaced = 0;
            for (int i = 0; i < children.Length; i++)
            {
                float angle = Mathf.Atan2(children[i].position.x, children[i].position.y);
                AnimationClip best = null;
                float bestDiff = float.MaxValue;
                foreach (KeyValuePair<Vector2, AnimationClip> pair in baked)
                {
                    float diff = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, Mathf.Atan2(pair.Key.x, pair.Key.y) * Mathf.Rad2Deg));
                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        best = pair.Value;
                    }
                }
                if (best != null && bestDiff < 10f)
                {
                    children[i].motion = best;
                    children[i].timeScale = 1f;
                    replaced++;
                }
            }
            tree.children = children;
            EditorUtility.SetDirty(tree);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return replaced;
        }

        // 확인용: 원래 KM 앞으로 뛰기 vs 구운 앞으로 뛰기를 KM 몸에 틀어서 꼬리 끝/발 위치를 로그로 비교
        [MenuItem("HJH/애니 비교/구운 클립 숫자로 확인 (로그)")]
        public static void Check()
        {
            AnimationClip original = Closest(OriginalRunClips(0), new Vector2(0f, 1f));
            var baked = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{OutFolder}/Gunner_Run_Forward.anim");
            if (original == null || baked == null)
            {
                Debug.LogError("[라이플 확인] 클립 없음");
                return;
            }
            AnimationClip mixamo = AssetDatabase.LoadAllAssetsAtPath($"{MixamoFolder}/Rifle_RunForward.fbx").OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            foreach (float n in new[] { 0f, 0.25f, 0.5f, 0.75f })
            {
                Debug.Log($"[라이플 확인] {n:0.00}  원래KM: {Probe(GunnerPrefab, original, n, "Bone", "Bone.025", "Bone.047")}" +
                          $"  |  구운거: {Probe(GunnerPrefab, baked, n, "Bone", "Bone.025", "Bone.047")}" +
                          $"  |  믹사모: {Probe(SkinFile, mixamo, n, "mixamorig:Hips", "mixamorig:RightFoot", "mixamorig:LeftFoot")}");
            }
        }

        // 프리팹을 하나 만들어서 클립을 그 시점으로 틀고 골반/발 위치를 글로
        private static string Probe(string prefabPath, AnimationClip clip, float normalized, string hips, string right, string left)
        {
            if (clip == null)
                return "(클립 없음)";
            var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            go.hideFlags = HideFlags.HideAndDontSave;
            bool started = !AnimationMode.InAnimationMode();
            try
            {
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Animator animator = go.GetComponentInChildren<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (started)
                    AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(animator.gameObject, clip, normalized * clip.length);
                AnimationMode.EndSampling();
                Transform[] all = animator.GetComponentsInChildren<Transform>();
                Vector3 P(string n) => all.FirstOrDefault(t => t.name == n)?.position ?? Vector3.zero;
                return $"골반{P(hips):F2} 오른발{P(right):F2} 왼발{P(left):F2}";
            }
            finally
            {
                if (started)
                    AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(go);
            }
        }

        // 원래 KM 컨트롤러 하체 Run 블렌드트리의 방향별 클립
        private static Dictionary<Vector2, AnimationClip> OriginalRunClips(int layer)
        {
            var result = new Dictionary<Vector2, AnimationClip>();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(OriginalController);
            AnimatorState run = controller != null
                ? controller.layers[layer].stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Run")
                : null;
            if (run?.motion is BlendTree tree)
            {
                foreach (ChildMotion child in tree.children)
                {
                    if (child.motion is AnimationClip clip)
                        result[child.position] = clip;
                }
            }
            return result;
        }

        private static AnimationClip Closest(Dictionary<Vector2, AnimationClip> clips, Vector2 dir)
        {
            AnimationClip best = null;
            float bestDiff = float.MaxValue;
            float want = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            foreach (KeyValuePair<Vector2, AnimationClip> pair in clips)
            {
                float diff = Mathf.Abs(Mathf.DeltaAngle(want, Mathf.Atan2(pair.Key.x, pair.Key.y) * Mathf.Rad2Deg));
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    best = pair.Value;
                }
            }
            return bestDiff < 10f ? best : null;
        }

        // 두 몸의 뼈 대응 + 바인드 자세
        private sealed class Rig
        {
            private readonly Animator _mixAnimator;
            private readonly Transform _kmRoot;
            private readonly List<(Transform km, Transform mix, Quaternion offset)> _pairs = new();
            private readonly Transform[] _kmBones;
            private readonly Dictionary<Transform, (Vector3 pos, Quaternion rot)> _kmBindLocal = new();
            private Transform _kmHips, _mixHips;
            private Vector3 _kmHipsBind, _mixHipsBind;
            private Quaternion _kmHipsBindRot = Quaternion.identity;
            private float _ratio = 1f;
            private string _error;
            private GameObject _kmOriginal;
            private readonly List<(Transform km, Transform orig, bool root)> _follow = new();

            public Rig(GameObject mixamo, GameObject km, GameObject kmOriginal)
            {
                _mixAnimator = mixamo.GetComponent<Animator>();
                Animator kmAnimator = km.GetComponentInChildren<Animator>();
                _kmRoot = kmAnimator != null ? kmAnimator.transform : km.transform;

                SkinnedMeshRenderer kmSkin = km.GetComponentInChildren<SkinnedMeshRenderer>();
                SkinnedMeshRenderer mixSkin = mixamo.GetComponentInChildren<SkinnedMeshRenderer>();
                if (_mixAnimator == null || kmSkin == null || mixSkin == null)
                {
                    _error = "애니메이터/스킨 메쉬 못 찾음";
                    return;
                }

                Dictionary<Transform, Matrix4x4> kmBind = BindWorld(kmSkin);
                Dictionary<Transform, Matrix4x4> mixBind = BindWorld(mixSkin);
                _kmBones = kmSkin.bones.Where(b => b != null).ToArray();

                // KM 을 바인드 자세로 돌려놓고 로컬값 기억 (매 프레임 여기서 시작함)
                foreach (Transform bone in _kmBones.OrderBy(Depth))
                {
                    if (kmBind.TryGetValue(bone, out Matrix4x4 m))
                        bone.SetPositionAndRotation(m.GetColumn(3), m.rotation);
                }
                foreach (Transform bone in _kmBones)
                    _kmBindLocal[bone] = (bone.localPosition, bone.localRotation);

                foreach ((string kmName, string mixName) in Map)
                {
                    Transform k = _kmBones.FirstOrDefault(b => b.name == kmName);
                    Transform x = mixBind.Keys.FirstOrDefault(b => b.name == "mixamorig:" + mixName || b.name.EndsWith(":" + mixName) || b.name == mixName);
                    if (k == null || x == null || !kmBind.ContainsKey(k))
                    {
                        _error = $"뼈 대응 못 찾음: {kmName} / {mixName}";
                        return;
                    }
                    _pairs.Add((k, x, Quaternion.Inverse(mixBind[x].rotation) * kmBind[k].rotation));
                    if (mixName == "Hips")
                    {
                        _kmHips = k;
                        _mixHips = x;
                        _kmHipsBind = kmBind[k].GetColumn(3);
                        _kmHipsBindRot = kmBind[k].rotation;
                        _mixHipsBind = mixBind[x].GetColumn(3);
                    }
                }
                _pairs.Sort((a, b) => Depth(a.km).CompareTo(Depth(b.km)));
                // 바닥(0) 기준 골반 높이 비율로 위아래 흔들림 크기 맞춤
                if (_mixHipsBind.y > 0.01f)
                    _ratio = _kmHipsBind.y / _mixHipsBind.y;
                FindFollowBones(kmOriginal);
            }

            // 꼬리처럼 골반에 바로 붙었는데 믹사모에 없는 가지: (내 뼈, 원래 KM 복사본의 같은 뼈, 가지 뿌리인지)
            private void FindFollowBones(GameObject kmOriginal)
            {
                Animator origAnimator = kmOriginal.GetComponentInChildren<Animator>();
                Transform origRoot = origAnimator != null ? origAnimator.transform : kmOriginal.transform;
                _kmOriginal = origRoot.gameObject; // 클립 경로가 애니메이터 기준이라 거기서 틀어야 함
                var mapped = new HashSet<Transform>(_pairs.Select(p => p.km));
                foreach (Transform child in _kmHips)
                {
                    if (mapped.Contains(child))
                        continue;
                    foreach (Transform bone in child.GetComponentsInChildren<Transform>())
                    {
                        Transform twin = origRoot.Find(AnimationUtility.CalculateTransformPath(bone, _kmRoot));
                        if (twin != null)
                            _follow.Add((bone, twin, bone == child));
                    }
                }
                _follow.Sort((a, b) => Depth(a.km).CompareTo(Depth(b.km)));
            }

            private static AnimationCurve Flat(AnimationCurve curve)
            {
                if (curve.length == 0)
                    return curve;
                float sum = 0f;
                foreach (Keyframe key in curve.keys)
                    sum += key.value;
                float mean = sum / curve.length;
                return AnimationCurve.Constant(0f, curve.keys[curve.length - 1].time, mean);
            }

            public bool Valid(out string why)
            {
                why = _error;
                return _error == null;
            }

            private static int Depth(Transform t)
            {
                int d = 0;
                for (Transform p = t.parent; p != null; p = p.parent) d++;
                return d;
            }

            // 바인드 자세 월드 행렬 (bindpose 는 메쉬 기준이라 렌더러 행렬 곱함)
            private static Dictionary<Transform, Matrix4x4> BindWorld(SkinnedMeshRenderer skin)
            {
                var result = new Dictionary<Transform, Matrix4x4>();
                Matrix4x4[] binds = skin.sharedMesh.bindposes;
                Transform[] bones = skin.bones;
                for (int i = 0; i < bones.Length && i < binds.Length; i++)
                {
                    if (bones[i] != null)
                        result[bones[i]] = skin.transform.localToWorldMatrix * binds[i].inverse;
                }
                return result;
            }

            // lockHips: 달리기는 골반 회전 고정(상체 레이어가 위에 얹힘), 시전 같은 전신 동작은 믹사모 골반 그대로
            public AnimationClip Bake(AnimationClip source, string path, string name, AnimationClip original, bool lockHips = true, bool loop = true)
            {
                _mixAnimator.applyRootMotion = false;
                _mixAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (_kmOriginal != null && _kmOriginal.TryGetComponent(out Animator originalAnimator))
                {
                    originalAnimator.applyRootMotion = false;
                    originalAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // KM 프리팹은 안 보이면 안 움직이는 설정이라
                }

                // 클립 틀기는 AnimationMode(타임라인이 쓰는 방식)로 함.
                // PlayableGraph.SetTime + Evaluate 는 에디터에서 시간이 안 먹어서 매 프레임 같은 자세가 나왔음 (다리 멈춤 버그)
                bool startedMode = !AnimationMode.InAnimationMode();
                if (startedMode)
                    AnimationMode.StartAnimationMode();
                bool followTail = original != null && _kmOriginal != null && _follow.Count > 0;

                // 골반 회전은 고정 (원래 KM 클립 첫 프레임 값). 다리는 월드 방향으로 넣어서 그대로 뛰고,
                // 상체(KM 클립)가 흔들리는 골반 위에 얹혀 떨리던 걸 막음. 위아래 움직임만 믹사모 거 씀
                Quaternion hipsLock = _kmHipsBindRot;
                if (original != null && _kmOriginal != null)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(_kmOriginal, original, 0f);
                    AnimationMode.EndSampling();
                    Transform twin = _kmOriginal.transform.Find(AnimationUtility.CalculateTransformPath(_kmHips, _kmRoot));
                    if (twin != null)
                        hipsLock = twin.rotation;
                }

                int frames = Mathf.Max(2, Mathf.RoundToInt(source.length * Fps) + 1);
                var rot = new Dictionary<Transform, AnimationCurve[]>();
                foreach (Transform bone in _kmBones)
                    rot[bone] = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                var hipPos = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                var last = new Dictionary<Transform, Quaternion>();

                for (int f = 0; f < frames; f++)
                {
                    float t = Mathf.Min(source.length, f / Fps);
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(_mixAnimator.gameObject, source, t);
                    if (followTail)
                        AnimationMode.SampleAnimationClip(_kmOriginal, original, source.length > 0f ? t / source.length * original.length : 0f);
                    AnimationMode.EndSampling();

                    foreach (Transform bone in _kmBones)
                    {
                        (Vector3 pos, Quaternion r) = _kmBindLocal[bone];
                        bone.localPosition = pos;
                        bone.localRotation = r;
                    }
                    foreach ((Transform k, Transform x, Quaternion offset) in _pairs)
                        k.rotation = k == _kmHips && lockHips ? hipsLock : x.rotation * offset;
                    _kmHips.position = _kmHipsBind + (_mixHips.position - _mixHipsBind) * _ratio;

                    // 꼬리: 원래 KM 클립을 같은 진행률로 틀어놓고 꼬리 뿌리는 월드 방향 그대로, 나머지 마디는 로컬 그대로
                    // (골반 회전이 KM 이랑 달라서 로컬만 복사하면 꼬리가 옆으로 뻗음)
                    if (followTail)
                    {
                        foreach ((Transform k, Transform o, bool root) in _follow)
                        {
                            if (root)
                                k.rotation = o.rotation;
                            else
                                k.localRotation = o.localRotation;
                        }
                    }

                    foreach (Transform bone in _kmBones)
                    {
                        Quaternion q = bone.localRotation;
                        if (last.TryGetValue(bone, out Quaternion prev) && Quaternion.Dot(prev, q) < 0f)
                            q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        last[bone] = q;
                        AnimationCurve[] c = rot[bone];
                        c[0].AddKey(t, q.x);
                        c[1].AddKey(t, q.y);
                        c[2].AddKey(t, q.z);
                        c[3].AddKey(t, q.w);
                    }
                    Vector3 hp = _kmHips.localPosition;
                    hipPos[0].AddKey(t, hp.x);
                    hipPos[1].AddKey(t, hp.y);
                    hipPos[2].AddKey(t, hp.z);
                }
                if (startedMode)
                    AnimationMode.StopAnimationMode();

                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                bool isNew = clip == null;
                if (isNew)
                    clip = new AnimationClip();
                clip.ClearCurves();
                clip.name = name;
                clip.frameRate = Fps;
                // 대응되는 뼈 + 꼬리만 씀. 나머지는 안 써야 프리팹 기본 자세/상체 레이어 값이 그대로 나옴
                var mapped = new HashSet<Transform>(_pairs.Select(p => p.km));
                if (original != null)
                    mapped.UnionWith(_follow.Select(f => f.km));
                foreach (Transform bone in _kmBones)
                {
                    if (!mapped.Contains(bone))
                        continue;
                    string bonePath = AnimationUtility.CalculateTransformPath(bone, _kmRoot);
                    AnimationCurve[] c = rot[bone];
                    clip.SetCurve(bonePath, typeof(Transform), "localRotation.x", c[0]);
                    clip.SetCurve(bonePath, typeof(Transform), "localRotation.y", c[1]);
                    clip.SetCurve(bonePath, typeof(Transform), "localRotation.z", c[2]);
                    clip.SetCurve(bonePath, typeof(Transform), "localRotation.w", c[3]);
                }
                // 골반 좌우/앞뒤 흔들림은 평균값으로 고정 (상체+총이 같이 흔들려서). 위아래만 남김
                // (아마추어가 Blender 축이라 로컬 축 중 월드 위쪽에 해당하는 걸 찾아서 그것만 남김)
                Vector3 up = _kmHips.parent != null ? _kmHips.parent.InverseTransformDirection(Vector3.up) : Vector3.up;
                int vertical = Mathf.Abs(up.x) > Mathf.Abs(up.y) ? (Mathf.Abs(up.x) > Mathf.Abs(up.z) ? 0 : 2) : (Mathf.Abs(up.y) > Mathf.Abs(up.z) ? 1 : 2);
                for (int axis = 0; axis < 3; axis++)
                {
                    if (axis != vertical)
                        hipPos[axis] = Flat(hipPos[axis]);
                }
                string hipsPath = AnimationUtility.CalculateTransformPath(_kmHips, _kmRoot);
                clip.SetCurve(hipsPath, typeof(Transform), "localPosition.x", hipPos[0]);
                clip.SetCurve(hipsPath, typeof(Transform), "localPosition.y", hipPos[1]);
                clip.SetCurve(hipsPath, typeof(Transform), "localPosition.z", hipPos[2]);
                clip.EnsureQuaternionContinuity();

                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);

                if (isNew)
                    AssetDatabase.CreateAsset(clip, path);
                else
                    EditorUtility.SetDirty(clip);
                return clip;
            }
        }
    }
}
