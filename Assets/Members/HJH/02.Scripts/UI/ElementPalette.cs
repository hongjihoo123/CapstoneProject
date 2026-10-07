using System;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // Color + badge icon per element, shared by every UI that shows an element.
    [CreateAssetMenu(menuName = "UI/Element Palette")]
    public class ElementPalette : ScriptableObject
    {
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
    }
}
