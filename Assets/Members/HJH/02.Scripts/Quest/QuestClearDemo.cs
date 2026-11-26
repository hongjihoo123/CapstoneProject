using Assets.Members.JJH._02_Scripts.Test;
using Members.JJH._02_Scripts.Agents.Enemies;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Quest
{
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
