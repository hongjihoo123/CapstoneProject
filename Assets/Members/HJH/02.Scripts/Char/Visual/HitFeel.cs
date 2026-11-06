using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Global hit-feel: brief time freeze (hit stop) + camera shake / kick / zoom + slow motion.
    // Stronger requests win over weaker ones that are still running; they never stack up.
    // timeScale 은 항상 시작 전 값으로 되돌림. 다른 시스템(일시정지 등)이 바꾸면 그쪽을 존중 (OwnsTimeScale)
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

        // 마지막으로 이쪽이 넣은 timeScale. 지금 값과 다르면 다른 시스템이 바꾼 것
        private float _written = 1f;
        private bool _pausedByOther;

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
        // hold(실제 초) 동안은 다가간 채로 있다가 남은 시간 동안 부드럽게 돌아옴 (강한 처치 슬로모션과 맞춤)
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

                // 15% 동안 다가감 → hold 유지 → 나머지 동안 부드럽게 복귀 (hold 0 이면 예전과 같음)
                float rise = Mathf.Min(0.15f, 1f - _zoomHold - 0.05f);
                float back = rise + _zoomHold;
                float k = t < rise ? t / rise
                    : t < back ? 1f
                    : 1f - Mathf.SmoothStep(0f, 1f, (t - back) / (1f - back));
                return _zoom * k;
            }
        }

        // 슬로모션: scale 배속으로 duration(실제 초) 동안, 끝 recover 비율(기본 40%)에서 부드럽게 원래 속도로.
        // 더 느린 쪽이 이김. 끝나면 시작 전 timeScale 로 정확히 되돌림
        public static void SlowMo(float scale, float duration, float recover = 0.4f)
        {
            if (Time.timeScale <= 0f)
                return;   // 일시정지 중엔 무시 (원래 속도를 0 으로 잘못 기억하지 않게)

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
                i.Write(i.SlowValue());   // 이번 프레임부터 바로 느려지게
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
            if (Time.timeScale <= 0f)
                return;   // 일시정지 중

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

            // 히트스톱이 먼저. 끝나면 슬로모션 중이면 슬로 속도로, 아니면 원래 속도로
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

        // 다른 시스템과 timeScale 다툼 방지
        //  - 누가 0 으로 만들었으면(일시정지) 건드리지 않고 기다림. 기다린 만큼 연출 시간을 뒤로 밀어서 풀린 뒤 이어서 진행
        //  - 일시정지가 풀리면 다시 이어받음 (그쪽이 되돌린 '느린 값'은 덮어쓰고, 끝나면 원래 속도로 복귀)
        //  - 일시정지가 아닌데 다른 값으로 바뀌었으면 그쪽 의도를 존중하고 이쪽 연출만 끝냄
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
            // 연출 중이 아니거나, 다른 시스템이 일시정지 중이면 그쪽에 맡김
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
