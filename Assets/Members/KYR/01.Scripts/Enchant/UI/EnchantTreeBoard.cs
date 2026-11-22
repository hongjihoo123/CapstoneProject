using System;
using System.Collections.Generic;
using Members.KYR._01.Scripts.Enchant.Data;
using Members.KYR._01.Scripts.Enchant.So;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Members.KYR._01.Scripts.Enchant.UI
{
    public sealed class EnchantTreeBoard
    {
        private const float NodeWidth = 180f;
        private const float NodeHeight = 96f;
        private const float Pad = 32f;

        private RectTransform _root;
        private ScrollRect _scroll;
        private static Sprite _pixel;

        public RectTransform Root => _root;
        public int StructureHash { get; private set; }

        public static int StructureHashOf(EnchantTree tree)
        {
            unchecked
            {
                int hash = 17;
                if (tree == null)
                    return hash;

                for (int i = 0; i < tree.Nodes.Count; i++)
                {
                    EnchantTreeNode node = tree.Nodes[i];
                    hash = hash * 31 + (node != null && node.id != null ? node.id.GetHashCode() : 0);
                }

                for (int i = 0; i < tree.Links.Count; i++)
                {
                    EnchantTreeLink link = tree.Links[i];
                    if (link == null)
                        continue;
                    hash = hash * 31 + (link.parentId != null ? link.parentId.GetHashCode() : 0);
                    hash = hash * 31 + (link.childId != null ? link.childId.GetHashCode() : 0);
                }

                hash = hash * 31 + tree.Nodes.Count;
                hash = hash * 31 + tree.Links.Count;
                return hash;
            }
        }

        public void Clear()
        {
            if (_root != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_root.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(_root.gameObject);
            }

            _root = null;
            _scroll = null;
            StructureHash = 0;
        }

        private static void RemoveLeftoverViews(RectTransform board)
        {
            for (int i = board.childCount - 1; i >= 0; i--)
            {
                Transform child = board.GetChild(i);
                if (child.name != "TreeView")
                    continue;

                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(child.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        // status: 노드별 (랭크, 열림). null이면 편집 미리보기로 그린다.
        public void Build(RectTransform board, EnchantTree tree, TMP_FontAsset font, string selectedId, Action<EnchantTreeNode> onSelect, Func<EnchantTreeNode, (int rank, bool open)> status = null)
        {
            bool keepScroll = _scroll != null;
            Vector2 scroll = keepScroll ? _scroll.normalizedPosition : new Vector2(0f, 1f);
            Clear();

            if (board == null || tree == null)
                return;

            RemoveLeftoverViews(board);
            _scroll = CreateScroll(board);
            _root = _scroll.transform as RectTransform;
            RectTransform content = _scroll.content;
            Dictionary<string, Vector2> topLefts = new Dictionary<string, Vector2>();
            float maxX = Pad;
            float maxY = Pad;

            foreach (EnchantTreeNode node in tree.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.id))
                    continue;

                Vector2 topLeft = new Vector2(node.position.x, -node.position.y);
                topLefts[node.id] = topLeft;
                maxX = Mathf.Max(maxX, node.position.x + NodeWidth + Pad);
                maxY = Mathf.Max(maxY, node.position.y + NodeHeight + Pad);
                CreateNode(content, tree, node, topLeft, font, node.id == selectedId, onSelect, status);
            }

            DrawLinks(content, topLefts, tree.Links);

            content.sizeDelta = new Vector2(maxX, maxY);
            for (int i = 0; i < content.childCount; i++)
            {
                if (content.GetChild(i).name == "Line")
                    content.GetChild(i).SetAsFirstSibling();
            }

            if (!Application.isPlaying)
                SetDontSave(_root.gameObject);

            StructureHash = StructureHashOf(tree);
            Canvas.ForceUpdateCanvases();
            _scroll.horizontalNormalizedPosition = keepScroll ? scroll.x : 0f;
            _scroll.verticalNormalizedPosition = keepScroll ? scroll.y : 1f;
        }

        public void RefreshLines(EnchantTree tree)
        {
            if (_scroll == null || tree == null)
                return;

            RectTransform content = _scroll.content;
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform child = content.GetChild(i);
                if (child.name != "Line")
                    continue;

                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(child.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            Dictionary<string, Vector2> topLefts = new Dictionary<string, Vector2>();
            EnchantTreeNodeWidget[] widgets = content.GetComponentsInChildren<EnchantTreeNodeWidget>(true);
            for (int i = 0; i < widgets.Length; i++)
            {
                RectTransform rect = widgets[i].transform as RectTransform;
                if (rect != null && !string.IsNullOrEmpty(widgets[i].NodeId))
                    topLefts[widgets[i].NodeId] = rect.anchoredPosition;
            }

            DrawLinks(content, topLefts, tree.Links);

            for (int i = 0; i < content.childCount; i++)
            {
                if (content.GetChild(i).name == "Line")
                    content.GetChild(i).SetAsFirstSibling();
            }
        }

        private static ScrollRect CreateScroll(RectTransform board)
        {
            RectTransform root = CreateRect("TreeView", board);
            Stretch(root);
            Image hit = root.gameObject.AddComponent<Image>();
            hit.sprite = Pixel();
            hit.color = new Color(0.09f, 0.09f, 0.085f, 1f);
            hit.raycastTarget = true;
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            RectTransform viewport = CreateRect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            RectTransform content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(100f, 100f);
            scroll.content = content;
            return scroll;
        }

        private static void CreateNode(RectTransform content, EnchantTree tree, EnchantTreeNode node, Vector2 topLeft, TMP_FontAsset font, bool selected, Action<EnchantTreeNode> onSelect, Func<EnchantTreeNode, (int rank, bool open)> status)
        {
            RectTransform rect = CreateRect(node.id, content);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = new Vector2(NodeWidth, NodeHeight);

            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = Pixel();
            image.color = KindColor(node.kind);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            EnchantTreeNode captured = node;
            button.onClick.AddListener(() => onSelect?.Invoke(captured));

            TMP_Text name = CreateLabel(rect, font, 18f, new Vector2(0f, -8f), new Vector2(-16f, 36f));
            TMP_Text info = CreateLabel(rect, font, 13f, new Vector2(0f, -46f), new Vector2(-16f, 40f));

            EnchantTreeNodeWidget widget = rect.gameObject.AddComponent<EnchantTreeNodeWidget>();
            widget.Tree = tree;
            widget.NodeId = node.id;
            widget.NameLabel = name;
            widget.InfoLabel = info;
            widget.Background = image;
            if (status != null)
            {
                (int rank, bool open) state = status(node);
                widget.Apply(node, selected, state.rank, state.open);
            }
            else
            {
                widget.Apply(node, selected);
            }
        }

        private struct NodeAnchor
        {
            public Vector2 Top;
            public Vector2 Bottom;
            public Vector2 Left;
            public Vector2 Right;
            public Vector2 Center;
        }

        private static NodeAnchor AnchorOf(Vector2 topLeft)
        {
            float cx = topLeft.x + NodeWidth * 0.5f;
            float top = topLeft.y;
            float bottom = topLeft.y - NodeHeight;
            float cy = topLeft.y - NodeHeight * 0.5f;
            float left = topLeft.x;
            float right = topLeft.x + NodeWidth;
            return new NodeAnchor
            {
                Top = new Vector2(cx, top),
                Bottom = new Vector2(cx, bottom),
                Left = new Vector2(left, cy),
                Right = new Vector2(right, cy),
                Center = new Vector2(cx, cy)
            };
        }

        private static void DrawLinks(RectTransform content, Dictionary<string, Vector2> topLefts, List<EnchantTreeLink> links)
        {
            if (links == null)
                return;

            Dictionary<string, List<string>> below = new Dictionary<string, List<string>>();
            List<EnchantTreeLink> side = new List<EnchantTreeLink>();
            for (int i = 0; i < links.Count; i++)
            {
                EnchantTreeLink link = links[i];
                if (link == null)
                    continue;

                Vector2 parentPos;
                Vector2 childPos;
                if (!topLefts.TryGetValue(link.parentId, out parentPos) || !topLefts.TryGetValue(link.childId, out childPos))
                    continue;

                NodeAnchor parent = AnchorOf(parentPos);
                NodeAnchor child = AnchorOf(childPos);
                if (child.Top.y < parent.Bottom.y - 8f)
                {
                    List<string> children;
                    if (!below.TryGetValue(link.parentId, out children))
                    {
                        children = new List<string>();
                        below.Add(link.parentId, children);
                    }

                    children.Add(link.childId);
                }
                else
                {
                    side.Add(link);
                }
            }

            foreach (KeyValuePair<string, List<string>> pair in below)
            {
                NodeAnchor parent = AnchorOf(topLefts[pair.Key]);
                float minX = parent.Bottom.x;
                float maxX = parent.Bottom.x;
                float nearest = float.NegativeInfinity;
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    NodeAnchor child = AnchorOf(topLefts[pair.Value[i]]);
                    minX = Mathf.Min(minX, child.Top.x);
                    maxX = Mathf.Max(maxX, child.Top.x);
                    nearest = Mathf.Max(nearest, child.Top.y);
                }

                float rail = (parent.Bottom.y + nearest) * 0.5f;
                Segment(content, parent.Bottom, new Vector2(parent.Bottom.x, rail));
                Segment(content, new Vector2(minX, rail), new Vector2(maxX, rail));
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    Vector2 top = AnchorOf(topLefts[pair.Value[i]]).Top;
                    Segment(content, new Vector2(top.x, rail), top);
                }
            }

            for (int i = 0; i < side.Count; i++)
            {
                EnchantTreeLink link = side[i];
                DrawElbow(content, AnchorOf(topLefts[link.parentId]), AnchorOf(topLefts[link.childId]));
            }
        }

        private static void DrawElbow(RectTransform content, NodeAnchor parent, NodeAnchor child)
        {
            float dx = Mathf.Abs(child.Center.x - parent.Center.x);
            float dy = Mathf.Abs(child.Center.y - parent.Center.y);
            bool childBelow = child.Center.y <= parent.Center.y;
            bool childRight = child.Center.x >= parent.Center.x;
            if (dy >= dx)
            {
                Vector2 from = childBelow ? parent.Bottom : parent.Top;
                Vector2 to = childBelow ? child.Top : child.Bottom;
                float mid = (from.y + to.y) * 0.5f;
                Segment(content, from, new Vector2(from.x, mid));
                Segment(content, new Vector2(from.x, mid), new Vector2(to.x, mid));
                Segment(content, new Vector2(to.x, mid), to);
            }
            else
            {
                Vector2 from = childRight ? parent.Right : parent.Left;
                Vector2 to = childRight ? child.Left : child.Right;
                float mid = (from.x + to.x) * 0.5f;
                Segment(content, from, new Vector2(mid, from.y));
                Segment(content, new Vector2(mid, from.y), new Vector2(mid, to.y));
                Segment(content, new Vector2(mid, to.y), to);
            }
        }

        private static void Segment(RectTransform content, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < 1f)
                return;

            bool vertical = Mathf.Abs(delta.y) >= Mathf.Abs(delta.x);
            RectTransform rect = CreateRect("Line", content);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            if (vertical)
            {
                rect.sizeDelta = new Vector2(2f, Mathf.Abs(delta.y));
                rect.anchoredPosition = new Vector2(from.x, (from.y + to.y) * 0.5f);
            }
            else
            {
                rect.sizeDelta = new Vector2(Mathf.Abs(delta.x), 2f);
                rect.anchoredPosition = new Vector2((from.x + to.x) * 0.5f, from.y);
            }

            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = Pixel();
            image.color = new Color(0.55f, 0.53f, 0.48f, 0.95f);
            image.raycastTarget = false;
        }

        private static TMP_Text CreateLabel(RectTransform parent, TMP_FontAsset font, float size, Vector2 position, Vector2 sizeDelta)
        {
            RectTransform rect = CreateRect("Label", parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = sizeDelta;

            TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static void SetDontSave(GameObject go)
        {
            go.hideFlags = HideFlags.DontSave;
            for (int i = 0; i < go.transform.childCount; i++)
                SetDontSave(go.transform.GetChild(i).gameObject);
        }

        internal static Color KindColor(EnchantNodeKind kind)
        {
            switch (kind)
            {
                case EnchantNodeKind.Entry: return new Color(0.62f, 0.46f, 0.16f);
                case EnchantNodeKind.Stat: return new Color(0.22f, 0.38f, 0.58f);
                case EnchantNodeKind.Ability: return new Color(0.48f, 0.28f, 0.56f);
                case EnchantNodeKind.Key: return new Color(0.62f, 0.30f, 0.16f);
                default: return new Color(0.25f, 0.25f, 0.25f);
            }
        }

        internal static string KindLabel(EnchantNodeKind kind)
        {
            switch (kind)
            {
                case EnchantNodeKind.Entry: return "입구";
                case EnchantNodeKind.Stat: return "스탯";
                case EnchantNodeKind.Ability: return "특성";
                case EnchantNodeKind.Key: return "핵심";
                default: return kind.ToString();
            }
        }

        private static Sprite Pixel()
        {
            if (_pixel != null)
                return _pixel;

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            _pixel = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _pixel.hideFlags = HideFlags.HideAndDontSave;
            return _pixel;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
