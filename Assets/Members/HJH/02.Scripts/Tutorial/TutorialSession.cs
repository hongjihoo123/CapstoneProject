using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    // 튜토리얼 진행 상태. 씬이 바뀌어도 남아야 해서 정적 값 (CharacterSelection 과 같은 방식).
    //   메인 화면 "처음인가요?" → 예 → Begin()  →  픽 화면에서 Pick 순서, 맵 씬에서 Stage 순서가 돎
    //   스테이지 클리어(마지막 단계) → Complete()  /  Tab 길게 → Abort()  /  죽음 → Abort()
    // 어떤 씬에서 어떤 순서를 돌릴지는 TutorialBootstrap 이 정함.
    public static class TutorialSession
    {
        private const string DoneKey = "HJH.Tutorial.Done";

        public static bool Active { get; private set; }

        // 이번 실행에서 "처음인가요?" 를 이미 물어봤는지 (메인 화면으로 돌아올 때 다시 안 묻게)
        public static bool AskedThisLaunch { get; set; }

        // 한 번이라도 끝까지 마쳤는지 (저장됨)
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
