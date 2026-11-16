using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Character
{
    // 픽 화면에서 고른 캐릭터를 씬이 바뀌어도 기억하는 곳 (정적 값이라 씬 로드로 사라지지 않음).
    //  - 고른 뒤 만들어지는 모든 씬의 플레이어가 CharacterSwitcher 를 통해 이 캐릭터로 시작함
    //  - 죽어서 픽 화면으로 돌아갈 때 Clear() 로 지움
    //  - 픽 없이 맵 씬을 바로 열면(개발 중) HasPick 이 false 라서 예전처럼 startIndex / 숫자키로 동작함
    public static class CharacterSelection
    {
        public static int Index { get; private set; } = -1;
        public static bool HasPick => Index >= 0;

        public static void Pick(int index) => Index = Mathf.Max(0, index);
        public static void Clear() => Index = -1;

        // "Enter Play Mode 옵션"으로 도메인을 다시 안 불러도 이전 플레이의 값이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Index = -1;
    }
}
