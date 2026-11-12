using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.JJH._02_Scripts.Agents;
using Members.KYR._01.Scripts.Player;
using RobotWeapons;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 공격 범위 바닥에 선으로 그려주는 거. 플레이어/적 둘 다 씀
    //  - 플레이어(씬에 하나): listenToBus 켜기 -> AttackAreaBus 로 올라오는 거 전부 그림 + 스킬 조준 미리보기
    //  - 적(EnemySkillModule): listenToBus 끄기 -> 버스는 안 듣고, 자기가 Show() 로 넘긴 것만 그림
    //    (적마다 버스 들으면 적 수만큼 같은 선이 겹쳐 그려짐)
    //    예) AreaVisualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning, 위치, 반지름, 경고시간));
    // owner 비워두면 부모에서 Agent 찾아서 씀 (바닥 높이 기준)
    public class AttackAreaVisualizer : MonoBehaviour, ISkillPreview
    {
        private const int CircleSegments = 40;

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

        [SerializeField, FormerlySerializedAs("player")]
        private Agent owner;
        [SerializeField]
        private bool listenToBus = true;
        [SerializeField] private float groundOffset = 0.06f;
        [SerializeField, Tooltip("끄면(기본) Show 한 순간 그 자리의 실제 바닥 높이에 고정 → 주인이 공중으로 떠도 범위는 바닥에 남음.\n켜면 예전처럼 매 프레임 주인 높이를 따라감")]
        private bool followOwnerHeight;
        [SerializeField, Tooltip("바닥 찾을 때 아래로 쏘는 레이 길이")] private float groundProbeDistance = 50f;
        [SerializeField] private float lineWidth = 0.08f;
        [SerializeField] private float guideLineWidth = 0.03f;
        [SerializeField, Tooltip("Debug: draw weapon/skill hit areas. F1 toggles in play mode. Aim previews and enemy warnings are always drawn.")]
        private bool showHitAreas;

        [Header("범위 테두리 (원·사각형·부채꼴)")]
        [SerializeField, Tooltip("테두리 안쪽으로 은은하게 퍼지는 빛의 폭 (m)")] private float rimGlowWidth = 0.5f;
        [SerializeField, Range(0f, 1f), Tooltip("안쪽 빛 세기 (0 = 테두리 선만)")] private float rimGlowStrength = 0.35f;
        [SerializeField, Range(0f, 1f), Tooltip("나무·바위 뒤에 가려진 부분의 진하기. 앞에 가리는 게 없으면 그대로")]
        private float occludedAlpha = 0.25f;

        [Header("Colors")]
        [SerializeField, Tooltip("스킬 키 누르고 있을 때 조준 범위임")] private Color previewColor = new(0.4f, 0.9f, 1f, 0.95f);
        [SerializeField, Tooltip("사거리 원, 방향선 (얇은 선)")] private Color guideColor = new(0.4f, 0.9f, 1f, 0.45f);

        // 따로 분류했으니까 이거만 쓰면 됨 ㅇㅋ?
        [SerializeField, Tooltip("적 공격 예고 (항상 보임)")] private Color enemyWarningColor = new(1f, 0.15f, 0.35f, 0.9f);

        [SerializeField, Tooltip("디버그: 무기 휘두르기 준비")] private Color weaponWindupColor = new(1f, 0.65f, 0.2f, 0.5f);
        [SerializeField, Tooltip("디버그: 무기 판정")] private Color weaponHitColor = new(1f, 0.2f, 0.2f, 0.95f);
        [SerializeField, Tooltip("디버그: 스킬 판정")] private Color skillHitColor = new(1f, 0.5f, 0f, 0.95f);

        private readonly List<TimedShape> _timed = new();
        private readonly List<Shape> _frame = new();
        private readonly List<LineRenderer> _pool = new();
        private static Material _material;   // 적마다 하나씩 안 만들게 같이 씀
        private int _used;

        // 범위 테두리: 같은 색 모양을 묶어서 바닥 사각형 하나에 "합친 테두리"로 그림 (셰이더 HJH/AreaRim)
        private const int MaxRimShapes = 8;   // 셰이더 MAX_SHAPES 와 같아야 함
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int LineWidthId = Shader.PropertyToID("_LineWidth");
        private static readonly int GlowWidthId = Shader.PropertyToID("_GlowWidth");
        private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");
        private static readonly int OccludedAlphaId = Shader.PropertyToID("_OccludedAlpha");
        private static readonly int ShapeCountId = Shader.PropertyToID("_ShapeCount");
        private static readonly int ShapeAId = Shader.PropertyToID("_ShapeA");
        private static readonly int ShapeBId = Shader.PropertyToID("_ShapeB");
        private static Material _rimMaterial;
        private static bool _rimMissing;
        private readonly List<Shape> _rimShapes = new();
        private readonly List<bool> _rimDone = new();
        private readonly List<Mesh> _rimMeshes = new();
        private readonly List<MaterialPropertyBlock> _rimBlocks = new();
        private readonly Vector4[] _shapeA = new Vector4[MaxRimShapes];
        private readonly Vector4[] _shapeB = new Vector4[MaxRimShapes];
        private readonly Vector3[] _quad = new Vector3[4];
        private float _nextOwnerSearch;

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

            for (int i = _timed.Count - 1; i >= 0; i--)
            {
                TimedShape timed = _timed[i];

                if (Time.time > timed.ExpireTime || (timed.HasFollow && timed.Follow == null))
                {
                    _timed.RemoveAt(i);
                    continue;
                }

                if (timed.HasFollow)
                {
                    ApplyFollow(ref timed);
                    _timed[i] = timed;
                }

                _frame.Add(timed.Shape);
            }

            // 플레이어가 나중에 생기거나 바뀌는 씬도 있어서, 비었으면 1초에 한 번 다시 찾아봄
            if (owner == null && listenToBus && Time.unscaledTime >= _nextOwnerSearch)
            {
                _nextOwnerSearch = Time.unscaledTime + 1f;
                ResolveOwner();
            }

            // 스킬 조준 미리보기는 플레이어만
            if (owner is PlayerAgent player && player.SkillFsm != null && player.SkillFsm.IsAimingSkill)
                player.SkillFsm.DescribeAimPreview(this);

            // 얇은 보조선(사거리 원·방향선)은 선으로, 범위(원·사각형·부채꼴)는 합친 테두리로
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

            DrawRims();
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

        // 색·높이가 같은 모양끼리 묶어서 한 번에 그림 → 겹친 곳은 안쪽 선이 사라지고 바깥 테두리만 이어짐
        private void DrawRims()
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
                    if (_rimDone[j] || !SameColor(shape.Color, first.Color) || Mathf.Abs(GroundY(shape) - y) > 0.5f)
                        continue;

                    _rimDone[j] = true;
                    float reach = Pack(shape, count++);
                    min = Vector2.Min(min, new Vector2(shape.Center.x - reach, shape.Center.z - reach));
                    max = Vector2.Max(max, new Vector2(shape.Center.x + reach, shape.Center.z + reach));
                }

                RenderRim(group++, first.Color, y, count, min, max);
            }
        }

        // 모양 하나를 셰이더 배열 칸에 넣음. 반환 = 중심에서 가장 먼 곳까지 거리 (그릴 사각형 크기용)
        private float Pack(Shape shape, int index)
        {
            float yaw = shape.Yaw * Mathf.Deg2Rad;
            if (shape.IsCircle)
            {
                _shapeA[index] = new Vector4(0f, shape.Center.x, shape.Center.z, 0f);
                _shapeB[index] = new Vector4(shape.Size.x, 0f, 0f, 0f);
                return shape.Size.x;
            }
            if (shape.IsCone)
            {
                _shapeA[index] = new Vector4(2f, shape.Center.x, shape.Center.z, yaw);
                _shapeB[index] = new Vector4(shape.Size.x, shape.Size.y * 0.5f * Mathf.Deg2Rad, 0f, 0f);
                return shape.Size.x;
            }

            float hx = shape.Size.x * 0.5f;
            float hz = shape.Size.z * 0.5f;
            _shapeA[index] = new Vector4(1f, shape.Center.x, shape.Center.z, yaw);
            _shapeB[index] = new Vector4(hx, hz, 0f, 0f);
            return Mathf.Sqrt(hx * hx + hz * hz);
        }

        private void RenderRim(int index, Color color, float y, int count, Vector2 min, Vector2 max)
        {
            if (count == 0)
                return;

            float margin = lineWidth + 0.1f;   // 테두리 선이 사각형 밖으로 살짝 나가도 안 잘리게
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
            block.SetColor(ColorId, color);
            block.SetFloat(LineWidthId, lineWidth);
            block.SetFloat(GlowWidthId, rimGlowWidth);
            block.SetFloat(GlowStrengthId, rimGlowStrength);
            block.SetFloat(OccludedAlphaId, occludedAlpha);
            block.SetFloat(ShapeCountId, count);
            block.SetVectorArray(ShapeAId, _shapeA);
            block.SetVectorArray(ShapeBId, _shapeB);

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
                    GroundY = followOwnerHeight ? 0f : FindGroundY(area.Center)
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

        void ISkillPreview.Circle(Vector3 center, float radius) =>
            _frame.Add(new Shape { IsCircle = true, Center = center, Size = new Vector3(radius, 0f, 0f), Color = previewColor });

        void ISkillPreview.Path(Vector3 from, Vector3 to, float width)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            float length = direction.magnitude;
            if (length < 0.01f)
                return;

            _frame.Add(new Shape
            {
                Center = (from + to) * 0.5f,
                Yaw = Quaternion.LookRotation(direction / length).eulerAngles.y,
                Size = new Vector3(width, 0f, length),
                Color = previewColor
            });
        }

        void ISkillPreview.RangeCircle(Vector3 center, float radius) =>
            _frame.Add(new Shape { IsCircle = true, IsThin = true, Center = center, Size = new Vector3(radius, 0f, 0f), Color = guideColor });

        void ISkillPreview.Cone(Vector3 apex, Vector3 direction, float radius, float angle)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;

            _frame.Add(new Shape
            {
                IsCone = true,
                Center = apex,
                Yaw = Quaternion.LookRotation(direction.normalized).eulerAngles.y,
                Size = new Vector3(radius, angle, 0f),
                Color = previewColor
            });
        }

        void ISkillPreview.DirectionLine(Vector3 from, Vector3 to) =>
            _frame.Add(new Shape { IsSegment = true, IsThin = true, Center = from, End = to, Color = guideColor });

        private Color GetColor(AttackAreaKind kind)
        {
            switch (kind)
            {
                case AttackAreaKind.WeaponWindup: return weaponWindupColor;
                case AttackAreaKind.WeaponHit: return weaponHitColor;
                case AttackAreaKind.EnemyWarning: return enemyWarningColor;
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
