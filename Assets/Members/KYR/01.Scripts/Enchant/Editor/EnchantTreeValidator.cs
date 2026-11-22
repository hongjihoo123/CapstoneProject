using System.Collections.Generic;
using Members.KYR._01.Scripts.Enchant.Data;
using Members.KYR._01.Scripts.Enchant.So;
using Members.KYR._01.Scripts.Stats;

namespace Members.KYR._01.Scripts.Enchant.Editor
{
    public readonly struct EnchantTreeIssue
    {
        public readonly bool IsError;
        public readonly string NodeId;
        public readonly string Message;

        public EnchantTreeIssue(bool isError, string nodeId, string message)
        {
            IsError = isError;
            NodeId = nodeId;
            Message = message;
        }
    }

    // 트리 데이터에서 게임에서 문제가 될 만한 것을 찾는다. 에디터 "검사" 버튼에서 쓴다.
    public static class EnchantTreeValidator
    {
        public static List<EnchantTreeIssue> Validate(EnchantTree tree)
        {
            List<EnchantTreeIssue> issues = new List<EnchantTreeIssue>();
            if (tree == null)
                return issues;

            if (tree.Character == null)
                issues.Add(new EnchantTreeIssue(false, null, "캐릭터가 지정되지 않았습니다. 캐릭터 없이 쓰면 모듈의 첫 트리로만 잡힙니다."));

            HashSet<string> ids = new HashSet<string>();
            foreach (EnchantTreeNode node in tree.Nodes)
            {
                if (node == null)
                    continue;

                string label = Label(node);
                if (string.IsNullOrEmpty(node.id))
                    issues.Add(new EnchantTreeIssue(true, null, label + ": ID가 비어 있습니다."));
                else if (!ids.Add(node.id))
                    issues.Add(new EnchantTreeIssue(true, node.id, label + ": ID가 중복됩니다 (" + node.id + ")."));

                if (string.IsNullOrWhiteSpace(node.displayName) || node.displayName == "새 노드")
                    issues.Add(new EnchantTreeIssue(false, node.id, label + ": 이름을 정하지 않았습니다."));

                if (node.maxRank < 1)
                    issues.Add(new EnchantTreeIssue(true, node.id, label + ": 최대 랭크가 1보다 작습니다."));

                bool hasEffect = false;
                foreach (EnchantStatEffect effect in node.Effects)
                {
                    if (effect == null)
                        continue;

                    hasEffect = true;
                    if (effect.stat == PlayerStatId.None)
                        issues.Add(new EnchantTreeIssue(true, node.id, label + ": 효과 스탯이 None입니다."));
                    else if (effect.valuePerRank == 0f)
                        issues.Add(new EnchantTreeIssue(false, node.id, label + ": " + EnchantText.StatLabel(effect.stat) + " 효과 값이 0입니다."));
                }

                if (!hasEffect && node.kind == EnchantNodeKind.Stat)
                    issues.Add(new EnchantTreeIssue(false, node.id, label + ": 스탯 노드인데 효과가 없습니다."));
                if (!hasEffect && node.kind != EnchantNodeKind.Stat && node.kind != EnchantNodeKind.Entry)
                    issues.Add(new EnchantTreeIssue(false, node.id, label + ": " + node.kind + " 노드 효과는 아직 런타임에 연결되지 않았습니다."));
            }

            foreach (EnchantTreeLink link in tree.Links)
            {
                if (link == null)
                    continue;
                if (tree.FindNode(link.parentId) == null || tree.FindNode(link.childId) == null)
                    issues.Add(new EnchantTreeIssue(true, null, "없는 노드를 가리키는 연결이 있습니다 (" + link.parentId + " → " + link.childId + ")."));
            }

            if (tree.Nodes.Count > 1)
            {
                foreach (EnchantTreeNode node in tree.Nodes)
                {
                    if (node == null || string.IsNullOrEmpty(node.id))
                        continue;

                    bool linked = false;
                    foreach (EnchantTreeLink link in tree.Links)
                    {
                        if (link != null && (link.parentId == node.id || link.childId == node.id))
                        {
                            linked = true;
                            break;
                        }
                    }

                    if (!linked && node.kind != EnchantNodeKind.Entry)
                        issues.Add(new EnchantTreeIssue(false, node.id, Label(node) + ": 아무 노드와도 연결되지 않았습니다."));
                }
            }

            CheckEntries(tree, issues);

            CheckExclusive(tree, issues);
            return issues;
        }

        // 트리는 입구에서 시작한다: 입구가 하나 이상 있어야 하고, 입구에는 선행이 없고, 입구가 아닌 노드는 선행이 있어야 한다.
        private static void CheckEntries(EnchantTree tree, List<EnchantTreeIssue> issues)
        {
            bool anyEntry = false;
            foreach (EnchantTreeNode node in tree.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.id))
                    continue;

                bool hasParents = tree.HasParents(node.id);
                if (node.kind == EnchantNodeKind.Entry)
                {
                    anyEntry = true;
                    if (hasParents)
                        issues.Add(new EnchantTreeIssue(true, node.id, Label(node) + ": 입구 노드에 선행이 연결되어 있습니다."));
                }
                else if (!hasParents)
                {
                    issues.Add(new EnchantTreeIssue(true, node.id, Label(node) + ": 선행이 없는 노드는 입구여야 합니다 (지금은 배울 수 없음)."));
                }
            }

            if (tree.Nodes.Count > 0 && !anyEntry)
                issues.Add(new EnchantTreeIssue(true, null, "입구 노드가 없습니다. 트리를 시작할 수 없습니다."));
        }

        // 같은 배타 그룹 안에서 한 노드가 다른 노드의 선행이면, 자식은 선행만 배운 상태로는 절대 배울 수 없다.
        private static void CheckExclusive(EnchantTree tree, List<EnchantTreeIssue> issues)
        {
            Dictionary<string, List<EnchantTreeNode>> groups = new Dictionary<string, List<EnchantTreeNode>>();
            foreach (EnchantTreeNode node in tree.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.exclusiveGroup))
                    continue;

                if (!groups.TryGetValue(node.exclusiveGroup, out List<EnchantTreeNode> list))
                {
                    list = new List<EnchantTreeNode>();
                    groups.Add(node.exclusiveGroup, list);
                }

                list.Add(node);
            }

            foreach (KeyValuePair<string, List<EnchantTreeNode>> pair in groups)
            {
                if (pair.Value.Count == 1)
                {
                    EnchantTreeNode only = pair.Value[0];
                    issues.Add(new EnchantTreeIssue(false, only.id, Label(only) + ": 배타 그룹 '" + pair.Key + "'에 노드가 하나뿐입니다."));
                    continue;
                }

                foreach (EnchantTreeNode child in pair.Value)
                {
                    if (AllParentsIn(tree, child, pair.Key))
                        issues.Add(new EnchantTreeIssue(true, child.id, Label(child) + ": 선행 노드가 모두 같은 배타 그룹이라 배울 수 없습니다."));
                }
            }
        }

        private static bool AllParentsIn(EnchantTree tree, EnchantTreeNode child, string group)
        {
            bool any = false;
            foreach (EnchantTreeLink link in tree.Links)
            {
                if (link == null || link.childId != child.id)
                    continue;

                EnchantTreeNode parent = tree.FindNode(link.parentId);
                if (parent == null)
                    continue;

                any = true;
                if (parent.exclusiveGroup != group)
                    return false;
            }

            return any;
        }

        private static string Label(EnchantTreeNode node)
        {
            return string.IsNullOrEmpty(node.displayName) ? (node.id ?? "(ID 없음)") : node.displayName;
        }
    }
}
