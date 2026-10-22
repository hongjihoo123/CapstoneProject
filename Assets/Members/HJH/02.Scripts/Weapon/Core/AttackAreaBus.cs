using System;
using UnityEngine;

namespace RobotWeapons
{
    public enum AttackAreaShape
    {
        Box,
        Circle,
        Cone    // Center = apex, Rotation = facing, Size.x = radius, Size.y = full angle (degrees)
    }

    // 색은 종류별로 AttackAreaVisualizer 인스펙터에서 정함
    public enum AttackAreaKind
    {
        WeaponWindup,   // 디버그(F1)
        WeaponHit,      // 디버그(F1)
        Skill,          // 디버그(F1)
        EnemyWarning    // 적 공격 예고. F1 과 상관없이 항상 보임
    }

    // 적에서 쓰는 법: AttackAreaBus.Raise(AttackArea.Circle(AttackAreaKind.EnemyWarning, 위치, 반지름, 경고시간));
    public struct AttackArea
    {
        public AttackAreaShape Shape;
        public AttackAreaKind Kind;
        public Vector3 Center;
        public Quaternion Rotation;
        public Vector3 Size;
        public float Duration;
        public bool HasColor;   // true 면 종류별 색 대신 Color 로 그림 (WithColor 로 설정)
        public Color Color;

        // 종류별 색 말고 특정 색으로 그리고 싶을 때만 (예: 속성 색)
        public AttackArea WithColor(Color color)
        {
            AttackArea area = this;
            area.HasColor = true;
            area.Color = color;
            return area;
        }

        public static AttackArea Box(AttackAreaKind kind, Vector3 center, Quaternion rotation, Vector3 size, float duration) =>
            new AttackArea { Shape = AttackAreaShape.Box, Kind = kind, Center = center, Rotation = rotation, Size = size, Duration = duration };

        public static AttackArea Circle(AttackAreaKind kind, Vector3 center, float radius, float duration) =>
            new AttackArea { Shape = AttackAreaShape.Circle, Kind = kind, Center = center, Rotation = Quaternion.identity, Size = new Vector3(radius, 0f, 0f), Duration = duration };

        public static AttackArea Cone(AttackAreaKind kind, Vector3 apex, Quaternion rotation, float radius, float angle, float duration) =>
            new AttackArea { Shape = AttackAreaShape.Cone, Kind = kind, Center = apex, Rotation = rotation, Size = new Vector3(radius, angle, 0f), Duration = duration };
    }

    public static class AttackAreaBus
    {
        public static event Action<AttackArea> Raised;

        public static void Raise(in AttackArea area) => Raised?.Invoke(area);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Raised = null;
    }
}
