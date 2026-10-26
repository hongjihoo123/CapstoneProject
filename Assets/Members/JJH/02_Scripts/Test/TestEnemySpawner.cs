using UnityEngine;

namespace Assets.Members.JJH._02_Scripts.Test
{
    public class TestEnemySpawner : MonoBehaviour
    {
        [Header("Enemy Prefabs")]
        [SerializeField] private GameObject[] enemyPrefabs;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] spawnPoints;

        [Header("Spawn Settings")]
        [SerializeField] private int spawnCount = 5;
        [SerializeField] private bool randomSpawn = true;

        private void Start()
        {
            SpawnEnemies();
        }

        public void SpawnEnemies()
        {
            if (enemyPrefabs == null || enemyPrefabs.Length == 0)
            {
                Debug.LogWarning("EnemySpawner: 적 프리팹이 설정되지 않았습니다.", this);
                return;
            }

            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogWarning("EnemySpawner: 스폰 위치가 설정되지 않았습니다.", this);
                return;
            }

            int count = Mathf.Min(spawnCount, spawnPoints.Length);

            for (int i = 0; i < count; i++)
            {
                int prefabIndex = Random.Range(0, enemyPrefabs.Length);
                int spawnIndex = randomSpawn ? Random.Range(0, spawnPoints.Length) : i;

                if (enemyPrefabs[prefabIndex] == null || spawnPoints[spawnIndex] == null)
                    continue;

                Instantiate(enemyPrefabs[prefabIndex], spawnPoints[spawnIndex].position, spawnPoints[spawnIndex].rotation);
            }
        }
    }
}