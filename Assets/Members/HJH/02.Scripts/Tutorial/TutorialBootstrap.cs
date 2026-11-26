using Assets.Members.HJH._02.Scripts.UI;
using Members.KYR._01.Scripts.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
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
