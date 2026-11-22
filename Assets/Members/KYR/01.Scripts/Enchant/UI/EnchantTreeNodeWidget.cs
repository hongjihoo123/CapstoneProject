using Members.KYR._01.Scripts.Enchant.So;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Members.KYR._01.Scripts.Enchant.UI
{
    public sealed class EnchantTreeNodeWidget : MonoBehaviour
    {
        public EnchantTree Tree;
        public string NodeId;
        public TMP_Text NameLabel;
        public TMP_Text InfoLabel;
        public Image Background;

        public void Apply(EnchantTreeNode node, bool selected)
        {
            if (node == null)
                return;

            gameObject.name = string.IsNullOrEmpty(node.displayName) ? node.id : node.displayName;
            if (NameLabel != null)
                NameLabel.text = string.IsNullOrEmpty(node.displayName) ? node.id : node.displayName;
            if (InfoLabel != null)
            {
                InfoLabel.text = EnchantTreeBoard.KindLabel(node.kind) + "   최대 " + node.maxRank + "   비용 " + node.cost;
                if (!string.IsNullOrEmpty(node.exclusiveGroup))
                    InfoLabel.text += "\n배타 " + node.exclusiveGroup;
            }

            if (Background != null)
            {
                Color color = EnchantTreeBoard.KindColor(node.kind);
                Background.color = selected ? Color.Lerp(color, Color.white, 0.35f) : color;
            }
        }
    }
}
