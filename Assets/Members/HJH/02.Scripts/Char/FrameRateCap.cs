using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char
{
    // 게임 시작 시 프레임 상한. 상한이 없으면 GPU·CPU가 쉬지 않고 최대로 돌아 노트북 팬이 켜짐
    // (2026-11-12 exe 측정: 상한 없음 GPU 74W·79°C → 60fps 43W·65°C).
    // vSync는 창 모드에서 안 먹혀서 targetFrameRate로 막음. 나중에 설정 메뉴가 생기면 여기 값을 바꾸면 됨.
    public static class FrameRateCap
    {
        public const int DefaultFps = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Apply()
        {
            QualitySettings.vSyncCount = 0;   // vSync가 켜져 있으면 targetFrameRate가 무시됨
            Application.targetFrameRate = DefaultFps;
        }
    }
}
