using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Element;
using Members.JJH._02_Scripts.ElementsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // Skill tooltip: icon + name + element, description, then the numbers from SkillData.DescribeStats
    // (label left, value right) and a footer line. Sits just above the card it describes.
    public class SkillTooltipView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image[] accents;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text elementText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private TMP_Text footerText;
        [SerializeField] private float gap = 14f;
        [SerializeField] private float fadeSpeed = 10f;

        private readonly List<SkillStat> _stats = new();
        private readonly StringBuilder _text = new();
        private RectTransform _anchor;
        private bool _visible;

        private RectTransform Rect => (RectTransform)transform;

        public static string ElementLine(SkillData data) =>
            data != null && data.TryGetElement(out ElementType element)
                ? $"{ElementComboBook.KoreanName(element)} 속성 · 콤보 입력"
                : "무속성 · 콤보 없음";

        public void Show(SkillData data, ElementPalette palette, RectTransform anchor, string footer)
        {
            if (data == null)
            {
                Hide();
                return;
            }

            _anchor = anchor;
            _visible = true;

            Color color = palette != null ? palette.NeutralColor : Color.gray;
            if (data.TryGetElement(out ElementType element) && palette != null && palette.TryGet(element, out ElementPalette.Entry entry))
                color = entry.color;

            foreach (Image accent in accents)
            {
                if (accent != null)
                    accent.color = new Color(color.r, color.g, color.b, accent.color.a);
            }

            icon.sprite = data.Icon;
            icon.enabled = data.Icon != null;
            nameText.text = data.DisplayName;
            elementText.text = ElementLine(data);
            elementText.color = color;

            descriptionText.text = string.IsNullOrEmpty(data.Description) ? "설명 없음" : HighlightNumbers(data.Description);

            _stats.Clear();
            data.DescribeStats(_stats);
            _text.Clear();
            foreach (SkillStat stat in _stats)
            {
                if (_text.Length > 0)
                    _text.Append('\n');
                _text.Append("<color=#8F96B3>").Append(stat.Label).Append("</color><pos=58%><color=#FFD36B><b>").Append(stat.Value).Append("</b></color>");
            }

            statsText.text = _text.ToString();
            statsText.gameObject.SetActive(_stats.Count > 0);

            footerText.text = footer;
            footerText.gameObject.SetActive(!string.IsNullOrEmpty(footer));

            LayoutRebuilder.ForceRebuildLayoutImmediate(Rect);
            Place();
        }

        public void Hide() => _visible = false;

        // Shape of Dreams style: numbers in the text stand out in the accent color.
        private static readonly Regex Numbers = new(@"\d+(\.\d+)?(초|m|%|회)?");

        private static string HighlightNumbers(string text) => Numbers.Replace(text, "<color=#FFD36B><b>$0</b></color>");

        private void Awake()
        {
            if (group != null)
                group.alpha = 0f;
        }

        private void LateUpdate()
        {
            if (group != null)
                group.alpha = Mathf.MoveTowards(group.alpha, _visible ? 1f : 0f, Time.unscaledDeltaTime * fadeSpeed);

            if (_visible)
                Place();
        }

        // Above the anchor card (pivot is bottom-center); below it when there is no room above.
        private void Place()
        {
            if (_anchor == null)
                return;

            RectTransform rect = Rect;
            Vector3 scale = rect.lossyScale;
            float halfWidth = rect.rect.width * 0.5f * scale.x;
            float height = rect.rect.height * scale.y;

            Vector3 p = _anchor.TransformPoint(new Vector3(_anchor.rect.center.x, _anchor.rect.yMax + gap, 0f));
            if (p.y + height > Screen.height - 8f)
                p.y = _anchor.TransformPoint(new Vector3(0f, _anchor.rect.yMin - gap, 0f)).y - height;

            p.x = Mathf.Clamp(p.x, halfWidth + 8f, Screen.width - halfWidth - 8f);
            p.y = Mathf.Max(p.y, 8f);
            rect.position = p;
        }
    }
}
