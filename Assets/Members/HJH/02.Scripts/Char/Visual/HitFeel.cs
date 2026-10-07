using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Global hit-feel: brief time freeze (hit stop) + camera shake.
    // Stronger requests win over weaker ones that are still running; they never stack up.
    public class HitFeel : MonoBehaviour
    {
        private const float StoppedTimeScale = 0.05f;

        private static HitFeel _instance;

        private float _stopEndRealtime;
        private float _restoreTimeScale = 1f;
        private bool _stopping;

        public static float ShakeAmplitude { get; private set; }
        private static float _shakeEndTime;
        private static float _shakeDuration;

        public static void Play(float hitStop, float shake)
        {
            if (shake > 0f)
                Shake(shake, 0.18f);

            if (hitStop > 0f)
                Instance.Stop(hitStop);
        }

        public static void Shake(float amplitude, float duration)
        {
            float current = CurrentShake;
            if (amplitude < current)
                return;

            ShakeAmplitude = amplitude;
            _shakeDuration = duration;
            _shakeEndTime = Time.unscaledTime + duration;
        }

        // Linearly fading amplitude; read by the camera every frame.
        public static float CurrentShake
        {
            get
            {
                float remaining = _shakeEndTime - Time.unscaledTime;
                return remaining <= 0f || _shakeDuration <= 0f ? 0f : ShakeAmplitude * (remaining / _shakeDuration);
            }
        }

        private static HitFeel Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new GameObject("[HitFeel]").AddComponent<HitFeel>();
                return _instance;
            }
        }

        private void Stop(float duration)
        {
            if (!_stopping)
            {
                _restoreTimeScale = Time.timeScale;
                _stopping = true;
            }

            Time.timeScale = StoppedTimeScale;
            _stopEndRealtime = Mathf.Max(_stopEndRealtime, Time.unscaledTime + duration);
        }

        private void Update()
        {
            if (!_stopping || Time.unscaledTime < _stopEndRealtime)
                return;

            _stopping = false;
            Time.timeScale = _restoreTimeScale;
        }

        private void OnDestroy()
        {
            if (_stopping)
                Time.timeScale = _restoreTimeScale;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            ShakeAmplitude = 0f;
            _shakeEndTime = 0f;
            _shakeDuration = 0f;
        }
    }
}
