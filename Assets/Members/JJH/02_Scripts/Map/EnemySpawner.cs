using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Map
{
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Enemy")]
        [SerializeField] private GameObject[] enemyPrefabs;
        [SerializeField] private int spawnCount = 5;

        [Header("Spawn Range")]
        [SerializeField] private Vector3 spawnArea = new Vector3(10f, 0f, 10f);
        [SerializeField] private float minDistance = 2f;

        [Header("Spawn Delay")]
        [SerializeField] private float minDelay = 0.1f;
        [SerializeField] private float maxDelay = 0.3f;

        [Header("Particle")]
        [SerializeField] private GameObject spawnParticlePrefab;

        [Header("Settings")]
        [SerializeField] private int maxPositionTryCount = 30;
        [SerializeField] private LayerMask enemyLayerMask;

        private void Start()
        {
            StartCoroutine(SpawnEnemies());
        }

        private IEnumerator SpawnEnemies()
        {
            if (enemyPrefabs == null || enemyPrefabs.Length == 0)
            {
                Debug.LogWarning("생성할 적 프리팹이 없습니다.", this);
                yield break;
            }

            for (int i = 0; i < spawnCount; i++)
            {
                if (TryGetSpawnPosition(out Vector3 spawnPosition))
                {
                    GameObject enemyPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];

                    Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);

                    if (spawnParticlePrefab != null)
                    {
                        GameObject particle = Instantiate(
                            spawnParticlePrefab,
                            spawnPosition,
                            Quaternion.identity
                        );

                        ParticleSystem particleSystem = particle.GetComponentInChildren<ParticleSystem>();

                        if (particleSystem != null)
                        {
                            Destroy(particle, particleSystem.main.duration + particleSystem.main.startLifetime.constantMax);
                        }
                        else
                        {
                            Destroy(particle, 3f);
                        }
                    }
                }

                yield return new WaitForSeconds(Random.Range(minDelay, maxDelay));
            }
        }

        private bool TryGetSpawnPosition(out Vector3 spawnPosition)
        {
            for (int i = 0; i < maxPositionTryCount; i++)
            {
                Vector3 randomPosition = transform.position + new Vector3(
                    Random.Range(-spawnArea.x * 0.5f, spawnArea.x * 0.5f),
                    Random.Range(-spawnArea.y * 0.5f, spawnArea.y * 0.5f),
                    Random.Range(-spawnArea.z * 0.5f, spawnArea.z * 0.5f)
                );

                Collider[] colliders = Physics.OverlapSphere(randomPosition, minDistance, enemyLayerMask);

                if (colliders.Length == 0)
                {
                    spawnPosition = randomPosition;
                    return true;
                }
            }

            spawnPosition = Vector3.zero;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, spawnArea);
        }
    }
}