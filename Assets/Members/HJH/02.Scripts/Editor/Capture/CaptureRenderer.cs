using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HJH.CaptureTool
{
    internal static class CaptureRenderer
    {
        public enum Pose { None, Ok, Error }

        public static Camera MainCamera()
        {
            if (Camera.main != null) return Camera.main;
            foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c.enabled && c.targetTexture == null) return c;
            return null;
        }

        public static GameObject FindTarget(string target)
        {
            if (string.IsNullOrEmpty(target)) return null;
            if (target.StartsWith("tag:"))
            {
                try { return GameObject.FindWithTag(target.Substring(4)); } catch (UnityException) { return null; }
            }
            return GameObject.Find(target);
        }

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
                bool vertical = Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.99f;
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
                    cam.CopyFrom(main);
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

        public static void RenderCamera(Camera cam, int w, int h, string path)
        {
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture prevTarget = cam.targetTexture, prevActive = RenderTexture.active;
            Texture2D tex = null;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
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
