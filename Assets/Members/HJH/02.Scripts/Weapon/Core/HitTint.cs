using UnityEngine;

namespace RobotWeapons
{
    public static class HitTint
    {
        private static IDamageable _target;
        private static Color _color;
        private static int _frame = -1;

        public static void Mark(IDamageable target, Color color)
        {
            _target = target;
            _color = color;
            _frame = Time.frameCount;
        }

        public static bool TryGet(IDamageable target, out Color color)
        {
            color = _color;
            return _frame == Time.frameCount && ReferenceEquals(_target, target);
        }
    }
}
