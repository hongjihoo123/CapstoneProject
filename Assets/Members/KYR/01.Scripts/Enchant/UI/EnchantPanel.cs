using Members.KYR._01.Scripts.Enchant.Data;
using Members.KYR._01.Scripts.Enchant.So;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Members.KYR._01.Scripts.Enchant.UI
{
    public class EnchantPanel : MonoBehaviour
    {
        [SerializeField] private EnchantModule module;
        [SerializeField] private RectTransform fragmentRow;
        [SerializeField] private RectTransform optionRow;
        [SerializeField] private Button buttonPrefab;

        private RectTransform _board;
        private TMP_Text _title;
        private RectTransform _actions;
        private EnchantFragment _current;
        private TMP_FontAsset _font;

        private void Awake()
        {
            if (module == null)
                module = FindFirstObjectByType<EnchantModule>();

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

            BuildChrome();
        }

        private void OnEnable()
        {
            if (module != null)
            {
                module.FragmentUnlocked += HandleChanged;
                module.OptionSelected += HandleOptionSelected;
            }

            Rebuild();
        }

        private void OnDisable()
        {
            if (module == null)
                return;

            module.FragmentUnlocked -= HandleChanged;
            module.OptionSelected -= HandleOptionSelected;
        }

        private void HandleChanged(EnchantFragment fragment)
        {
            Rebuild();
        }

        private void HandleOptionSelected(EnchantFragment fragment, int optionIndex)
        {
            Rebuild();
        }

        private void BuildChrome()
        {
            _board = CreateRect("Board", transform);
            Stretch(_board, 0f, 130f, 0f, 0f);

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
        }

        private void Rebuild()
        {
            if (_board == null)
                return;

            Clear(_board);
            Clear(_actions);

            if (module == null)
            {
                _title.text = "파편 모듈이 없습니다";
                return;
            }

            foreach (EnchantFragment fragment in module.Fragments)
            {
                if (fragment == null)
                    continue;

                bool unlocked = module.IsUnlocked(fragment);
                bool viewing = fragment == _current;
                Button node = Spawn(_board, string.Empty, BranchColor(fragment.Branch, unlocked || viewing));
                RectTransform rect = node.transform as RectTransform;
                Vector2 position = fragment.BoardPosition;
                rect.anchorMin = rect.anchorMax = new Vector2(
                    Mathf.Clamp01(position.x),
                    Mathf.Clamp01(position.y));
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(viewing ? 64f : 44f, viewing ? 64f : 44f);

                LayoutElement element = node.gameObject.AddComponent<LayoutElement>();
                element.ignoreLayout = true;

                EnchantFragment captured = fragment;
                node.onClick.AddListener(() =>
                {
                    _current = captured;
                    Rebuild();
                });
            }

            if (_current == null)
            {
                _title.text = "파편을 누르세요";
                return;
            }

            _title.text = _current.DisplayName;
            if (!module.IsUnlocked(_current))
            {
                Button activate = Spawn(_actions, "활성화", Color.white);
                EnchantFragment captured = _current;
                activate.onClick.AddListener(() => module.Activate(captured));
                return;
            }

            int selected = module.GetSelectedOption(_current);
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
                button.onClick.AddListener(() => module.TrySelectOption(captured, index));
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

        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
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
