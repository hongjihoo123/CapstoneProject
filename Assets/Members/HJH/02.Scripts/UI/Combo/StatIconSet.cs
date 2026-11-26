using System;
using System.Collections.Generic;
using Members.KYR._01.Scripts.Stats;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.UI
{
    [CreateAssetMenu(menuName = "HUD/Stat Icon Set")]
    public class StatIconSet : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public PlayerStatId stat;
            public Sprite icon;
            public Color color = Color.white;
        }

        [SerializeField] private List<Entry> entries = new();
        [SerializeField] private Sprite fallback;
        [SerializeField] private Color fallbackColor = Color.white;

        public Sprite IconOf(PlayerStatId stat) => Find(stat)?.icon ?? fallback;
        public Color ColorOf(PlayerStatId stat) => Find(stat)?.color ?? fallbackColor;

        private Entry Find(PlayerStatId stat)
        {
            foreach (Entry entry in entries)
            {
                if (entry.stat == stat && entry.icon != null)
                    return entry;
            }
            return null;
        }

        public void Set(List<Entry> list, Sprite other, Color otherColor)
        {
            entries = list ?? new List<Entry>();
            fallback = other;
            fallbackColor = otherColor;
        }
    }
}
