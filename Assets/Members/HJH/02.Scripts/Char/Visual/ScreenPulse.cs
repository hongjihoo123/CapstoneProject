using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    public class ScreenPulse : MonoBehaviour
    {
        private static ScreenPulse _instance;

        private Volume _volume;
        private ColorAdjustments _color;
        private float _strength;
        private float _start;
        private float _duration;

        public static void Play(Color tint, float strength, float duration = 0.28f)
        {
            ScreenPulse p = Instance;
            if (strength <= p.Current)
                return;

            p._strength = Mathf.Clamp01(strength);
            p._start = Time.unscaledTime;
            p._duration = Mathf.Max(0.05f, duration);
        }

        public static void ImpactFrame(float duration = 0.07f)
        {
            ScreenPulse p = Instance;
            p._impactUntil = Time.unscaledTime + duration;
            p._impactVolume.weight = 1f;
        }

        private float _impactUntil;
        private Volume _impactVolume;

        public static void KillPunch(KillPostFx settings)
        {
            if (settings == null || !settings.enabled)
                return;

            ScreenPulse p = Instance;
            p._kill.chromatic.intensity.Override(settings.chromaticAberration);
            p._kill.distortion.intensity.Override(settings.lensDistortion);
            p._kill.distortion.scale.Override(settings.lensScale);
            p._kill.vignette.intensity.Override(settings.vignette);
            p._kill.vignette.smoothness.Override(settings.vignetteSmoothness);
            p._kill.color.saturation.Override(settings.saturation);
            p._kill.color.contrast.Override(settings.contrast);
            p._kill.color.postExposure.Override(settings.exposure);
            p._kill.blur.intensity.Override(settings.motionBlur);
            p._kill.blur.active = settings.motionBlur > 0f;

            p._killStart = Time.unscaledTime;
            p._killHold = Mathf.Max(0f, settings.hold);
            p._killDuration = Mathf.Max(p._killHold + 0.05f, settings.duration);
        }

        private sealed class KillProfile
        {
            public ChromaticAberration chromatic;
            public LensDistortion distortion;
            public Vignette vignette;
            public ColorAdjustments color;
            public MotionBlur blur;
        }

        private readonly KillProfile _kill = new();
        private Volume _killVolume;
        private float _killStart = -999f;
        private float _killHold;
        private float _killDuration;
        private const float KillRise = 0.04f;

        private float KillWeight
        {
            get
            {
                float t = Time.unscaledTime - _killStart;
                if (_killDuration <= 0f || t < 0f || t >= _killDuration)
                    return 0f;
                if (t < KillRise)
                    return t / KillRise;
                if (t < _killHold)
                    return 1f;
                float back = Mathf.Max(0.01f, _killDuration - Mathf.Max(_killHold, KillRise));
                return 1f - Mathf.SmoothStep(0f, 1f, (t - Mathf.Max(_killHold, KillRise)) / back);
            }
        }

        private void CreateKillVolume()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _kill.chromatic = profile.Add<ChromaticAberration>(true);
            _kill.distortion = profile.Add<LensDistortion>(true);
            _kill.vignette = profile.Add<Vignette>(true);
            _kill.vignette.color.Override(Color.black);
            _kill.vignette.center.Override(new Vector2(0.5f, 0.5f));
            _kill.color = profile.Add<ColorAdjustments>(true);
            _kill.blur = profile.Add<MotionBlur>(true);
            _kill.blur.mode.Override(MotionBlurMode.CameraOnly);
            _kill.blur.quality.Override(MotionBlurQuality.Medium);

            _killVolume = gameObject.AddComponent<Volume>();
            _killVolume.isGlobal = true;
            _killVolume.priority = 102f;
            _killVolume.weight = 0f;
            _killVolume.sharedProfile = profile;
        }

        private float Current
        {
            get
            {
                float t = (Time.unscaledTime - _start) / _duration;
                return t >= 1f ? 0f : _strength * (1f - t);
            }
        }

        private static ScreenPulse Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new GameObject("[ScreenPulse]").AddComponent<ScreenPulse>();
                return _instance;
            }
        }

        private void Awake()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            _color = profile.Add<ColorAdjustments>(true);
            _color.postExposure.Override(0.15f);

            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
            _volume.weight = 0f;
            _volume.sharedProfile = profile;

            var impactProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            ColorAdjustments impact = impactProfile.Add<ColorAdjustments>(true);
            impact.saturation.Override(-100f);
            impact.contrast.Override(70f);
            impact.postExposure.Override(0.6f);
            _impactVolume = gameObject.AddComponent<Volume>();
            _impactVolume.isGlobal = true;
            _impactVolume.priority = 101f;
            _impactVolume.weight = 0f;
            _impactVolume.sharedProfile = impactProfile;

            CreateKillVolume();
        }

        private void OnDestroy()
        {
            if (_volume != null) Destroy(_volume.sharedProfile);
            if (_impactVolume != null) Destroy(_impactVolume.sharedProfile);
            if (_killVolume != null) Destroy(_killVolume.sharedProfile);
        }

        private void Update()
        {
            float t = _duration > 0f ? (Time.unscaledTime - _start) / _duration : 1f;
            float w = t >= 1f ? 0f : t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f;
            _volume.weight = _strength * Mathf.Clamp01(w);
            if (_impactVolume != null && _impactVolume.weight > 0f && Time.unscaledTime >= _impactUntil)
                _impactVolume.weight = 0f;
            if (_killVolume != null)
                _killVolume.weight = KillWeight;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }

    [System.Serializable]
    public class KillPostFx
    {
        public bool enabled = true;
        [Range(0f, 1f), Tooltip("색수차. 가장자리 색 번짐. 0.5~0.8 이 '쾅' 느낌, 1 은 화면이 깨지는 느낌")]
        public float chromaticAberration = 0.7f;
        [Range(-1f, 1f), Tooltip("렌즈 왜곡. + = 가운데가 부풀어 줌인이 더 세 보임 (0.2~0.35). - = 가운데가 멀어지는 워프 느낌")]
        public float lensDistortion = 0.28f;
        [Range(0.5f, 1.5f), Tooltip("왜곡으로 가장자리가 비면 살짝 키움 (1 = 그대로)")]
        public float lensScale = 1.02f;
        [Range(0f, 1f), Tooltip("검은 비네트 진하기. 시선이 가운데(처치한 적)로 모임. 0.35~0.45")]
        public float vignette = 0.4f;
        [Range(0.01f, 1f), Tooltip("비네트 부드러움")] public float vignetteSmoothness = 0.45f;
        [Range(-100f, 100f), Tooltip("채도. 음수 = 색이 빠져 슬로모션이 '멈춘 순간'처럼 보임. -30~-50")]
        public float saturation = -40f;
        [Range(-100f, 100f), Tooltip("대비. 15~25 로 올리면 묵직해짐")] public float contrast = 20f;
        [Tooltip("밝기 (EV). 0 = 그대로. 블룸 대신 살짝만 (0.1~0.2)")] public float exposure = 0.1f;
        [Range(0f, 1f), Tooltip("카메라 모션 블러. 줌인 움직임에 잔상이 생김. 0 = 끔 (0.3~0.5)")]
        public float motionBlur = 0.4f;

        [Tooltip("최대로 유지하는 시간 (실제 초). 줌 유지 시간과 맞추면 좋음")] public float hold = 0.22f;
        [Tooltip("켜졌다 다 빠질 때까지 전체 시간 (실제 초). 줌 전체 시간과 맞추면 좋음")] public float duration = 0.8f;
    }
}
