using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.JJH._02_Scripts.Agents;
using Members.KYR._01.Scripts.Player;
using RobotWeapons;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 공격 범위를 바닥에 그려주는 거. 플레이어/적 둘 다 씀
    //  - 플레이어(씬에 하나): listenToBus 켜기 -> AttackAreaBus 로 올라오는 거 전부 그림 + 스킬 조준 미리보기
    //  - 적(EnemySkillModule): listenToBus 끄기 -> 버스는 안 듣고, 자기가 Show() 로 넘긴 것만 그림
    //    (적마다 버스 들으면 적 수만큼 같은 선이 겹쳐 그려짐)
    //    예) AreaVisualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning, 위치, 반지름, 경고시간));
    // owner 비워두면 부모에서 Agent 찾아서 씀 (바닥 높이 기준)
    //
    // 그리는 방식 (셰이더 HJH/AreaRim 의 _Style)
    //  - 플레이어 조준: 파란 얇은 글로우 테두리 + 아주 옅은 채움 + 안쪽 호 가이드 + 다이아몬드 마커 + 발밑 링
    //    목(Path)과 부채꼴(Cone)이 이어져 있으면 부채꼴 꼭짓점을 뒤로 당겨서 테두리 하나로 이어지게 함
    //  - 적 예고: 빨강/분홍 얇은 테두리 + 옅은 채움 + 경고 시간 동안 안쪽에서 차오르는 충전 + 막바지 깜빡임
    //  - 디버그(F1): 예전 테두리
    public class AttackAreaVisualizer : MonoBehaviour, ISkillPreview
    {
        private const int CircleSegments = 40;

        private enum RimStyle { Debug = 0, Aim = 1, Enemy = 2 }

        private struct Shape
        {
            public bool IsCircle;
            public bool IsSegment;  // open two-point line
            public bool IsThin;     // drawn with guideLineWidth instead of lineWidth
            public bool IsCone;     // Center = apex, Yaw = facing, Size.x = radius, Size.y = full angle
            public Vector3 Center;
            public float Yaw;
            public Vector3 Size;
            public Vector3 End;     // segment end (segment starts at Center)
            public Color Color;
            public bool HasGround;  // true 면 GroundY 높이에 고정 (주인이 날아올라도 안 따라감)
            public float GroundY;
            public RimStyle Style;
            public float Progress;  // 적 예고 충전 0~1
            public float Neck;      // 부채꼴: 목과 잇느라 꼭짓점을 뒤로 당긴 거리 (원래 꼭짓점 = Center + 앞 * Neck)
        }

        private struct TimedShape
        {
            public Shape Shape;
            public AttackAreaKind Kind;
            public float StartTime;
            public float ExpireTime;

            public bool HasFollow;
            public Transform Follow;
            public Vector3 LocalOffset;
            public float YawOffset;
            public bool FollowRotation;
        }

        // 셰이더에 넘길 그룹 하나의 생김새
        private struct RimLook
        {
            public RimStyle Style;
            public Color Color;
            public float LineWidth;
            public float GlowWidth;
            public float GlowStrength;
            public float OuterGlow;
            public float Fill;
        }

        [SerializeField, FormerlySerializedAs("player")]
        private Agent owner;
        [SerializeField]
        private bool listenToBus = true;
        [SerializeField] private float groundOffset = 0.06f;
        [SerializeField, Tooltip("끄면(기본) Show 한 순간 그 자리의 실제 바닥 높이에 고정 → 주인이 공중으로 떠도 범위는 바닥에 남음.\n켜면 예전처럼 매 프레임 주인 높이를 따라감")]
        private bool followOwnerHeight;
        [SerializeField, Tooltip("바닥 찾을 때 아래로 쏘는 레이 길이")] private float groundProbeDistance = 50f;
        [SerializeField, Tooltip("디버그 판정 테두리 두께")] private float lineWidth = 0.08f;
        [SerializeField] private float guideLineWidth = 0.03f;
        [SerializeField, Tooltip("Debug: draw weapon/skill hit areas. F1 toggles in play mode. Aim previews and enemy warnings are always drawn.")]
        private bool showHitAreas;

        [Header("디버그 판정 테두리 (F1)")]
        [SerializeField, Tooltip("테두리 안쪽으로 은은하게 퍼지는 빛의 폭 (m)")] private float rimGlowWidth = 0.5f;
        [SerializeField, Range(0f, 1f), Tooltip("안쪽 빛 세기 (0 = 테두리 선만)")] private float rimGlowStrength = 0.35f;
        [SerializeField, Range(0f, 1f), Tooltip("나무·바위 뒤에 가려진 부분의 진하기. 앞에 가리는 게 없으면 그대로")]
        private float occludedAlpha = 0.25f;

        [Header("플레이어 조준 미리보기")]
        [SerializeField, Tooltip("테두리·마커·링 색")] private Color aimColor = new(0.35f, 0.72f, 1f, 1f);
        [SerializeField, Tooltip("테두리 선 두께 (m)")] private float aimLineWidth = 0.045f;
        [SerializeField, Range(0f, 0.5f), Tooltip("안쪽 전체 채움 진하기")] private float aimFillAlpha = 0.06f;
        [SerializeField, Range(0f, 1f), Tooltip("테두리 안쪽 빛 세기")] private float aimInnerGlow = 0.16f;
        [SerializeField, Tooltip("테두리 바깥으로 번지는 빛 폭 (m)")] private float aimOuterGlow = 0.14f;
        [SerializeField, Range(1, 8), Tooltip("안쪽 호 가이드 칸 수 (3 = 호 두 줄)")] private int aimGuideCells = 3;
        [SerializeField, Tooltip("발밑 링 반지름 (0 = 안 그림)")] private float footRingRadius = 0.55f;
        [SerializeField, Tooltip("다이아몬드 마커 크기 (m)")] private float markerSize = 0.26f;
        [SerializeField, Tooltip("방향선 (얇은 선)")] private Color guideColor = new(0.4f, 0.8f, 1f, 0.3f);

        [Header("적 공격 예고 (항상 보임)")]
        [SerializeField] private Color enemyColor = new(1f, 0.25f, 0.42f, 1f);
        [SerializeField, Tooltip("테두리 선 두께 (m)")] private float enemyLineWidth = 0.05f;
        [SerializeField, Range(0f, 0.5f), Tooltip("안쪽 전체 채움 진하기 (충전 전)")] private float enemyFillAlpha = 0.05f;
        [SerializeField, Tooltip("이보다 짧은 예고는 계속 다시 Show 되는 '커지는 경고'라서 충전 대신 가득 찬 걸로 그림 (초)")]
        private float chargeMinDuration = 0.3f;

        [SerializeField, Tooltip("디버그: 무기 휘두르기 준비")] private Color weaponWindupColor = new(1f, 0.65f, 0.2f, 0.5f);
        [SerializeField, Tooltip("디버그: 무기 판정")] private Color weaponHitColor = new(1f, 0.2f, 0.2f, 0.95f);
        [SerializeField, Tooltip("디버그: 스킬 판정")] private Color skillHitColor = new(1f, 0.5f, 0f, 0.95f);

        private readonly List<TimedShape> _timed = new();
        private readonly List<Shape> _frame = new();
        private readonly List<LineRenderer> _pool = new();
        private static Material _material;   // 적마다 하나씩 안 만들게 같이 씀
        private int _used;

        // 범위 테두리: 같은 색·방식 모양을 묶어서 바닥 사각형 하나에 "합친 테두리"로 그림 (셰이더 HJH/AreaRim)
        private const int MaxRimShapes = 8;   // 셰이더 MAX_SHAPES 와 같아야 함
        private const int MaxExtras = 4;      // 셰이더 MAX_EXTRAS 와 같아야 함
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int StyleId = Shader.PropertyToID("_Style");
        private static readonly int LineWidthId = Shader.PropertyToID("_LineWidth");
        private static readonly int GlowWidthId = Shader.PropertyToID("_GlowWidth");
        private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");
        private static readonly int OuterGlowId = Shader.PropertyToID("_OuterGlow");
        private static readonly int FillAlphaId = Shader.PropertyToID("_FillAlpha");
        private static readonly int OccludedAlphaId = Shader.PropertyToID("_OccludedAlpha");
        private static readonly int ShapeCountId = Shader.PropertyToID("_ShapeCount");
        private static readonly int ShapeAId = Shader.PropertyToID("_ShapeA");
        private static readonly int ShapeBId = Shader.PropertyToID("_ShapeB");
        private static readonly int GuideId = Shader.PropertyToID("_Guide");
        private static readonly int GuideInfoId = Shader.PropertyToID("_GuideInfo");
        private static readonly int RingCountId = Shader.PropertyToID("_RingCount");
        private static readonly int RingsId = Shader.PropertyToID("_Rings");
        private static readonly int MarkerCountId = Shader.PropertyToID("_MarkerCount");
        private static readonly int MarkersId = Shader.PropertyToID("_Markers");
        private static Material _rimMaterial;
        private static bool _rimMissing;
        private readonly List<Shape> _rimShapes = new();
        private readonly List<bool> _rimDone = new();
        private readonly List<Mesh> _rimMeshes = new();
        private readonly List<MaterialPropertyBlock> _rimBlocks = new();
        private readonly Vector4[] _shapeA = new Vector4[MaxRimShapes];
        private readonly Vector4[] _shapeB = new Vector4[MaxRimShapes];
        private readonly Vector3[] _quad = new Vector3[4];

        // 조준 미리보기 (매 프레임 새로 받음)
        private readonly List<Shape> _aimShapes = new();
        private readonly List<Vector4> _aimRangeRings = new();   // xz 중심, 반지름
        private Vector4 _guide;
        private Vector4 _guideInfo = new(-1f, 0f, 0f, 0f);
        private int _ringCount;
        private readonly Vector4[] _rings = new Vector4[MaxExtras];
        private int _markerCount;
        private readonly Vector4[] _markers = new Vector4[MaxExtras];

        private float _nextOwnerSearch;

        // 플레이어 스킬 대신 매 프레임 조준 미리보기를 채워 넣는 곳 (데모·튜토리얼). null 이면 플레이어 스킬 조준
        public System.Action<ISkillPreview> PreviewOverride { get; set; }

        private void Awake() => ResolveOwner();

        // 비어 있으면 부모 Agent -> (플레이어용이면) 씬의 플레이어 순서로 찾음.
        // 플레이어용 Visualizer 는 보통 플레이어 밖에 따로 놓여 있어서 부모에서 못 찾음 -> 조준 미리보기가 안 뜨던 원인
        private void ResolveOwner()
        {
            if (owner != null)
                return;

            owner = GetComponentInParent<Agent>();
            if (owner == null && listenToBus)
                owner = FindFirstObjectByType<PlayerAgent>();
        }

        private void OnEnable()
        {
            if (listenToBus)
                AttackAreaBus.Raised += Show;
        }

        private void OnDisable()
        {
            AttackAreaBus.Raised -= Show;
            _timed.Clear();
            HideUnused(0);
        }

        private void Update()
        {
            // F1 디버그 토글은 버스 듣는 애(플레이어 쪽)만
            if (!listenToBus)
                return;

            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                showHitAreas = !showHitAreas;
                _timed.RemoveAll(timed => IsDebug(timed.Kind));   // 적 예고는 남겨둠
            }
        }

        private void LateUpdate()
        {
            _frame.Clear();
            _aimShapes.Clear();
            _aimRangeRings.Clear();

            for (int i = _timed.Count - 1; i >= 0; i--)
            {
                TimedShape timed = _timed[i];

                if (Time.time > timed.ExpireTime || (timed.HasFollow && timed.Follow == null))
                {
                    _timed.RemoveAt(i);
                    continue;
                }

                if (timed.HasFollow)
                    ApplyFollow(ref timed);

                if (timed.Shape.Style == RimStyle.Enemy)
                {
                    float duration = timed.ExpireTime - timed.StartTime;
                    timed.Shape.Progress = duration < chargeMinDuration
                        ? 1f
                        : Mathf.Clamp01((Time.time - timed.StartTime) / duration);
                }

                _timed[i] = timed;
                _frame.Add(timed.Shape);
            }

            // 플레이어가 나중에 생기거나 바뀌는 씬도 있어서, 비었으면 1초에 한 번 다시 찾아봄
            if (owner == null && listenToBus && Time.unscaledTime >= _nextOwnerSearch)
            {
                _nextOwnerSearch = Time.unscaledTime + 1f;
                ResolveOwner();
            }

            // 스킬 조준 미리보기는 플레이어만 (PreviewOverride 가 있으면 그걸로: 데모·튜토리얼용)
            if (PreviewOverride != null)
                PreviewOverride(this);
            else if (owner is PlayerAgent player && player.SkillFsm != null && player.SkillFsm.IsAimingSkill)
                player.SkillFsm.DescribeAimPreview(this);

            // 얇은 보조선(방향선)은 선으로, 범위(원·사각형·부채꼴)는 합친 테두리로
            _used = 0;
            _rimShapes.Clear();
            bool rims = RimMaterial != null;
            foreach (Shape shape in _frame)
            {
                if (shape.IsThin || shape.IsSegment || !rims)
                    Draw(shape);
                else
                    _rimShapes.Add(shape);
            }

            int groups = DrawRims();
            DrawAim(groups, rims);
            HideUnused(_used);
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in _rimMeshes)
                if (mesh != null)
                    Destroy(mesh);
            _rimMeshes.Clear();
        }

        private static Material RimMaterial
        {
            get
            {
                if (_rimMaterial != null || _rimMissing)
                    return _rimMaterial;
                Shader shader = Shader.Find("HJH/AreaRim");
                if (shader == null)
                {
                    _rimMissing = true;   // 셰이더 없으면 예전 선으로
                    return null;
                }
                _rimMaterial = new Material(shader) { name = "AreaRim (runtime)", hideFlags = HideFlags.HideAndDontSave };
                return _rimMaterial;
            }
        }

        private RimLook LookFor(RimStyle style, Color color)
        {
            switch (style)
            {
                case RimStyle.Aim:
                    return new RimLook { Style = style, Color = color, LineWidth = aimLineWidth, GlowWidth = 0.45f,
                                         GlowStrength = aimInnerGlow, OuterGlow = aimOuterGlow, Fill = aimFillAlpha };
                case RimStyle.Enemy:
                    return new RimLook { Style = style, Color = color, LineWidth = enemyLineWidth, GlowWidth = 0.35f,
                                         GlowStrength = 0.1f, OuterGlow = 0.12f, Fill = enemyFillAlpha };
                default:
                    return new RimLook { Style = style, Color = color, LineWidth = lineWidth, GlowWidth = rimGlowWidth,
                                         GlowStrength = rimGlowStrength, OuterGlow = 0.001f, Fill = 0f };
            }
        }

        // 색·방식·높이가 같은 모양끼리 묶어서 한 번에 그림 → 겹친 곳은 안쪽 선이 사라지고 바깥 테두리만 이어짐
        // 반환 = 쓴 그룹 수 (다음 그룹 번호)
        private int DrawRims()
        {
            _rimDone.Clear();
            for (int i = 0; i < _rimShapes.Count; i++)
                _rimDone.Add(false);

            int group = 0;
            for (int i = 0; i < _rimShapes.Count; i++)
            {
                if (_rimDone[i])
                    continue;

                Shape first = _rimShapes[i];
                float y = GroundY(first);
                var min = new Vector2(float.MaxValue, float.MaxValue);
                var max = new Vector2(float.MinValue, float.MinValue);
                int count = 0;

                for (int j = i; j < _rimShapes.Count && count < MaxRimShapes; j++)
                {
                    Shape shape = _rimShapes[j];
                    if (_rimDone[j] || shape.Style != first.Style || !SameColor(shape.Color, first.Color) ||
                        Mathf.Abs(GroundY(shape) - y) > 0.5f)
                        continue;

                    _rimDone[j] = true;
                    float reach = Pack(shape, count++);
                    Encapsulate(ref min, ref max, shape.Center, reach);
                }

                ClearExtras();
                RenderRim(group++, LookFor(first.Style, first.Color), y, count, min, max);
            }
            return group;
        }

        private static void Encapsulate(ref Vector2 min, ref Vector2 max, Vector3 center, float reach)
        {
            min = Vector2.Min(min, new Vector2(center.x - reach, center.z - reach));
            max = Vector2.Max(max, new Vector2(center.x + reach, center.z + reach));
        }

        // 모양 하나를 셰이더 배열 칸에 넣음. 반환 = 중심에서 가장 먼 곳까지 거리 (그릴 사각형 크기용)
        private float Pack(Shape shape, int index)
        {
            float yaw = shape.Yaw * Mathf.Deg2Rad;
            if (shape.IsCircle)
            {
                _shapeA[index] = new Vector4(0f, shape.Center.x, shape.Center.z, 0f);
                _shapeB[index] = new Vector4(shape.Size.x, 0f, shape.Progress, 0f);
                return shape.Size.x;
            }
            if (shape.IsCone)
            {
                _shapeA[index] = new Vector4(2f, shape.Center.x, shape.Center.z, yaw);
                _shapeB[index] = new Vector4(shape.Size.x, shape.Size.y * 0.5f * Mathf.Deg2Rad, shape.Progress, 0f);
                return shape.Size.x;
            }

            float hx = shape.Size.x * 0.5f;
            float hz = shape.Size.z * 0.5f;
            _shapeA[index] = new Vector4(1f, shape.Center.x, shape.Center.z, yaw);
            _shapeB[index] = new Vector4(hx, hz, shape.Progress, 0f);
            return Mathf.Sqrt(hx * hx + hz * hz);
        }

        private void ClearExtras()
        {
            _guideInfo = new Vector4(-1f, 0f, 0f, 0f);
            _ringCount = 0;
            _markerCount = 0;
        }

        private void RenderRim(int index, RimLook look, float y, int count, Vector2 min, Vector2 max)
        {
            if (count == 0 && _ringCount == 0)
                return;

            float margin = look.LineWidth + look.OuterGlow * 4f + 0.1f;   // 테두리·바깥 빛이 사각형 밖으로 안 잘리게
            min -= Vector2.one * margin;
            max += Vector2.one * margin;

            Mesh mesh = RentRimMesh(index);
            _quad[0] = new Vector3(min.x, y, min.y);
            _quad[1] = new Vector3(min.x, y, max.y);
            _quad[2] = new Vector3(max.x, y, max.y);
            _quad[3] = new Vector3(max.x, y, min.y);
            mesh.SetVertices(_quad);
            mesh.RecalculateBounds();

            for (int i = count; i < MaxRimShapes; i++)
            {
                _shapeA[i] = Vector4.zero;
                _shapeB[i] = Vector4.zero;
            }

            MaterialPropertyBlock block = RentRimBlock(index);
            block.SetColor(ColorId, look.Color);
            block.SetFloat(StyleId, (float)look.Style);
            block.SetFloat(LineWidthId, look.LineWidth);
            block.SetFloat(GlowWidthId, look.GlowWidth);
            block.SetFloat(GlowStrengthId, look.GlowStrength);
            block.SetFloat(OuterGlowId, look.OuterGlow);
            block.SetFloat(FillAlphaId, look.Fill);
            block.SetFloat(OccludedAlphaId, occludedAlpha);
            block.SetFloat(ShapeCountId, count);
            block.SetVectorArray(ShapeAId, _shapeA);
            block.SetVectorArray(ShapeBId, _shapeB);
            block.SetVector(GuideId, _guide);
            block.SetVector(GuideInfoId, _guideInfo);
            block.SetFloat(RingCountId, _ringCount);
            block.SetVectorArray(RingsId, _rings);
            block.SetFloat(MarkerCountId, _markerCount);
            block.SetVectorArray(MarkersId, _markers);

            var renderParams = new RenderParams(RimMaterial)
            {
                matProps = block,
                layer = gameObject.layer,
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false
            };
            Graphics.RenderMesh(renderParams, mesh, 0, Matrix4x4.identity);
        }

        private Mesh RentRimMesh(int index)
        {
            while (_rimMeshes.Count <= index)
            {
                var mesh = new Mesh { name = "AreaRim", hideFlags = HideFlags.HideAndDontSave };
                mesh.MarkDynamic();
                mesh.SetVertices(_quad);
                mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
                _rimMeshes.Add(mesh);
            }
            return _rimMeshes[index];
        }

        private MaterialPropertyBlock RentRimBlock(int index)
        {
            while (_rimBlocks.Count <= index)
                _rimBlocks.Add(new MaterialPropertyBlock());
            return _rimBlocks[index];
        }

        private static bool SameColor(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f &&
            Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;

        #region 조준 미리보기

        // 이번 프레임에 스킬이 넘겨준 모양들을 하나의 실루엣 + 가이드·마커·링으로 그림
        private void DrawAim(int groupIndex, bool rims)
        {
            if (_aimShapes.Count == 0 && _aimRangeRings.Count == 0)
            {
                return;
            }

            Vector3 origin = owner != null ? owner.transform.position
                : _aimShapes.Count > 0 ? _aimShapes[0].Center : new Vector3(_aimRangeRings[0].x, 0f, _aimRangeRings[0].z);

            if (!rims)
            {
                // 셰이더가 없으면 예전 선으로라도
                foreach (Shape shape in _aimShapes)
                    Draw(shape);
                return;
            }

            JoinNecks();

            var aimGround = new Shape { Center = origin };
            float y = GroundY(aimGround);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            int count = Mathf.Min(_aimShapes.Count, MaxRimShapes);
            for (int i = 0; i < count; i++)
                Encapsulate(ref min, ref max, _aimShapes[i].Center, Pack(_aimShapes[i], i));

            ClearExtras();
            int guide = PickGuideShape(count);
            if (guide >= 0)
                BuildGuide(guide, origin);

            if (footRingRadius > 0f)
                AddRing(origin, footRingRadius, 1f);
            foreach (Vector4 ring in _aimRangeRings)
                AddRing(new Vector3(ring.x, 0f, ring.z), ring.w, 0.35f);

            for (int i = 0; i < _ringCount; i++)
                Encapsulate(ref min, ref max, new Vector3(_rings[i].x, 0f, _rings[i].y), _rings[i].z);

            RenderRim(groupIndex, LookFor(RimStyle.Aim, aimColor), y, count, min, max);
        }

        // 목(Path) 앞 끝에 부채꼴 꼭짓점이 붙어 있으면, 부채꼴 양 옆선이 목의 앞 모서리를 지나도록 꼭짓점을 뒤로 당김
        // → 합집합 테두리가 "목 → 부채꼴" 하나로 이어짐 (당긴 만큼 반지름도 늘려서 호 위치는 그대로)
        private void JoinNecks()
        {
            for (int c = 0; c < _aimShapes.Count; c++)
            {
                Shape cone = _aimShapes[c];
                if (!cone.IsCone || cone.Neck > 0f)
                    continue;

                float halfAngle = cone.Size.y * 0.5f;
                if (halfAngle < 1f || halfAngle >= 89f)
                    continue;

                Vector3 forward = Quaternion.Euler(0f, cone.Yaw, 0f) * Vector3.forward;
                for (int b = 0; b < _aimShapes.Count; b++)
                {
                    Shape box = _aimShapes[b];
                    if (box.IsCircle || box.IsCone || box.IsSegment)
                        continue;

                    Vector3 boxForward = Quaternion.Euler(0f, box.Yaw, 0f) * Vector3.forward;
                    Vector3 front = box.Center + boxForward * (box.Size.z * 0.5f);
                    Vector3 gap = front - cone.Center;
                    gap.y = 0f;
                    if (gap.magnitude > 0.3f || Vector3.Angle(boxForward, forward) > 5f)
                        continue;

                    float back = Mathf.Min(box.Size.x * 0.5f / Mathf.Tan(halfAngle * Mathf.Deg2Rad), box.Size.z);
                    cone.Center = front - forward * back;
                    cone.Size.x += back;
                    cone.Neck = back;
                    _aimShapes[c] = cone;
                    break;
                }
            }
        }

        // 가이드(호·마커)를 붙일 주인공 모양: 부채꼴 > 원 > 사각형 (나중에 넘긴 것 우선)
        private int PickGuideShape(int count)
        {
            int circle = -1, box = -1;
            for (int i = count - 1; i >= 0; i--)
            {
                Shape shape = _aimShapes[i];
                if (shape.IsCone)
                    return i;
                if (shape.IsCircle && circle < 0)
                    circle = i;
                else if (!shape.IsCircle && box < 0)
                    box = i;
            }
            return circle >= 0 ? circle : box;
        }

        private void BuildGuide(int index, Vector3 origin)
        {
            Shape shape = _aimShapes[index];
            Vector3 forward = Quaternion.Euler(0f, shape.Yaw, 0f) * Vector3.forward;

            if (shape.IsCone)
            {
                float halfAngle = shape.Size.y * 0.5f;
                _guide = new Vector4(shape.Center.x, shape.Center.z, shape.Neck, shape.Size.x);
                _guideInfo = new Vector4(index, aimGuideCells, shape.Yaw * Mathf.Deg2Rad,
                                         halfAngle < 80f ? halfAngle * Mathf.Deg2Rad : 0f);
                AddMarker(shape.Center + forward * shape.Neck, shape.Yaw);       // 꼭짓점(목과 이어지는 곳)
                AddMarker(shape.Center + forward * shape.Size.x, shape.Yaw);     // 호 가운데
                return;
            }

            if (shape.IsCircle)
            {
                _guide = new Vector4(shape.Center.x, shape.Center.z, 0f, shape.Size.x);
                _guideInfo = new Vector4(index, aimGuideCells, 0f, 0f);
                Vector3 toCenter = shape.Center - origin;
                toCenter.y = 0f;
                if (toCenter.sqrMagnitude > 0.01f)
                    AddMarker(shape.Center, Quaternion.LookRotation(toCenter).eulerAngles.y);   // 떨어진 원이면 중심에
                return;
            }

            // 사각형(직선): 뒤 끝에서 앞으로 일정 간격 호 눈금, 앞 끝에 마커
            float length = shape.Size.z;
            Vector3 back = shape.Center - forward * (length * 0.5f);
            _guide = new Vector4(back.x, back.z, 0f, length);
            _guideInfo = new Vector4(index, Mathf.Clamp(Mathf.Round(length / 2f), 2f, 6f), 0f, 0f);
            AddMarker(shape.Center + forward * (length * 0.5f), shape.Yaw);
        }

        private void AddRing(Vector3 center, float radius, float strength)
        {
            if (_ringCount >= MaxExtras)
                return;
            _rings[_ringCount++] = new Vector4(center.x, center.z, radius, strength);
        }

        private void AddMarker(Vector3 position, float yawDegrees)
        {
            if (_markerCount >= MaxExtras || markerSize <= 0f)
                return;
            _markers[_markerCount++] = new Vector4(position.x, position.z, markerSize, yawDegrees * Mathf.Deg2Rad);
        }

        void ISkillPreview.Circle(Vector3 center, float radius) =>
            _aimShapes.Add(new Shape { IsCircle = true, Center = center, Size = new Vector3(radius, 0f, 0f), Color = aimColor, Style = RimStyle.Aim });

        void ISkillPreview.Path(Vector3 from, Vector3 to, float width)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            float length = direction.magnitude;
            if (length < 0.01f)
                return;

            _aimShapes.Add(new Shape
            {
                Center = (from + to) * 0.5f,
                Yaw = Quaternion.LookRotation(direction / length).eulerAngles.y,
                Size = new Vector3(width, 0f, length),
                Color = aimColor,
                Style = RimStyle.Aim
            });
        }

        void ISkillPreview.RangeCircle(Vector3 center, float radius) =>
            _aimRangeRings.Add(new Vector4(center.x, 0f, center.z, radius));

        void ISkillPreview.Cone(Vector3 apex, Vector3 direction, float radius, float angle)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;

            _aimShapes.Add(new Shape
            {
                IsCone = true,
                Center = apex,
                Yaw = Quaternion.LookRotation(direction.normalized).eulerAngles.y,
                Size = new Vector3(radius, angle, 0f),
                Color = aimColor,
                Style = RimStyle.Aim
            });
        }

        void ISkillPreview.DirectionLine(Vector3 from, Vector3 to) =>
            _frame.Add(new Shape { IsSegment = true, IsThin = true, Center = from, End = to, Color = guideColor });

        #endregion

        // 이 Visualizer 에 범위 하나 그림. 버스 듣는 애는 버스에서 자동으로 불림, 적은 직접 부르면 됨
        public void Show(AttackArea area) => Show(area, null, false);

        public void Show(AttackArea area, Transform follow, bool followRotation = true)
        {
            if (IsDebug(area.Kind) && !showHitAreas)
                return;

            if (area.Kind == AttackAreaKind.WeaponWindup || area.Kind == AttackAreaKind.WeaponHit)
                _timed.RemoveAll(timed => timed.Kind == area.Kind);
            else if (area.Kind == AttackAreaKind.Skill)
                _timed.RemoveAll(timed => timed.Kind == area.Kind && Mathf.Approximately(timed.Shape.Size.x, area.Size.x) && Time.time - timed.StartTime < 0.1f);

            float yaw = area.Rotation.eulerAngles.y;
            var timedShape = new TimedShape
            {
                Kind = area.Kind,
                StartTime = Time.time,
                ExpireTime = Time.time + area.Duration,
                Shape = new Shape
                {
                    IsCircle = area.Shape == AttackAreaShape.Circle,
                    IsCone = area.Shape == AttackAreaShape.Cone,
                    Center = area.Center,
                    Yaw = yaw,
                    Size = area.Size,
                    Color = area.HasColor ? area.Color : GetColor(area.Kind),
                    HasGround = !followOwnerHeight,
                    GroundY = followOwnerHeight ? 0f : FindGroundY(area.Center),
                    Style = area.Kind == AttackAreaKind.EnemyWarning ? RimStyle.Enemy : RimStyle.Debug
                }
            };

            if (follow != null)
            {
                timedShape.HasFollow = true;
                timedShape.Follow = follow;
                timedShape.FollowRotation = followRotation;
                timedShape.LocalOffset = followRotation
                    ? Quaternion.Inverse(follow.rotation) * (area.Center - follow.position)
                    : area.Center - follow.position;
                timedShape.YawOffset = yaw - follow.eulerAngles.y;
            }

            _timed.Add(timedShape);
        }

        private static readonly RaycastHit[] GroundHits = new RaycastHit[16];

        // point 바로 아래의 바닥 높이. 캐릭터(Agent) 콜라이더는 무시함 (적/플레이어 몸 위에 그려지면 안 되니까)
        // 바닥을 못 찾으면 지금 주인 높이 (그것도 없으면 point 높이)
        private float FindGroundY(Vector3 point)
        {
            float fallback = owner != null ? owner.transform.position.y : point.y;
            int count = Physics.RaycastNonAlloc(point + Vector3.up * 0.5f, Vector3.down, GroundHits,
                                                groundProbeDistance, ~0, QueryTriggerInteraction.Ignore);

            float bestDistance = float.MaxValue;
            float groundY = fallback;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = GroundHits[i];
                if (hit.distance >= bestDistance || hit.collider.GetComponentInParent<Agent>() != null)
                    continue;

                bestDistance = hit.distance;
                groundY = hit.point.y;
            }

            return groundY;
        }

        private Color GetColor(AttackAreaKind kind)
        {
            switch (kind)
            {
                case AttackAreaKind.WeaponWindup: return weaponWindupColor;
                case AttackAreaKind.WeaponHit: return weaponHitColor;
                case AttackAreaKind.EnemyWarning: return enemyColor;
                default: return skillHitColor;
            }
        }

        // F1 로 켜고 끄는 디버그용 종류인지 (적 예고는 항상 보임)
        private static bool IsDebug(AttackAreaKind kind) => kind != AttackAreaKind.EnemyWarning;

        private float GroundY(Shape shape) =>
            (shape.HasGround ? shape.GroundY
                : owner != null ? owner.transform.position.y : shape.Center.y) + groundOffset;

        public void Translate(Vector3 delta)
        {
            for (int i = 0; i < _timed.Count; i++)
            {
                TimedShape timed = _timed[i];
                timed.Shape.Center += delta;

                if (timed.Shape.HasGround)
                    timed.Shape.GroundY = FindGroundY(timed.Shape.Center);

                _timed[i] = timed;
            }
        }

        private void ApplyFollow(ref TimedShape timed)
        {
            Transform follow = timed.Follow;
            Vector3 center = timed.FollowRotation
                ? follow.position + follow.rotation * timed.LocalOffset
                : follow.position + timed.LocalOffset;

            timed.Shape.Center = center;

            if (timed.FollowRotation)
                timed.Shape.Yaw = follow.eulerAngles.y + timed.YawOffset;

            // 밀려서 경사/단차 위로 이동해도 바닥에 붙도록 다시 찾음
            if (timed.Shape.HasGround)
                timed.Shape.GroundY = FindGroundY(center);
        }

        // 셰이더가 없을 때·얇은 보조선용 LineRenderer
        private void Draw(Shape shape)
        {
            LineRenderer line = Rent();
            float y = GroundY(shape);

            line.widthMultiplier = shape.IsThin ? guideLineWidth : lineWidth;
            line.loop = !shape.IsSegment;

            if (shape.IsSegment)
            {
                line.positionCount = 2;
                line.SetPosition(0, new Vector3(shape.Center.x, y, shape.Center.z));
                line.SetPosition(1, new Vector3(shape.End.x, y, shape.End.z));
            }
            else if (shape.IsCone)
            {
                // 꼭짓점 -> 호(arc) 따라 쭉 -> 다시 꼭짓점 (loop 라서 마지막 변은 자동으로 닫힘)
                const int arcSegments = 24;
                Vector3 apex = new Vector3(shape.Center.x, y, shape.Center.z);
                float half = shape.Size.y * 0.5f;
                line.positionCount = arcSegments + 2;
                line.SetPosition(0, apex);
                for (int i = 0; i <= arcSegments; i++)
                {
                    float yaw = shape.Yaw - half + shape.Size.y * i / arcSegments;
                    line.SetPosition(i + 1, apex + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * shape.Size.x);
                }
            }
            else if (shape.IsCircle)
            {
                line.positionCount = CircleSegments;
                for (int i = 0; i < CircleSegments; i++)
                {
                    float angle = i / (float)CircleSegments * Mathf.PI * 2f;
                    line.SetPosition(i, new Vector3(
                        shape.Center.x + Mathf.Cos(angle) * shape.Size.x, y,
                        shape.Center.z + Mathf.Sin(angle) * shape.Size.x));
                }
            }
            else
            {
                Quaternion yaw = Quaternion.Euler(0f, shape.Yaw, 0f);
                float hx = shape.Size.x * 0.5f;
                float hz = shape.Size.z * 0.5f;
                Vector3 center = new Vector3(shape.Center.x, y, shape.Center.z);

                line.positionCount = 4;
                line.SetPosition(0, center + yaw * new Vector3(-hx, 0f, -hz));
                line.SetPosition(1, center + yaw * new Vector3(hx, 0f, -hz));
                line.SetPosition(2, center + yaw * new Vector3(hx, 0f, hz));
                line.SetPosition(3, center + yaw * new Vector3(-hx, 0f, hz));
            }

            line.startColor = shape.Color;
            line.endColor = shape.Color;
        }

        private LineRenderer Rent()
        {
            if (_used == _pool.Count)
                _pool.Add(CreateLine());

            LineRenderer line = _pool[_used++];
            line.gameObject.SetActive(true);
            return line;
        }

        private void HideUnused(int from)
        {
            for (int i = from; i < _pool.Count; i++)
                _pool[i].gameObject.SetActive(false);
        }

        public void Hide()
        {
            _timed.Clear();
            _frame.Clear();
            HideUnused(0);
        }

        public void Hide(AttackAreaKind kind)
        {
            _timed.RemoveAll(timed => timed.Kind == kind);
            _frame.Clear();
            HideUnused(0);
        }

        private LineRenderer CreateLine()
        {
            if (_material == null)
            {
                // 보이는 부분은 그대로, 나무·바위 뒤에 가려진 부분만 연하게 (HJH/AimLine). 없으면 예전 셰이더
                Shader shader = Shader.Find("HJH/AimLine") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                _material = new Material(shader);
                if (_material.HasProperty(OccludedAlphaId))
                    _material.SetFloat(OccludedAlphaId, occludedAlpha);
            }

            var go = new GameObject("AreaLine");
            go.transform.SetParent(transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.widthMultiplier = lineWidth;
            line.material = _material;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        public void HideEnemyWarning()
        {
            _timed.RemoveAll(timed => timed.Kind == AttackAreaKind.EnemyWarning);
        }

        private void OnDrawGizmos()
        {
            foreach (Shape shape in _frame)
            {
                Gizmos.color = shape.Color;
                float y = GroundY(shape);
                var center = new Vector3(shape.Center.x, y, shape.Center.z);

                if (shape.IsSegment)
                {
                    Gizmos.DrawLine(center, new Vector3(shape.End.x, y, shape.End.z));
                    continue;
                }

                if (shape.IsCircle)
                {
                    Gizmos.DrawWireSphere(center, shape.Size.x);
                    continue;
                }

                if (shape.IsCone)
                {
                    float half = shape.Size.y * 0.5f;
                    Gizmos.DrawLine(center, center + Quaternion.Euler(0f, shape.Yaw - half, 0f) * Vector3.forward * shape.Size.x);
                    Gizmos.DrawLine(center, center + Quaternion.Euler(0f, shape.Yaw + half, 0f) * Vector3.forward * shape.Size.x);
                    continue;
                }

                Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, shape.Yaw, 0f), Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(shape.Size.x, 0.05f, shape.Size.z));
                Gizmos.matrix = Matrix4x4.identity;
            }
        }
    }
}
