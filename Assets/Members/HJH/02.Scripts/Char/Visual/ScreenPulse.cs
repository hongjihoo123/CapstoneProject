using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 큰 타격 때 화면이 잠깐 살짝 밝아짐 (블룸·색수차·비네트·색 필터는 안 씀).
    // 피드백: 화면 가장자리가 원소 색(불 = 빨강)으로 물드는 게 과함 → 밝기만 짧게 튐
    // 씬의 Global Volume 위에 우선순위 높은 Volume 을 하나 얹고, 그 weight 만 0 → 1 → 0 으로 움직인다.
    // (씬 Volume 설정은 건드리지 않음. 카메라의 Post Processing 이 켜져 있어야 보임)
    public class ScreenPulse : MonoBehaviour
    {
        private static ScreenPulse _instance;

        private Volume _volume;
        private ColorAdjustments _color;
        private float _strength;
        private float _start;
        private float _duration;

        // strength 0~1. 더 센 요청만 덮어씀 (작은 게 큰 걸 끊지 않게)
        public static void Play(Color tint, float strength, float duration = 0.28f)
        {
            ScreenPulse p = Instance;
            if (strength <= p.Current)
                return;

            p._strength = Mathf.Clamp01(strength);
            p._start = Time.unscaledTime;
            p._duration = Mathf.Max(0.05f, duration);
        }

        // 임팩트 프레임 (젠존제 풍): 큰 타격 순간 2~3프레임 동안 화면이 흑백 고대비로 번쩍. 색은 안 입힘
        public static void ImpactFrame(float duration = 0.07f)
        {
            ScreenPulse p = Instance;
            p._impactUntil = Time.unscaledTime + duration;
            p._impactVolume.weight = 1f;
        }

        private float _impactUntil;
        private Volume _impactVolume;

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

            // 블룸은 건드리지 않음 (씬 블룸 위에 더하면 화면이 하얗게 뜸 — 피드백 반영)
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
        }

        private void Update()
        {
            // 처음 15% 에 확 올라가고 나머지 동안 빠짐
            float t = _duration > 0f ? (Time.unscaledTime - _start) / _duration : 1f;
            float w = t >= 1f ? 0f : t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f;
            _volume.weight = _strength * Mathf.Clamp01(w);
            if (_impactVolume != null && _impactVolume.weight > 0f && Time.unscaledTime >= _impactUntil)
                _impactVolume.weight = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
