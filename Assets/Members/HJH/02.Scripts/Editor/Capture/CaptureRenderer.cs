using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HJH.CaptureTool
{
    // 시점 계산 + 카메라를 PNG 로 굽는 부분만 담당. 씬을 바꾸지 않는다 (임시 카메라는 HideAndDontSave 후 바로 제거).
    internal static class CaptureRenderer
    {
        public enum Pose { None, Ok, Error }

        public static Camera MainCamera()
        {
            if (Camera.main != null) return Camera.main;
            foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c.enabled && c.targetTexture == null) return c;   // 미니맵처럼 RT 로 찍는 카메라는 제외
            return null;
        }

        public static GameObject FindTarget(string target)
        {
            if (string.IsNullOrEmpty(target)) return null;
            if (target.StartsWith("tag:"))
            {
                try { return GameObject.FindWithTag(target.Substring(4)); } catch (UnityException) { return null; }   // 없는 태그
            }
            return GameObject.Find(target);
        }

        // target 기준 또는 절대 좌표로 카메라 위치·회전을 구한다. 아무것도 안 적혀 있으면 None (원래 시점 유지)
        public static Pose GetPose(CaptureShot s, out Vector3 pos, out Quaternion rot, out string error)
        {
            pos = Vector3.zero; rot = Quaternion.identity; error = null;
            if (!string.IsNullOrEmpty(s.target))
            {
                GameObject go = FindTarget(s.target);
                if (go == null) { error = $"대상 '{s.target}' 을 씬에서 못 찾음"; return Pose.Error; }
                Quaternion yaw = s.space == "world" ? Quaternion.identity : Quaternion.Euler(0f, go.transform.eulerAngles.y, 0f);
                pos = go.transform.position + yaw * s.offset;
                Vector3 dir = go.transform.position + yaw * s.lookAt - pos;
                if (dir.sqrMagnitude < 1e-6f) { error = "offset 과 lookAt 이 같아 방향을 못 정함"; return Pose.Error; }
                bool vertical = Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.99f;   // 바로 위에서 내려다볼 때: 대상 정면이 화면 위쪽
                rot = Quaternion.LookRotation(dir, vertical ? yaw * Vector3.forward : Vector3.up);
                return Pose.Ok;
            }
            if (s.position != Vector3.zero || s.euler != Vector3.zero)
            {
                pos = s.position; rot = Quaternion.Euler(s.euler);
                return Pose.Ok;
            }
            return Pose.None;
        }

        // 임시 카메라를 만들어 찍는다. 메인 카메라가 있으면 설정(컬링·배경·포스트프로세싱)을 그대로 가져온다.
        public static void RenderFree(CaptureShot s, int w, int h, string path)
        {
            Pose pose = GetPose(s, out Vector3 pos, out Quaternion rot, out string error);
            if (pose == Pose.Error) throw new System.InvalidOperationException(error);
            Camera main = MainCamera();
            if (pose == Pose.None && main == null) throw new System.InvalidOperationException("시점이 없고 메인 카메라도 없음");

            var go = new GameObject("__HJHCaptureCam") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                Camera cam = go.AddComponent<Camera>();
                UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                if (main != null)
                {
                    cam.CopyFrom(main);   // 위치·회전도 같이 복사됨
                    if (main.TryGetComponent(out UniversalAdditionalCameraData src))
                    {
                        data.renderPostProcessing = src.renderPostProcessing;
                        data.antialiasing = src.antialiasing;
                        data.antialiasingQuality = src.antialiasingQuality;
                        data.volumeLayerMask = src.volumeLayerMask;
                        data.renderShadows = src.renderShadows;
                    }
                }
                cam.enabled = false;
                cam.targetTexture = null;
                cam.rect = new Rect(0f, 0f, 1f, 1f);
                if (pose == Pose.Ok) cam.transform.SetPositionAndRotation(pos, rot);
                if (s.orthoSize > 0f) { cam.orthographic = true; cam.orthographicSize = s.orthoSize; }
                else if (pose == Pose.Ok || s.fov > 0f)
                {
                    cam.orthographic = false;
                    cam.fieldOfView = s.fov > 0f ? s.fov : (main != null && !main.orthographic ? main.fieldOfView : 40f);
                }
                RenderCamera(cam, w, h, path);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // 이미 있는 카메라(메인 카메라, Scene 뷰 카메라)를 그 시점 그대로 찍는다. targetTexture 는 원래대로 돌려놓는다.
        public static void RenderCamera(Camera cam, int w, int h, string path)
        {
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture prevTarget = cam.targetTexture, prevActive = RenderTexture.active;
            Texture2D tex = null;
            try
            {
                cam.targetTexture = rt;
                cam.Render();   // 첫 장은 준비용 (셰이더 컴파일·누적 효과)
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
                if (tex != null) Object.DestroyImmediate(tex);
            }
        }

        // ── 플레이 모드 조건 (게임 코드 없이 Animator 상태 이름만 본다) ──
        private static Animator FindAnimator(string target)
        {
            GameObject go = FindTarget(target);
            return go != null ? go.GetComponentInChildren<Animator>() : null;
        }

        public static bool InState(string target, string state)
        {
            Animator a = FindAnimator(target);
            if (a == null || a.runtimeAnimatorController == null) return false;
            for (int i = 0; i < a.layerCount; i++)
                if (a.GetCurrentAnimatorStateInfo(i).IsName(state)) return true;
            return false;
        }

        public static bool ForceState(string target, string state)
        {
            Animator a = FindAnimator(target);
            if (a == null || a.runtimeAnimatorController == null) return false;
            int hash = Animator.StringToHash(state); bool any = false;
            for (int i = 0; i < a.layerCount; i++)
                if (a.HasState(i, hash)) { a.Play(hash, i, 0f); any = true; }
            return any;
        }
    }
}
