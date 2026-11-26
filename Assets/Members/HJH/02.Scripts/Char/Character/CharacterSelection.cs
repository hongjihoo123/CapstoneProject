using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Character
{
    public static class CharacterSelection
    {
        public static int Index { get; private set; } = -1;
        public static bool HasPick => Index >= 0;

        public static void Pick(int index) => Index = Mathf.Max(0, index);
        public static void Clear() => Index = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Index = -1;
    }
}
