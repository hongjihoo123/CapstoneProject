using Members.KYR._01_Scripts;
using Members.KYR._01.Scripts.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 플레이어가 맞았을 때 / 딸피일 때 화면 효과
    //  - 빨간 테두리 + 흰색 하이라이트를 화면 "위에 얹어서" 그림 (밝게만 함).
    //    예전엔 URP Vignette 를 썼는데, Vignette 는 가장자리를 색으로 "곱해서" 어두운 장면에선 검붉게 됨
    //    + 딸피 채도 빠짐이 회색으로 보임 → 피드백 "빨강+검정 X, 빨강+흰 하이라이트" (2026-11-05)
    //  - 맞음: 흰 하이라이트가 가장자리에서 번쩍(짧게) → 빨간 테두리가 남았다가 천천히 빠짐. 화면 가운데는 안 건드림 (SoD 측정)
    //  - 딸피: 빨간 테두리가 천천히 욱신거리듯 차올랐다 빠짐 (이징), 빨강만. 채도는 안 뺌 (회색 X)
    //  - 딸피일 때 맞으면 평소보다 진하게
    //  - 죽음: 테두리 꽉 + 이때만 화면 회색
    // 화면 위 오버레이(캔버스)와 Volume 을 런타임에 직접 만들어서 씀 → 씬에 따로 만들 거 없음. 플레이어에 붙이면 끝
    public class PlayerDamageScreenFx : MonoBehaviour
    {
        [SerializeField] private PlayerAgent player;

        [Header("색")]
        [SerializeField] private Color hitColor = new(1f, 0.15f, 0.1f);
        [SerializeField, Tooltip("흰 하이라이트 색")] private Color highlightColor = new(1f, 0.96f, 0.94f);
        [SerializeField, Range(0.05f, 0.5f), Tooltip("빨간 테두리 두께 (화면 비율)")] private float redEdgeWidth = 0.28f;
        [SerializeField, Range(0.02f, 0.3f), Tooltip("흰 하이라이트 두께 (화면 비율, 빨강보다 얇게)")] private float highlightWidth = 0.08f;
        [SerializeField, Tooltip("오버레이 캔버스 순서. HUD 보다 아래여야 HUD 가 안 가려짐")] private int overlaySortingOrder = -50;

        [Header("맞았을 때")]
        [SerializeField, Range(0f, 1f), Tooltip("빨간 테두리 진하기 (최대로 맞았을 때)")] private float hitRedAlpha = 0.45f;
        [SerializeField, Range(0f, 1f), Tooltip("맞는 순간 흰 하이라이트 진하기")] private float hitHighlightAlpha = 0.7f;
        [SerializeField, Tooltip("흰 하이라이트가 빠지는 시간 (짧게 번쩍)")] private float hitHighlightTime = 0.15f;
        [SerializeField, Tooltip("Bloom 번쩍임 세기. 흰빛 쪽으로 (가운데 밝기는 거의 안 바뀌게 약하게)")]
        private float hitFlashBloom = 1f;
        [SerializeField, Tooltip("빨강이 꽉 찬 채로 유지되는 시간 (SoD ≈ 0.1~0.15초)")] private float hitPeakTime = 0.1f;
        [SerializeField, Tooltip("유지 뒤 빠지는 시간 (SoD ≈ 0.8초)")] private float hitFadeOutTime = 0.8f;
        [SerializeField, Tooltip("Bloom 번쩍임이 빠지는 시간 (짧게)")] private float bloomFadeTime = 0.2f;
        [SerializeField, Tooltip("최대 HP의 이 비율만큼 맞으면 효과 최대")] private float fullHitRatio = 0.2f;
        [SerializeField, Range(0f, 1f), Tooltip("아무리 약하게 맞아도 이만큼은 보이게")] private float minHitStrength = 0.4f;
        [SerializeField] private float hitShake = 0.15f;

        [Header("딸피 (HP 낮음)")]
        [SerializeField, Range(0f, 1f), Tooltip("HP가 이 비율 아래면 위험")] private float lowHpRatio = 0.35f;
        [SerializeField, Tooltip("빨간 테두리 진하기: x = 가장 옅을 때, y = 가장 진할 때 (HP 0 에 가까울 때)")]
        private Vector2 lowHpRedAlpha = new(0.22f, 0.5f);
        [SerializeField, Range(1f, 2f), Tooltip("딸피일 때 맞으면 빨강·하이라이트를 이만큼 더 진하게")]
        private float lowHpHitBoost = 1.4f;
        [SerializeField, Tooltip("한 번 차올랐다 빠지는 데 걸리는 초 (위험 시작 / HP 거의 없을 때). 길수록 느리게 욱신")]
        private Vector2 throbSeconds = new(2.6f, 1.6f);

        [Header("죽음")]
        [SerializeField, Range(0f, 1f)] private float deathRedAlpha = 0.6f;
        [SerializeField, Range(-100f, 0f), Tooltip("죽었을 때만 화면 회색")] private float deathSaturation = -80f;

        private RawImage _red;
        private RawImage _highlight;
        private Texture2D _redTex;
        private Texture2D _highlightTex;
        private GameObject _overlay;

        //private Volume _flashVolume;   // 짧은 흰빛 Bloom
        private Volume _deathVolume;   // 죽었을 때 회색
        private float _flash;        // 0..1, Bloom 번쩍임
        private float _hitStrength;  // 마지막으로 맞은 세기
        private float _hitFade;      // 1 = 꽉, 0 = 꺼짐
        private float _hitHold;      // 남은 유지 시간
        private float _hitHighlight; // 0..1, 맞는 순간 흰 하이라이트
        private float _hitBoost = 1f;
        private float _danger;       // 0..1, 위험 정도 (부드럽게 따라감)
        private float _beatPhase;
        private bool _dead;

        private void Awake()
        {
            if (player == null)
                player = GetComponentInParent<PlayerAgent>();

            CreateOverlay();

            //_flashVolume = CreateVolume("PlayerHitFlashVolume", 201f, out VolumeProfile flashProfile);
            //Bloom bloom = flashProfile.Add<Bloom>(true);
            //bloom.intensity.value = hitFlashBloom;
            //bloom.threshold.value = 0.4f;   // 낮춰야 어두운 장면에서도 번쩍임이 보임
            //bloom.scatter.value = 0.8f;
            //bloom.tint.value = Color.Lerp(hitColor, highlightColor, 0.6f);   // 빨강보다 흰빛 쪽

            _deathVolume = CreateVolume("PlayerDeathVolume", 199f, out VolumeProfile deathProfile);
            ColorAdjustments grey = deathProfile.Add<ColorAdjustments>(false);
            grey.saturation.Override(deathSaturation);
        }

        private void Start()
        {
            // 카메라에서 Post Processing 꺼져 있으면 Bloom·회색이 안 보이니까 켜줌
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

            _flash = 0f;
            _hitStrength = 0f;
            _hitFade = 0f;
            _hitHold = 0f;
            _hitHighlight = 0f;
            _danger = 0f;
            Apply(0f);
        }

        private void OnDestroy()
        {
            //if (_flashVolume != null) Destroy(_flashVolume.sharedProfile);
            if (_deathVolume != null) Destroy(_deathVolume.sharedProfile);
            if (_redTex != null) Destroy(_redTex);
            if (_highlightTex != null) Destroy(_highlightTex);
            if (_overlay != null) Destroy(_overlay);
        }

        private void HandleDamageTaken(float amount, GameObject source)
        {
            if (amount <= 0f || player == null || player.Health == null)
                return;

            float maxHp = Mathf.Max(1f, player.Health.MaxHp);
            float strength = Mathf.Clamp01(amount / (maxHp * Mathf.Max(0.01f, fullHitRatio)));
            strength = Mathf.Max(minHitStrength, strength);

            // 연속으로 맞으면 지금 보이는 세기보다 약해지지 않게 하고 유지 시간을 다시 채움
            _hitStrength = Mathf.Max(HitWeight, strength);
            _hitFade = 1f;
            _hitHold = hitPeakTime;
            _hitHighlight = Mathf.Max(_hitHighlight, strength);
            _hitBoost = _danger > 0.01f ? lowHpHitBoost : 1f;
            _flash = Mathf.Max(_flash, strength);
            HitFeel.Shake(hitShake * strength, 0.15f);
        }

        // 즉시 켜짐 → 유지 → 부드럽게(SmoothStep) 꺼짐
        private float HitWeight => _hitStrength * Mathf.SmoothStep(0f, 1f, _hitFade);

        private void Update()
        {
            // 히트스톱(timeScale 0) 중에도 화면 효과는 움직여야 해서 unscaled
            float dt = Time.unscaledDeltaTime;

            if (_hitHold > 0f)
                _hitHold -= dt;
            else
                _hitFade = Mathf.MoveTowards(_hitFade, 0f, dt / Mathf.Max(0.01f, hitFadeOutTime));
            _hitHighlight = Mathf.MoveTowards(_hitHighlight, 0f, dt / Mathf.Max(0.01f, hitHighlightTime));
            _flash = Mathf.MoveTowards(_flash, 0f, dt / Mathf.Max(0.01f, bloomFadeTime));

            float target = 0f;
            bool dead = player != null && player.Health != null && !player.IsAlive;
            _dead = dead;
            if (dead)
                target = 1f;
            else if (player != null && player.Health != null)
            {
                float ratio = player.Health.CurrentHealth / Mathf.Max(1f, player.Health.MaxHp);
                if (ratio < lowHpRatio)
                    target = 1f - ratio / Mathf.Max(0.01f, lowHpRatio);   // 경계에서 0, HP 0 에 가까울수록 1
            }
            _danger = Mathf.MoveTowards(_danger, target, dt * (dead ? 1.2f : 2f));

            Apply(dt);
        }

        private void Apply(float dt)
        {
            //if (_flashVolume != null)
                //_flashVolume.weight = _flash * _flash;   // 제곱 → 확 빠지고 끝만 살짝
            if (_deathVolume != null)
                _deathVolume.weight = _dead ? _danger : 0f;

            // 맞았을 때
            float red = HitWeight * hitRedAlpha * _hitBoost;
            float white = _hitHighlight * _hitHighlight * hitHighlightAlpha * _hitBoost;

            // 딸피 / 죽음
            if (_dead)
            {
                red = Mathf.Max(red, _danger * deathRedAlpha);
            }
            else if (_danger > 0.001f)
            {
                // 천천히 욱신: 부드럽게 차올랐다가 부드럽게 빠짐 (심장 "쿵쿵" 대신 숨쉬듯)
                _beatPhase += dt / Mathf.Max(0.1f, Mathf.Lerp(throbSeconds.x, throbSeconds.y, _danger));
                float beat = Throb(_beatPhase % 1f);

                float lowRed = _danger * Mathf.Lerp(lowHpRedAlpha.x, lowHpRedAlpha.y, beat);
                red = Mathf.Max(red, lowRed);   // 딸피는 빨강만 (흰 하이라이트 X, 2026-11-05 피드백)
            }
            else
            {
                _beatPhase = 0f;
            }

            SetAlpha(_red, hitColor, Mathf.Clamp01(red));
            SetAlpha(_highlight, highlightColor, Mathf.Clamp01(white));
        }

        // 0 → 1 → 0 한 바퀴. 코사인(위아래 끝에서 느려짐)에 SmoothStep 을 한 번 더 걸어서
        // 진할 때·옅을 때 조금씩 머무르고, 그 사이를 천천히 넘어감
        private static float Throb(float t)
        {
            float wave = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f);
            return Mathf.SmoothStep(0f, 1f, wave);
        }

        private static void SetAlpha(RawImage image, Color color, float alpha)
        {
            if (image == null)
                return;
            color.a = alpha;
            image.color = color;
            image.enabled = alpha > 0.002f;
        }

        // ------------------------------------------------ 화면 오버레이

        private void CreateOverlay()
        {
            _overlay = new GameObject("PlayerDamageOverlay");
            _overlay.transform.SetParent(transform, false);
            Canvas canvas = _overlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = overlaySortingOrder;

            _redTex = EdgeTexture(redEdgeWidth, 2f);
            _highlightTex = EdgeTexture(highlightWidth, 1.5f);
            _red = CreateLayer("RedEdge", _redTex);
            _highlight = CreateLayer("WhiteHighlight", _highlightTex);   // 빨강 위에
        }

        private RawImage CreateLayer(string layerName, Texture texture)
        {
            var go = new GameObject(layerName, typeof(RectTransform));
            go.transform.SetParent(_overlay.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            RawImage image = go.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        // 가장자리에서 1, 안쪽으로 width 만큼 가면 0 이 되는 흰 텍스처 (색은 RawImage.color 로).
        // 네 변 중 가까운 변까지 거리를 부드럽게 합쳐서 모서리가 둥글게
        private static Texture2D EdgeTexture(float width, float falloff)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = "DamageEdge (runtime)"
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float v = (y + 0.5f) / size;
                float edge = 0f;
                foreach (float d in new[] { u, 1f - u, v, 1f - v })
                {
                    float near = 1f - Mathf.Clamp01(d / width);
                    edge = 1f - (1f - edge) * (1f - near);   // 모서리에서 두 변이 겹쳐 진해지게
                }
                byte alpha = (byte)(Mathf.Pow(edge, falloff) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
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
