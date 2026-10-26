using System;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Members.KYR._01.Scripts.Enchant.Data;
using Members.KYR._01.Scripts.Enchant.So;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Members.KYR._01.Scripts.Enchant.UI
{
    public class EnchantPanel : MonoBehaviour
    {
        [Serializable]
        public struct CharacterBoard
        {
            public CharacterData character;
            public RectTransform board;
        }

        [SerializeField] private EnchantModule module;
        [SerializeField] private CharacterSwitcher switcher;
        [SerializeField] private CharacterBoard[] boards = Array.Empty<CharacterBoard>();
        [SerializeField] private RectTransform board;
        [SerializeField] private RectTransform fragmentRow;
        [SerializeField] private RectTransform optionRow;
        [SerializeField] private Button buttonPrefab;

        private RectTransform _board;
        private TMP_Text _title;
        private RectTransform _actions;
        private Button _openButton;
        private EnchantFragment _current;
        private TMP_FontAsset _font;

        private void Awake()
        {
            if (module == null)
                module = FindFirstObjectByType<EnchantModule>();
            if (switcher == null)
                switcher = FindFirstObjectByType<CharacterSwitcher>();

            VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>();
            if (layout != null)
                layout.enabled = false;

            if (fragmentRow != null)
                fragmentRow.gameObject.SetActive(false);
            if (optionRow != null)
                optionRow.gameObject.SetActive(false);

            if (buttonPrefab != null)
            {
                TMP_Text sample = buttonPrefab.GetComponentInChildren<TMP_Text>(true);
                if (sample != null)
                    _font = sample.font;
            }

            _board = board != null ? board : transform.Find("Board") as RectTransform;
            BuildChrome();
            BindNodes();
            ShowCurrentBoard();
        }

        private void OnEnable()
        {
            if (module != null)
            {
                module.FragmentUnlocked += HandleChanged;
                module.OptionSelected += HandleOptionSelected;
            }

            if (switcher != null)
                switcher.CharacterChanged += HandleCharacterChanged;

            if (_openButton != null)
                _openButton.gameObject.SetActive(false);

            ShowCurrentBoard();
        }

        private void OnDisable()
        {
            if (module != null)
            {
                module.FragmentUnlocked -= HandleChanged;
                module.OptionSelected -= HandleOptionSelected;
            }

            if (switcher != null)
                switcher.CharacterChanged -= HandleCharacterChanged;
        }

        private void HandleCharacterChanged(CharacterData character)
        {
            _current = null;
            ShowCurrentBoard();
        }

        private CharacterData CurrentCharacter => switcher != null ? switcher.Current : null;

        private void ShowCurrentBoard()
        {
            CharacterData current = CurrentCharacter;
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
                active = _board;
            }

            _board = active;
            Rebuild();
        }

        private void HandleChanged(EnchantFragment fragment)
        {
            Rebuild();
        }

        private void HandleOptionSelected(EnchantFragment fragment, int optionIndex)
        {
            Rebuild();
        }

        private void BindNodes()
        {
            if (boards != null)
            {
                for (int i = 0; i < boards.Length; i++)
                    BindBoard(boards[i].board);
            }

            BindBoard(_board);
        }

        private void BindBoard(RectTransform target)
        {
            if (target == null)
                return;

            foreach (EnchantNode node in target.GetComponentsInChildren<EnchantNode>(true))
            {
                Button button = node.GetComponent<Button>();
                if (button == null || node.Fragment == null)
                    continue;

                button.transition = Selectable.Transition.None;
                button.onClick.RemoveAllListeners();
                EnchantFragment captured = node.Fragment;
                button.onClick.AddListener(() =>
                {
                    _current = captured;
                    Rebuild();
                });
            }
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

        private void Rebuild()
        {
            Clear(_actions);
            RefreshNodes();

            if (module == null)
            {
                _title.text = "파편 모듈이 없습니다";
                return;
            }

            if (_current == null)
            {
                _title.text = "파편을 누르세요";
                return;
            }

            _title.text = _current.DisplayName;
            CharacterData character = CurrentCharacter;
            if (!module.IsUnlocked(character, _current))
            {
                Button activate = Spawn(_actions, "활성화", Color.white);
                EnchantFragment captured = _current;
                activate.onClick.AddListener(() => module.Activate(character, captured));
                return;
            }

            int selected = module.GetSelectedOption(character, _current);
            if (_current.Options == null)
                return;

            for (int i = 0; i < _current.Options.Length; i++)
            {
                EnchantOption option = _current.Options[i];
                if (option == null)
                    continue;

                int index = i;
                bool chosen = selected == i;
                Color color = chosen ? Color.white : BranchColor(_current.Branch, true);
                Button button = Spawn(_actions, option.DisplayName, color);
                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.color = chosen ? new Color(0.07f, 0.08f, 0.1f) : Color.white;

                EnchantFragment captured = _current;
                button.onClick.AddListener(() => module.TrySelectOption(character, captured, index));
            }
        }

        private void RefreshNodes()
        {
            if (_board == null || module == null)
                return;

            foreach (EnchantNode node in _board.GetComponentsInChildren<EnchantNode>(true))
            {
                EnchantFragment fragment = node.Fragment;
                if (fragment == null)
                    continue;

                bool unlocked = module.IsUnlocked(CurrentCharacter, fragment);
                bool viewing = fragment == _current;
                node.SetColor(BranchColor(fragment.Branch, unlocked || viewing));
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

        private static Color BranchColor(EnchantBranch branch, bool bright)
        {
            Color color = branch switch
            {
                EnchantBranch.Attack => new Color(1f, 0.35f, 0.35f),
                EnchantBranch.Defense => new Color(0.35f, 0.66f, 1f),
                EnchantBranch.Utility => new Color(0.35f, 1f, 0.54f),
                _ => Color.white
            };

            if (!bright)
                color = Color.Lerp(Color.black, color, 0.35f);

            color.a = 1f;
            return color;
        }
    }
}
