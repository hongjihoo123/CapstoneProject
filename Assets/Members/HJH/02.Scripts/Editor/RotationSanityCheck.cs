using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // Finds Transforms whose rotation is not unit length — the cause of
    // "QuaternionToEuler: Input quaternion was not normalized" (logged by the Inspector / Scene view,
    // so the console stack never shows the script that wrote the bad rotation).
    //  - HJH > Debug > Find Unnormalized Rotations: one scan now.
    //  - HJH > Debug > Watch Unnormalized Rotations (on by default): scans twice a second in play mode
    //    and reports each offending object once. Click the log line to select it.
    [InitializeOnLoad]
    public static class RotationSanityCheck
    {
        private const string WatchPref = "HJH.RotationSanityCheck.Watch";
        private const string WatchMenu = "HJH/Debug/Watch Unnormalized Rotations";
        private const double Interval = 0.5;

        private static readonly HashSet<int> Reported = new();
        private static double _nextScan;

        static RotationSanityCheck()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += _ => Reported.Clear();
        }

        private static bool Watching
        {
            get => EditorPrefs.GetBool(WatchPref, true);
            set => EditorPrefs.SetBool(WatchPref, value);
        }

        [MenuItem("HJH/Debug/Find Unnormalized Rotations")]
        public static void Find()
        {
            Reported.Clear();
            Debug.Log($"[RotationCheck] 정규화 안 된 회전 {Scan()}개");
        }

        [MenuItem(WatchMenu)]
        private static void ToggleWatch() => Watching = !Watching;

        [MenuItem(WatchMenu, true)]
        private static bool ToggleWatchValidate()
        {
            Menu.SetChecked(WatchMenu, Watching);
            return true;
        }

        private static void Tick()
        {
            if (!Watching || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < _nextScan)
                return;

            _nextScan = EditorApplication.timeSinceStartup + Interval;
            Scan();
        }

        private static int Scan()
        {
            int found = 0;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Quaternion q = t.localRotation;
                float lengthSq = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
                if (Mathf.Abs(lengthSq - 1f) < 0.001f)
                    continue;

                found++;
                if (Reported.Add(t.GetInstanceID()))
                    Debug.LogWarning($"[RotationCheck] {Path(t)} localRotation {q} (|q|²={lengthSq:F4}) — 이 오브젝트의 회전을 쓰는 코드를 확인하세요.", t);
            }
            return found;
        }

        private static string Path(Transform t) => t.parent == null ? t.name : $"{Path(t.parent)}/{t.name}";
    }
}
