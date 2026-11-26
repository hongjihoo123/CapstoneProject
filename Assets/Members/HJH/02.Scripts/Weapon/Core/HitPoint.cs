using UnityEngine;

namespace RobotWeapons
{
    public static class HitPoint
    {
        private static IDamageable _target;
        private static Vector3 _point;
        private static Vector3 _direction;
        private static int _frame = -1;

        public static void Mark(IDamageable target, Vector3 point, Vector3 direction)
        {
            _target = target;
            _point = point;
            _direction = direction;
            _frame = Time.frameCount;
        }

        public static bool TryTake(IDamageable target, out Vector3 point, out Vector3 direction)
        {
            bool valid = _frame == Time.frameCount && ReferenceEquals(_target, target);
            point = _point;
            direction = _direction;
            if (valid)
                _target = null;
            return valid;
        }
    }

    public interface IAimPointProvider
    {
        Vector3 AimPoint { get; }
    }
}
