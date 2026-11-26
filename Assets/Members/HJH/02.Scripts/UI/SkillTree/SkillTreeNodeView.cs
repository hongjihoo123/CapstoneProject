using System;
using Members.KYR._01.Scripts.Enchant.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    public class SkillTreeNodeView : MonoBehaviour
    {
        [Serializable]
        public sealed class Shape
        {
            public GameObject root;
            public Image outline;
            public Image fill;
        }

        [SerializeField] private Shape diamond = new();
        [SerializeField] private Shape circle = new();
        [SerializeField] private Shape entry = new();
        [SerializeField] private Shape key = new();
        [SerializeField] private GameObject plate;
        [SerializeField] private TMP_Text glyph;
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Button button;
        [SerializeField] private CanvasGroup group;

        [Header("색")]
        [SerializeField] private Color fillColor = new(0.149f, 0.149f, 0.133f, 1f);
        [SerializeField] private Color lineColor = new(0.302f, 0.294f, 0.267f, 1f);
        [SerializeField] private Color lineOnColor = new(0.847f, 0.827f, 0.769f, 1f);
        [SerializeField] private Color goldColor = new(0.878f, 0.706f, 0.255f, 1f);
        [SerializeField] private Color textColor = new(0.914f, 0.902f, 0.863f, 1f);
        [SerializeField] private Color mutedColor = new(0.608f, 0.592f, 0.541f, 1f);
        [SerializeField] private Color darkColor = new(0.078f, 0.078f, 0.071f, 1f);

        public string NodeId { get; private set; }
        public Button Button => button;

        public void Bind(string id, string displayName, EnchantNodeKind kind, bool cooldownNode, bool wired = true)
        {
            NodeId = id;
            _wired = wired;
            nameText.text = displayName;
            diamond.root.SetActive(kind == EnchantNodeKind.Stat);
            circle.root.SetActive(kind == EnchantNodeKind.Ability);
            entry.root.SetActive(kind == EnchantNodeKind.Entry);
            key.root.SetActive(kind == EnchantNodeKind.Key);
            _kind = kind;
            glyph.text = kind switch
            {
                EnchantNodeKind.Stat => cooldownNode ? "CD" : "+",
                EnchantNodeKind.Ability => "특",
                EnchantNodeKind.Entry => "입",
                _ => "핵",
            };
            glyph.fontSize = kind == EnchantNodeKind.Stat ? 15f : 20f;
            rankText.gameObject.SetActive(false);
            if (kind == EnchantNodeKind.Key)
            {
                glyph.gameObject.SetActive(false);
                plate.SetActive(false);
                nameText.rectTransform.anchoredPosition = Vector2.zero;
                nameText.rectTransform.sizeDelta = new Vector2(82f, 44f);
                nameText.fontSize = 14f;
            }
        }

        private EnchantNodeKind _kind;
        private bool _wired = true;

        private static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

        public void Show(int rank, int max, bool open, bool selected)
        {
            bool full = rank >= max;
            bool learned = rank > 0;
            bool locked = !learned && !open && _kind != EnchantNodeKind.Entry;

            Shape shape = _kind switch
            {
                EnchantNodeKind.Stat => diamond,
                EnchantNodeKind.Ability => circle,
                EnchantNodeKind.Entry => entry,
                _ => key,
            };

            bool gold = _kind == EnchantNodeKind.Key;
            Color stroke = selected ? goldColor : gold ? goldColor : learned ? lineOnColor : lineColor;
            shape.outline.color = stroke;
            shape.fill.color = learned && full ? (gold ? goldColor : lineOnColor) : fillColor;
            glyph.color = learned && full ? darkColor : gold ? goldColor : textColor;

            rankText.text = rank + "/" + max;
            if (_kind == EnchantNodeKind.Stat)
                glyph.text = rank + "/" + max;
            rankText.color = full && learned ? goldColor : mutedColor;
            nameText.color = locked || !_wired ? mutedColor : textColor;
            if (_kind == EnchantNodeKind.Key && learned && full)
                nameText.color = darkColor;
            if (locked)
            {
                shape.outline.color = WithAlpha(shape.outline.color, 0.45f);
                shape.fill.color = WithAlpha(shape.fill.color, 0.6f);
                glyph.color = WithAlpha(glyph.color, 0.45f);
            }
            if (group != null)
                group.alpha = 1f;
        }
    }
}
