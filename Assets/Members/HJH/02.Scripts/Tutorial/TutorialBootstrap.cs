using Assets.Members.HJH._02.Scripts.UI;
using Members.KYR._01.Scripts.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    // 씬이 열릴 때 튜토리얼 중이면 그 씬에 맞는 순서를 틂 (씬에 따로 놓을 것 없음).
    //   픽 화면(ITutorialPickScreen 이 있음) → Tutorial_Pick
    //   플레이어가 있는 맵 씬            → Tutorial_Stage (스포너는 실전 단계 전까지 멈춰 둠)
    public static class TutorialBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryStart();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
                TryStart();
        }

        private static void TryStart()
        {
            if (!TutorialSession.Active)
                return;

            if (Object.FindFirstObjectByType<CharacterPickScreen>() != null)
            {
                TutorialDirector.Run(TutorialDefaults.Load(TutorialDefaults.PickName));
                return;
            }

            if (Object.FindFirstObjectByType<PlayerAgent>() != null)
            {
                TutorialDirector director = TutorialDirector.Run(TutorialDefaults.Load(TutorialDefaults.StageName));
                if (director != null)
                    director.HoldSpawnersNow();
            }
        }
    }
}
