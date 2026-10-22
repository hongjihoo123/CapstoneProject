using UnityEngine;

namespace Members.KYR._01.Scripts.Currency
{
    public class GameBootstrap : MonoBehaviour
    {
        public static CurrencyService Currency { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (Currency != null)
                return;

            var go = new GameObject(nameof(GameBootstrap));
            go.AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            if (Currency != null)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            Currency = new CurrencyService(new JsonFileSaveRepository());
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                Currency?.SaveIfDirty();
        }

        private void OnApplicationQuit()
        {
            Currency?.SaveIfDirty();
        }
    }
}
