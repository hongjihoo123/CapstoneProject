using System;
using Members.KYR._01.Scripts.Enchant.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 스킬 트리 노드 한 칸. 모양은 프리팹(SkillTreeScreen 안의 NodeTemplate)에 저장돼 있고,
    // 여기선 종류에 맞는 모양을 켜고 색(배움/잠김/선택)과 글자만 바꿈.
    //   ◆ 마름모 = 스탯(여러 랭크)   ● 원 = 능력(특성)   ■ 작은 사각 = 계열 입구   ■ 금색 큰 사각 = 핵심
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

        // 색 (HTML 시안의 어두운 팔레트)
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
            rankText.gameObject.SetActive(false);   // 랭크는 모양 안에 (스탯) / 채움 색으로 (나머지)
            if (kind == EnchantNodeKind.Key)
            {
                // 핵심: 이름을 큰 사각 안에 (아래 칸이 모자라서)
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

        // rank/max: 랭크, open: 지금 배울 수 있는 위치인가(선행을 배웠나), selected: 오른쪽 패널에 보이는 중
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
                nameText.color = darkColor;   // 이름이 금색 채움 위에 있음
            // 잠긴 노드는 모양만 흐리게 (글자 판 뒤의 선이 비치지 않게 그룹 알파는 안 씀)
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
