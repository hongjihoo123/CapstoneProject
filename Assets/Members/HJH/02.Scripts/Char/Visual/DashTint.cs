using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 대시 잔상 색을 잠깐 바꿈 (스킬 돌진은 캐릭터 테마색 대신 원소 색: 화염 돌진 = 주황, 도약 = 바람색)
    // CombatFeedback 이 매 프레임 읽음. 시간이 지나면 저절로 테마색으로 돌아감
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
