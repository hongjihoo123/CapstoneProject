using System.Collections.Generic;
using UnityEngine;

namespace Members.KYR._01_Scripts.Spawner
{
    public readonly struct PlannedSpawn
    {
        public PlannedSpawn(SpawnPoint point, GameObject prefab)
        {
            Point = point;
            Prefab = prefab;
        }

        public SpawnPoint Point { get; }
        public GameObject Prefab { get; }
    }

    public sealed class SpawnPointIndex
    {
        public Dictionary<SpawnPointId, SpawnPoint> ById { get; } = new Dictionary<SpawnPointId, SpawnPoint>();
        public List<SpawnPoint> Unique { get; } = new List<SpawnPoint>();
    }

    public static class WaveSpawnPlanner
    {
        public static SpawnPointIndex IndexPoints(IReadOnlyList<SpawnPoint> points, List<string> warnings)
        {
            var index = new SpawnPointIndex();
            if (points == null)
                return index;

            for (int i = 0; i < points.Count; i++)
            {
                SpawnPoint point = points[i];
                if (point == null)
                    continue;

                if (point.Id == SpawnPointId.None)
                {
                    index.Unique.Add(point);
                    continue;
                }

                if (index.ById.ContainsKey(point.Id))
                {
                    warnings.Add($"포인트 ID {point.Id}가 중복입니다. {index.ById[point.Id].name}만 사용합니다.");
                    continue;
                }

                index.ById.Add(point.Id, point);
                index.Unique.Add(point);
            }

            return index;
        }

        public static void Validate(RoomWaveSetSO waveSet, IReadOnlyList<SpawnPoint> points, List<string> warnings)
        {
            SpawnPointIndex index = IndexPoints(points, warnings);
            if (waveSet == null)
                return;

            if (waveSet.Waves == null || waveSet.Waves.Count == 0)
            {
                warnings.Add($"{waveSet.name}: 웨이브가 없습니다.");
                return;
            }

            for (int i = 0; i < waveSet.Waves.Count; i++)
            {
                EnemyWaveSO wave = waveSet.Waves[i];
                if (wave == null)
                {
                    warnings.Add($"{waveSet.name}: {i + 1}번째 웨이브가 비어 있습니다.");
                    continue;
                }

                AppendEnemyWarnings(wave, warnings);
                SelectPoints(wave, index, warnings, pickRandom: false);
            }
        }

        public static List<PlannedSpawn> Build(EnemyWaveSO wave, SpawnPointIndex index, List<string> warnings)
        {
            var plans = new List<PlannedSpawn>();
            if (wave == null || index == null)
                return plans;

            AppendEnemyWarnings(wave, warnings);
            List<SpawnPoint> slots = SelectPoints(wave, index, warnings, pickRandom: true);
            if (!HasSpawnableEnemy(wave))
                return plans;

            for (int i = 0; i < slots.Count; i++)
                plans.Add(new PlannedSpawn(slots[i], PickEnemy(wave)));

            return plans;
        }

        private static void AppendEnemyWarnings(EnemyWaveSO wave, List<string> warnings)
        {
            if (wave.Enemies == null)
                return;

            for (int i = 0; i < wave.Enemies.Count; i++)
            {
                EnemySpawnEntry entry = wave.Enemies[i];
                if (entry != null && entry.Prefab == null && entry.Weight > 0f)
                {
                    warnings.Add($"{wave.name}: 프리팹이 비어 있는 적 항목이 있습니다.");
                    break;
                }
            }

            if (!HasSpawnableEnemy(wave))
                warnings.Add($"{wave.name}: 가중치가 0보다 큰 적 프리팹이 없습니다.");
        }

        private static List<SpawnPoint> SelectPoints(
            EnemyWaveSO wave,
            SpawnPointIndex index,
            List<string> warnings,
            bool pickRandom)
        {
            var chosen = new List<SpawnPoint>();
            var requiredIds = new HashSet<SpawnPointId>();

            if (wave.RequiredPointIds != null)
            {
                for (int i = 0; i < wave.RequiredPointIds.Count; i++)
                {
                    SpawnPointId id = wave.RequiredPointIds[i];
                    if (id == SpawnPointId.None)
                    {
                        warnings.Add($"{wave.name}: 필수 포인트에는 None을 넣을 수 없습니다.");
                        continue;
                    }

                    if (!requiredIds.Add(id))
                    {
                        warnings.Add($"{wave.name}: 필수 포인트 {id}가 목록에 중복입니다. 한 번만 사용합니다.");
                        continue;
                    }

                    if (index.ById.TryGetValue(id, out SpawnPoint point))
                        chosen.Add(point);
                    else
                        warnings.Add($"{wave.name}: 필수 포인트 {id}가 이 방에 없습니다. 이 포인트는 건너뜁니다.");
                }
            }

            int total = Mathf.Max(0, wave.TotalCount);
            if (chosen.Count > total)
            {
                warnings.Add($"{wave.name}: 필수 포인트가 {chosen.Count}개인데 총 마릿수가 {total}입니다. 총 마릿수를 {chosen.Count}로 올려 필수를 모두 소환합니다.");
                total = chosen.Count;
            }

            var pool = new List<SpawnPoint>();
            for (int i = 0; i < index.Unique.Count; i++)
            {
                SpawnPoint point = index.Unique[i];
                if (point.Weight <= 0f)
                    continue;
                if (requiredIds.Contains(point.Id) && index.ById.ContainsKey(point.Id))
                    continue;
                pool.Add(point);
            }

            int randomCount = Mathf.Max(0, total - chosen.Count);
            if (randomCount > pool.Count)
            {
                warnings.Add($"{wave.name}: 포인트가 부족합니다. 랜덤 스폰 {randomCount}마리를 {pool.Count}마리로 줄입니다.");
                randomCount = pool.Count;
            }

            if (!pickRandom)
                return chosen;

            for (int i = 0; i < randomCount; i++)
            {
                SpawnPoint pick = PickPoint(pool);
                chosen.Add(pick);
                pool.Remove(pick);
            }

            return chosen;
        }

        private static bool HasSpawnableEnemy(EnemyWaveSO wave)
        {
            if (wave.Enemies == null)
                return false;

            for (int i = 0; i < wave.Enemies.Count; i++)
            {
                EnemySpawnEntry entry = wave.Enemies[i];
                if (entry != null && entry.Prefab != null && entry.Weight > 0f)
                    return true;
            }

            return false;
        }

        private static SpawnPoint PickPoint(List<SpawnPoint> pool)
        {
            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
                total += pool[i].Weight;

            float roll = Random.Range(0f, total);
            float cursor = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                cursor += pool[i].Weight;
                if (roll < cursor)
                    return pool[i];
            }

            return pool[pool.Count - 1];
        }

        private static GameObject PickEnemy(EnemyWaveSO wave)
        {
            float total = 0f;
            for (int i = 0; i < wave.Enemies.Count; i++)
            {
                EnemySpawnEntry entry = wave.Enemies[i];
                if (entry == null || entry.Prefab == null || entry.Weight <= 0f)
                    continue;
                total += entry.Weight;
            }

            float roll = Random.Range(0f, total);
            float cursor = 0f;
            for (int i = 0; i < wave.Enemies.Count; i++)
            {
                EnemySpawnEntry entry = wave.Enemies[i];
                if (entry == null || entry.Prefab == null || entry.Weight <= 0f)
                    continue;

                cursor += entry.Weight;
                if (roll < cursor)
                    return entry.Prefab;
            }

            return null;
        }
    }
}
