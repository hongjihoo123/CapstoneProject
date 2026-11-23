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

        // rank < 0: 편집 미리보기(진행도 없음). open: 선행 조건 충족 여부.
        public void Apply(EnchantTreeNode node, bool selected, int rank = -1, bool open = true)
        {
            if (node == null)
                return;

            gameObject.name = string.IsNullOrEmpty(node.displayName) ? node.id : node.displayName;
            if (NameLabel != null)
                NameLabel.text = string.IsNullOrEmpty(node.displayName) ? node.id : node.displayName;
            if (InfoLabel != null)
            {
                if (rank < 0)
                {
                    InfoLabel.text = EnchantTreeBoard.KindLabel(node.kind) + "   최대 " + node.maxRank + "   비용 " + node.cost;
                    if (!string.IsNullOrEmpty(node.exclusiveGroup))
                        InfoLabel.text += "\n배타 " + node.exclusiveGroup;
                }
                else
                {
                    InfoLabel.text = "랭크 " + rank + "/" + node.maxRank + "   비용 " + node.cost;
                }
            }

            if (Background != null)
            {
                Color color = EnchantTreeBoard.KindColor(node.kind);
                if (rank == 0 && !open)
                    color = Color.Lerp(color, Color.black, 0.6f);
                else if (rank == 0)
                    color = Color.Lerp(color, Color.black, 0.3f);
                Background.color = selected ? Color.Lerp(color, Color.white, 0.35f) : color;
            }
        }
    }
}
