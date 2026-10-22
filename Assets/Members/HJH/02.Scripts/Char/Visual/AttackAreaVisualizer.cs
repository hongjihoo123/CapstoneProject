using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.JJH._02_Scripts.Agents;
using Members.KYR._01_Scripts;
using RobotWeapons;
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
        }

        private struct TimedShape
        {
            public Shape Shape;
            public AttackAreaKind Kind;
            public float StartTime;
            public float ExpireTime;
        }

        [SerializeField]
        private Agent owner;
        [SerializeField]
        private bool listenToBus = true;
        [SerializeField] private float groundOffset = 0.06f;
        [SerializeField] private float lineWidth = 0.08f;
        [SerializeField] private float guideLineWidth = 0.03f;
        [SerializeField, Tooltip("Debug: draw weapon/skill hit areas. F1 toggles in play mode. Aim previews and enemy warnings are always drawn.")]
        private bool showHitAreas;

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

        private void Awake()
        {
            if (owner == null)
                owner = GetComponentInParent<Agent>();
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
                if (Time.time > _timed[i].ExpireTime)
                    _timed.RemoveAt(i);
                else
                    _frame.Add(_timed[i].Shape);
            }

            // 스킬 조준 미리보기는 플레이어만
            if (owner is PlayerAgent player && player.SkillFsm != null && player.SkillFsm.IsAimingSkill)
                player.SkillFsm.DescribeAimPreview(this);

            _used = 0;
            foreach (Shape shape in _frame)
                Draw(shape);

            HideUnused(_used);
        }

        // 이 Visualizer 에 범위 하나 그림. 버스 듣는 애는 버스에서 자동으로 불림, 적은 직접 부르면 됨
        public void Show(AttackArea area)
        {
            if (IsDebug(area.Kind) && !showHitAreas)
                return;

            // 적 예고는 적마다 따로 떠야 해서 안 지움
            if (area.Kind == AttackAreaKind.WeaponWindup || area.Kind == AttackAreaKind.WeaponHit)
                _timed.RemoveAll(timed => timed.Kind == area.Kind);
            else if (area.Kind == AttackAreaKind.Skill)
                _timed.RemoveAll(timed => timed.Kind == area.Kind && Mathf.Approximately(timed.Shape.Size.x, area.Size.x) && Time.time - timed.StartTime < 0.1f);

            _timed.Add(new TimedShape
            {
                Kind = area.Kind,
                StartTime = Time.time,
                ExpireTime = Time.time + area.Duration,
                Shape = new Shape
                {
                    IsCircle = area.Shape == AttackAreaShape.Circle,
                    IsCone = area.Shape == AttackAreaShape.Cone,
                    Center = area.Center,
                    Yaw = area.Rotation.eulerAngles.y,
                    Size = area.Size,
                    Color = area.HasColor ? area.Color : GetColor(area.Kind)
                }
            });
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
            (owner != null ? owner.transform.position.y : shape.Center.y) + groundOffset;

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

        private LineRenderer CreateLine()
        {
            if (_material == null)
            {
                Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                _material = new Material(shader);
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
