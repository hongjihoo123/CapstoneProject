using Assets.Members.HJH._02.Scripts.Hud;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Element;
using DG.Tweening;
using Members.JJH._02_Scripts.ElementsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // Move list, fighting-game style: one row per combo in JJH's table, written as the key caps that
    // cast each element with the current kit. Toggled with toggleKey (closed by default: only a small
    // "[Tab] 콤보 리스트" tab shows). While a chain is running, only the combos it can still continue are
    // listed, furthest first; with no chain (or nothing to continue) every combo is listed.
    // Rows light up the inputs already in the chain and flash when their combo lands.
    public class ComboListView : MonoBehaviour
    {
        [SerializeField] private RectTransform rowTemplate;
        [SerializeField] private RectTransform rowParent;
        [SerializeField] private GameObject body;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text closedTab;
        [SerializeField] private Key toggleKey = Key.Tab;
        [SerializeField] private bool startOpen;
        [SerializeField] private Color idleRowColor = new Color(0.55f, 0.48f, 0.32f, 0.75f);
        [SerializeField] private Color flashRowColor = new Color(1f, 0.83f, 0.3f, 0.9f);
        [SerializeField] private float pendingStepAlpha = 0.35f;
        [SerializeField] private float flashDuration = 0.8f;

        private sealed class Row
        {
            public ElementComboBook.Recipe Recipe;
            public CanvasGroup Group;
            public Image Background;
            public Image[] Steps;
            public TMP_Text[] StepKeys;
            public int Progress;
            public Tween Flash;
        }

        private readonly List<Row> _rows = new();
        private readonly List<Row> _sorted = new();
        private ElementPalette _palette;
        private bool _open;

        // Scenes built before the toggle existed have no Body: fall back to hiding the rows and title.
        private void Awake()
        {
            if (body == null && rowParent != null)
                body = rowParent.gameObject;
            SetOpen(startOpen);
        }

        public void Build(ElementComboBook book, ElementPalette palette)
        {
            _palette = palette;
            foreach (Row row in _rows)
                Destroy(row.Group.gameObject);
            _rows.Clear();

            rowTemplate.gameObject.SetActive(false);
            foreach (ElementComboBook.Recipe recipe in book.Recipes)
                _rows.Add(CreateRow(recipe));
        }

        public void Refresh(IReadOnlyList<ElementType> inputs, Dictionary<ElementType, string> keys)
        {
            bool anyOnRoute = false;
            foreach (Row row in _rows)
            {
                row.Progress = row.Recipe.TailProgress(inputs);
                anyOnRoute |= row.Progress > 0;
            }

            // Rows in progress light only the inputs already made; the full list shows every step lit.
            foreach (Row row in _rows)
            {
                for (int i = 0; i < row.Steps.Length; i++)
                {
                    ElementType element = row.Recipe.Sequence[i];
                    float alpha = !anyOnRoute || i < row.Progress ? 1f : pendingStepAlpha;
                    Color color = _palette != null && _palette.TryGet(element, out ElementPalette.Entry entry) ? entry.color : Color.gray;
                    row.Steps[i].color = new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, alpha);
                    row.StepKeys[i].text = ComboKeyMap.KeyFor(keys, element);
                    row.StepKeys[i].alpha = alpha;
                }
            }

            // Filter: continuable combos only, furthest along first. Nothing continuable -> the whole list.
            _sorted.Clear();
            _sorted.AddRange(_rows);
            if (anyOnRoute)
                _sorted.Sort((a, b) => b.Progress != a.Progress ? b.Progress.CompareTo(a.Progress) : a.Recipe.Length.CompareTo(b.Recipe.Length));

            for (int i = 0; i < _sorted.Count; i++)
            {
                Row row = _sorted[i];
                bool visible = !anyOnRoute || row.Progress > 0;
                row.Group.gameObject.SetActive(visible);
                row.Group.transform.SetSiblingIndex(i + 1); // index 0 is the hidden template
            }

            if (title != null)
                title.text = anyOnRoute ? "이어갈 수 있는 콤보" : "콤보 리스트";
        }

        public void FlashLanded(ElementComboBook.Recipe recipe)
        {
            foreach (Row row in _rows)
            {
                if (row.Recipe.Name != recipe.Name)
                    continue;

                row.Flash?.Kill();
                row.Background.color = flashRowColor;
                row.Flash = row.Background.DOColor(idleRowColor, flashDuration)
                    .SetEase(Ease.InQuad)
                    .SetUpdate(true)
                    .SetLink(row.Background.gameObject);
            }
        }

        private void SetOpen(bool open)
        {
            _open = open;
            if (body != null)
                body.SetActive(open);
            if (title != null && !title.transform.IsChildOf(body != null ? body.transform : transform))
                title.gameObject.SetActive(open);
            if (closedTab != null)
                closedTab.gameObject.SetActive(!open);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
                SetOpen(!_open);
        }

        private Row CreateRow(ElementComboBook.Recipe recipe)
        {
            RectTransform rect = Instantiate(rowTemplate, rowParent);
            rect.gameObject.SetActive(true);
            rect.name = $"Row_{recipe.Name}";

            var row = new Row
            {
                Recipe = recipe,
                Group = rect.GetComponent<CanvasGroup>(),
                Background = rect.GetComponent<Image>(),
            };
            row.Background.color = idleRowColor;
            rect.Find("Name").GetComponent<TMP_Text>().text = recipe.Name;

            // The template holds one step cap; clone it once per input of the combo.
            Transform stepTemplate = rect.Find("Steps").GetChild(0);
            row.Steps = new Image[recipe.Length];
            row.StepKeys = new TMP_Text[recipe.Length];
            for (int i = 0; i < recipe.Length; i++)
            {
                Transform step = i == 0 ? stepTemplate : Instantiate(stepTemplate, stepTemplate.parent);
                row.Steps[i] = step.GetComponent<Image>();
                row.StepKeys[i] = step.GetComponentInChildren<TMP_Text>();
            }

            return row;
        }
    }
}
