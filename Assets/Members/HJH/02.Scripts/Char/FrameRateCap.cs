using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char
{
    public static class FrameRateCap
    {
        public const int DefaultFps = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Apply()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = DefaultFps;
        }
    }
}
