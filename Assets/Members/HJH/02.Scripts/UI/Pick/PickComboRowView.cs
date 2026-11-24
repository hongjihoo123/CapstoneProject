using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Element;
using Members.JJH._02_Scripts.ElementsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 픽 화면 오른쪽 "시작 콤보" 한 줄. 프리팹(05.Data/Prefab/UI/PickComboRow) → 모양은 프리팹에서 수정.
    // CharacterPickScreen 이 캐릭터마다 이 프리팹을 복제해서 Setup 으로 내용만 채움
    public class PickComboRowView : MonoBehaviour, IUiBake
    {
        [SerializeField] private Image back;
        [SerializeField, Tooltip("고른 줄 왼쪽 금색 막대")] private Image mark;
        [SerializeField, Tooltip("원소 아이콘 (콤보 길이만큼 켜짐)")] private Image[] icons;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text effectText;
        [SerializeField] private TMP_Text bonusText;
        [SerializeField] private Button button;
        [SerializeField] private Color selectedBack = new(0.84f, 0.72f, 0.47f, 0.06f);
        [SerializeField] private Color normalBack = new(1f, 1f, 1f, 0.015f);
        [SerializeField] private Color selectedName = new(0.84f, 0.72f, 0.47f, 1f);
        [SerializeField] private Color normalName = new(0.91f, 0.94f, 0.97f, 1f);

        public ElementType[] Sequence { get; private set; }

        public void Setup(ElementType[] sequence, ElementPalette palette, Action onClick)
        {
            Sequence = sequence;
            for (int k = 0; k < icons.Length; k++)
            {
                bool has = k < sequence.Length;
                icons[k].gameObject.SetActive(has);
                if (!has)
                    continue;
                Sprite badge = palette != null ? palette.BadgeOf(sequence[k]) : null;
                icons[k].sprite = badge;
                icons[k].color = badge != null ? Color.white : (palette != null ? palette.ColorOf(sequence[k]) : Color.white);
            }

            ComboEffect effect = ComboLoadout.Evaluate(sequence);
            nameText.text = ComboLoadout.NameOf(sequence);
            effectText.text = effect.Summary();
            bonusText.text = BonusLine(effect);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
        }

        public void SetSelected(bool selected)
        {
            mark.enabled = selected;
            back.color = selected ? selectedBack : normalBack;
            nameText.color = selected ? selectedName : normalName;
        }

        // 이웃 보너스만 모아서 한 줄로 ("보너스  불→물 상성: 불 -5% · 물 +10%")
        private static string BonusLine(ComboEffect effect)
        {
            var parts = new List<string>();
            foreach (ComboEffect.Part part in effect.Parts)
            {
                if (part.IsBonus)
                    parts.Add($"{part.Source}: {ComboEffect.Describe(part.Mod)}");
            }
            return parts.Count == 0 ? "" : "<color=#D6B878>보너스</color>  " + string.Join("  ·  ", parts);
        }

        [ContextMenu("레이아웃 굽기 (자식 다시 만들기)")]
        public void BakeLayout()
        {
#if UNITY_EDITOR
            const float width = 580f, height = 110f;
            HudUiKit.ClearChildren(transform);
            var root = (RectTransform)transform;
            HudUiKit.Place(root, new Vector2(0f, 1f), Vector2.zero, new Vector2(width, height), new Vector2(0f, 1f));
            var element = GetComponent<LayoutElement>() ? GetComponent<LayoutElement>() : gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.preferredWidth = width;

            back = HudUiKit.NewImage("Back", root, null, normalBack);
            HudUiKit.Stretch(back.rectTransform);
            back.raycastTarget = true;

            mark = HudUiKit.NewImage("Mark", root, null, HudUiKit.Gold);
            HudUiKit.Place(mark.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, height - 12f));

            icons = new Image[4];
            for (int k = 0; k < icons.Length; k++)
            {
                icons[k] = HudUiKit.NewImage("Element_" + k, root, HudUiKit.Load("slot_circle_bg"), Color.white);
                icons[k].preserveAspect = true;
                HudUiKit.Place(icons[k].rectTransform, new Vector2(0f, 1f), new Vector2(18f + k * 40f, -12f), new Vector2(34f, 34f));
            }

            nameText = HudUiKit.NewText("Name", root, "불 → 번개", 22f, HudUiKit.Title, TextAlignmentOptions.MidlineLeft);
            HudUiKit.Place(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(110f, -12f), new Vector2(width - 120f, 34f));

            effectText = HudUiKit.NewText("Effect", root, "공격력 +10% · 공격 속도 +10%", 18f, HudUiKit.Gold, TextAlignmentOptions.MidlineLeft);
            HudUiKit.Place(effectText.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -54f), new Vector2(width - 28f, 26f));

            bonusText = HudUiKit.NewText("Detail", root, "", 15f, HudUiKit.Label, TextAlignmentOptions.MidlineLeft);
            bonusText.richText = true;
            HudUiKit.Place(bonusText.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -82f), new Vector2(width - 28f, 22f));

            button = GetComponent<Button>() ? GetComponent<Button>() : gameObject.AddComponent<Button>();
            button.targetGraphic = back;
            button.transition = Selectable.Transition.None;
#endif
        }
    }
}
