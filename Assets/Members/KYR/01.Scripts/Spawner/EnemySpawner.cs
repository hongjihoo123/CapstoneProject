using System.Collections;
using System.Collections.Generic;
using Members.JJH._02_Scripts.Agents.Modules;
using Members.KYR._01.Scripts.Spawner;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Members.KYR._01_Scripts.Spawner
{
    public class EnemySpawner : MonoBehaviour
    {
        private const float NavMeshSampleRadius = 2f;

        [SerializeField] private RoomWaveSetSO waveSet;

        [SerializeField, Min(0f)]
        [Tooltip("생성 시간")]
        private float telegraphDuration = 1f;

        [SerializeField, Min(0f)]
        [Tooltip("소환 시 충돌무시 시간")]
        private float overlapIgnoreDuration = 0.5f;

        [SerializeField]
        [Tooltip("적 위치 표시")]
        private GameObject telegraphPrefab;

        [SerializeField] private bool playOnStart = true;

        [SerializeField] private UnityEvent onRoomCleared;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<SpawnedEnemyTracker> _trackers = new List<SpawnedEnemyTracker>();
        private readonly List<GameObject> _markers = new List<GameObject>();
        private readonly List<IgnoredPair> _activeIgnores = new List<IgnoredPair>();

        private int _runId;
        private int _pending;
        private int _alive;
        private bool _running;

#if UNITY_EDITOR
        private static readonly HashSet<string> LoggedEditorWarnings = new HashSet<string>();
#endif

        public bool IsRunning => _running;
        public bool IsCleared { get; private set; }

        private void Start()
        {
            if (playOnStart)
                Begin();
        }

        private void OnDisable()
        {
            _runId++;
            _running = false;
            StopAllCoroutines();
            ClearTransient();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (waveSet == null)
                return;

            var warnings = new List<string>();
            WaveSpawnPlanner.Validate(waveSet, CollectPointsForEditor(), warnings);
            if (warnings.Count == 0)
                return;

            string key = GetEditorWarningKey(warnings);
            if (!LoggedEditorWarnings.Add(key))
                return;

            LogWarnings(warnings);
        }

        private string GetEditorWarningKey(List<string> warnings)
        {
            var path = new System.Text.StringBuilder(name);
            Transform current = transform.parent;
            while (current != null)
            {
                path.Insert(0, current.name + "/");
                current = current.parent;
            }

            path.Append('\n');
            for (int i = 0; i < warnings.Count; i++)
            {
                path.Append(warnings[i]);
                path.Append('\n');
            }

            return path.ToString();
        }

        private List<SpawnPoint> CollectPointsForEditor()
        {
            SpawnPoint[] found = GetComponentsInChildren<SpawnPoint>(true);
            var points = new List<SpawnPoint>(found.Length);
            for (int i = 0; i < found.Length; i++)
            {
                SpawnPoint point = found[i];
                if (point == null || !point.enabled || !point.gameObject.activeSelf)
                    continue;

                points.Add(point);
            }

            return points;
        }
#endif

        [ContextMenu("Begin")]
        public void Begin()
        {
            if (!isActiveAndEnabled || _running)
                return;

            if (waveSet == null || waveSet.Waves == null || waveSet.Waves.Count == 0)
            {
                Debug.LogWarning("RoomWaveSet에 웨이브가 없습니다.", this);
                return;
            }

            _runId++;
            _running = true;
            IsCleared = false;
            ClearTransient();
            StartCoroutine(RunSet(_runId));
        }

        private IEnumerator RunSet(int runId)
        {
            var roomWarnings = new List<string>();
            SpawnPointIndex index = WaveSpawnPlanner.IndexPoints(CollectPoints(), roomWarnings);
            LogWarnings(roomWarnings);

            for (int i = 0; i < waveSet.Waves.Count; i++)
            {
                if (runId != _runId)
                    yield break;

                EnemyWaveSO wave = waveSet.Waves[i];
                if (wave == null)
                {
                    Debug.LogWarning($"{waveSet.name}: {i + 1}번째 웨이브가 비어 있어 건너뜁니다.", this);
                    continue;
                }

                yield return RunWave(wave, index, runId);

                bool isLast = i == waveSet.Waves.Count - 1;
                if (!isLast && wave.DelayAfterWave > 0f)
                    yield return new WaitForSeconds(wave.DelayAfterWave);
            }

            if (runId != _runId)
                yield break;

            _running = false;
            IsCleared = true;
            onRoomCleared?.Invoke();
        }

        private IEnumerator RunWave(EnemyWaveSO wave, SpawnPointIndex index, int runId)
        {
            var warnings = new List<string>();
            List<PlannedSpawn> plans = WaveSpawnPlanner.Build(wave, index, warnings);
            LogWarnings(warnings);

            if (plans.Count == 0 || runId != _runId)
                yield break;

            _pending = plans.Count;
            _alive = 0;

            for (int i = 0; i < plans.Count; i++)
                StartCoroutine(SpawnRoutine(plans[i], runId));

            yield return new WaitUntil(() => runId != _runId || (_pending <= 0 && _alive <= 0));
        }

        private IEnumerator SpawnRoutine(PlannedSpawn plan, int runId)
        {
            GameObject marker = null;
            if (plan.Point != null)
                marker = ShowMarker(plan.Point);

            if (telegraphDuration > 0f)
                yield return new WaitForSeconds(telegraphDuration);

            HideMarker(marker);

            if (runId != _runId)
                yield break;

            if (plan.Point == null || plan.Prefab == null)
            {
                _pending--;
                yield break;
            }

            Vector3 position = ResolveSpawnPosition(plan.Point);
            GameObject enemy = Instantiate(plan.Prefab, position, plan.Point.transform.rotation);
            _pending--;

            if (enemy == null)
                yield break;

            var tracker = enemy.AddComponent<SpawnedEnemyTracker>();
            tracker.Watch(enemy.GetComponentInChildren<AgentHealth>());
            tracker.Died += OnTrackedDeath;
            _trackers.Add(tracker);
            _alive++;
            _spawned.Add(enemy);

            if (overlapIgnoreDuration > 0f)
                IgnoreAgainstExisting(enemy);
        }

        private void OnTrackedDeath(SpawnedEnemyTracker tracker)
        {
            if (tracker != null)
                tracker.Died -= OnTrackedDeath;

            _trackers.Remove(tracker);
            _alive = Mathf.Max(0, _alive - 1);
        }

        private void IgnoreAgainstExisting(GameObject enemy)
        {
            Collider[] mine = enemy.GetComponentsInChildren<Collider>();
            var created = new List<IgnoredPair>();

            for (int i = 0; i < _spawned.Count; i++)
            {
                GameObject other = _spawned[i];
                if (other == null || other == enemy)
                    continue;

                Collider[] theirs = other.GetComponentsInChildren<Collider>();
                for (int a = 0; a < mine.Length; a++)
                {
                    Collider left = mine[a];
                    if (left == null || left.isTrigger)
                        continue;

                    for (int b = 0; b < theirs.Length; b++)
                    {
                        Collider right = theirs[b];
                        if (right == null || right.isTrigger)
                            continue;

                        Physics.IgnoreCollision(left, right, true);
                        var pair = new IgnoredPair { A = left, B = right };
                        created.Add(pair);
                        _activeIgnores.Add(pair);
                    }
                }
            }

            if (created.Count > 0)
                StartCoroutine(RestoreIgnore(created, overlapIgnoreDuration));
        }

        private IEnumerator RestoreIgnore(List<IgnoredPair> pairs, float duration)
        {
            yield return new WaitForSeconds(duration);

            for (int i = 0; i < pairs.Count; i++)
            {
                IgnoredPair pair = pairs[i];
                if (pair.A != null && pair.B != null)
                    Physics.IgnoreCollision(pair.A, pair.B, false);
                _activeIgnores.Remove(pair);
            }
        }

        private static Vector3 ResolveSpawnPosition(SpawnPoint point)
        {
            Vector3 position = point.transform.position;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, NavMeshSampleRadius, NavMesh.AllAreas))
                return hit.position;

            Debug.LogWarning($"{point.name} 주변 {NavMeshSampleRadius}m 안에 내비메시가 없습니다. 포인트 위치에 생성합니다.", point);
            return position;
        }

        private GameObject ShowMarker(SpawnPoint point)
        {
            Vector3 position = point.transform.position + Vector3.up * 0.05f;
            Quaternion rotation = point.transform.rotation;
            GameObject marker = telegraphPrefab != null
                ? Instantiate(telegraphPrefab, position, rotation)
                : SpawnTelegraph.Create(position, rotation);

            _markers.Add(marker);
            return marker;
        }

        private void HideMarker(GameObject marker)
        {
            if (marker == null)
                return;

            _markers.Remove(marker);
            Destroy(marker);
        }

        private List<SpawnPoint> CollectPoints()
        {
            SpawnPoint[] found = GetComponentsInChildren<SpawnPoint>(true);
            var points = new List<SpawnPoint>(found.Length);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && found[i].isActiveAndEnabled)
                    points.Add(found[i]);
            }

            return points;
        }

        private void ClearTransient()
        {
            _pending = 0;
            _alive = 0;

            for (int i = 0; i < _trackers.Count; i++)
            {
                if (_trackers[i] != null)
                    _trackers[i].Died -= OnTrackedDeath;
            }

            _trackers.Clear();
            _spawned.Clear();

            for (int i = 0; i < _activeIgnores.Count; i++)
            {
                IgnoredPair pair = _activeIgnores[i];
                if (pair.A != null && pair.B != null)
                    Physics.IgnoreCollision(pair.A, pair.B, false);
            }

            _activeIgnores.Clear();

            for (int i = _markers.Count - 1; i >= 0; i--)
            {
                if (_markers[i] != null)
                    Destroy(_markers[i]);
            }

            _markers.Clear();
        }

        private void LogWarnings(List<string> warnings)
        {
            for (int i = 0; i < warnings.Count; i++)
                Debug.LogWarning(warnings[i], this);
        }

        private void OnDrawGizmosSelected()
        {
            SpawnPoint[] points = GetComponentsInChildren<SpawnPoint>(true);
            Gizmos.color = new Color(1f, 0.35f, 0.3f, 0.9f);
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null)
                    continue;

                Gizmos.DrawLine(transform.position, points[i].transform.position);
            }
        }

        private sealed class IgnoredPair
        {
            public Collider A;
            public Collider B;
        }
    }
}
