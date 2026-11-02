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
        private static Vector3 _kick;
        private static float _kickStart;
        private static float _kickDuration;
        private static float _zoom;
        private static float _zoomStart;
        private static float _zoomDuration;

        private bool _slowing;
        private float _slowScale = 1f;
        private float _slowBase = 1f;
        private float _slowEnd;
        private float _slowDuration;

        public static void Play(float hitStop, float shake)
        {
            if (shake > 0f)
                Shake(shake, 0.24f);

            if (hitStop > 0f)
                Instance.Stop(hitStop);
        }

        // 전체 흔들림 배율 (피드백: 흔들림이 약함 → 1.8배). 모든 Shake 요청에 곱해짐
        public const float ShakeScale = 1.8f;

        public static void Shake(float amplitude, float duration)
        {
            amplitude *= ShakeScale;
            duration = Mathf.Max(duration, 0.2f);
            float current = CurrentShake;
            if (amplitude < current)
                return;

            ShakeAmplitude = amplitude;
            _shakeDuration = duration;
            _shakeEndTime = Time.unscaledTime + duration;
        }

        // Directional punch: the camera snaps by offset (world units) and eases back over duration.
        // Used for recoil (offset opposite the shot) and slams (offset down). Stronger wins, like Shake.
        public static void Kick(Vector3 offset, float duration = 0.22f)
        {
            if (duration <= 0f || offset.sqrMagnitude < CurrentKick.sqrMagnitude)
                return;

            _kick = offset;
            _kickStart = Time.unscaledTime;
            _kickDuration = duration;
        }

        // 줌 펀치: amount 만큼 카메라가 다가갔다(+) / 물러났다(-) 돌아옴. 0.06 = 거리 6%. 센 쪽이 이김
        public static void Zoom(float amount, float duration = 0.3f)
        {
            if (duration <= 0f || Mathf.Abs(amount) < Mathf.Abs(CurrentZoom))
                return;

            _zoom = amount;
            _zoomStart = Time.unscaledTime;
            _zoomDuration = duration;
        }

        public static float CurrentZoom
        {
            get
            {
                if (_zoomDuration <= 0f)
                    return 0f;
                float t = (Time.unscaledTime - _zoomStart) / _zoomDuration;
                if (t >= 1f)
                    return 0f;
                float k = t < 0.15f ? t / 0.15f : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.15f) / 0.85f);
                return _zoom * k;
            }
        }

        // 슬로모션: scale 배속으로 duration(실제 시간) 동안, 끝 40% 에서 부드럽게 원래 속도로. 더 느린 쪽이 이김
        public static void SlowMo(float scale, float duration)
        {
            HitFeel i = Instance;
            if (i._slowing && scale >= i._slowScale && Time.unscaledTime < i._slowEnd)
                return;

            if (!i._slowing)
                i._slowBase = i._stopping ? i._restoreTimeScale : Time.timeScale;
            i._slowing = true;
            i._slowScale = Mathf.Clamp(scale, 0.05f, 1f);
            i._slowDuration = Mathf.Max(0.05f, duration);
            i._slowEnd = Time.unscaledTime + i._slowDuration;
        }

        private float SlowValue()
        {
            float t = 1f - (_slowEnd - Time.unscaledTime) / _slowDuration;
            return Mathf.Lerp(_slowScale, _slowBase, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, t)));
        }

        public static Vector3 CurrentKick
        {
            get
            {
                if (_kickDuration <= 0f)
                    return Vector3.zero;

                float t = (Time.unscaledTime - _kickStart) / _kickDuration;
                if (t >= 1f)
                    return Vector3.zero;

                // 15% snap out, then smooth return.
                float k = t < 0.15f ? t / 0.15f : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.15f) / 0.85f);
                return _kick * k;
            }
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
            // 히트스톱이 먼저. 끝나면 슬로모션 중이면 슬로 속도로, 아니면 원래 속도로
            if (_stopping)
            {
                if (Time.unscaledTime < _stopEndRealtime)
                    return;
                _stopping = false;
                Time.timeScale = _slowing ? SlowValue() : _restoreTimeScale;
                return;
            }

            if (!_slowing)
                return;

            if (Time.unscaledTime >= _slowEnd)
            {
                _slowing = false;
                Time.timeScale = _slowBase;
            }
            else
            {
                Time.timeScale = SlowValue();
            }
        }

        private void OnDestroy()
        {
            if (_stopping)
                Time.timeScale = _slowing ? _slowBase : _restoreTimeScale;
            else if (_slowing)
                Time.timeScale = _slowBase;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            ShakeAmplitude = 0f;
            _shakeEndTime = 0f;
            _shakeDuration = 0f;
            _kick = Vector3.zero;
            _kickStart = 0f;
            _kickDuration = 0f;
            _zoom = 0f;
            _zoomStart = 0f;
            _zoomDuration = 0f;
        }
    }
}
