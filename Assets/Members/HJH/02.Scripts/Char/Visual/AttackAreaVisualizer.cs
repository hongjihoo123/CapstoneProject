using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01_Scripts;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    public class AttackAreaVisualizer : MonoBehaviour, ISkillPreview
    {
        private const int CircleSegments = 40;

        private static readonly Color PreviewColor = new Color(0.4f, 0.9f, 1f, 0.95f);
        private static readonly Color GuideColor = new Color(0.4f, 0.9f, 1f, 0.45f);
        private static readonly Color WindupColor = new Color(1f, 0.65f, 0.2f, 0.5f);
        private static readonly Color WeaponHitColor = new Color(1f, 0.2f, 0.2f, 0.95f);
        private static readonly Color SkillHitColor = new Color(1f, 0.5f, 0f, 0.95f);

        private struct Shape
        {
            public bool IsCircle;
            public bool IsSegment;  // open two-point line
            public bool IsThin;     // drawn with guideLineWidth instead of lineWidth
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

        [SerializeField] private PlayerAgent player;
        [SerializeField] private float groundOffset = 0.06f;
        [SerializeField] private float lineWidth = 0.08f;
        [SerializeField] private float guideLineWidth = 0.03f;
        [SerializeField, Tooltip("Debug: draw weapon/skill hit areas. F1 toggles in play mode. Aim previews are always drawn.")]
        private bool showHitAreas;

        private readonly List<TimedShape> _timed = new();
        private readonly List<Shape> _frame = new();
        private readonly List<LineRenderer> _pool = new();
        private Material _material;
        private int _used;

        private void OnEnable() => AttackAreaBus.Raised += OnAreaRaised;

        private void OnDisable()
        {
            AttackAreaBus.Raised -= OnAreaRaised;
            _timed.Clear();
            HideUnused(0);
        }

        private void Update()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                showHitAreas = !showHitAreas;
                _timed.Clear();
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

            if (player != null && player.SkillFsm != null && player.SkillFsm.IsAimingSkill)
                player.SkillFsm.DescribeAimPreview(this);

            _used = 0;
            foreach (Shape shape in _frame)
                Draw(shape);

            HideUnused(_used);
        }

        private void OnAreaRaised(AttackArea area)
        {
            if (!showHitAreas)
                return;

            if (area.Kind != AttackAreaKind.Skill)
                _timed.RemoveAll(timed => timed.Kind == area.Kind);
            else
                _timed.RemoveAll(timed => timed.Kind == area.Kind && Mathf.Approximately(timed.Shape.Size.x, area.Size.x) && Time.time - timed.StartTime < 0.1f);

            _timed.Add(new TimedShape
            {
                Kind = area.Kind,
                StartTime = Time.time,
                ExpireTime = Time.time + area.Duration,
                Shape = new Shape
                {
                    IsCircle = area.Shape == AttackAreaShape.Circle,
                    Center = area.Center,
                    Yaw = area.Rotation.eulerAngles.y,
                    Size = area.Size,
                    Color = GetColor(area.Kind)
                }
            });
        }

        void ISkillPreview.Circle(Vector3 center, float radius) =>
            _frame.Add(new Shape { IsCircle = true, Center = center, Size = new Vector3(radius, 0f, 0f), Color = PreviewColor });

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
                Color = PreviewColor
            });
        }

        void ISkillPreview.RangeCircle(Vector3 center, float radius) =>
            _frame.Add(new Shape { IsCircle = true, IsThin = true, Center = center, Size = new Vector3(radius, 0f, 0f), Color = GuideColor });

        void ISkillPreview.DirectionLine(Vector3 from, Vector3 to) =>
            _frame.Add(new Shape { IsSegment = true, IsThin = true, Center = from, End = to, Color = GuideColor });

        private static Color GetColor(AttackAreaKind kind)
        {
            switch (kind)
            {
                case AttackAreaKind.WeaponWindup: return WindupColor;
                case AttackAreaKind.WeaponHit: return WeaponHitColor;
                default: return SkillHitColor;
            }
        }

        private float GroundY(Shape shape) =>
            (player != null ? player.transform.position.y : shape.Center.y) + groundOffset;

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

                Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, shape.Yaw, 0f), Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(shape.Size.x, 0.05f, shape.Size.z));
                Gizmos.matrix = Matrix4x4.identity;
            }
        }
    }
}
