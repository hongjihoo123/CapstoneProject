using UnityEngine;

namespace Assets.Members.HJH.Environment
{
    [DefaultExecutionOrder(-50)]
    public class WaterRippleSimulation : MonoBehaviour
    {
        private const int MaxDrops = 32;
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

        private void SyncFromSurface()
        {
            if (waterSurface == null)
                return;

            Bounds b = waterSurface.bounds;
            waterHeight = b.center.y;
            areaMin = new Vector2(b.min.x, b.min.z);
            areaSize = Mathf.Max(b.size.x, b.size.z);
        }

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

            resolution = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(areaSize / 0.1f)), resolution, 2048);

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
            _stepAccumulator += Time.deltaTime * stepsPerSecond;
            int steps = Mathf.Min((int)_stepAccumulator, MaxStepsPerFrame);
            if (steps == 0)
                return;

            _stepAccumulator = Mathf.Min(_stepAccumulator - steps, 1f);

            for (int i = 0; i < _dropCount; i++)
                _drops[i].w /= steps;

            _material.SetVectorArray(DropsId, _drops);
            _material.SetFloat(DropCountId, _dropCount);
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

        public bool IsOverWater(Vector3 position)
        {
            if (!Contains(position))
                return false;

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
