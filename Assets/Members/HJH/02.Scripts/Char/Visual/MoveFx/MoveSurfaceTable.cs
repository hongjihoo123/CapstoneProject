using System;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
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
