using System;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 터레인 레이어(바닥 페인트) → 바닥 종류 표. 05.Data/Fx/MoveFx/MoveSurfaceTable.asset
    //  - 표에 없는 레이어는 이름으로 짐작 (Mud→진흙, Moss/Grass→이끼, Litter/Leaf→낙엽, 나머지→흙)
    //  - tint 는 HJH/FX/이동 이펙트 메뉴가 레이어 텍스처 평균색으로 채움 (손으로 고쳐도 됨, 다시 채우기 전까지 유지)
    [CreateAssetMenu(menuName = "HJH/FX/Move Surface Table", fileName = "MoveSurfaceTable")]
    public class MoveSurfaceTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public TerrainLayer layer;
            public MoveSurfaceKind kind;
            public Color tint;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        [SerializeField, Tooltip("물 높이보다 이만큼 위까지는 물가 진흙으로 침")] private float shoreMudHeight = 0.25f;

        public float ShoreMudHeight => shoreMudHeight;

        public Entry[] Entries
        {
            get => entries;
            set => entries = value;
        }

        public void Resolve(TerrainLayer layer, out MoveSurfaceKind kind, out Color tint)
        {
            foreach (Entry e in entries)
            {
                if (e.layer != layer)
                    continue;
                kind = e.kind;
                tint = e.tint;
                return;
            }
            kind = Guess(layer != null ? layer.name : "");
            tint = Color.white;
        }

        public static MoveSurfaceKind Guess(string layerName)
        {
            string n = layerName.ToLowerInvariant();
            if (n.Contains("mud"))
                return MoveSurfaceKind.Mud;
            if (n.Contains("moss") || n.Contains("grass"))
                return MoveSurfaceKind.Moss;
            if (n.Contains("litter") || n.Contains("leaf"))
                return MoveSurfaceKind.Leaf;
            return MoveSurfaceKind.Dust;
        }
    }
}
