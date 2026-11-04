using Members.KYR._01_Scripts;
using Members.KYR._01.Scripts.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 플레이어가 맞았을 때 / 위험할 때 화면 효과 (URP 포스트프로세싱)
    //  - 맞음: 빨간 Bloom + 가장자리 빨간 비네트가 확 들어왔다가 빠짐 (세게 맞을수록 진하게) + 살짝 흔들림
    //  - 위험(HP 낮음): 가장자리 빨간 비네트가 심장 뛰듯 깜빡 + 채도 빠짐. HP 낮을수록 빠르고 진하게
    // Volume 두 개를 런타임에 직접 만들어서 씀 → 씬에 Volume/프로필 따로 안 만들어도 됨.
    // 플레이어(PlayerAgent 있는 오브젝트)에 붙이면 끝.
    public class PlayerDamageScreenFx : MonoBehaviour
    {
        [SerializeField] private PlayerAgent player;

        [Header("맞았을 때")]
        [SerializeField] private Color hitColor = new(1f, 0.15f, 0.1f);
        [SerializeField, Tooltip("Bloom 세기 (최대로 맞았을 때)")] private float hitBloom = 6f;
        [SerializeField, Range(0f, 1f)] private float hitVignette = 0.4f;
        [SerializeField, Tooltip("들어온 효과가 빠지는 시간")] private float hitFadeTime = 0.35f;
        [SerializeField, Tooltip("최대 HP의 이 비율만큼 맞으면 효과 최대")] private float fullHitRatio = 0.2f;
        [SerializeField, Range(0f, 1f), Tooltip("아무리 약하게 맞아도 이만큼은 보이게")] private float minHitStrength = 0.4f;
        [SerializeField] private float hitShake = 0.15f;

        [Header("위험할 때 (HP 낮음)")]
        [SerializeField, Range(0f, 1f), Tooltip("HP가 이 비율 아래면 위험")] private float dangerRatio = 0.3f;
        [SerializeField, Range(0f, 1f)] private float dangerVignette = 0.45f;
        [SerializeField, Range(-100f, 0f)] private float dangerSaturation = -45f;
        [SerializeField, Tooltip("1초에 몇 번 뛰는지 (위험 시작 / HP 거의 없을 때)")]
        private Vector2 heartbeatRate = new(1.1f, 2.2f);

        private Volume _hitVolume;
        private Volume _dangerVolume;
        private float _hit;          // 0..1, 맞으면 올라갔다가 서서히 0
        private float _danger;       // 0..1, 위험 정도 (부드럽게 따라감)
        private float _beatPhase;

        private void Awake()
        {
            if (player == null)
                player = GetComponentInParent<PlayerAgent>();

            _hitVolume = CreateVolume("PlayerHitVolume", 200f, out VolumeProfile hitProfile);
            Bloom bloom = hitProfile.Add<Bloom>(true);
            bloom.intensity.value = hitBloom;
            bloom.threshold.value = 0.4f;   // 낮춰야 어두운 장면에서도 번쩍임이 보임
            bloom.scatter.value = 0.8f;
            bloom.tint.value = hitColor;
            Vignette hitVig = hitProfile.Add<Vignette>(true);
            hitVig.color.value = hitColor;
            hitVig.intensity.value = hitVignette;
            hitVig.smoothness.value = 1f;

            _dangerVolume = CreateVolume("PlayerDangerVolume", 199f, out VolumeProfile dangerProfile);
            Vignette dangerVig = dangerProfile.Add<Vignette>(true);
            dangerVig.color.value = hitColor;
            dangerVig.intensity.value = dangerVignette;
            dangerVig.smoothness.value = 1f;
            ColorAdjustments color = dangerProfile.Add<ColorAdjustments>(true);
            color.saturation.value = dangerSaturation;
        }

        private void Start()
        {
            // 카메라에서 Post Processing 꺼져 있으면 아무것도 안 보이니까 켜줌
            Camera cam = Camera.main;
            if (cam != null && cam.TryGetComponent(out UniversalAdditionalCameraData data))
                data.renderPostProcessing = true;
        }

        private void OnEnable()
        {
            if (player != null)
                player.DamageTaken += HandleDamageTaken;
        }

        private void OnDisable()
        {
            if (player != null)
                player.DamageTaken -= HandleDamageTaken;

            _hit = 0f;
            _danger = 0f;
            Apply(0f);
        }

        private void OnDestroy()
        {
            if (_hitVolume != null) Destroy(_hitVolume.sharedProfile);
            if (_dangerVolume != null) Destroy(_dangerVolume.sharedProfile);
        }

        private void HandleDamageTaken(float amount, GameObject source)
        {
            if (amount <= 0f || player == null || player.Health == null)
                return;

            float maxHp = Mathf.Max(1f, player.Health.MaxHp);
            float strength = Mathf.Clamp01(amount / (maxHp * Mathf.Max(0.01f, fullHitRatio)));
            strength = Mathf.Max(minHitStrength, strength);

            _hit = Mathf.Max(_hit, strength);
            HitFeel.Shake(hitShake * strength, 0.15f);
        }

        private void Update()
        {
            // 히트스톱(timeScale 0) 중에도 화면 효과는 움직여야 해서 unscaled
            float dt = Time.unscaledDeltaTime;

            _hit = Mathf.MoveTowards(_hit, 0f, dt / Mathf.Max(0.01f, hitFadeTime));

            float target = 0f;
            if (player != null && player.Health != null && player.IsAlive)
            {
                float ratio = player.Health.CurrentHealth / Mathf.Max(1f, player.Health.MaxHp);
                if (ratio < dangerRatio)
                    target = 1f - ratio / Mathf.Max(0.01f, dangerRatio);   // 경계에서 0, HP 0 에 가까울수록 1
            }
            _danger = Mathf.MoveTowards(_danger, target, dt * 2f);

            Apply(dt);
        }

        private void Apply(float dt)
        {
            // 들어올 땐 바로, 빠질 땐 부드럽게 (제곱 → 끝이 길게 빠짐)
            if (_hitVolume != null)
                _hitVolume.weight = _hit * _hit;

            if (_dangerVolume == null)
                return;

            if (_danger <= 0.001f)
            {
                _dangerVolume.weight = 0f;
                _beatPhase = 0f;
                return;
            }

            // 심장 박동: 두 번 쿵쿵 뛰고 쉬는 모양
            _beatPhase += dt * Mathf.Lerp(heartbeatRate.x, heartbeatRate.y, _danger);
            float t = _beatPhase % 1f;
            float beat = Mathf.Max(Pulse(t, 0.0f), Pulse(t, 0.22f) * 0.7f);

            float baseLevel = Mathf.Lerp(0.35f, 0.6f, _danger);
            _dangerVolume.weight = Mathf.Clamp01(_danger * (baseLevel + (1f - baseLevel) * beat));
        }

        // start 에서 확 올라갔다가 빠지는 짧은 맥박 하나
        private static float Pulse(float t, float start)
        {
            float x = t - start;
            if (x < 0f || x > 0.25f)
                return 0f;
            return x < 0.05f ? x / 0.05f : 1f - (x - 0.05f) / 0.2f;
        }

        private Volume CreateVolume(string objectName, float priority, out VolumeProfile profile)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(transform, false);

            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;   // 씬 기본 Volume 보다 위에서 덮어씀
            volume.weight = 0f;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = objectName + " (runtime)";
            volume.sharedProfile = profile;
            return volume;
        }
    }
}
