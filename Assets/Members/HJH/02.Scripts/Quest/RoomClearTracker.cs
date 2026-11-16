using System;
using System.Collections.Generic;
using Assets.Members.JJH._02_Scripts.Test;
using Members.JJH._02_Scripts.Agents.Enemies;
using Members.KYR._01.Scripts.Spawner;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Quest
{
    // 방 클리어 진행: 잡은 수 / 전체 적의 수. 적이 죽을 때(AbstractEnemy.Dead → AnyDied)마다 잡은 수가 올라가고,
    // 다 잡으면 Cleared 를 한 번 쏜다.
    //
    // 전체 적의 수는 "나중에 나올 적까지" 처음부터 알아야 해서(세트로 나눠 나오면 첫 세트만 잡고 끝나 버림) 이렇게 정함:
    //   1) 씬에 RoomClearSettings 가 있고 값이 있으면 그 값
    //   2) 아니면 스포너(EnemySpawner)의 모든 웨이브 합 + 씬에 이미 있는 적 전부 (꺼져 있는 적도 포함 = 세트로 켜지는 적)
    //      스포너가 만든 적은 합계에 이미 들어 있어서 다시 세지 않음
    // UI 와 포탈은 이 클래스의 값/이벤트만 보면 됨.
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

        // 새로 생긴 적, 스포너 합계, 방 설정을 다시 읽어서 전체 수를 맞춤 (꺼져 있는 적도 찾음)
        private void Rescan()
        {
            // 아직 안 나온 적까지 시작부터 센다: 플레이어가 다가가면 켜지는 스포너(TestEnemySpawner)와 웨이브 스포너의 예정 수
            int spawnerTotal = 0;
            foreach (EnemySpawner spawner in FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                spawnerTotal += spawner.TotalEnemyCount;
            foreach (TestEnemySpawner spawner in FindObjectsByType<TestEnemySpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                spawnerTotal += spawner.PlannedCount;
            _spawnerTotal = spawnerTotal;

            foreach (AbstractEnemy enemy in FindObjectsByType<AbstractEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (_seen.Add(enemy) && !IsSpawnerMade(enemy))
                    _sceneEnemies++;
            }

            RoomClearSettings settings = FindFirstObjectByType<RoomClearSettings>(FindObjectsInactive.Include);
            _forcedTotal = settings != null ? settings.ExpectedTotal : 0;

            Refresh();
        }

        private void OnEnemyDied(AbstractEnemy enemy)
        {
            if (enemy == null || !_dead.Add(enemy))
                return;

            // 찾기 전에 죽은 적도 놓치지 않게
            if (_seen.Add(enemy) && !IsSpawnerMade(enemy))
                _sceneEnemies++;

            Kills++;
            Refresh(force: true);
            CheckCleared();
        }

        // force = 잡은 수가 바뀐 경우(전체 수가 같아도 화면 갱신)
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

        [SerializeField, Min(0f), Tooltip("전체 수를 자동 계산할 때, 다 잡은 뒤 이 시간 동안 새 적이 안 나타나야 클리어")]
        private float autoConfirmSeconds = 1f;

        private bool _confirming;

        private void CheckCleared()
        {
            if (IsCleared || _confirming || Total <= 0 || Kills < Total)
                return;

            StartCoroutine(ConfirmCleared());
        }

        // 다 잡은 것처럼 보여도 곧바로 성공 처리하지 않고 잠깐 지켜봄:
        //  - 그 사이 전체 수가 늘거나(뒤늦게 나온 적) 살아 있는 적이 보이면 취소
        //  - 방에 RoomClearSettings 로 전체 수를 적어 둔 경우는 그 수가 확실하니 거의 바로 성공
        private System.Collections.IEnumerator ConfirmCleared()
        {
            _confirming = true;
            int totalAtStart = Total;
            // 전체 수를 처음부터 아는 방(직접 적어 둠 / 스포너 예정 수)은 거의 바로, 모르는 방만 잠깐 지켜봄
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
                yield break;   // 그 적이 죽으면(Kills 증가) 다시 판단

            IsCleared = true;
            Cleared?.Invoke();
        }

        private static bool AnyAliveActiveEnemy()
        {
            foreach (AbstractEnemy enemy in FindObjectsByType<AbstractEnemy>(FindObjectsSortMode.None))
            {
                if (enemy != null && enemy.IsAlive)
                    return true;
            }
            return false;
        }
    }
}
