using System.Collections.Generic;
using Members.KYR._01.Scripts.Enchant.Data;
using Members.KYR._01.Scripts.Enchant.So;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Members.KYR._01.Scripts.Enchant.Editor
{
    public sealed class EnchantTreeGraphView : GraphView
    {
        private readonly Dictionary<string, EnchantTreeNodeView> _views = new Dictionary<string, EnchantTreeNodeView>();
        private EnchantTree _tree;
        private int _moveGroup = -1;

        public bool MuteWrites { get; private set; }

        public event System.Action<EnchantTreeNodeView> SelectionChanged;
        public event System.Action<string> Rejected;

        public EnchantTreeGraphView()
        {
            style.flexGrow = 1;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            GridBackground grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            graphViewChanged = OnGraphViewChanged;
            RegisterCallback<PointerUpEvent>(_ => EndMove(), TrickleDown.TrickleDown);
            RegisterCallback<GeometryChangedEvent>(_ => grid.StretchToParentSize());
        }

        public string SelectedId
        {
            get
            {
                EnchantTreeNodeView view = SelectedView();
                return view != null ? view.Data.id : null;
            }
        }

        public void SetTree(EnchantTree tree)
        {
            _tree = tree;
            Reload();
        }

        public void Reload()
        {
            MuteWrites = true;
            List<GraphElement> existing = new List<GraphElement>();
            foreach (GraphElement element in graphElements)
                existing.Add(element);
            if (existing.Count > 0)
                DeleteElements(existing);
            _views.Clear();

            if (_tree != null)
            {
                foreach (EnchantTreeNode node in _tree.Nodes)
                    AddView(node);

                foreach (EnchantTreeLink link in _tree.Links)
                    AddLinkView(link);
            }

            MuteWrites = false;
        }

        public void Select(string id)
        {
            ClearSelection();
            if (string.IsNullOrEmpty(id))
            {
                SelectionChanged?.Invoke(null);
                return;
            }

            EnchantTreeNodeView view;
            if (_views.TryGetValue(id, out view))
                AddToSelection(view);
            else
                SelectionChanged?.Invoke(null);
        }

        public Vector2 ViewCenter()
        {
            return contentViewContainer.WorldToLocal(worldBound.center);
        }

        public void AddNode(Vector2 position)
        {
            if (_tree == null)
                return;

            Undo.RecordObject(_tree, "노드 추가");
            EnchantTreeNode node = new EnchantTreeNode
            {
                id = _tree.NewNodeId(),
                displayName = "새 노드",
                kind = EnchantNodeKind.Stat,
                maxRank = 5,
                cost = 1,
                position = position
            };
            _tree.Nodes.Add(node);
            DirtyTree();

            MuteWrites = true;
            EnchantTreeNodeView view = AddView(node);
            MuteWrites = false;

            ClearSelection();
            AddToSelection(view);
        }

        public void RecordMove()
        {
            if (_tree == null)
                return;

            if (_moveGroup < 0)
            {
                Undo.IncrementCurrentGroup();
                _moveGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("노드 이동");
            }

            Undo.RecordObject(_tree, "노드 이동");
        }

        public void EndMove()
        {
            if (_moveGroup < 0)
                return;

            Undo.CollapseUndoOperations(_moveGroup);
            _moveGroup = -1;
        }

        public void DirtyTree()
        {
            if (_tree != null)
                EditorUtility.SetDirty(_tree);
        }

        public override List<Port> GetCompatiblePorts(Port start, NodeAdapter adapter)
        {
            List<Port> result = new List<Port>();
            ports.ForEach(port =>
            {
                if (port == start || port.node == start.node || port.direction == start.direction)
                    return;
                result.Add(port);
            });
            return result;
        }

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            if (!MuteWrites && selectable is EnchantTreeNodeView view)
                SelectionChanged?.Invoke(view);
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            if (!MuteWrites)
                SelectionChanged?.Invoke(SelectedView());
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            if (!MuteWrites)
                SelectionChanged?.Invoke(null);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (MuteWrites || _tree == null)
                return change;

            if (change.edgesToCreate != null)
            {
                change.edgesToCreate.RemoveAll(edge => !CanConnect(edge));
                if (change.edgesToCreate.Count > 0)
                {
                    Undo.RecordObject(_tree, "연결");
                    foreach (Edge edge in change.edgesToCreate)
                        _tree.Links.Add(ToLink(edge));
                    DirtyTree();
                }
            }

            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            {
                Undo.RecordObject(_tree, "삭제");
                foreach (GraphElement element in change.elementsToRemove)
                {
                    EnchantTreeNodeView node = element as EnchantTreeNodeView;
                    if (node != null)
                    {
                        string id = node.Data.id;
                        _tree.Nodes.RemoveAll(item => item.id == id);
                        _tree.Links.RemoveAll(link => link.parentId == id || link.childId == id);
                        _views.Remove(id);
                        continue;
                    }

                    Edge edge = element as Edge;
                    if (edge == null)
                        continue;

                    EnchantTreeLink link = ToLink(edge);
                    if (link == null)
                        continue;
                    _tree.Links.RemoveAll(item => item.parentId == link.parentId && item.childId == link.childId);
                }

                _tree.Links.RemoveAll(link => !HasNode(link.parentId) || !HasNode(link.childId));
                DirtyTree();
            }

            return change;
        }

        private bool CanConnect(Edge edge)
        {
            EnchantTreeLink link = ToLink(edge);
            if (link == null || link.parentId == link.childId)
                return false;

            foreach (EnchantTreeLink existing in _tree.Links)
            {
                if (existing.parentId == link.parentId && existing.childId == link.childId)
                    return false;
            }

            if (Reaches(link.childId, link.parentId))
            {
                Rejected?.Invoke("순환은 연결할 수 없습니다");
                return false;
            }

            return true;
        }

        private bool Reaches(string start, string goal)
        {
            Stack<string> stack = new Stack<string>();
            HashSet<string> seen = new HashSet<string>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                string id = stack.Pop();
                if (!seen.Add(id))
                    continue;
                if (id == goal)
                    return true;

                foreach (EnchantTreeLink link in _tree.Links)
                {
                    if (link.parentId == id)
                        stack.Push(link.childId);
                }
            }

            return false;
        }

        private bool HasNode(string id)
        {
            foreach (EnchantTreeNode node in _tree.Nodes)
            {
                if (node.id == id)
                    return true;
            }

            return false;
        }

        private EnchantTreeNodeView AddView(EnchantTreeNode node)
        {
            if (node == null || string.IsNullOrEmpty(node.id) || _views.ContainsKey(node.id))
                return null;

            EnchantTreeNodeView view = new EnchantTreeNodeView(this, node);
            _views.Add(node.id, view);
            AddElement(view);
            return view;
        }

        private void AddLinkView(EnchantTreeLink link)
        {
            if (link == null)
                return;

            EnchantTreeNodeView parent;
            EnchantTreeNodeView child;
            if (!_views.TryGetValue(link.parentId, out parent) || !_views.TryGetValue(link.childId, out child))
                return;

            Edge edge = parent.Output.ConnectTo(child.Input);
            AddElement(edge);
        }

        private static EnchantTreeLink ToLink(Edge edge)
        {
            EnchantTreeNodeView parent = edge != null && edge.output != null ? edge.output.node as EnchantTreeNodeView : null;
            EnchantTreeNodeView child = edge != null && edge.input != null ? edge.input.node as EnchantTreeNodeView : null;
            if (parent == null || child == null || parent.Data == null || child.Data == null)
                return null;

            return new EnchantTreeLink
            {
                parentId = parent.Data.id,
                childId = child.Data.id
            };
        }

        private EnchantTreeNodeView SelectedView()
        {
            foreach (ISelectable selectable in selection)
            {
                EnchantTreeNodeView view = selectable as EnchantTreeNodeView;
                if (view != null)
                    return view;
            }

            return null;
        }
    }
}
