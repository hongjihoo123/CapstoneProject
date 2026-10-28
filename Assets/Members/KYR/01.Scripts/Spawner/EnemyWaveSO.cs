using System;
using System.Collections.Generic;
using Members.KYR._01_Scripts.Spawner;
using UnityEngine;

namespace Members.KYR._01.Scripts.Spawner
{
    [Serializable]
    public class EnemySpawnEntry
    {
        [SerializeField] private GameObject prefab;

        [SerializeField, Min(0f)]
        [Tooltip("적 종류 가중치")]
        private float weight = 1f;

        public GameObject Prefab => prefab;
        public float Weight => weight;
    }

    [CreateAssetMenu(fileName = "Enemy Wave", menuName = "SO/Enemy/Wave", order = 2)]
    public class EnemyWaveSO : ScriptableObject
    {
        [SerializeField, Min(0)]
        [Tooltip("웨이브에서 나올 적")]
        private int totalCount = 5;

        [SerializeField] private List<EnemySpawnEntry> enemies = new List<EnemySpawnEntry>();

        [SerializeField]
        [Tooltip("고정 스폰 포인트")]
        private List<SpawnPointId> requiredPointIds = new List<SpawnPointId>();

        [SerializeField, Min(0f)]
        [Tooltip("다음 웨이브까지 대기시간")]
        private float delayAfterWave = 1f;

        public int TotalCount => totalCount;
        public IReadOnlyList<EnemySpawnEntry> Enemies => enemies;
        public IReadOnlyList<SpawnPointId> RequiredPointIds => requiredPointIds;
        public float DelayAfterWave => delayAfterWave;
    }
}
