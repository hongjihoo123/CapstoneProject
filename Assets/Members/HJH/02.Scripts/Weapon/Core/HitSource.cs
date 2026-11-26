namespace RobotWeapons
{
    public enum HitSourceKind { Weapon, Passive, Skill }

    public static class HitSource
    {
        private const int MaxDepth = 8;
        private static readonly HitSourceKind[] Stack = new HitSourceKind[MaxDepth];
        private static readonly int[] CastStack = new int[MaxDepth];
        private static int _depth;

        public static HitSourceKind Current => _depth > 0 && _depth <= MaxDepth ? Stack[_depth - 1] : HitSourceKind.Weapon;

        public static int CurrentCastId => _depth > 0 && _depth <= MaxDepth ? CastStack[_depth - 1] : 0;

        public static void Push(HitSourceKind kind, int castId = 0)
        {
            if (_depth < MaxDepth)
            {
                Stack[_depth] = kind;
                CastStack[_depth] = castId;
            }
            _depth++;
        }

        public static void Pop()
        {
            if (_depth > 0)
                _depth--;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _depth = 0;
    }
}
