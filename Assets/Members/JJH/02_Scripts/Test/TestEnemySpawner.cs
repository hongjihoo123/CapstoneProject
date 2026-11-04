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

        [Header("Detect Settings")]
        [SerializeField] private float detectRadius = 10f;
        [SerializeField] private LayerMask playerLayer; 
        private float checkInterval = 0.2f;
        private bool spawn = true;

        private bool hasSpawned;
        private float checkTimer;

        private void Update()
        {
            if (spawn && hasSpawned)
                return;

            checkTimer += Time.deltaTime;
            if (checkTimer < checkInterval)
                return;
            checkTimer = 0f;

            if (Physics.CheckSphere(transform.position, detectRadius, playerLayer))
            {
                SpawnEnemies();
                hasSpawned = true;
            }
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

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, detectRadius);
        }
    }
}