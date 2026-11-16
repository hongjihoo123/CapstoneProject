using Assets.Members.JJH._02_Scripts.Test;
using Members.JJH._02_Scripts.Agents.Enemies;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Quest
{
    // 퀘스트 클리어 흐름 확인용 데모 (에디터 메뉴 HJH/디버그/적 자동 처치 데모 로 켬, 에디터 전용).
    // 플레이 시작 후: 3초에 모든 스포너가 적을 한꺼번에 내보내고(원래는 플레이어가 가까이 가야 함),
    // 이후 0.3초마다 살아 있는 적을 하나씩 처치 → 잡은 수가 오르다가 다 잡으면 포탈이 켜짐.
    // (포탈이 꺼진 상태 → 켜지는 모습을 보려는 용도)
    public sealed class QuestClearDemo : MonoBehaviour
    {
        public const string SessionKey = "HJH.QuestClearDemo";

        private float _spawnAt = 6f;
        private float _killAt = 8f;
        private bool _spawned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool(SessionKey, false))
                new GameObject("QuestClearDemo").AddComponent<QuestClearDemo>();
#endif
        }

        private void Update()
        {
            if (!_spawned && Time.time >= _spawnAt)
            {
                _spawned = true;
                foreach (TestEnemySpawner spawner in FindObjectsByType<TestEnemySpawner>(FindObjectsSortMode.None))
                    spawner.SpawnEnemies();
                Debug.Log("[QuestClearDemo] 모든 스포너가 적을 내보냄");
            }

            if (Time.time < _killAt)
                return;

            foreach (AbstractEnemy enemy in FindObjectsByType<AbstractEnemy>(FindObjectsSortMode.None))
            {
                if (enemy == null || !enemy.IsAlive)
                    continue;

                enemy.Dead();
                _killAt = Time.time + 0.3f;
                return;
            }
        }
    }
}
