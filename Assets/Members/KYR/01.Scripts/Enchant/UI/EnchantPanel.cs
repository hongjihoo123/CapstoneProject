using System;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Members.KYR._01.Scripts.Enchant.Data;
using Members.KYR._01.Scripts.Enchant.So;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Members.KYR._01.Scripts.Enchant.UI
{
    // 지금 캐릭터의 인챈트 트리를 그리고, 노드를 고르면 하단 바에서 설명과 "배우기"를 보여 준다.
    public class EnchantPanel : MonoBehaviour
    {
        [Serializable]
        public struct CharacterBoard
        {
            public CharacterData character;
            public RectTransform board;
        }

        [SerializeField] private EnchantModule module;
        [SerializeField] private CharacterBoard[] boards = Array.Empty<CharacterBoard>();
        [SerializeField] private RectTransform board;
        [SerializeField] private Button buttonPrefab;

        private readonly EnchantTreeBoard _treeBoard = new EnchantTreeBoard();
        private RectTransform _board;
        private TMP_Text _title;
        private RectTransform _actions;
        private Button _openButton;
        private string _selectedNodeId;
        private TMP_FontAsset _font;
        private bool _editTreeVisible;

        public EnchantModule Module => module;
        public bool EditTreeVisible => _editTreeVisible;

        public void EnsureEditView(bool allowRebuild)
        {
            if (Application.isPlaying)
                return;

            if (_board == null)
                _board = board != null ? board : transform.Find("Board") as RectTransform;
            if (_font == null && buttonPrefab != null)
            {
                TMP_Text sample = buttonPrefab.GetComponentInChildren<TMP_Text>(true);
                if (sample != null)
                    _font = sample.font;
            }

            EnchantTree tree = CurrentTree;
            bool show = tree != null && tree.Nodes.Count > 0 && _board != null;
            _editTreeVisible = show;
            if (!show)
            {
                _treeBoard.Clear();
                return;
            }

            int hash = EnchantTreeBoard.StructureHashOf(tree);
            if (allowRebuild && (_treeBoard.Root == null || _treeBoard.StructureHash != hash))
                _treeBoard.Build(_board, tree, _font, _selectedNodeId, null);
        }

        public void RefreshEditLines()
        {
            EnchantTree tree = CurrentTree;
            if (tree != null)
                _treeBoard.RefreshLines(tree);
        }

        private EnchantTree CurrentTree
        {
            get
            {
                if (module == null)
                    return null;

                return Application.isPlaying ? module.ActiveTree : module.TreeFor(null);
            }
        }

        private void Awake()
        {
            if (module == null)
                module = FindFirstObjectByType<EnchantModule>();

            VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>();
            if (layout != null)
                layout.enabled = false;

            if (buttonPrefab != null)
            {
                TMP_Text sample = buttonPrefab.GetComponentInChildren<TMP_Text>(true);
                if (sample != null)
                    _font = sample.font;
            }

            _board = board != null ? board : transform.Find("Board") as RectTransform;
            BuildChrome();
            ShowCurrentBoard();
        }

        private void OnEnable()
        {
            if (module != null)
            {
                module.RankChanged += HandleRankChanged;
                module.ActiveTreeChanged += HandleActiveTreeChanged;
            }

            if (_openButton != null)
                _openButton.gameObject.SetActive(false);

            if (_title != null)
                ShowCurrentBoard();
        }

        private void OnDisable()
        {
            if (module != null)
            {
                module.RankChanged -= HandleRankChanged;
                module.ActiveTreeChanged -= HandleActiveTreeChanged;
            }
        }

        private void HandleActiveTreeChanged(EnchantTree tree)
        {
            _selectedNodeId = null;
            ShowCurrentBoard();
        }

        private void HandleRankChanged(EnchantTree tree, EnchantTreeNode node, int rank)
        {
            if (tree == CurrentTree)
                Rebuild();
        }

        private void ShowCurrentBoard()
        {
            CharacterData current = module != null ? module.CurrentCharacter : null;
            RectTransform active = null;

            if (boards != null && boards.Length > 0)
            {
                for (int i = 0; i < boards.Length; i++)
                {
                    RectTransform candidate = boards[i].board;
                    if (candidate == null)
                        continue;

                    bool match = current == null ? i == 0 : boards[i].character == current;
                    if (match && active == null)
                        active = candidate;
                }

                for (int i = 0; i < boards.Length; i++)
                {
                    if (boards[i].board != null)
                        boards[i].board.gameObject.SetActive(boards[i].board == active);
                }
            }
            else
            {
                active = board != null ? board : _board;
            }

            _board = active;
            Rebuild();
        }

        private void BuildChrome()
        {
            RectTransform bar = CreateRect("Bar", transform);
            bar.anchorMin = new Vector2(0.5f, 0f);
            bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0f, 24f);
            bar.sizeDelta = new Vector2(720f, 110f);

            _title = CreateText("Title", bar, 22f);
            RectTransform titleRect = _title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = Vector2.zero;
            titleRect.sizeDelta = new Vector2(0f, 32f);

            _actions = CreateRect("Actions", bar);
            _actions.anchorMin = new Vector2(0f, 0f);
            _actions.anchorMax = new Vector2(1f, 1f);
            _actions.offsetMin = new Vector2(0f, 0f);
            _actions.offsetMax = new Vector2(0f, -36f);

            HorizontalLayoutGroup row = _actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 8f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = false;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;

            if (buttonPrefab == null)
                return;

            Button close = Spawn((RectTransform)transform, "닫기", new Color(0.15f, 0.17f, 0.22f));
            PlaceCorner(close, new Vector2(-16f, -16f));
            close.onClick.AddListener(Close);

            RectTransform canvas = transform.parent as RectTransform;
            if (canvas == null)
                return;

            _openButton = Spawn(canvas, "열기", new Color(0.15f, 0.17f, 0.22f));
            PlaceCorner(_openButton, new Vector2(-16f, -16f));
            _openButton.onClick.AddListener(Open);
            _openButton.gameObject.SetActive(false);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            if (_openButton != null)
                _openButton.gameObject.SetActive(false);
        }

        public void Close()
        {
            if (_openButton != null)
                _openButton.gameObject.SetActive(true);

            gameObject.SetActive(false);
        }

        private static void PlaceCorner(Button button, Vector2 offset)
        {
            RectTransform rect = button.transform as RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(96f, 40f);
        }

        private void HandleTreeNode(EnchantTreeNode node)
        {
            _selectedNodeId = node != null ? node.id : null;
            Rebuild();
        }

        private (int rank, bool open) StatusOf(EnchantTree tree, EnchantTreeNode node)
        {
            if (module == null)
                return (0, true);

            return (module.GetRank(tree, node.id), module.IsOpen(tree, node));
        }

        private void Rebuild()
        {
            if (_actions == null)
                return;

            Clear(_actions);

            EnchantTree tree = CurrentTree;
            if (module == null)
            {
                _treeBoard.Clear();
                _title.text = "인챈트 모듈이 없습니다";
                return;
            }

            if (tree == null || tree.Nodes.Count == 0 || _board == null)
            {
                _treeBoard.Clear();
                _title.text = "이 캐릭터의 인챈트 트리가 없습니다";
                return;
            }

            _treeBoard.Build(_board, tree, _font, _selectedNodeId, HandleTreeNode, node => StatusOf(tree, node));
            ShowTreeNode(tree);
        }

        private void ShowTreeNode(EnchantTree tree)
        {
            EnchantTreeNode selected = tree.FindNode(_selectedNodeId);
            if (selected == null)
            {
                _title.text = "노드를 누르세요";
                return;
            }

            int rank = module.GetRank(tree, selected.id);
            string name = string.IsNullOrEmpty(selected.displayName) ? selected.id : selected.displayName;
            _title.text = name + "   랭크 " + rank + "/" + selected.maxRank;

            string body = selected.description ?? string.Empty;
            string now = EnchantText.Effects(selected, rank, ", ");
            string next = rank < selected.maxRank ? EnchantText.Effects(selected, rank + 1, ", ") : string.Empty;
            if (!string.IsNullOrEmpty(next))
                body += (body.Length > 0 ? "\n" : string.Empty) + (rank > 0 ? now + "  →  " : "다음: ") + next;
            else if (!string.IsNullOrEmpty(now))
                body += (body.Length > 0 ? "\n" : string.Empty) + now;

            TMP_Text text = CreateText("Desc", _actions, 15f);
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.text = body;
            text.rectTransform.sizeDelta = new Vector2(520f, 64f);

            if (buttonPrefab == null)
                return;

            EnchantLearnResult result = module.CanLearn(tree, selected);
            bool ok = result == EnchantLearnResult.Ok;
            Button learn = Spawn(_actions, LearnLabel(result, selected.cost), ok ? Color.white : new Color(0.3f, 0.3f, 0.32f));
            learn.interactable = ok;
            TMP_Text label = learn.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.color = ok ? new Color(0.07f, 0.08f, 0.1f) : new Color(0.75f, 0.75f, 0.75f);

            EnchantTreeNode captured = selected;
            learn.onClick.AddListener(() => module.TryLearn(tree, captured));
        }

        private static string LearnLabel(EnchantLearnResult result, int cost)
        {
            switch (result)
            {
                case EnchantLearnResult.Ok: return cost > 0 ? "배우기 (" + cost + ")" : "배우기";
                case EnchantLearnResult.MaxRank: return "최대 랭크";
                case EnchantLearnResult.Locked: return "선행 필요";
                case EnchantLearnResult.Exclusive: return "배타 선택됨";
                case EnchantLearnResult.NotEnoughCurrency: return "재화 부족 (" + cost + ")";
                default: return "배울 수 없음";
            }
        }

        private Button Spawn(RectTransform parent, string label, Color color)
        {
            Button button = Instantiate(buttonPrefab, parent);
            button.gameObject.SetActive(true);
            button.transition = Selectable.Transition.None;

            RectTransform rect = button.transform as RectTransform;
            rect.sizeDelta = new Vector2(160f, 44f);

            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = color;

            TMP_Text text = button.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = label;
                text.color = Color.white;
            }

            return button;
        }

        private TMP_Text CreateText(string name, Transform parent, float size)
        {
            RectTransform rect = CreateRect(name, parent);
            TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.91f, 0.9f, 0.85f);
            text.text = string.Empty;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Clear(RectTransform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }
    }
}
