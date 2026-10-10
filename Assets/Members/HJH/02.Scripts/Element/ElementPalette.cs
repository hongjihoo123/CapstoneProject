using System;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element
{
    // Color + badge icon per element, shared by every UI and world visual that shows an element.
    // The project's palette lives at Data/Resources/HJH/ElementPalette.asset, so Default needs no wiring.
    [CreateAssetMenu(menuName = "UI/Element Palette")]
    public class ElementPalette : ScriptableObject
    {
        private const string DefaultPath = "HJH/ElementPalette";
        private static ElementPalette _default;

        public static ElementPalette Default => _default != null ? _default : _default = Resources.Load<ElementPalette>(DefaultPath);

        [Serializable]
        public struct Entry
        {
            public ElementType element;
            public Color color;
            public Sprite badge;
        }

        [SerializeField] private Entry[] entries;
        [SerializeField] private Color neutralColor = new Color(0.71f, 0.7f, 0.66f);

        public Color NeutralColor => neutralColor;

        public bool TryGet(ElementType element, out Entry entry)
        {
            if (entries != null)
            {
                foreach (Entry candidate in entries)
                {
                    if (candidate.element == element)
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = default;
            return false;
        }

        public Color ColorOf(ElementType element) => TryGet(element, out Entry entry) ? entry.color : neutralColor;

        public Sprite BadgeOf(ElementType element) => TryGet(element, out Entry entry) ? entry.badge : null;
    }
}
