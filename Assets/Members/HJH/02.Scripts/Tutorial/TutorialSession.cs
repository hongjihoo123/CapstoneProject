using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    public static class TutorialSession
    {
        private const string DoneKey = "HJH.Tutorial.Done";

        public static bool Active { get; private set; }

        public static bool AskedThisLaunch { get; set; }

        public static bool EverCompleted => PlayerPrefs.GetInt(DoneKey, 0) == 1;

        public static event System.Action Ended;

        public static void Begin() => Active = true;

        public static void Complete()
        {
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
            End();
        }

        public static void Abort() => End();

        private static void End()
        {
            if (!Active)
                return;

            Active = false;
            Ended?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Active = false;
            AskedThisLaunch = false;
            Ended = null;
        }
    }
}
