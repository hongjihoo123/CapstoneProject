using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Editor
{
    // ComboBindStation 프리팹 연출 굽기: 코어/링 피벗 정리 + 루프 애니메이션 + 라이트 위치.
    // 속도·밝기 같은 값은 만들어진 Idle.anim / 라이트에서 직접 수정 (다시 굽기 해도 이미 정리된 구조는 재사용)
    public static class ComboBindStationBakeMenu
    {
        private const string PrefabPath = "Assets/Members/HJH/05.Data/Prefab/UI/ComboBindStation.prefab";
        private const string AnimDir = "Assets/Members/HJH/05.Data/Combo/BindStation";
        private const string ClipPath = AnimDir + "/ComboBindStation_Idle.anim";
        private const string ControllerPath = AnimDir + "/ComboBindStation.controller";
        private const float LoopSeconds = 12f;   // 클립 한 바퀴 길이. 속도는 Animator Speed 나 키로 조절

        // FBX 폴더 이름 앞부분으로 파츠 구분 (Tripo 생성 순서)
        private static readonly Dictionary<string, string> PartByGuidPrefix = new()
        {
            { "4ae70083", "RingA" },
            { "a637b1bb", "RingB" },
            { "9228e14b", "Core" },
            { "fe89fadf", "Base" },
        };

        [MenuItem("HJH/콤보 바인딩 상점/연출 굽기 (회전 애니 + 라이트 위치)")]
        public static void Bake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[ComboBindStation] 플레이 중에는 굽지 않습니다.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform visual = root.transform.Find("Visual");
                if (visual == null) { Debug.LogError("[ComboBindStation] Visual 자식이 없습니다."); return; }

                Dictionary<string, Transform> parts = FindParts(visual);
                if (parts.Count < 4)
                {
                    Debug.LogError($"[ComboBindStation] 파츠 4개를 못 찾음 (찾은 것: {string.Join(",", parts.Keys)})");
                    return;
                }

                // 구조 정리 전에 월드 기준 중심을 먼저 잡는다
                Bounds coreB = WorldBounds(parts["Core"]);
                Bounds baseB = WorldBounds(parts["Base"]);
                Vector3 center = coreB.center;

                Transform floating = GetOrCreate(visual, "Floating");
                floating.position = center;
                floating.rotation = Quaternion.identity;

                // 코어: Core 피벗(Y 회전) 아래에 모델
                Transform corePivot = Rewrap(floating, "Core", parts["Core"], center, Quaternion.identity);

                // 링: Tilt(고정, 링 축을 위로 맞춤) → Spin(Y 회전 애니) → 모델
                Transform ringA = RewrapRing(floating, "RingA", parts["RingA"], center);
                Transform ringB = RewrapRing(floating, "RingB", parts["RingB"], center);

                AnimationClip clip = BuildClip(floating.localPosition.y);
                AnimatorController controller = BuildController(clip);
                Animator anim = visual.GetComponent<Animator>();
                if (anim == null) anim = visual.gameObject.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                PlaceLights(root.transform, baseB, coreB);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ComboBindStation] 굽기 완료. 코어 중심 로컬 y={root.transform.InverseTransformPoint(center).y:F2}, " +
                          $"코어 상단 y={root.transform.InverseTransformPoint(new Vector3(center.x, coreB.max.y, center.z)).y:F2}, " +
                          $"받침대 상단 y={root.transform.InverseTransformPoint(new Vector3(center.x, baseB.max.y, center.z)).y:F2}, " +
                          $"받침대 반경≈{Mathf.Max(baseB.extents.x, baseB.extents.z):F2}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ── 파츠 찾기 (이미 Floating 안으로 옮겼어도 찾음) ─────────────
        private static Dictionary<string, Transform> FindParts(Transform visual)
        {
            var result = new Dictionary<string, Transform>();
            foreach (Transform t in visual.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                string src = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject));
                foreach (var kv in PartByGuidPrefix)
                    if (src.Contains("tripo_convert_" + kv.Key)) result[kv.Value] = t;
            }
            return result;
        }

        private static Bounds WorldBounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static Transform GetOrCreate(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null) return t;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // 피벗(center, rot) 만들고 모델을 월드 위치 유지한 채 그 아래로. 피벗 Transform 반환
        private static Transform Rewrap(Transform floating, string name, Transform model, Vector3 center, Quaternion rot)
        {
            Transform pivot = GetOrCreate(floating, name);
            pivot.SetPositionAndRotation(center, rot);
            pivot.localScale = Vector3.one;
            model.SetParent(pivot, true);
            model.name = "Model";
            return pivot;
        }

        private static Transform RewrapRing(Transform floating, string name, Transform model, Vector3 center)
        {
            Vector3 normal = RingNormal(model);
            Transform tilt = GetOrCreate(floating, name + "_Tilt");
            tilt.SetPositionAndRotation(center, Quaternion.FromToRotation(Vector3.up, normal));
            tilt.localScale = Vector3.one;
            Transform spin = GetOrCreate(tilt, "Spin");
            spin.localPosition = Vector3.zero;
            spin.localRotation = Quaternion.identity;
            spin.localScale = Vector3.one;
            model.SetParent(spin, true);
            model.name = "Model";
            return spin;
        }

        // 링의 법선: 메시 로컬 바운드에서 가장 얇은 축 → 월드 방향
        private static Vector3 RingNormal(Transform model)
        {
            var mf = model.GetComponentInChildren<MeshFilter>(true);
            if (mf == null || mf.sharedMesh == null) return Vector3.up;
            Vector3 s = mf.sharedMesh.bounds.size;
            Vector3 axis = (s.x <= s.y && s.x <= s.z) ? Vector3.right : (s.y <= s.z ? Vector3.up : Vector3.forward);
            Vector3 n = mf.transform.TransformDirection(axis).normalized;
            if (n.y < 0f) n = -n;
            return n;
        }

        // ── 애니메이션 ───────────────────────────────────────────
        private static AnimationClip BuildClip(float baseY)
        {
            EnsureFolder(AnimDir);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "ComboBindStation_Idle", frameRate = 30f };
                AssetDatabase.CreateAsset(clip, ClipPath);
            }
            clip.ClearCurves();

            // 둥실: 위아래 (12초에 3번)
            const float bob = 0.08f;
            var bobCurve = new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(1f, bob), new Keyframe(3f, -bob), new Keyframe(4f, 0f),
                new Keyframe(5f, bob), new Keyframe(7f, -bob), new Keyframe(8f, 0f),
                new Keyframe(9f, bob), new Keyframe(11f, -bob), new Keyframe(12f, 0f));
            for (int i = 0; i < bobCurve.length; i++) { AnimationUtility.SetKeyLeftTangentMode(bobCurve, i, AnimationUtility.TangentMode.Auto); AnimationUtility.SetKeyRightTangentMode(bobCurve, i, AnimationUtility.TangentMode.Auto); }
            var bobAbs = new AnimationCurve();
            foreach (Keyframe k in bobCurve.keys) bobAbs.AddKey(new Keyframe(k.time, baseY + k.value));
            for (int i = 0; i < bobAbs.length; i++) { AnimationUtility.SetKeyLeftTangentMode(bobAbs, i, AnimationUtility.TangentMode.Auto); AnimationUtility.SetKeyRightTangentMode(bobAbs, i, AnimationUtility.TangentMode.Auto); }
            clip.SetCurve("Floating", typeof(Transform), "localPosition.y", bobAbs);

            // 회전: 코어 느리게 1바퀴, 링A 2바퀴, 링B 반대로 3바퀴 (12초 기준)
            clip.SetCurve("Floating/Core", typeof(Transform), "localEulerAnglesRaw.y", Linear(0f, 360f));
            clip.SetCurve("Floating/RingA_Tilt/Spin", typeof(Transform), "localEulerAnglesRaw.y", Linear(0f, 720f));
            clip.SetCurve("Floating/RingB_Tilt/Spin", typeof(Transform), "localEulerAnglesRaw.y", Linear(0f, -1080f));

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationCurve Linear(float from, float to)
        {
            var c = new AnimationCurve(new Keyframe(0f, from), new Keyframe(LoopSeconds, to));
            for (int i = 0; i < c.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Linear);
            }
            return c;
        }

        private static AnimatorController BuildController(AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = controller.layers[0].stateMachine;
            AnimatorState state = null;
            foreach (var s in sm.states) if (s.state.name == "Idle") state = s.state;
            if (state == null) state = sm.AddState("Idle");
            state.motion = clip;
            sm.defaultState = state;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        // ── 라이트: 위치만 (색·세기·각도는 그대로) ────────────────
        private static void PlaceLights(Transform root, Bounds baseB, Bounds coreB)
        {
            Vector3 c = coreB.center;
            Light[] lights = root.GetComponentsInChildren<Light>(true);
            Light core = null, spot = null, ground = null;
            foreach (Light l in lights)
            {
                if (l.name == "Glow") core = l;
                else if (l.name == "Spot Light") spot = l;
                else if (l.name == "GroundGlow") ground = l;
            }

            // 코어 글로우: 코어 한가운데 (위아래 둥실은 작아서 고정)
            if (core != null) core.transform.position = c;

            // 위에서 내리쬐는 스포트: 코어 위쪽에서 받침대 윗면을 향해 수직으로
            if (spot != null)
            {
                spot.transform.position = new Vector3(c.x, coreB.max.y + 1.2f, c.z);
                spot.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            // 바닥 글로우: 받침대 윗면 바로 위, 주변 바닥·이끼에 푸른 빛 번짐
            if (ground == null && core != null)
            {
                var go = new GameObject("GroundGlow");
                go.transform.SetParent(core.transform.parent, false);
                ground = go.AddComponent<Light>();
                ground.type = LightType.Point;
                ground.color = core.color;
                ground.intensity = core.intensity * 0.6f;
                ground.range = 5f;
                ground.shadows = LightShadows.None;
            }
            if (ground != null)
                ground.transform.position = new Vector3(c.x, baseB.max.y + 0.15f, c.z);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
