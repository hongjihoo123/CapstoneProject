using System;
using System.Collections.Generic;
using System.Text;
using Assets.Members.HJH._02.Scripts.Element.Upgrade;
using DG.Tweening;
using Members.KYR._01.Scripts.Enchant;
using Members.KYR._01.Scripts.Enchant.Data;
using Members.KYR._01.Scripts.Enchant.So;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 아웃 업그레이드(스킬 트리) 창. 캐릭터 선택 화면의 "아웃 업그레이드" 버튼이 프리팹을 넘겨 연다.
    //  - 위: 제목 + 가진 소울 + 닫기, 그 아래 탭(노드의 tab 이름별), 왼쪽 트리 그래프, 오른쪽 고른 노드 정보 + 배우기/되돌리기
    //  - 그래프는 위에서 아래로: 윗 노드를 1랭크 이상 배워야 아래가 열림 (선은 직각으로 꺾임)
    //  - 진행도·소울은 SkillTreeProgress (씬을 넘어 저장됨), 이 창은 그걸 보여주고 누른 것만 전달
    // 모양은 프리팹(05.Data/Prefab/UI/SkillTreeScreen)에 저장된 그대로 → 폰트·크기·위치·색은 프리팹에서 수정.
    // 실행 중에는 글자와 색만 바꾸고, 노드/선/탭은 프리팹 안의 견본(NodeTemplate, LineTemplate, TabTemplate)을 복제함.
    public class SkillTreeScreen : MonoBehaviour, IUiBake
    {
        private const float LineThickness = 3f;
        private const float BusGap = 40f;   // 아래 노드 위에서 가로선이 꺾이는 높이

        public static SkillTreeScreen Current { get; private set; }
        public static bool IsOpen => Current != null;

        [Header("부품 (레이아웃 굽기가 채움)")]
        [SerializeField] private RectTransform window;
        [SerializeField] private CanvasGroup windowGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text soulText;
        [SerializeField] private RectTransform tabRow;
        [SerializeField] private RectTransform graphContent;
        [SerializeField] private TMP_Text emptyText;
        [SerializeField] private SkillTreeNodeView nodeTemplate;
        [SerializeField] private Image lineTemplate;
        [SerializeField] private Button tabTemplate;

        [Header("고른 노드 패널")]
        [SerializeField] private TMP_Text nodeName;
        [SerializeField] private TMP_Text nodeKind;
        [SerializeField] private TMP_Text nodeRank;
        [SerializeField] private TMP_Text nodeEffect;
        [SerializeField] private TMP_Text nodeDesc;
        [SerializeField] private TMP_Text nodeMessage;
        [SerializeField] private Button learnButton;
        [SerializeField] private TMP_Text learnLabel;
        [SerializeField] private Button refundButton;
        [SerializeField] private TMP_Text refundLabel;

        [Header("색")]
        [SerializeField] private Color lineColor = new(0.302f, 0.294f, 0.267f, 1f);
        [SerializeField] private Color lineOnColor = new(0.847f, 0.827f, 0.769f, 1f);
        [SerializeField] private Color tabOffColor = new(0.114f, 0.114f, 0.102f, 1f);
        [SerializeField] private Color tabOnColor = new(0.914f, 0.902f, 0.863f, 1f);
        [SerializeField] private Color tabOffText = new(0.914f, 0.902f, 0.863f, 1f);
        [SerializeField] private Color tabOnText = new(0.078f, 0.078f, 0.071f, 1f);
        [SerializeField] private Color warnColor = new(0.91f, 0.5f, 0.42f, 1f);
        [SerializeField] private Color goodColor = new(0.42f, 0.77f, 0.56f, 1f);

        private sealed class Edge
        {
            public string parent;
            public string child;
            public List<Image> parts = new();
        }

        private readonly Dictionary<string, SkillTreeNodeView> _views = new();
        private readonly List<Edge> _edges = new();
        private readonly List<(Button button, TMP_Text label, TMP_Text spent, Image bg)> _tabs = new();
        private List<string> _tabNames = new();
        private EnchantTree _tree;
        private string _tab;
        private string _selected;
        private Action _onClosed;
        private bool _closing;

        // 노드 종류별 반지름 (선이 모양 가장자리에서 시작하게)
        private static float RadiusOf(EnchantNodeKind kind) => kind switch
        {
            EnchantNodeKind.Stat => 28f,
            EnchantNodeKind.Ability => 31f,
            EnchantNodeKind.Entry => 34f,
            _ => 48f,
        };

        public static SkillTreeScreen Open(SkillTreeScreen prefab, EnchantTree tree, string characterName, Action onClosed = null)
        {
            if (Current != null)
                return Current;
            if (prefab == null)
            {
                Debug.LogError("[SkillTreeScreen] 프리팹이 연결되지 않았습니다 (CharacterPickScreen 의 Skill Tree Prefab).");
                return null;
            }

            SkillTreeScreen screen = Instantiate(prefab);
            screen.name = prefab.name;
            screen._onClosed = onClosed;
            screen.Init(tree, characterName);
            return screen;
        }

        private void Init(EnchantTree tree, string characterName)
        {
            Current = this;
            HudUiKit.EnsureEventSystem();
            SkillTreeProgress.EnsureLoaded();
            SkillTreeProgress.Changed += OnProgressChanged;

            _tree = tree;
            nodeTemplate.gameObject.SetActive(false);
            lineTemplate.gameObject.SetActive(false);
            tabTemplate.gameObject.SetActive(false);

            titleText.text = "스킬 트리";
            subtitleText.text = tree != null
                ? characterName + " · 소울로 노드를 배웁니다. 윗 노드를 먼저 배워야 아래가 열립니다."
                : characterName;

            if (tree == null || tree.Nodes.Count == 0)
            {
                emptyText.gameObject.SetActive(true);
                emptyText.text = "이 캐릭터의 스킬 트리는 아직 준비 중입니다.";
                SetPanelEmpty();
                RefreshSoul();
            }
            else
            {
                emptyText.gameObject.SetActive(false);
                BuildTabs();
                SelectTab(_tabNames.Count > 0 ? _tabNames[0] : string.Empty);
            }

            windowGroup.alpha = 0f;
            windowGroup.DOFade(1f, 0.15f).SetUpdate(true);
            Vector2 home = window.anchoredPosition;
            window.anchoredPosition = home + new Vector2(0f, -16f);
            window.DOAnchorPosY(home.y, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                Close();
        }

        private void OnDestroy()
        {
            SkillTreeProgress.Changed -= OnProgressChanged;
            if (Current == this)
                Current = null;
        }

        // 버튼 OnClick 용
        public void Close()
        {
            if (_closing)
                return;
            _closing = true;
            SkillTreeProgress.Changed -= OnProgressChanged;
            windowGroup.interactable = false;
            windowGroup.DOFade(0f, 0.12f).SetUpdate(true).OnComplete(() =>
            {
                _onClosed?.Invoke();
                Destroy(gameObject);
            });
        }

        public void OnLearnClicked()
        {
            EnchantTreeNode node = SelectedNode;
            if (node == null)
                return;
            if (!SkillTreeProgress.TryLearn(_tree, node))
                Refresh();   // 안 되는 이유를 패널이 보여줌
        }

        public void OnRefundClicked()
        {
            EnchantTreeNode node = SelectedNode;
            if (node != null)
                SkillTreeProgress.TryRefund(_tree, node);
        }

        public void OnRefundAllClicked()
        {
            if (_tree != null)
                SkillTreeProgress.RefundAll(_tree);
        }

        private EnchantTreeNode SelectedNode => _tree != null && !string.IsNullOrEmpty(_selected) ? _tree.FindNode(_selected) : null;

        private void OnProgressChanged() => Refresh();

        // ---------------------------------------------------------------- 탭 / 그래프

        private void BuildTabs()
        {
            _tabNames = SkillTreeProgress.TabsOf(_tree);
            foreach (var t in _tabs)
                Destroy(t.button.gameObject);
            _tabs.Clear();

            foreach (string name in _tabNames)
            {
                string tabName = name;
                Button button = Instantiate(tabTemplate, tabRow);
                button.gameObject.SetActive(true);
                button.name = "Tab_" + tabName;
                TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(true);
                texts[0].text = string.IsNullOrEmpty(tabName) ? "전체" : tabName;
                button.onClick.AddListener(() => SelectTab(tabName));
                _tabs.Add((button, texts[0], texts[1], button.targetGraphic as Image));
            }
        }

        private void SelectTab(string tab)
        {
            _tab = tab;
            _selected = null;
            BuildGraph();
            Refresh();
        }

        private void BuildGraph()
        {
            for (int i = graphContent.childCount - 1; i >= 0; i--)
                Destroy(graphContent.GetChild(i).gameObject);
            _views.Clear();
            _edges.Clear();

            var inTab = new Dictionary<string, EnchantTreeNode>();
            foreach (EnchantTreeNode node in _tree.Nodes)
            {
                if (node != null && !string.IsNullOrEmpty(node.id) && SkillTreeProgress.SameTab(node.tab, _tab))
                    inTab[node.id] = node;
            }

            // 선을 먼저 (노드가 위에 그려지게)
            foreach (EnchantTreeLink link in _tree.Links)
            {
                if (link == null || !inTab.TryGetValue(link.parentId, out EnchantTreeNode parent) || !inTab.TryGetValue(link.childId, out EnchantTreeNode child))
                    continue;
                var edge = new Edge { parent = parent.id, child = child.id };
                DrawEdge(edge, parent, child);
                _edges.Add(edge);
            }

            foreach (EnchantTreeNode node in inTab.Values)
            {
                SkillTreeNodeView view = Instantiate(nodeTemplate, graphContent);
                view.gameObject.SetActive(true);
                view.name = "Node_" + node.id;
                ((RectTransform)view.transform).anchoredPosition = LocalOf(node);
                view.Bind(node.id, node.displayName, node.kind, IsCooldownNode(node), IsWired(node));
                string id = node.id;
                view.Button.onClick.AddListener(() => Select(id));
                _views[node.id] = view;
            }
        }

        // 노드 위치(디자인 좌표, y 아래로)를 그래프 칸의 좌표로. 가로는 가운데 기준
        private static Vector2 LocalOf(EnchantTreeNode node) => new(node.position.x - 400f, -node.position.y);

        private static bool IsCooldownNode(EnchantTreeNode node)
        {
            if (node.SkillEffects.Count == 0)
                return node.displayName != null && node.displayName.Contains("쿨");
            if (node.SkillEffects.Count == 0)
                return false;
            foreach (EnchantSkillEffect effect in node.SkillEffects)
            {
                if (effect.kind != SkillUpgradeKind.Cooldown)
                    return false;
            }
            return true;
        }

        private void DrawEdge(Edge edge, EnchantTreeNode parent, EnchantTreeNode child)
        {
            Vector2 p = LocalOf(parent);
            Vector2 c = LocalOf(child);
            float rp = RadiusOf(parent.kind);
            float rc = RadiusOf(child.kind);

            if (Mathf.Abs(p.y - c.y) < 2f)
            {
                float dir = c.x > p.x ? 1f : -1f;
                Segment(edge, new Vector2(p.x + dir * rp, p.y), new Vector2(c.x - dir * rc, c.y));
                return;
            }

            // 위쪽 노드 아래에서 내려와, 아래 노드 위 BusGap 에서 가로로 꺾고, 다시 내려감
            bool parentIsTop = p.y > c.y;
            Vector2 top = parentIsTop ? p : c;
            Vector2 bottom = parentIsTop ? c : p;
            float rt = parentIsTop ? rp : rc;
            float rb = parentIsTop ? rc : rp;
            float mid = bottom.y + Mathf.Max(BusGap, rb + 10f);
            Segment(edge, new Vector2(top.x, top.y - rt), new Vector2(top.x, mid));
            if (!Mathf.Approximately(top.x, bottom.x))
                Segment(edge, new Vector2(top.x, mid), new Vector2(bottom.x, mid));
            Segment(edge, new Vector2(bottom.x, mid), new Vector2(bottom.x, bottom.y + rb));
        }

        private void Segment(Edge edge, Vector2 a, Vector2 b)
        {
            Image line = Instantiate(lineTemplate, graphContent);
            line.gameObject.SetActive(true);
            line.name = "Line";
            RectTransform rect = line.rectTransform;
            rect.anchoredPosition = (a + b) * 0.5f;
            Vector2 size = new(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
            rect.sizeDelta = new Vector2(Mathf.Max(size.x, LineThickness), Mathf.Max(size.y, LineThickness));
            edge.parts.Add(line);
        }

        private void Select(string id)
        {
            _selected = id;
            Refresh();
        }

        // ---------------------------------------------------------------- 갱신

        private void Refresh()
        {
            if (_tree == null)
                return;

            RefreshSoul();

            foreach (var pair in _views)
            {
                EnchantTreeNode node = _tree.FindNode(pair.Key);
                pair.Value.Show(SkillTreeProgress.Rank(_tree, node), Mathf.Max(1, node.maxRank), SkillTreeProgress.IsOpen(_tree, node), pair.Key == _selected);
            }

            foreach (Edge edge in _edges)
            {
                bool on = SkillTreeProgress.Rank(_tree, _tree.FindNode(edge.parent)) > 0 && SkillTreeProgress.Rank(_tree, _tree.FindNode(edge.child)) > 0;
                foreach (Image part in edge.parts)
                    part.color = on ? lineOnColor : lineColor;
            }

            for (int i = 0; i < _tabs.Count; i++)
            {
                bool on = SkillTreeProgress.SameTab(_tabNames[i], _tab);
                var tab = _tabs[i];
                tab.bg.color = on ? tabOnColor : tabOffColor;
                tab.label.color = on ? tabOnText : tabOffText;
                tab.spent.color = on ? tabOnText : tabOffText;
                tab.spent.text = SkillTreeProgress.SpentInTab(_tree, _tabNames[i]) + "점";
            }

            RefreshPanel();
        }

        private void RefreshSoul() => soulText.text = "소울  <color=#E0B441>" + SkillTreeProgress.Soul + "</color>";

        private void SetPanelEmpty()
        {
            nodeName.text = "노드를 누르세요";
            nodeKind.text = string.Empty;
            nodeRank.text = string.Empty;
            nodeEffect.text = string.Empty;
            nodeDesc.text = "누르면 여기에 효과와 비용이 나옵니다.";
            nodeMessage.text = string.Empty;
            learnButton.gameObject.SetActive(false);
            refundButton.gameObject.SetActive(false);
        }

        private void RefreshPanel()
        {
            EnchantTreeNode node = SelectedNode;
            if (node == null || !SkillTreeProgress.SameTab(node.tab, _tab))
            {
                SetPanelEmpty();
                return;
            }

            int rank = SkillTreeProgress.Rank(_tree, node);
            int max = Mathf.Max(1, node.maxRank);
            nodeName.text = node.displayName;
            nodeKind.text = KindLabel(node.kind);
            nodeDesc.text = node.description;

            nodeRank.text = $"랭크 {rank}/{max}   ·   랭크당 비용 <color=#E0B441>{node.cost}</color>";
            nodeEffect.text = IsWired(node) ? EffectLines(node, rank, max) : "<color=#9B978A>효과 준비 중 - 아직 게임에 적용되지 않는 노드입니다.</color>";

            EnchantLearnResult result = SkillTreeProgress.CanLearn(_tree, node);
            learnButton.gameObject.SetActive(true);
            refundButton.gameObject.SetActive(true);
            learnButton.interactable = result == EnchantLearnResult.Ok;
            learnLabel.text = result == EnchantLearnResult.MaxRank ? "최대 랭크" : $"배우기  ({node.cost})";
            refundButton.interactable = SkillTreeProgress.CanRefund(_tree, node);
            refundLabel.text = $"되돌리기  (+{SkillTreeProgress.RefundOf(node)})";

            nodeMessage.color = result == EnchantLearnResult.Ok ? goodColor : warnColor;
            nodeMessage.text = result switch
            {
                EnchantLearnResult.Ok => "배울 수 있습니다.",
                EnchantLearnResult.Locked => "윗 노드를 먼저 배워야 합니다.",
                EnchantLearnResult.NotEnoughCurrency => $"소울이 부족합니다. (필요 {node.cost}, 보유 {SkillTreeProgress.Soul})",
                EnchantLearnResult.Exclusive => "같은 그룹의 다른 노드를 이미 배웠습니다.",
                EnchantLearnResult.MaxRank => string.Empty,
                _ => string.Empty,
            };
        }

        private static bool IsWired(EnchantTreeNode node) => node.SkillEffects.Count > 0 || node.Effects.Count > 0;

        private static string EffectLines(EnchantTreeNode node, int rank, int max)
        {
            var text = new StringBuilder();
            if (rank > 0)
                text.Append("<color=#9B978A>지금</color>   ").Append(EnchantText.Effects(node, rank, "  ·  "));
            if (rank < max)
            {
                if (text.Length > 0)
                    text.Append('\n');
                text.Append("<color=#9B978A>다음</color>   ").Append(EnchantText.Effects(node, rank + 1, "  ·  "));
            }
            return text.ToString();
        }

        private static string KindLabel(EnchantNodeKind kind) => kind switch
        {
            EnchantNodeKind.Entry => "계열 입구",
            EnchantNodeKind.Stat => "스탯 노드",
            EnchantNodeKind.Ability => "특성 노드",
            _ => "핵심 노드",
        };

        // ---------------------------------------------------------------- 레이아웃 굽기 (에디터 전용)

#if UNITY_EDITOR
        private const float WindowWidth = 1840f;
        private const float WindowHeight = 1056f;
        private const float Pad = 48f;
        private const float GraphWidth = 1240f;
        private const float GraphHeight = 812f;
        private const float SideWidth = 480f;

        private static readonly Color Dim = new(0.04f, 0.04f, 0.035f, 0.9f);
        private static readonly Color BgColor = new(0.078f, 0.078f, 0.071f, 1f);
        private static readonly Color PanelColor = new(0.114f, 0.114f, 0.102f, 1f);
        private static readonly Color EdgeColor = new(0.302f, 0.294f, 0.267f, 1f);
        private static readonly Color Fg = new(0.914f, 0.902f, 0.863f, 1f);
        private static readonly Color Muted = new(0.608f, 0.592f, 0.541f, 1f);
        private static readonly Color GoldColor = new(0.878f, 0.706f, 0.255f, 1f);
        private static readonly Color NodeFill = new(0.149f, 0.149f, 0.133f, 1f);

        [ContextMenu("레이아웃 굽기 (자식 다시 만들기)")]
        public void BakeLayout()
        {
            HudUiKit.ClearChildren(transform);

            var canvas = GetComponent<Canvas>() ? GetComponent<Canvas>() : gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = GetComponent<CanvasScaler>() ? GetComponent<CanvasScaler>() : gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            Image dim = HudUiKit.NewImage("Dim", transform, null, Dim);
            HudUiKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            Image panel = HudUiKit.NewImage("Window", transform, null, BgColor);
            HudUiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WindowWidth, WindowHeight));
            panel.raycastTarget = true;
            window = panel.rectTransform;
            windowGroup = panel.gameObject.AddComponent<CanvasGroup>();
            Outline(window, EdgeColor, 2f);

            // 머리: 제목 / 부제 / 소울 / 닫기
            titleText = Label("Title", window, "스킬 트리", 42f, Fg, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(Pad, -30f), new Vector2(700f, 54f));
            subtitleText = Label("Subtitle", window, "", 20f, Muted, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(Pad, -86f), new Vector2(1100f, 30f));
            soulText = Label("Soul", window, "소울  60", 32f, Fg, TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(-Pad - 200f, -38f), new Vector2(360f, 44f));
            Button close = TextButton("Close", window, "닫기  ESC", new Vector2(1f, 1f), new Vector2(-Pad, -34f), new Vector2(170f, 54f), 20f, null, out _);
            Wire(close, Close);

            // 탭 줄
            tabRow = HudUiKit.NewRect("Tabs", window);
            HudUiKit.Place(tabRow, new Vector2(0f, 1f), new Vector2(Pad, -128f), new Vector2(GraphWidth, 64f));
            var tabs = tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabs.spacing = 8f;
            tabs.childAlignment = TextAnchor.MiddleLeft;
            tabs.childControlWidth = false;
            tabs.childControlHeight = false;
            tabs.childForceExpandWidth = false;
            tabs.childForceExpandHeight = false;

            // 그래프 칸 (넘치면 잘림)
            Image graph = HudUiKit.NewImage("Graph", window, null, PanelColor);
            HudUiKit.Place(graph.rectTransform, new Vector2(0f, 1f), new Vector2(Pad, -204f), new Vector2(GraphWidth, GraphHeight));
            graph.raycastTarget = true;
            Outline(graph.rectTransform, EdgeColor, 2f);
            graph.gameObject.AddComponent<RectMask2D>();
            graphContent = HudUiKit.NewRect("Content", graph.transform);
            HudUiKit.Place(graphContent, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(10f, 10f));
            emptyText = Label("Empty", graph.transform, "", 28f, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 60f));
            emptyText.gameObject.SetActive(false);

            // 범례: 그래프 칸 왼쪽 위 (노드는 가운데에 모여 있어서 비어 있음)
            Label("Legend", graph.transform, "◆ 스탯 (여러 랭크)\n● 특성 (둘 중 하나)\n■ 계열 입구\n■(금색) 핵심\n선: 윗 노드를 먼저 배워야 열림", 16f, Muted,
                TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f), new Vector2(20f, 16f), new Vector2(300f, 110f));

            BuildSide();
            BuildTemplates();
            SetPanelEmpty();
            RefreshSoul();
        }

        private void BuildSide()
        {
            Image side = HudUiKit.NewImage("Side", window, null, PanelColor);
            HudUiKit.Place(side.rectTransform, new Vector2(1f, 1f), new Vector2(-Pad, -128f), new Vector2(SideWidth, GraphHeight + 76f));
            side.raycastTarget = true;
            Outline(side.rectTransform, EdgeColor, 2f);
            RectTransform s = side.rectTransform;
            const float w = SideWidth - 56f;

            nodeName = Label("NodeName", s, "", 34f, Fg, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(w, 46f));
            nodeKind = Label("NodeKind", s, "", 19f, GoldColor, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(28f, -78f), new Vector2(w, 28f));
            nodeRank = Label("NodeRank", s, "", 21f, Fg, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(28f, -116f), new Vector2(w, 30f));
            nodeEffect = Label("NodeEffect", s, "", 22f, Fg, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(28f, -166f), new Vector2(w, 100f));
            nodeEffect.textWrappingMode = TextWrappingModes.Normal;
            nodeDesc = Label("NodeDesc", s, "", 20f, Muted, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(28f, -290f), new Vector2(w, 170f));
            nodeDesc.textWrappingMode = TextWrappingModes.Normal;
            nodeMessage = Label("NodeMessage", s, "", 19f, warnColor, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(28f, -480f), new Vector2(w, 60f));
            nodeMessage.textWrappingMode = TextWrappingModes.Normal;

            Image learn = ActionButton("Learn", s, "button_hover", "배우기", new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(w, 70f), 28f, out learnLabel);
            learnButton = learn.GetComponent<Button>();
            Wire(learnButton, OnLearnClicked);
            Image refund = ActionButton("Refund", s, "button_normal", "되돌리기", new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(w, 58f), 22f, out refundLabel);
            refundButton = refund.GetComponent<Button>();
            Wire(refundButton, OnRefundClicked);
            TextButton("RefundAll", s, "찍은 것 전부 되돌리기", new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(w, 42f), 17f, null, out _);
            Wire(s.Find("RefundAll").GetComponent<Button>(), OnRefundAllClicked);
        }

        // 견본 (실행 중에 복제함). 창 밖에 꺼 둔 채로 저장
        private void BuildTemplates()
        {
            RectTransform templates = HudUiKit.NewRect("Templates", window);
            templates.gameObject.SetActive(false);

            Image line = HudUiKit.NewImage("LineTemplate", templates, null, EdgeColor);
            HudUiKit.Place(line.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(LineThickness, LineThickness), new Vector2(0.5f, 0.5f));
            lineTemplate = line;

            Image tab = HudUiKit.NewImage("TabTemplate", templates, null, PanelColor);
            HudUiKit.Place(tab.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(150f, 60f));
            tab.raycastTarget = true;
            tabTemplate = tab.gameObject.AddComponent<Button>();
            tabTemplate.targetGraphic = tab;
            tabTemplate.transition = Selectable.Transition.None;
            Outline(tab.rectTransform, EdgeColor, 2f);
            TMP_Text tabLabel = Label("Label", tab.transform, "탭", 24f, Fg, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(150f, 32f));
            tabLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            TMP_Text tabSpent = Label("Spent", tab.transform, "0점", 15f, Fg, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(150f, 20f));
            tabSpent.rectTransform.pivot = new Vector2(0.5f, 0f);
            tab.gameObject.AddComponent<HoverScale>().Scale = 1.03f;

            nodeTemplate = BuildNodeTemplate(templates);
        }

        private SkillTreeNodeView BuildNodeTemplate(Transform parent)
        {
            RectTransform root = HudUiKit.NewRect("NodeTemplate", parent);
            HudUiKit.Place(root, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(150f, 130f), new Vector2(0.5f, 0.5f));
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            var group = root.gameObject.AddComponent<CanvasGroup>();
            root.gameObject.AddComponent<HoverScale>().Scale = 1.06f;

            Sprite circleSprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            // 모양마다 바깥선(outline) + 안쪽(fill). 마름모는 사각을 45도 돌림
            SkillTreeNodeView.Shape diamond = BuildShape(root, "Diamond", 46f, 5f, null, 45f, EdgeColor);
            SkillTreeNodeView.Shape circle = BuildShape(root, "Circle", 62f, 5f, circleSprite, 0f, EdgeColor);
            SkillTreeNodeView.Shape entry = BuildShape(root, "Entry", 66f, 5f, null, 0f, EdgeColor);
            SkillTreeNodeView.Shape key = BuildShape(root, "Key", 90f, 6f, null, 0f, GoldColor);

            // 글자 판: 노드 아래로 지나가는 선이 글자를 가로지르지 않게 그래프 바탕색으로 덮음
            Image plate = HudUiKit.NewImage("LabelPlate", root, null, PanelColor);
            HudUiKit.Place(plate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -57f), new Vector2(146f, 42f));
            TMP_Text glyph = Label("Glyph", root, "+", 26f, Fg, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 40f));
            TMP_Text rank = Label("Rank", root, "0/5", 15f, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, -64f), new Vector2(140f, 20f));
            TMP_Text nameLabel = Label("Name", root, "노드", 15f, Fg, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, -57f), new Vector2(142f, 40f));
            nameLabel.textWrappingMode = TextWrappingModes.Normal;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            nameLabel.lineSpacing = 0f;

            var view = root.gameObject.AddComponent<SkillTreeNodeView>();
            var so = new UnityEditor.SerializedObject(view);
            SetShape(so, "diamond", diamond);
            SetShape(so, "circle", circle);
            SetShape(so, "entry", entry);
            SetShape(so, "key", key);
            so.FindProperty("plate").objectReferenceValue = plate.gameObject;
            so.FindProperty("glyph").objectReferenceValue = glyph;
            so.FindProperty("rankText").objectReferenceValue = rank;
            so.FindProperty("nameText").objectReferenceValue = nameLabel;
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("group").objectReferenceValue = group;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static SkillTreeNodeView.Shape BuildShape(RectTransform parent, string name, float size, float stroke, Sprite sprite, float rotation, Color outlineColor)
        {
            RectTransform group = HudUiKit.NewRect(name, parent);
            HudUiKit.Place(group, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Image outline = HudUiKit.NewImage("Outline", group, sprite, outlineColor);
            HudUiKit.Place(outline.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            outline.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);

            Image fill = HudUiKit.NewImage("Fill", group, sprite, NodeFill);
            HudUiKit.Place(fill.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size - stroke * 2f, size - stroke * 2f));
            fill.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);

            return new SkillTreeNodeView.Shape { root = group.gameObject, outline = outline, fill = fill };
        }

        private static void SetShape(UnityEditor.SerializedObject so, string field, SkillTreeNodeView.Shape shape)
        {
            UnityEditor.SerializedProperty p = so.FindProperty(field);
            p.FindPropertyRelative("root").objectReferenceValue = shape.root;
            p.FindPropertyRelative("outline").objectReferenceValue = shape.outline;
            p.FindPropertyRelative("fill").objectReferenceValue = shape.fill;
        }

        // 가장자리 선 (안쪽으로 thickness)
        private static void Outline(RectTransform target, Color color, float thickness)
        {
            RectTransform frame = HudUiKit.NewRect("Edge", target);
            HudUiKit.Stretch(frame);
            void Bar(string name, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
            {
                Image bar = HudUiKit.NewImage(name, frame, null, color);
                bar.rectTransform.anchorMin = min;
                bar.rectTransform.anchorMax = max;
                bar.rectTransform.offsetMin = offMin;
                bar.rectTransform.offsetMax = offMax;
            }
            Bar("T", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero);
            Bar("B", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness));
            Bar("L", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f));
            Bar("R", new Vector2(1f, 0f), Vector2.one, new Vector2(-thickness, 0f), Vector2.zero);
        }

        private static TMP_Text Label(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, Vector2 anchor, Vector2 pos, Vector2 boxSize)
        {
            TMP_Text label = HudUiKit.NewText(name, parent, text, size, color, align);
            HudUiKit.Place(label.rectTransform, anchor, pos, boxSize, anchor);
            return label;
        }

        private Button TextButton(string name, Transform parent, string text, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, string sprite, out TMP_Text label)
        {
            Image image = HudUiKit.NewImage(name, parent, sprite != null ? HudUiKit.Load(sprite) : null, PanelColor);
            HudUiKit.Place(image.rectTransform, anchor, pos, size);
            image.raycastTarget = true;
            if (sprite == null)
                Outline(image.rectTransform, EdgeColor, 2f);
            label = HudUiKit.NewText("Label", image.transform, text, fontSize, Fg, TextAlignmentOptions.Center);
            HudUiKit.Stretch(label.rectTransform);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            image.gameObject.AddComponent<HoverScale>().Scale = 1.03f;
            return button;
        }

        private Image ActionButton(string name, Transform parent, string sprite, string text, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, out TMP_Text label)
        {
            Image image = HudUiKit.NewImage(name, parent, HudUiKit.Load(sprite), Color.white);
            HudUiKit.Place(image.rectTransform, anchor, pos, size);
            image.raycastTarget = true;
            label = HudUiKit.NewText("Label", image.transform, text, fontSize, HudUiKit.Title, TextAlignmentOptions.Center, 3f);
            HudUiKit.Stretch(label.rectTransform);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            button.colors = colors;
            image.gameObject.AddComponent<HoverScale>().Scale = 1.02f;
            return image;
        }

        private static void Wire(Button button, UnityEngine.Events.UnityAction action) =>
            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, action);
#else
        public void BakeLayout() { }
#endif
    }
}
