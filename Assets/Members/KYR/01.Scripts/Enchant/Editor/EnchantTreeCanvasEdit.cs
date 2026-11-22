using System.Collections.Generic;
using Members.KYR._01.Scripts.Enchant.Data;
using Members.KYR._01.Scripts.Enchant.So;
using Members.KYR._01.Scripts.Enchant.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Members.KYR._01.Scripts.Enchant.Editor
{
    [InitializeOnLoad]
    public static class EnchantTreeCanvasEdit
    {
        private static bool _legacyHidden;
        private static bool _dragging;
        private static int _dragGroup = -1;
        private static bool _snapFromAsset;
        public static bool LinesDirty;

        static EnchantTreeCanvasEdit()
        {
            EditorApplication.update += Tick;
            Undo.undoRedoPerformed += () => _snapFromAsset = true;
        }

        private static void Tick()
        {
            if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            EnchantPanel[] panels = Object.FindObjectsByType<EnchantPanel>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            bool showTree = false;
            bool allowRebuild = GUIUtility.hotControl == 0;
            for (int i = 0; i < panels.Length; i++)
            {
                EnchantPanel panel = panels[i];
                if (panel == null)
                    continue;

                panel.EnsureEditView(allowRebuild);
                if (panel.EditTreeVisible)
                    showTree = true;
                SyncWidgets(panel);
                if (LinesDirty)
                {
                    panel.RefreshEditLines();
                    LinesDirty = false;
                }
            }

            SetLegacyVisible(!showTree);
            if (_dragging && GUIUtility.hotControl == 0)
            {
                Undo.CollapseUndoOperations(_dragGroup);
                _dragging = false;
                _dragGroup = -1;
            }

            _snapFromAsset = false;
        }

        private static void SyncWidgets(EnchantPanel panel)
        {
            EnchantTreeNodeWidget[] widgets = panel.GetComponentsInChildren<EnchantTreeNodeWidget>(true);
            for (int i = 0; i < widgets.Length; i++)
            {
                EnchantTreeNodeWidget widget = widgets[i];
                if (widget == null || widget.Tree == null)
                    continue;

                EnchantTreeNode node = FindNode(widget.Tree, widget.NodeId);
                if (node == null)
                    continue;

                RectTransform rect = widget.transform as RectTransform;
                if (rect == null)
                    continue;

                Vector2 fromRect = new Vector2(rect.anchoredPosition.x, -rect.anchoredPosition.y);
                bool moved = (fromRect - node.position).sqrMagnitude > 0.25f;
                bool selected = Selection.Contains(widget.gameObject);
                if (moved && selected && !_snapFromAsset)
                {
                    if (GUIUtility.hotControl != 0 && !_dragging)
                    {
                        Undo.IncrementCurrentGroup();
                        _dragGroup = Undo.GetCurrentGroup();
                        Undo.SetCurrentGroupName("노드 이동");
                        _dragging = true;
                    }

                    Undo.RecordObject(widget.Tree, "노드 이동");
                    node.position = fromRect;
                    EditorUtility.SetDirty(widget.Tree);
                    LinesDirty = true;
                }
                else if (moved && (!selected || _snapFromAsset))
                {
                    rect.anchoredPosition = new Vector2(node.position.x, -node.position.y);
                    LinesDirty = true;
                }

                if (!selected || GUIUtility.hotControl == 0)
                    widget.Apply(node, selected);
            }
        }

        private static void SetLegacyVisible(bool visible)
        {
            if (_legacyHidden == !visible)
                return;

            EnchantNode[] nodes = Object.FindObjectsByType<EnchantNode>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] == null)
                    continue;
                if (visible)
                    SceneVisibilityManager.instance.Show(nodes[i].gameObject, true);
                else
                    SceneVisibilityManager.instance.Hide(nodes[i].gameObject, true);
            }

            _legacyHidden = !visible;
        }

        public static EnchantTreeNode FindNode(EnchantTree tree, string id)
        {
            if (tree == null || string.IsNullOrEmpty(id))
                return null;

            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                EnchantTreeNode node = tree.Nodes[i];
                if (node != null && node.id == id)
                    return node;
            }

            return null;
        }

        public static string NextId(EnchantTree tree)
        {
            int max = 0;
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                EnchantTreeNode node = tree.Nodes[i];
                if (node == null || string.IsNullOrEmpty(node.id) || node.id[0] != 'n')
                    continue;

                int value;
                if (int.TryParse(node.id.Substring(1), out value) && value > max)
                    max = value;
            }

            return "n" + (max + 1);
        }

        public static void AddNode(EnchantTree tree)
        {
            if (tree == null)
                return;

            float y = 40f;
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                if (tree.Nodes[i] != null)
                    y = Mathf.Max(y, tree.Nodes[i].position.y + 120f);
            }

            Undo.RecordObject(tree, "노드 추가");
            tree.Nodes.Add(new EnchantTreeNode
            {
                id = NextId(tree),
                displayName = "새 노드",
                kind = EnchantNodeKind.Stat,
                maxRank = 5,
                cost = 1,
                position = new Vector2(80f, y)
            });
            EditorUtility.SetDirty(tree);
        }

        public static void DeleteNode(EnchantTree tree, string id)
        {
            if (tree == null || string.IsNullOrEmpty(id))
                return;

            Undo.RecordObject(tree, "노드 삭제");
            tree.Nodes.RemoveAll(node => node != null && node.id == id);
            tree.Links.RemoveAll(link => link != null && (link.parentId == id || link.childId == id));
            EditorUtility.SetDirty(tree);
        }

        public static bool HasLink(EnchantTree tree, string parentId, string childId)
        {
            for (int i = 0; i < tree.Links.Count; i++)
            {
                EnchantTreeLink link = tree.Links[i];
                if (link != null && link.parentId == parentId && link.childId == childId)
                    return true;
            }

            return false;
        }

        public static bool WouldCycle(EnchantTree tree, string parentId, string childId)
        {
            Stack<string> stack = new Stack<string>();
            HashSet<string> seen = new HashSet<string>();
            stack.Push(childId);
            while (stack.Count > 0)
            {
                string id = stack.Pop();
                if (!seen.Add(id))
                    continue;
                if (id == parentId)
                    return true;

                for (int i = 0; i < tree.Links.Count; i++)
                {
                    EnchantTreeLink link = tree.Links[i];
                    if (link != null && link.parentId == id)
                        stack.Push(link.childId);
                }
            }

            return false;
        }
    }

    [CustomEditor(typeof(EnchantPanel))]
    public sealed class EnchantPanelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EnchantPanel panel = (EnchantPanel)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("플레이 없이 씬 캔버스에서 노드를 끌어 옮기면 트리에 저장됩니다. 노드를 선택하면 인스펙터에서 값을 고칩니다.", MessageType.Info);
            if (GUILayout.Button("노드 추가"))
            {
                serializedObject.Update();
                EnchantTree tree = FirstTree();
                EnchantTreeCanvasEdit.AddNode(tree);
            }
        }

        private EnchantTree FirstTree()
        {
            SerializedProperty trees = serializedObject.FindProperty("trees");
            if (trees == null || trees.arraySize == 0)
                return null;

            return trees.GetArrayElementAtIndex(0).objectReferenceValue as EnchantTree;
        }
    }

    [CustomEditor(typeof(EnchantTreeNodeWidget))]
    public sealed class EnchantTreeNodeWidgetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EnchantTreeNodeWidget widget = (EnchantTreeNodeWidget)target;
            EnchantTreeNode node = EnchantTreeCanvasEdit.FindNode(widget.Tree, widget.NodeId);
            if (widget.Tree == null || node == null)
            {
                EditorGUILayout.HelpBox("트리에서 이 노드를 찾지 못했습니다.", MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();
            string name = EditorGUILayout.TextField("이름", node.displayName ?? string.Empty);
            EnchantNodeKind kind = (EnchantNodeKind)EditorGUILayout.EnumPopup("종류", node.kind);
            int max = Mathf.Max(1, EditorGUILayout.IntField("최대 랭크", node.maxRank));
            int cost = Mathf.Max(0, EditorGUILayout.IntField("비용", node.cost));
            string excl = EditorGUILayout.TextField("배타 그룹", node.exclusiveGroup ?? string.Empty);
            string desc = EditorGUILayout.TextField("설명", node.description ?? string.Empty);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(widget.Tree, "노드 수정");
                node.displayName = name;
                node.kind = kind;
                node.maxRank = max;
                node.cost = cost;
                node.exclusiveGroup = excl;
                node.description = desc;
                EditorUtility.SetDirty(widget.Tree);
                widget.Apply(node, true);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("선행");
            for (int i = 0; i < widget.Tree.Nodes.Count; i++)
            {
                EnchantTreeNode other = widget.Tree.Nodes[i];
                if (other == null || other.id == node.id)
                    continue;

                bool linked = EnchantTreeCanvasEdit.HasLink(widget.Tree, other.id, node.id);
                string label = string.IsNullOrEmpty(other.displayName) ? other.id : other.displayName;
                bool next = EditorGUILayout.ToggleLeft(label, linked);
                if (next == linked)
                    continue;

                if (next && EnchantTreeCanvasEdit.WouldCycle(widget.Tree, other.id, node.id))
                {
                    EditorUtility.DisplayDialog("인챈트 트리", "순환은 연결할 수 없습니다.", "확인");
                    continue;
                }

                Undo.RecordObject(widget.Tree, "연결");
                if (next)
                {
                    widget.Tree.Links.Add(new EnchantTreeLink
                    {
                        parentId = other.id,
                        childId = node.id
                    });
                }
                else
                {
                    widget.Tree.Links.RemoveAll(link => link != null && link.parentId == other.id && link.childId == node.id);
                }

                EditorUtility.SetDirty(widget.Tree);
                EnchantTreeCanvasEdit.LinesDirty = true;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("이 노드 삭제"))
                EnchantTreeCanvasEdit.DeleteNode(widget.Tree, node.id);
        }
    }
}
