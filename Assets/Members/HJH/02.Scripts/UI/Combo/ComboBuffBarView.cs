using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Element;
using Assets.Members.HJH._02.Scripts.Hud;
using Members.JJH._02_Scripts.ElementsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 지금 켜져 있는 콤보 효과를 작은 아이콘으로 보여줌 (왼쪽 위, 골드 밑).
    //  - 원 안에 콤보 속성 아이콘, 시계 방향으로 줄어드는 청록 빛, 아래에 남은 초와 이름
    //  - 값은 PlayerHudSource.TryGetComboEffectTime 에서 매 프레임 읽음
    // 프리팹(05.Data/Prefab/UI/ComboBuffBar). 칸 6개가 HorizontalLayoutGroup 으로 줄 섬 → 간격·크기는 프리팹에서 수정.
    // 실행 중에는 칸 켜고 끄기, 글자·아이콘·빛 양만 바꿈
    public class ComboBuffBarView : MonoBehaviour, IUiBake
    {
        [Serializable]
        public sealed class Slot
        {
            public RectTransform root;
            public Image wipe;
            public Image[] badges;
            public TMP_Text time;
            public TMP_Text label;
        }

        [SerializeField] private List<Slot> slots = new();
        [SerializeField, Tooltip("아이콘이 3개 이상일 때 / 2개 이하일 때 크기")] private Vector2 badgeSize = new(20f, 26f);
        [SerializeField] private RectTransform tip;
        [SerializeField] private TMP_Text tipText;
        [SerializeField, Tooltip("효과 설명 상자: 아이콘 위쪽 기준 아래로 얼마나")] private float tipOffsetY = -124f;

        private PlayerHudSource _hud;
        private Canvas _canvas;

        private void Start()
        {
            _hud = PlayerHudSource.Instance;
            Canvas canvas = GetComponentInParent<Canvas>();
            _canvas = canvas != null ? canvas.rootCanvas : null;
            if (_hud == null || _canvas == null)
            {
                enabled = false;
                return;
            }
            foreach (Slot slot in slots)
                slot.root.gameObject.SetActive(false);
            if (tip != null)
                tip.gameObject.SetActive(false);
        }

        private void Update()
        {
            int used = 0;
            Slot hovered = null;
            string hoveredName = null;
            Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1f, -1f);
            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            foreach (ElementComboBook.Recipe recipe in _hud.ComboList)
            {
                if (used >= slots.Count || !_hud.TryGetComboEffectTime(recipe.Name, out float left, out float total))
                    continue;

                Slot slot = slots[used++];
                slot.root.gameObject.SetActive(true);
                slot.wipe.fillAmount = total > 0f ? Mathf.Clamp01(left / total) : 1f;
                slot.time.text = $"{Mathf.CeilToInt(left)}s";   // 정수 초 (0.x 초 남아도 1s 로)
                slot.label.text = recipe.Name;
                if (RectTransformUtility.RectangleContainsScreenPoint(slot.root, mouse, cam))
                {
                    hovered = slot;
                    hoveredName = recipe.Name;
                }

                float size = recipe.Length > 2 ? badgeSize.x : badgeSize.y;
                for (int i = 0; i < slot.badges.Length; i++)
                {
                    bool has = i < recipe.Length;
                    slot.badges[i].gameObject.SetActive(has);
                    if (!has)
                        continue;

                    slot.badges[i].sprite = _hud.Palette != null ? _hud.Palette.BadgeOf(recipe.Sequence[i]) : null;
                    slot.badges[i].color = slot.badges[i].sprite != null ? Color.white : _hud.ColorOf(recipe.Sequence[i]);
                    slot.badges[i].rectTransform.sizeDelta = new Vector2(size, size);   // 줄 세우기는 Badges 의 HorizontalLayoutGroup
                }
            }

            UpdateTip(hovered, hoveredName);

            for (int i = used; i < slots.Count; i++)
                slots[i].root.gameObject.SetActive(false);
        }

        // 효과 아이콘에 마우스를 올리면 아이콘 아래에 작은 상자로 무슨 효과인지 짧게 보여줌
        private void UpdateTip(Slot slot, string comboName)
        {
            if (tip == null || tipText == null)
                return;
            if (slot == null)
            {
                tip.gameObject.SetActive(false);
                return;
            }

            string effect = ComboLoadout.EffectOf(comboName);
            tipText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(HudUiKit.Cyan)}><size=85%>{comboName}</size></color>\n{(string.IsNullOrEmpty(effect) ? "효과 없음" : effect)}";
            tip.gameObject.SetActive(true);
            tip.SetAsLastSibling();

            // 상자 크기는 ContentSizeFitter 가 글자에 맞춤. 위치만 아이콘 왼쪽 끝 아래로
            Vector3 corner = slot.root.TransformPoint(new Vector3(slot.root.rect.xMin, slot.root.rect.yMax, 0f));
            Vector3 local = ((RectTransform)tip.parent).InverseTransformPoint(corner);
            tip.anchoredPosition = Vector2.zero;
            tip.localPosition = new Vector3(local.x, local.y + tipOffsetY, 0f);
        }

        [ContextMenu("레이아웃 굽기 (자식 다시 만들기)")]
        public void BakeLayout()
        {
#if UNITY_EDITOR
            const int count = 6;
            const float step = 84f;
            float d = step - 8f;
            HudUiKit.ClearChildren(transform);
            foreach (Component old in new Component[] { GetComponent<HorizontalLayoutGroup>() })
            {
                if (old != null)
                    DestroyImmediate(old);
            }

            var root = (RectTransform)transform;
            HudUiKit.Place(root, new Vector2(0f, 1f), new Vector2(48f, -172f), new Vector2(step * count, 100f), new Vector2(0f, 1f));
            var row = gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 8f;
            row.childAlignment = TextAnchor.UpperLeft;
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            slots = new List<Slot>();
            for (int index = 0; index < count; index++)
            {
                var slot = new Slot { root = HudUiKit.NewRect("Effect" + index, transform) };
                slot.root.sizeDelta = new Vector2(d, 96f);

                Image back = HudUiKit.NewImage("Back", slot.root, HudUiKit.Load("slot_circle_bg"), new Color(1f, 1f, 1f, 0.9f));
                HudUiKit.Place(back.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -d * 0.5f), new Vector2(d, d), new Vector2(0.5f, 0.5f));

                slot.wipe = HudUiKit.NewImage("Wipe", slot.root, HudUiKit.Load("slot_circle_bg"), new Color(0.51f, 0.88f, 1f, 0.38f));
                slot.wipe.type = Image.Type.Filled;
                slot.wipe.fillMethod = Image.FillMethod.Radial360;
                slot.wipe.fillOrigin = (int)Image.Origin360.Top;
                slot.wipe.fillClockwise = false;
                HudUiKit.Place(slot.wipe.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -d * 0.5f), new Vector2(d, d), new Vector2(0.5f, 0.5f));

                Image frame = HudUiKit.NewImage("Frame", slot.root, HudUiKit.Load("slot_circle_frame"), new Color(1f, 1f, 1f, 0.95f));
                HudUiKit.Place(frame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -d * 0.5f), new Vector2(d, d), new Vector2(0.5f, 0.5f));

                // 아이콘 줄: 가운데 정렬 가로 줄
                RectTransform badges = HudUiKit.NewRect("Badges", slot.root);
                HudUiKit.Place(badges, new Vector2(0.5f, 1f), new Vector2(0f, -d * 0.5f), new Vector2(d, 30f), new Vector2(0.5f, 0.5f));
                var badgeRow = badges.gameObject.AddComponent<HorizontalLayoutGroup>();
                badgeRow.spacing = 2f;
                badgeRow.childAlignment = TextAnchor.MiddleCenter;
                badgeRow.childControlWidth = false;
                badgeRow.childControlHeight = false;
                badgeRow.childForceExpandWidth = false;
                badgeRow.childForceExpandHeight = false;
                slot.badges = new Image[3];
                for (int i = 0; i < 3; i++)
                {
                    slot.badges[i] = HudUiKit.NewImage("Badge" + i, badges, null, Color.white);
                    slot.badges[i].preserveAspect = true;
                    slot.badges[i].rectTransform.sizeDelta = new Vector2(26f, 26f);
                }

                slot.time = HudUiKit.NewText("Time", slot.root, "10s", 18f, HudUiKit.Cyan, TextAlignmentOptions.Center);
                HudUiKit.Place(slot.time.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -d - 12f), new Vector2(d + 10f, 22f), new Vector2(0.5f, 0.5f));
                slot.label = HudUiKit.NewText("Label", slot.root, "불 → 물", 13f, new Color(0.69f, 0.75f, 0.82f, 0.85f), TextAlignmentOptions.Center);
                HudUiKit.Place(slot.label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -d - 32f), new Vector2(d + 10f, 18f), new Vector2(0.5f, 0.5f));
                slots.Add(slot);
            }

            // 효과 설명 상자 (레이아웃 무시, 글자에 맞춰 크기 자동)
            tip = HudUiKit.NewRect("EffectTip", transform);
            tip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            HudUiKit.Place(tip, new Vector2(0f, 1f), new Vector2(0f, tipOffsetY), new Vector2(200f, 70f), new Vector2(0f, 1f));
            Image tipBack = tip.gameObject.AddComponent<Image>();
            tipBack.color = new Color(0.02f, 0.06f, 0.09f, 0.92f);
            tipBack.raycastTarget = false;
            var tipLayout = tip.gameObject.AddComponent<HorizontalLayoutGroup>();
            tipLayout.padding = new RectOffset(14, 14, 13, 13);
            tipLayout.childControlWidth = true;
            tipLayout.childControlHeight = true;
            var tipFit = tip.gameObject.AddComponent<ContentSizeFitter>();
            tipFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            tipFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Image edge = HudUiKit.NewImage("Edge", tip, null, new Color(0.51f, 0.88f, 1f, 0.55f));
            edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            edge.rectTransform.anchorMin = new Vector2(0f, 0f);
            edge.rectTransform.anchorMax = new Vector2(0f, 1f);
            edge.rectTransform.pivot = new Vector2(0f, 0.5f);
            edge.rectTransform.sizeDelta = new Vector2(2f, 0f);
            edge.rectTransform.anchoredPosition = Vector2.zero;
            tipText = HudUiKit.NewText("Text", tip, "콤보 이름\n공격력 +10%", 18f, HudUiKit.Value, TextAlignmentOptions.MidlineLeft);
            tipText.lineSpacing = 8f;
            tipText.richText = true;
            tip.gameObject.SetActive(false);
#endif
        }
    }
}
