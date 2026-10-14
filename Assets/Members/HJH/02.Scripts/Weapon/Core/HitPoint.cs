using UnityEngine;

namespace RobotWeapons
{
    // Where the last hit actually landed. A weapon marks it right before applying damage, and the
    // presentation layer reads it in the same frame to put the impact effect on the exact spot.
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

    // Lets weapons aim at the cursor point instead of straight along the character's facing.
    public interface IAimPointProvider
    {
        Vector3 AimPoint { get; }
    }
}
