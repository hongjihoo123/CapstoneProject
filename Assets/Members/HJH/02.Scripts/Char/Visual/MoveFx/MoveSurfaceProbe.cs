using System.Collections.Generic;
using Assets.Members.HJH.Environment;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 발밑이 무슨 바닥인지 알아냄. 순서: 물속 → 물가(진흙) → MoveFxSurface 붙은 콜라이더 → 터레인 페인트 → 기본(흙)
    public class MoveSurfaceProbe
    {
        private readonly MoveSurfaceTable _table;
        private readonly Dictionary<TerrainData, (MoveSurfaceKind[] kinds, Color[] tints)> _terrainCache = new();

        public MoveSurfaceProbe(MoveSurfaceTable table) => _table = table;

        public MoveSurfaceHit Classify(RaycastHit hit)
        {
            var result = new MoveSurfaceHit { Point = hit.point, Normal = hit.normal, Tint = Color.white, Kind = MoveSurfaceKind.Dust };

            if (JungleWater.IsInWater(hit.point, 0.05f))
            {
                result.Kind = MoveSurfaceKind.Water;
                result.Point.y = Mathf.Max(hit.point.y, JungleWater.SurfaceHeight);
                result.Normal = Vector3.up;
                return result;
            }

            var surface = hit.collider.GetComponentInParent<MoveFxSurface>();
            if (surface != null)
            {
                result.Kind = surface.Kind;
                result.Tint = surface.Tint;
                return result;
            }

            if (hit.collider is TerrainCollider && hit.collider.TryGetComponent(out Terrain terrain))
                SampleTerrain(terrain, hit.point, ref result);

            // 물가 바로 위의 흙·이끼는 젖은 진흙으로
            float shore = _table != null ? _table.ShoreMudHeight : 0.25f;
            if (result.Kind is MoveSurfaceKind.Dust or MoveSurfaceKind.Moss && shore > 0f && JungleWater.IsInWater(hit.point, shore))
                result.Kind = MoveSurfaceKind.Mud;
            return result;
        }

        private void SampleTerrain(Terrain terrain, Vector3 point, ref MoveSurfaceHit result)
        {
            TerrainData data = terrain.terrainData;
            if (data == null || data.alphamapLayers == 0)
                return;
            if (!_terrainCache.TryGetValue(data, out var info))
            {
                TerrainLayer[] layers = data.terrainLayers;
                info = (new MoveSurfaceKind[layers.Length], new Color[layers.Length]);
                for (int i = 0; i < layers.Length; i++)
                {
                    if (_table != null)
                        _table.Resolve(layers[i], out info.kinds[i], out info.tints[i]);
                    else
                    {
                        info.kinds[i] = MoveSurfaceTable.Guess(layers[i] != null ? layers[i].name : "");
                        info.tints[i] = Color.white;
                    }
                }
                _terrainCache[data] = info;
            }

            Vector3 local = point - terrain.transform.position;
            int res = data.alphamapResolution;
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / data.size.x * res), 0, res - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(local.z / data.size.z * res), 0, res - 1);
            float[,,] weights = data.GetAlphamaps(x, z, 1, 1);

            // 가장 진하게 칠해진 레이어의 종류, 색은 비율대로 섞음
            int count = Mathf.Min(weights.GetLength(2), info.kinds.Length);
            int best = 0;
            Color tint = Color.black;
            for (int i = 0; i < count; i++)
            {
                float w = weights[0, 0, i];
                tint += info.tints[i] * w;
                if (w > weights[0, 0, best])
                    best = i;
            }
            result.Kind = info.kinds[best];
            tint.a = 1f;
            result.Tint = tint;
        }
    }
}
