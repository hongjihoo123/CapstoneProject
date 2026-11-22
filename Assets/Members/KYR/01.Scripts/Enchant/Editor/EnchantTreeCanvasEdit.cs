using System.Collections.Generic;
using Members.KYR._01.Scripts.Enchant.Data;
using Members.KYR._01.Scripts.Enchant.So;
using Members.KYR._01.Scripts.Enchant.UI;
using Members.KYR._01.Scripts.Stats;
using UnityEditor;
using UnityEngine;

namespace Members.KYR._01.Scripts.Enchant.Editor
{
    [InitializeOnLoad]
    public static class EnchantTreeCanvasEdit
    {
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
            bool allowRebuild = GUIUtility.hotControl == 0;
            for (int i = 0; i < panels.Length; i++)
            {
                EnchantPanel panel = panels[i];
                if (panel == null)
                    continue;

                panel.EnsureEditView(allowRebuild);
                SyncWidgets(panel);
                if (LinesDirty)
                {
                    panel.RefreshEditLines();
                    LinesDirty = false;
                }
            }

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

        public static EnchantTreeNode FindNode(EnchantTree tree, string id)
        {
            return tree != null ? tree.FindNode(id) : null;
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
                id = tree.NewNodeId(),
                displayName = tree.Nodes.Count == 0 ? "입구" : "새 노드",
                kind = tree.Nodes.Count == 0 ? EnchantNodeKind.Entry : EnchantNodeKind.Stat,
                maxRank = tree.Nodes.Count == 0 ? 1 : 5,
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
            EnchantModule module = ((EnchantPanel)target).Module;
            if (module == null)
            {
                EditorUtility.DisplayDialog("인챈트 트리", "패널에 EnchantModule을 연결하세요. 트리 목록은 모듈이 들고 있습니다.", "확인");
                return null;
            }

            return module.TreeFor(null);
        }
    }

    [CustomEditor(typeof(EnchantTreeNodeWidget))]
    public sealed class EnchantTreeNodeWidgetEditor : UnityEditor.Editor
    {
        // 그래프 창이 노드 객체 참조를 들고 있으므로 SerializedObject 대신 직접 고친다.
        private static void DrawEffects(EnchantTreeNodeWidget widget, EnchantTreeNode node)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("효과 (랭크당 값)", EditorStyles.boldLabel);

            List<EnchantStatEffect> effects = node.Effects;
            int remove = -1;
            for (int i = 0; i < effects.Count; i++)
            {
                EnchantStatEffect effect = effects[i];
                if (effect == null)
                    continue;

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUI.BeginChangeCheck();
                    PlayerStatId stat = (PlayerStatId)EditorGUILayout.EnumPopup("스탯", effect.stat);
                    StatModifierType type = (StatModifierType)EditorGUILayout.EnumPopup("방식", effect.type);
                    float value = EditorGUILayout.FloatField(new GUIContent("랭크당 값", "PercentAdd/PercentMore는 0.05 = 5%"), effect.valuePerRank);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(widget.Tree, "효과 수정");
                        effect.stat = stat;
                        effect.type = type;
                        effect.valuePerRank = value;
                        EditorUtility.SetDirty(widget.Tree);
                    }

                    EditorGUILayout.LabelField("1랭크 " + EnchantText.Effect(effect, 1) + "   최대 " + EnchantText.Effect(effect, node.maxRank), EditorStyles.miniLabel);
                    if (GUILayout.Button("효과 삭제"))
                        remove = i;
                }
            }

            if (remove >= 0)
            {
                Undo.RecordObject(widget.Tree, "효과 삭제");
                effects.RemoveAt(remove);
                EditorUtility.SetDirty(widget.Tree);
            }

            if (GUILayout.Button("효과 추가"))
            {
                Undo.RecordObject(widget.Tree, "효과 추가");
                effects.Add(new EnchantStatEffect());
                EditorUtility.SetDirty(widget.Tree);
            }
        }

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

            DrawEffects(widget, node);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("선행");
            if (node.kind == EnchantNodeKind.Entry)
            {
                EditorGUILayout.HelpBox("입구 노드는 트리 시작점이라 선행을 가질 수 없습니다.", MessageType.None);
                if (GUILayout.Button("이 노드 삭제"))
                    EnchantTreeCanvasEdit.DeleteNode(widget.Tree, node.id);
                return;
            }

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
