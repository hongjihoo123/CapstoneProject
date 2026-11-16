using System.Collections;
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
        [Tooltip("적 사이 스폰 간격"), SerializeField] private float spawnDelay = 0.3f;
        [Tooltip("간격에 더해질 랜덤 편차 (0이면 고정)"), SerializeField] private float randomDelayRange = 0.1f;

        [Header("Spawn Effect")]
        [SerializeField] private GameObject spawnParticlePrefab;
        [Tooltip("파티클 재생 후 적이 나타나기까지 시간"), SerializeField] private float particleLeadTime = 0.3f;

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
                hasSpawned = true;
                SpawnEnemies();
            }
        }

        // 이 스포너가 만들 적의 수 (플레이어가 가까이 가기 전에도 알 수 있음). 퀘스트의 "전체 적의 수" 계산용
        public int PlannedCount
        {
            get
            {
                if (enemyPrefabs == null || enemyPrefabs.Length == 0 || spawnPoints == null)
                    return 0;
                return Mathf.Min(spawnCount, spawnPoints.Length);
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

            StartCoroutine(SpawnRoutine());
        }

        private IEnumerator SpawnRoutine()
        {
            int count = Mathf.Min(spawnCount, spawnPoints.Length);

            for (int i = 0; i < count; i++)
            {
                int prefabIndex = Random.Range(0, enemyPrefabs.Length);
                int spawnIndex = randomSpawn ? Random.Range(0, spawnPoints.Length) : i;

                GameObject prefab = enemyPrefabs[prefabIndex];
                Transform point = spawnPoints[spawnIndex];

                if (prefab == null || point == null)
                    continue;

                PlayParticle(point);

                if (particleLeadTime > 0f)
                    yield return new WaitForSeconds(particleLeadTime);

                GameObject spawned = Instantiate(prefab, point.position, point.rotation);
                // 스포너가 만든 적 표시: 퀘스트가 "이 적은 이미 전체 수에 들어 있다"고 알 수 있게
                if (spawned != null)
                    spawned.AddComponent<global::Members.KYR._01.Scripts.Spawner.SpawnedEnemyTracker>();

                if (i < count - 1)
                {
                    float delay = spawnDelay + Random.Range(0f, randomDelayRange);
                    if (delay > 0f)
                        yield return new WaitForSeconds(delay);
                }
            }
        }

        private void PlayParticle(Transform point)
        {
            if (spawnParticlePrefab == null)
                return;

            GameObject fx = Instantiate(spawnParticlePrefab, point.position, point.rotation);

            ParticleSystem[] particles = fx.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in particles)
                ps.Play();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, detectRadius);
        }
    }
}