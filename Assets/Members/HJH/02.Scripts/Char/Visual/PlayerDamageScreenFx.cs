using Members.KYR._01.Scripts.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
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
        [SerializeField, Range(0f, 1f), Tooltip("죽었을 때 테두리 진하기")] private float deathEdgeAlpha = 0.35f;
        [SerializeField, Tooltip("죽었을 때 테두리 색 (회색 화면에 맞춘 회색). 맞았을 때 빨강에서 이 색으로 서서히 바뀜")]
        private Color deathEdgeColor = new(0.62f, 0.62f, 0.66f);
        [SerializeField, Range(-100f, 0f), Tooltip("죽었을 때만 화면 회색")] private float deathSaturation = -80f;

        private RawImage _red;
        private RawImage _highlight;
        private Texture2D _redTex;
        private Texture2D _highlightTex;
        private GameObject _overlay;

        private Volume _deathVolume;
        private float _flash;
        private float _hitStrength;
        private float _hitFade;
        private float _hitHold;
        private float _hitHighlight;
        private float _hitBoost = 1f;
        private float _danger;
        private float _beatPhase;
        private bool _dead;

        private void Awake()
        {
            if (player == null)
                player = GetComponentInParent<PlayerAgent>();

            CreateOverlay();

            _deathVolume = CreateVolume("PlayerDeathVolume", 199f, out VolumeProfile deathProfile);
            ColorAdjustments grey = deathProfile.Add<ColorAdjustments>(false);
            grey.saturation.Override(deathSaturation);
        }

        private void Start()
        {
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

            _hitStrength = Mathf.Max(HitWeight, strength);
            _hitFade = 1f;
            _hitHold = hitPeakTime;
            _hitHighlight = Mathf.Max(_hitHighlight, strength);
            _hitBoost = _danger > 0.01f ? lowHpHitBoost : 1f;
            _flash = Mathf.Max(_flash, strength);
            HitFeel.Shake(hitShake * strength, 0.15f);
        }

        private float HitWeight => _hitStrength * Mathf.SmoothStep(0f, 1f, _hitFade);

        private void Update()
        {
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
                    target = 1f - ratio / Mathf.Max(0.01f, lowHpRatio);
            }
            _danger = Mathf.MoveTowards(_danger, target, dt * (dead ? 1.2f : 2f));

            Apply(dt);
        }

        private void Apply(float dt)
        {
            if (_deathVolume != null)
                _deathVolume.weight = _dead ? _danger : 0f;

            float red = HitWeight * hitRedAlpha * _hitBoost;
            float white = _hitHighlight * _hitHighlight * hitHighlightAlpha * _hitBoost;

            Color edgeColor = hitColor;
            if (_dead)
            {
                edgeColor = Color.Lerp(hitColor, deathEdgeColor, _danger);
                red = Mathf.Lerp(red, deathEdgeAlpha, _danger);
            }
            else if (_danger > 0.001f)
            {
                _beatPhase += dt / Mathf.Max(0.1f, Mathf.Lerp(throbSeconds.x, throbSeconds.y, _danger));
                float beat = Throb(_beatPhase % 1f);

                float lowRed = _danger * Mathf.Lerp(lowHpRedAlpha.x, lowHpRedAlpha.y, beat);
                red = Mathf.Max(red, lowRed);
            }
            else
            {
                _beatPhase = 0f;
            }

            SetAlpha(_red, edgeColor, Mathf.Clamp01(red));
            SetAlpha(_highlight, highlightColor, Mathf.Clamp01(white));
        }

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
            _highlight = CreateLayer("WhiteHighlight", _highlightTex);
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
                    edge = 1f - (1f - edge) * (1f - near);
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
            volume.priority = priority;
            volume.weight = 0f;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = objectName + " (runtime)";
            volume.sharedProfile = profile;
            return volume;
        }
    }
}
