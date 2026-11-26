using System;
using System.Collections.Generic;
using Assets.Members.JJH._02_Scripts.Test;
using Members.JJH._02_Scripts.Agents.Enemies;
using Members.KYR._01.Scripts.Spawner;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Quest
{
    public class RoomClearTracker : MonoBehaviour
    {
        public event Action Changed;
        public event Action Cleared;

        public int Kills { get; private set; }
        public int Total { get; private set; }
        public bool IsCleared { get; private set; }

        private readonly HashSet<AbstractEnemy> _seen = new();
        private readonly HashSet<AbstractEnemy> _dead = new();
        private int _spawnerTotal;
        private int _sceneEnemies;
        private int _forcedTotal;
        private float _nextScan;

        private void OnEnable()
        {
            AbstractEnemy.AnyDied += OnEnemyDied;
            Rescan();
        }

        private void OnDisable() => AbstractEnemy.AnyDied -= OnEnemyDied;

        private void Update()
        {
            if (Time.unscaledTime < _nextScan)
                return;

            _nextScan = Time.unscaledTime + 0.25f;
            Rescan();
        }

        private void Rescan()
        {
            int spawnerTotal = 0;
            foreach (EnemySpawner spawner in FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                spawnerTotal += spawner.TotalEnemyCount;
            foreach (TestEnemySpawner spawner in FindObjectsByType<TestEnemySpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                spawnerTotal += spawner.PlannedCount;
            _spawnerTotal = spawnerTotal;

            foreach (AbstractEnemy enemy in FindObjectsByType<AbstractEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!IsIgnored(enemy) && _seen.Add(enemy) && !IsSpawnerMade(enemy))
                    _sceneEnemies++;
            }

            RoomClearSettings settings = FindFirstObjectByType<RoomClearSettings>(FindObjectsInactive.Include);
            _forcedTotal = settings != null ? settings.ExpectedTotal : 0;

            Refresh();
        }

        private void OnEnemyDied(AbstractEnemy enemy)
        {
            if (enemy == null || IsIgnored(enemy) || !_dead.Add(enemy))
                return;

            if (_seen.Add(enemy) && !IsSpawnerMade(enemy))
                _sceneEnemies++;

            Kills++;
            Refresh(force: true);
            CheckCleared();
        }

        private void Refresh(bool force = false)
        {
            int total = _forcedTotal > 0 ? _forcedTotal : _spawnerTotal + _sceneEnemies;
            if (total == Total && !force)
                return;

            Total = total;
            Debug.Log($"[RoomClear] 전체 적 {Total}마리 (스포너 예정 {_spawnerTotal} + 씬에 있는 적 {_sceneEnemies}{(_forcedTotal > 0 ? $", 방 설정 {_forcedTotal} 사용" : "")})");
            Changed?.Invoke();
        }

        private static bool IsSpawnerMade(AbstractEnemy enemy) => enemy.GetComponentInParent<SpawnedEnemyTracker>() != null;

        private static bool IsIgnored(AbstractEnemy enemy) => enemy.GetComponentInParent<RoomClearIgnore>() != null;

        [SerializeField, Min(0f), Tooltip("전체 수를 자동 계산할 때, 다 잡은 뒤 이 시간 동안 새 적이 안 나타나야 클리어")]
        private float autoConfirmSeconds = 1f;

        private bool _confirming;

        private void CheckCleared()
        {
            if (IsCleared || _confirming || Total <= 0 || Kills < Total)
                return;

            StartCoroutine(ConfirmCleared());
        }

        private System.Collections.IEnumerator ConfirmCleared()
        {
            _confirming = true;
            int totalAtStart = Total;
            float wait = (_forcedTotal > 0 || _spawnerTotal > 0) ? 0.15f : autoConfirmSeconds;

            float elapsed = 0f;
            while (elapsed < wait)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                if (Total != totalAtStart || Kills < Total)
                {
                    _confirming = false;
                    yield break;
                }
            }

            _confirming = false;
            if (AnyAliveActiveEnemy())
                yield break;

            IsCleared = true;
            Cleared?.Invoke();
        }

        private static bool AnyAliveActiveEnemy()
        {
            foreach (AbstractEnemy enemy in FindObjectsByType<AbstractEnemy>(FindObjectsSortMode.None))
            {
                if (enemy != null && enemy.IsAlive && !IsIgnored(enemy))
                    return true;
            }
            return false;
        }
    }
}
