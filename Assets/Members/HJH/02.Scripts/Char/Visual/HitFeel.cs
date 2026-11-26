using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
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
        private static float _zoomHold;

        private bool _slowing;
        private float _slowScale = 1f;
        private float _slowBase = 1f;
        private float _slowEnd;
        private float _slowDuration;
        private float _slowRecover = 0.4f;

        private float _written = 1f;
        private bool _pausedByOther;

        public static void Play(float hitStop, float shake)
        {
            if (shake > 0f)
                Shake(shake, 0.24f);

            if (hitStop > 0f)
                Instance.Stop(hitStop);
        }

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

        public static void Kick(Vector3 offset, float duration = 0.22f)
        {
            if (duration <= 0f || offset.sqrMagnitude < CurrentKick.sqrMagnitude)
                return;

            _kick = offset;
            _kickStart = Time.unscaledTime;
            _kickDuration = duration;
        }

        public static void Zoom(float amount, float duration = 0.3f, float hold = 0f)
        {
            if (duration <= 0f || Mathf.Abs(amount) < Mathf.Abs(CurrentZoom))
                return;

            _zoom = amount;
            _zoomStart = Time.unscaledTime;
            _zoomDuration = duration;
            _zoomHold = Mathf.Clamp(hold / duration, 0f, 0.8f);
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

                float rise = Mathf.Min(0.15f, 1f - _zoomHold - 0.05f);
                float back = rise + _zoomHold;
                float k = t < rise ? t / rise
                    : t < back ? 1f
                    : 1f - Mathf.SmoothStep(0f, 1f, (t - back) / (1f - back));
                return _zoom * k;
            }
        }

        public static void SlowMo(float scale, float duration, float recover = 0.4f)
        {
            if (Time.timeScale <= 0f)
                return;

            HitFeel i = Instance;
            if (i._slowing && scale >= i._slowScale && Time.unscaledTime < i._slowEnd)
                return;

            if (!i._slowing)
                i._slowBase = i._stopping ? i._restoreTimeScale : Time.timeScale;
            i._slowing = true;
            i._slowScale = Mathf.Clamp(scale, 0.05f, 1f);
            i._slowDuration = Mathf.Max(0.05f, duration);
            i._slowRecover = Mathf.Clamp(recover, 0.05f, 1f);
            i._slowEnd = Time.unscaledTime + i._slowDuration;
            if (!i._stopping)
                i.Write(i.SlowValue());
        }

        private float SlowValue()
        {
            float t = 1f - (_slowEnd - Time.unscaledTime) / _slowDuration;
            float back = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - _slowRecover, 1f, t));
            return Mathf.Lerp(_slowScale, _slowBase, back);
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

                float k = t < 0.15f ? t / 0.15f : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.15f) / 0.85f);
                return _kick * k;
            }
        }

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
            if (Time.timeScale <= 0f)
                return;

            if (!_stopping)
            {
                _restoreTimeScale = _slowing ? _slowBase : Time.timeScale;
                _stopping = true;
            }

            Write(StoppedTimeScale);
            _stopEndRealtime = Mathf.Max(_stopEndRealtime, Time.unscaledTime + duration);
        }

        private void Write(float scale)
        {
            Time.timeScale = scale;
            _written = Time.timeScale;
        }

        private void Update()
        {
            if (!_stopping && !_slowing)
                return;

            if (!OwnsTimeScale())
                return;

            if (_stopping)
            {
                if (Time.unscaledTime < _stopEndRealtime)
                    return;
                _stopping = false;
                Write(_slowing ? SlowValue() : _restoreTimeScale);
                return;
            }

            if (Time.unscaledTime >= _slowEnd)
            {
                _slowing = false;
                Write(_slowBase);
            }
            else
            {
                Write(SlowValue());
            }
        }

        private bool OwnsTimeScale()
        {
            float now = Time.timeScale;
            if (Mathf.Abs(now - _written) < 0.0001f)
                return true;

            if (now <= 0f)
            {
                float dt = Time.unscaledDeltaTime;
                _stopEndRealtime += dt;
                _slowEnd += dt;
                _pausedByOther = true;
                return false;
            }

            if (_pausedByOther)
            {
                _pausedByOther = false;
                return true;
            }

            _stopping = false;
            _slowing = false;
            return false;
        }

        private void OnDestroy()
        {
            if ((!_stopping && !_slowing) || Time.timeScale <= 0f || Mathf.Abs(Time.timeScale - _written) > 0.0001f)
                return;

            Time.timeScale = _stopping && !_slowing ? _restoreTimeScale : _slowBase;
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
            _zoomHold = 0f;
        }
    }
}
