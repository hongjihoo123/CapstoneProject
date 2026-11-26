using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.TopDown;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
    public class SoundListener : MonoBehaviour
    {
        private const float ScanInterval = 1f;

        public static bool HasPosition { get; private set; }
        public static Vector3 Position { get; private set; }

        public static bool IsAudible(Vector3 at, SoundSpatial spatial, float rangeScale = 1f)
        {
            if (!HasPosition || spatial.blend < 0.999f)
                return true;
            float range = spatial.maxDistance * rangeScale;
            return (at - Position).sqrMagnitude <= range * range;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => HasPosition = false;

        private AudioListener _own;
        private TopDownCameraFollow _follow;
        private float _nextScan;
        private bool _engaged;
        private readonly List<AudioListener> _silenced = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<SoundListener>() != null)
                return;
            var go = new GameObject("[SoundListener]");
            DontDestroyOnLoad(go);
            go.AddComponent<SoundListener>();
        }

        private void LateUpdate()
        {
            if (_follow == null && Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + ScanInterval;
                _follow = FindFirstObjectByType<TopDownCameraFollow>();
            }

            if (_follow == null || !_follow.isActiveAndEnabled || _follow.Target == null)
            {
                Disengage();
                return;
            }

            if (!_engaged)
                Engage();
            else if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + ScanInterval;
                SilenceOthers();
            }

            transform.SetPositionAndRotation(
                _follow.Target.position,
                Quaternion.Euler(0f, _follow.transform.eulerAngles.y, 0f));
            Position = transform.position;
            HasPosition = true;
        }

        private void OnDestroy() => Disengage();

        private void Engage()
        {
            _engaged = true;
            SilenceOthers();
            if (_own == null)
                _own = gameObject.AddComponent<AudioListener>();
            _own.enabled = true;
        }

        private void Disengage()
        {
            HasPosition = false;
            if (!_engaged)
                return;
            _engaged = false;
            if (_own != null)
                _own.enabled = false;
            foreach (AudioListener listener in _silenced)
                if (listener != null)
                    listener.enabled = true;
            _silenced.Clear();
        }

        private void SilenceOthers()
        {
            foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if (listener == _own || !listener.enabled)
                    continue;
                listener.enabled = false;
                _silenced.Add(listener);
            }
        }
    }
}
