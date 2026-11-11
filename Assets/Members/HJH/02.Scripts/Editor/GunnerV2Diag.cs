using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // 진단용: 건슬링어 v2 클립을 Unity 안에서 실제로 틀어서 위/옆 사진을 Temp/GunnerV2Diag/ 에 저장.
    //  - clip_* : 클립 하나만 (AnimationMode 샘플)
    //  - ctrl_* : 컨트롤러로 하체+상체 레이어 합친 상태 (Animator.Update)
    // 메뉴 HJH/건슬링어 v2/진단 사진. 컴파일 직후 한 번 자동 (Temp 표시 파일 기준)
    [InitializeOnLoad]
    public static class GunnerV2Diag
    {
        private const string Root = "Assets/Members/HJH/05.Data/Character/GunnerV2";
        private const string OutDir = "Temp/GunnerV2Diag";
        private const string Marker = OutDir + "/done_v1.txt";

        static GunnerV2Diag()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !File.Exists(Marker) && File.Exists(Root + "/GunnerV2.controller"))
                    Run();
            };
        }

        private static AnimationClip Clip(string file)
        {
            string path = $"{Root}/Generic/GunnerV2_G_{file}.fbx";
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(a => !a.name.StartsWith("__preview"));
        }

        [MenuItem("HJH/건슬링어 v2/진단 사진")]
        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/GunnerV2.prefab");
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Root + "/GunnerV2.controller");
            GameObject go = Object.Instantiate(prefab);
            go.transform.position = new Vector3(0, -800, 0);
            Animator anim = go.GetComponentInChildren<Animator>();

            var camGo = new GameObject("DiagCam"); var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.35f);
            var lightGo = new GameObject("DiagLight"); var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; lightGo.transform.rotation = Quaternion.Euler(60, -30, 0);
            var rt = new RenderTexture(384, 384, 24); cam.targetTexture = rt;
            var log = new System.Text.StringBuilder();

            void Shot(string name)
            {
                Bounds b = new Bounds(go.transform.position + Vector3.up * 0.5f, Vector3.one * 0.1f);
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.y, b.extents.z) * 1.1f;
                // top: camera above, model forward (+Z) shown pointing UP in the image; side: from the model's right (+X)
                foreach (var (tag, pos, up) in new[] { ("top", Vector3.up, Vector3.forward), ("side", Vector3.right, Vector3.up) })
                {
                    cam.transform.position = b.center + pos * 10f;
                    cam.transform.rotation = Quaternion.LookRotation(-pos, up);
                    cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(384, 384, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, 384, 384), 0, 0); tex.Apply();
                    File.WriteAllBytes($"{OutDir}/{name}_{tag}.png", tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                }
                Transform T(string n) => go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
                Transform l = T("mixamorig:LeftFoot"), lt = T("mixamorig:LeftToeBase"), rh = T("mixamorig:RightHand"), lh = T("mixamorig:LeftHand");
                Transform aim = T("AimOrigin");
                Vector3 foot = lt != null && l != null ? lt.position - l.position : Vector3.zero;
                Vector3 hands = lh.position - rh.position;
                Vector3 muzzle = aim != null ? aim.position - rh.position : Vector3.zero;
                log.AppendLine($"{name}: footDir {Yaw(foot):0} handsDir {Yaw(hands):0} muzzleDir {Yaw(muzzle):0} handGap {hands.magnitude:0.00}");
            }

            // 0. what's inside the clips (curve count, a few arm muscle values at t=0)
            foreach (string c in new[] { "C_AimIdle", "C_Attack", "RunStd_F" })
            {
                AnimationClip clip = Clip(c);
                if (clip == null) { log.AppendLine($"curves {c}: 없음"); continue; }
                EditorCurveBinding[] bs = AnimationUtility.GetCurveBindings(clip);
                string Val(string attr)
                {
                    EditorCurveBinding b = bs.FirstOrDefault(x => x.propertyName == attr);
                    if (b.propertyName == null) return "-";
                    AnimationCurve cv = AnimationUtility.GetEditorCurve(clip, b);
                    return cv != null ? cv.Evaluate(0f).ToString("0.00") : "-";
                }
                log.AppendLine($"curves {c}: n={bs.Length} human={clip.humanMotion} len={clip.length:0.00} RArmDownUp={Val("Right Arm Down-Up")} RForearmStretch={Val("Right Forearm Stretch")} LArmFrontBack={Val("Left Arm Front-Back")} SpineTwist={Val("Spine Twist Left-Right")} ChestTwist={Val("Chest Twist Left-Right")} first={string.Join(",", bs.Take(4).Select(x => x.path + ":" + x.propertyName))}");
            }

            // 1. single clips
            AnimationMode.StartAnimationMode();
            foreach (string c in new[] { "C_CarryIdle", "C_CarryRun", "FRun_F", "C_AimIdle", "C_Attack", "C_Dash", "C_WeaponSkill", "C_Ultimate" })
            {
                AnimationClip clip = Clip(c);
                if (clip == null) { log.AppendLine(c + ": 클립 없음"); continue; }
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(anim.gameObject, clip, clip.length * 0.45f);
                AnimationMode.EndSampling();
                Shot("clip_" + c);
            }
            AnimationMode.StopAnimationMode();

            // 2. controller (both layers)
            anim.runtimeAnimatorController = ctrl;
            anim.Rebind();
            foreach (var (state, mx, my) in new[] { ("Idle", 0f, 0f), ("Run", 0f, 1f), ("Run", 1f, 0f), ("Dash", 0f, 1f), ("WeaponSkill", 0f, 0f) })
            {
                anim.SetFloat("MoveX", mx); anim.SetFloat("MoveY", my);
                anim.Play(state, 0, 0f);
                if (state is "Idle" or "Run") anim.Play(state, 1, 0f);
                anim.SetLayerWeight(1, state is "Idle" or "Run" ? 1f : 0f);
                anim.Update(0f); anim.Update(0.12f);
                Shot($"ctrl_{state}_{mx}{my}");
            }

            RenderTexture.active = null; cam.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo); Object.DestroyImmediate(lightGo); Object.DestroyImmediate(go);
            File.WriteAllText(Marker, log.ToString());
            Debug.Log("[GunnerV2Diag]\n" + log);
        }

        // 0 = model forward (+Z), + = toward model's right (+X)
        private static float Yaw(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
    }
}
