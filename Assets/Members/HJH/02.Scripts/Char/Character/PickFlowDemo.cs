using Assets.Members.HJH._02.Scripts.UI;
using Members.KYR._01.Scripts.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Members.HJH._02.Scripts.Char.Character
{
    public sealed class PickFlowDemo : MonoBehaviour
    {
        public const string SessionKey = "HJH.PickFlowDemo";
        private const string PickScene = "CharacterPick";

        private int _nextPick;
        private float _sceneStart;
        private bool _killed;
        private float _overlaySeenAt = -1f;
        private string _lastScene = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool(SessionKey, false) && FindFirstObjectByType<PickFlowDemo>() == null)
            {
                var go = new GameObject("PickFlowDemo");
                DontDestroyOnLoad(go);
                go.AddComponent<PickFlowDemo>();
            }
#endif
        }

        private void Update()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (scene != _lastScene)
            {
                _lastScene = scene;
                _sceneStart = Time.unscaledTime;
                _killed = false;
                _overlaySeenAt = -1f;
            }

            float t = Time.unscaledTime - _sceneStart;

            if (scene == PickScene)
            {
                if (t > 2f)
                {
                    var screen = FindFirstObjectByType<CharacterPickScreen>();
                    if (screen != null)
                    {
                        Debug.Log($"[PickFlowDemo] {_nextPick + 1}번 캐릭터 선택");
                        screen.DebugPick(_nextPick % 3);
                        _nextPick++;
                        _sceneStart = float.MaxValue;
                    }
                }
                return;
            }

            PlayerAgent agent = FindFirstObjectByType<PlayerAgent>();
            if (agent == null)
                return;

            if (!_killed && t > 3f)
            {
                _killed = true;
                Debug.Log($"[PickFlowDemo] 플레이어 사망 시킴 (선택 인덱스 {CharacterSelection.Index})");
                agent.TakeDamage(99999f, null);
            }

            var overlay = FindFirstObjectByType<DeathOverlay>();
            if (overlay != null && overlay.IsShown)
            {
                if (_overlaySeenAt < 0f)
                    _overlaySeenAt = Time.unscaledTime;
                else if (Time.unscaledTime - _overlaySeenAt > 2f)
                {
                    Debug.Log("[PickFlowDemo] 버튼 누름 → 픽 화면");
                    overlay.DebugGoToPick();
                    _overlaySeenAt = float.MaxValue;
                }
            }
        }
    }
}
