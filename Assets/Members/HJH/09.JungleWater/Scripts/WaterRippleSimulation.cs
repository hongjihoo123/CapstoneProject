using UnityEngine;
using UnityEngine.Rendering;

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
        [Tooltip("물 판(Water). 넣어 두면 아래 높이·영역을 물 판 위치에서 자동으로 맞춘다 (씬에서 옮겨도 OK)")]
        [SerializeField] private Renderer waterSurface;
        [SerializeField] private float waterHeight = 1f;
        [SerializeField] private Vector2 areaMin = Vector2.zero;
        [SerializeField] private float areaSize = 48f;
        [Tooltip("Splash 판정 때 바닥을 찾을 레이어 (캐릭터는 빼기)")]
        [SerializeField] private LayerMask groundMask = ~(1 << 2);

        [Header("Simulation")]
        [SerializeField] private int resolution = 512;
        [Tooltip("초당 계산 횟수. 올리면 물결이 빨리 퍼진다")]
        [SerializeField] private float stepsPerSecond = 60f;
        [Tooltip("물결이 거의 다 가라앉기까지 걸리는 시간 (초). 짧을수록 빨리 잔잔해짐")]
        [SerializeField, Range(0.3f, 4f)] private float settleSeconds = 0.9f;


        private static readonly int RippleTexId = Shader.PropertyToID("_JungleRippleTex");
        private static readonly int RippleAreaId = Shader.PropertyToID("_JungleRippleArea");
        private static readonly int DropsId = Shader.PropertyToID("_Drops");
        private static readonly int DropCountId = Shader.PropertyToID("_DropCount");
        private static readonly int DampingId = Shader.PropertyToID("_Damping");

        private static readonly RaycastHit[] GroundHits = new RaycastHit[8];
        private readonly Vector4[] _drops = new Vector4[MaxDrops];
        private int _dropCount;
        private float _stepAccumulator;
        private CustomRenderTexture _texture;
        private Material _material;

        public float WaterHeight => waterHeight;

        private void OnValidate() => SyncFromSurface();

        // 물 판의 위치·크기 → 물 높이, 시뮬레이션 영역
        private void SyncFromSurface()
        {
            if (waterSurface == null)
                return;

            Bounds b = waterSurface.bounds;
            waterHeight = b.center.y;
            areaMin = new Vector2(b.min.x, b.min.z);
            areaSize = Mathf.Max(b.size.x, b.size.z);
        }

        // 물 판이 연결 안 돼 있으면 물 셰이더(ShallowWater)를 쓰는 가장 큰 판을 찾는다 (씬에서 이름을 바꿔도 동작)
        private void FindSurfaceIfMissing()
        {
            if (waterSurface != null)
                return;

            float best = 0f;
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                Material m = r.sharedMaterial;
                if (m == null || m.shader == null || !m.shader.name.Contains("ShallowWater"))
                    continue;
                float area = r.bounds.size.x * r.bounds.size.z;
                if (area > best)
                {
                    best = area;
                    waterSurface = r;
                }
            }

            if (waterSurface == null)
                Debug.LogWarning($"{name}: 물 셰이더를 쓰는 판을 찾지 못해 기본 영역({areaMin}, {areaSize}m)으로 돕니다.", this);
        }

        private void OnEnable()
        {
            FindSurfaceIfMissing();
            SyncFromSurface();

            // 넓은 맵에서도 물결이 뭉개지지 않게 약 10cm 당 1칸 (512~2048)
            resolution = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(areaSize / 0.1f)), resolution, 2048);

            // 스킬 물튀김은 리스너가 있어야 동작 → 없으면 붙인다
            if (GetComponent<WaterSkillSplashListener>() == null)
                gameObject.AddComponent<WaterSkillSplashListener>();

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
            // settleSeconds 동안 세기가 5% 로 줄도록 한 걸음마다 곱할 값
            float damping = Mathf.Pow(0.05f, 1f / (Mathf.Max(0.1f, settleSeconds) * stepsPerSecond));
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

            // 적·나무 줄기에 먼저 맞을 수 있으니 가장 낮은 지점(= 바닥)을 쓴다
            Vector3 origin = new(position.x, Mathf.Max(position.y, waterHeight) + 2f, position.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, GroundHits, 50f, groundMask, QueryTriggerInteraction.Ignore);
            if (count == 0)
                return position.y <= waterHeight + 0.3f;

            float lowest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
                lowest = Mathf.Min(lowest, GroundHits[i].point.y);
            return lowest < waterHeight;
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
