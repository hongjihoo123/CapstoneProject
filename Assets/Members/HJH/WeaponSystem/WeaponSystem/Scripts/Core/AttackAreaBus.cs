using System;
using UnityEngine;

namespace RobotWeapons
{
    public enum AttackAreaShape
    {
        Box,
        Circle
    }

    public enum AttackAreaKind
    {
        WeaponWindup,
        WeaponHit,
        Skill
    }

    public struct AttackArea
    {
        public AttackAreaShape Shape;
        public AttackAreaKind Kind;
        public Vector3 Center;
        public Quaternion Rotation;
        public Vector3 Size;
        public float Duration;

        public static AttackArea Box(AttackAreaKind kind, Vector3 center, Quaternion rotation, Vector3 size, float duration) =>
            new AttackArea { Shape = AttackAreaShape.Box, Kind = kind, Center = center, Rotation = rotation, Size = size, Duration = duration };

        public static AttackArea Circle(AttackAreaKind kind, Vector3 center, float radius, float duration) =>
            new AttackArea { Shape = AttackAreaShape.Circle, Kind = kind, Center = center, Rotation = Quaternion.identity, Size = new Vector3(radius, 0f, 0f), Duration = duration };
    }

    public static class AttackAreaBus
    {
        public static event Action<AttackArea> Raised;

        public static void Raise(in AttackArea area) => Raised?.Invoke(area);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Raised = null;
    }
}
