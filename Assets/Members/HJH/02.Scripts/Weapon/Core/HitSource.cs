namespace RobotWeapons
{
    // 지금 나가는 피해가 무엇에서 왔는지 (평타 무기 / 패시브 / 스킬).
    // DamageDealtInfo 는 다른 팀원 코드(PlayerAgent)가 만들어서 출처를 못 담음 →
    // 피해를 주는 쪽이 Push/Pop 으로 감싸면, 그 사이에 동기로 불리는 DamageDealt 구독자(연출)가 Current 로 읽음.
    // 감싸지 않은 피해(총알 등)는 Weapon.
    public enum HitSourceKind { Weapon, Passive, Skill }

    public static class HitSource
    {
        private const int MaxDepth = 8;
        private static readonly HitSourceKind[] Stack = new HitSourceKind[MaxDepth];
        private static int _depth;

        public static HitSourceKind Current => _depth > 0 ? Stack[_depth - 1] : HitSourceKind.Weapon;

        public static void Push(HitSourceKind kind)
        {
            if (_depth < MaxDepth)
                Stack[_depth] = kind;
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
