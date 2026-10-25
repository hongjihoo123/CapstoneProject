using UnityEngine;

namespace Assets.Members.HJH.Environment
{
    // 맵 전체를 덮는 텍스처 한 장에서 물결 파동을 돌린다 (Custom Render Texture + JungleRippleSim.shader).
    // 물 셰이더는 전역 값 _JungleRippleTex / _JungleRippleArea 로 결과를 읽는다.
    [DefaultExecutionOrder(-50)]
    public class WaterRippleSimulation : MonoBehaviour
    {
        private const int MaxDrops = 32;   // 셰이더의 MAX_DROPS 와 같아야 함
        private const int MaxStepsPerFrame = 4;

        [SerializeField] private Shader simShader;

        [Header("Water Area")]
        [SerializeField] private float waterHeight = 1f;
        [SerializeField] private Vector2 areaMin = Vector2.zero;
        [SerializeField] private float areaSize = 48f;
        [Tooltip("Splash 판정 때 바닥을 찾을 레이어 (캐릭터는 빼기)")]
        [SerializeField] private LayerMask groundMask = ~(1 << 2);

        [Header("Simulation")]
        [SerializeField] private int resolution = 512;
        [Tooltip("초당 계산 횟수. 올리면 물결이 빨리 퍼진다")]
        [SerializeField] private float stepsPerSecond = 60f;
        [Range(0.9f, 1f)]
        [SerializeField] private float damping = 0.985f;

        [Header("Fog Reveal")]
        [Tooltip("안개가 이 대상 주변에서 걷힌다")]
        [SerializeField] private Transform revealTarget;

        private static readonly int RippleTexId = Shader.PropertyToID("_JungleRippleTex");
        private static readonly int RippleAreaId = Shader.PropertyToID("_JungleRippleArea");
        private static readonly int PlayerPosId = Shader.PropertyToID("_JunglePlayerPos");
        private static readonly int DropsId = Shader.PropertyToID("_Drops");
        private static readonly int DropCountId = Shader.PropertyToID("_DropCount");
        private static readonly int DampingId = Shader.PropertyToID("_Damping");

        private readonly Vector4[] _drops = new Vector4[MaxDrops];
        private int _dropCount;
        private float _stepAccumulator;
        private CustomRenderTexture _texture;
        private Material _material;

        public float WaterHeight => waterHeight;

        private void OnEnable()
        {
            if (simShader == null)
            {
                Debug.LogError($"{name}: Ripple Sim 셰이더가 비어 있습니다.", this);
                enabled = false;
                return;
            }

            _material = new Material(simShader) { hideFlags = HideFlags.HideAndDontSave };
            _texture = new CustomRenderTexture(resolution, resolution, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear)
            {
                name = "JungleRipple",
                material = _material,
                doubleBuffered = true,
                updateMode = CustomRenderTextureUpdateMode.OnDemand,
                initializationMode = CustomRenderTextureUpdateMode.OnDemand,
                initializationSource = CustomRenderTextureInitializationSource.TextureAndColor,
                initializationColor = Color.clear,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            _texture.Create();
            _texture.Initialize();

            Shader.SetGlobalTexture(RippleTexId, _texture);
            Shader.SetGlobalVector(RippleAreaId, new Vector4(areaMin.x, areaMin.y, 1f / areaSize, 1f / resolution));

            JungleWater.Register(this);
        }

        private void OnDisable()
        {
            JungleWater.Unregister(this);
            Shader.SetGlobalVector(RippleAreaId, Vector4.zero);
            Shader.SetGlobalVector(PlayerPosId, Vector4.zero);

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }

            if (_material != null)
                Destroy(_material);
        }

        private void Update()
        {
            Shader.SetGlobalVector(PlayerPosId, revealTarget != null ? (Vector4)revealTarget.position + new Vector4(0, 0, 0, 1) : Vector4.zero);

            // 프레임레이트와 상관없이 일정한 속도로 퍼지도록 고정 간격으로 계산
            _stepAccumulator += Time.deltaTime * stepsPerSecond;
            int steps = Mathf.Min((int)_stepAccumulator, MaxStepsPerFrame);
            if (steps == 0)
                return;   // 이번 프레임은 계산 없음 → 물방울은 다음 프레임에 들어간다

            _stepAccumulator = Mathf.Min(_stepAccumulator - steps, 1f);

            // 같은 물방울이 계산 횟수만큼 반복해서 들어가므로 나눠 준다
            for (int i = 0; i < _dropCount; i++)
                _drops[i].w /= steps;

            _material.SetVectorArray(DropsId, _drops);
            _material.SetFloat(DropCountId, _dropCount);
            _material.SetFloat(DampingId, damping);
            _texture.Update(steps);

            _dropCount = 0;
        }

        public bool Contains(Vector3 position)
        {
            Vector2 uv = ToUV(position);
            return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }

        // 아래쪽 바닥이 수면보다 낮으면 물 위
        public bool IsOverWater(Vector3 position)
        {
            if (!Contains(position))
                return false;

            Vector3 origin = new(position.x, Mathf.Max(position.y, waterHeight) + 2f, position.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 50f, groundMask, QueryTriggerInteraction.Ignore))
                return hit.point.y < waterHeight;

            return position.y <= waterHeight + 0.3f;
        }

        public void AddDrop(Vector3 position, float radius, float strength)
        {
            if (_dropCount >= MaxDrops || !Contains(position))
                return;

            Vector2 uv = ToUV(position);
            float radiusUV = Mathf.Max(radius / areaSize, 1.5f / resolution);
            _drops[_dropCount++] = new Vector4(uv.x, uv.y, radiusUV, strength);
        }

        private Vector2 ToUV(Vector3 position) =>
            (new Vector2(position.x, position.z) - areaMin) / areaSize;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            var center = new Vector3(areaMin.x + areaSize * 0.5f, waterHeight, areaMin.y + areaSize * 0.5f);
            Gizmos.DrawWireCube(center, new Vector3(areaSize, 0f, areaSize));
        }
    }
}
