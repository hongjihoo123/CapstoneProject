using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    public static class DashTint
    {
        private static Color _color;
        private static float _until;

        public static void Set(Color color, float seconds)
        {
            _color = color;
            _until = Time.time + seconds;
        }

        public static bool TryGet(out Color color)
        {
            color = _color;
            return Time.time < _until;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _until = 0f;
    }
}
