using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Element;
using Assets.Members.HJH._02.Scripts.Hud;
using Members.KYR._01.Scripts.Stats;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Mod = Assets.Members.HJH._02.Scripts.Element.ComboStatBuff.Mod;

namespace Assets.Members.HJH._02.Scripts.UI
{
    public class ComboBuffBarView : MonoBehaviour, IUiBake
    {
        [Serializable]
        public sealed class Slot
        {
            public RectTransform root;
            public Image wipe;
            public Image ring;
            public Image icon;
            public TMP_Text amount;
            public TMP_Text time;
        }

        private struct Entry
        {
            public string Combo;
            public Mod Mod;
            public float Left;
            public float Total;
        }

        [SerializeField] private List<Slot> slots = new();
        [SerializeField] private StatIconSet icons;
        [SerializeField] private Color lowerColor = new(1f, 0.42f, 0.38f, 1f);
        [SerializeField, Tooltip("남은 시간이 이 초 이하면 아이콘이 깜빡임")] private float blinkBelow = 3f;
        [SerializeField] private RectTransform tip;
        [SerializeField] private TMP_Text tipText;
        [SerializeField, Tooltip("설명 상자: 아이콘 위 끝에서 얼마나 띄울지")] private float tipGap = 8f;

        private readonly List<Entry> _entries = new();
        private readonly List<PlayerHudSource.TimedBuff> _timed = new();
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
            Collect();

            Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1f, -1f);
            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            int hovered = -1;
            int shown = Mathf.Min(_entries.Count, slots.Count);
            for (int i = 0; i < shown; i++)
            {
                Fill(slots[i], _entries[i]);
                if (RectTransformUtility.RectangleContainsScreenPoint(slots[i].root, mouse, cam))
                    hovered = i;
            }
            for (int i = shown; i < slots.Count; i++)
                slots[i].root.gameObject.SetActive(false);

            UpdateTip(hovered >= 0 ? slots[hovered] : null, hovered >= 0 ? _entries[hovered] : default);
        }

        private void Collect()
        {
            _entries.Clear();
            foreach (ElementComboBook.Recipe recipe in _hud.ComboList)
            {
                if (!_hud.TryGetComboEffectTime(recipe.Name, out float left, out float total))
                    continue;
                foreach (Mod mod in ComboLoadout.ModsOf(recipe.Name))
                    _entries.Add(new Entry { Combo = recipe.Name, Mod = mod, Left = left, Total = total });
            }

            _hud.CollectTimedBuffs(_timed);
            foreach (PlayerHudSource.TimedBuff buff in _timed)
                _entries.Add(new Entry { Combo = buff.Name, Mod = new Mod(buff.Stat, buff.Type, buff.Value), Left = buff.Left, Total = buff.Total });

            _entries.Sort((a, b) =>
            {
                int byTime = b.Left.CompareTo(a.Left);
                if (byTime != 0)
                    return byTime;
                int byCombo = string.CompareOrdinal(a.Combo, b.Combo);
                return byCombo != 0 ? byCombo : a.Mod.stat.CompareTo(b.Mod.stat);
            });
        }

        private void Fill(Slot slot, Entry entry)
        {
            slot.root.gameObject.SetActive(true);
            PlayerStatId stat = entry.Mod.stat;
            Color color = icons != null ? icons.ColorOf(stat) : Color.white;
            bool lower = entry.Mod.value < 0f;

            slot.icon.sprite = icons != null ? icons.IconOf(stat) : null;
            slot.icon.enabled = slot.icon.sprite != null;
            float blink = entry.Left <= blinkBelow ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f;
            slot.icon.color = new Color(color.r, color.g, color.b, blink);
            if (slot.ring != null)
                slot.ring.color = new Color(color.r, color.g, color.b, slot.ring.color.a);

            slot.wipe.fillAmount = entry.Total > 0f ? Mathf.Clamp01(entry.Left / entry.Total) : 1f;
            slot.wipe.color = new Color(color.r, color.g, color.b, slot.wipe.color.a);

            slot.time.text = $"{Mathf.CeilToInt(entry.Left)}s";
            if (slot.amount != null)
            {
                slot.amount.text = ComboEffect.Amount(entry.Mod);
                slot.amount.color = lower ? lowerColor : HudUiKit.Value;
            }
        }

        private void UpdateTip(Slot slot, Entry entry)
        {
            if (tip == null || tipText == null)
                return;
            if (slot == null)
            {
                tip.gameObject.SetActive(false);
                return;
            }

            string color = ColorUtility.ToHtmlStringRGB(icons != null ? icons.ColorOf(entry.Mod.stat) : HudUiKit.Cyan);
            tipText.text = $"<color=#{color}>{ComboEffect.Describe(entry.Mod)}</color>\n" +
                           $"<size=80%><color=#{ColorUtility.ToHtmlStringRGB(HudUiKit.Label)}>{entry.Combo} · {Mathf.CeilToInt(entry.Left)}초</color></size>";
            tip.gameObject.SetActive(true);
            tip.SetAsLastSibling();

            LayoutRebuilder.ForceRebuildLayoutImmediate(tip);
            Vector3 corner = slot.root.TransformPoint(new Vector3(slot.root.rect.xMin, slot.root.rect.yMax, 0f));
            Vector3 local = ((RectTransform)tip.parent).InverseTransformPoint(corner);
            tip.localPosition = new Vector3(local.x + tip.rect.width * tip.pivot.x, local.y + tipGap + tip.rect.height * tip.pivot.y, 0f);
        }

        [ContextMenu("레이아웃 굽기 (자식 다시 만들기)")]
        public void BakeLayout()
        {
#if UNITY_EDITOR
            const int columns = 5;
            const int count = columns * 3;
            const float d = 52f;
            var cell = new Vector2(d + 4f, d + 22f);
            HudUiKit.ClearChildren(transform);
            foreach (Component old in new Component[] { GetComponent<HorizontalLayoutGroup>(), GetComponent<GridLayoutGroup>(), GetComponent<ContentSizeFitter>() })
            {
                if (old != null)
                    DestroyImmediate(old);
            }

            icons = UnityEditor.AssetDatabase.LoadAssetAtPath<StatIconSet>(StatIconSetPath);

            var root = (RectTransform)transform;
            HudUiKit.Place(root, new Vector2(0f, 1f), new Vector2(48f, -172f), new Vector2(cell.x * columns, cell.y), new Vector2(0f, 1f));
            var grid = gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = new Vector2(6f, 6f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            var fit = gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Sprite circle = HudUiKit.Load("slot_circle_bg");
            Sprite ring = HudUiKit.Load("slot_circle_frame");
            slots = new List<Slot>();
            for (int index = 0; index < count; index++)
            {
                var slot = new Slot { root = HudUiKit.NewRect("Buff" + index, transform) };
                var top = new Vector2(0.5f, 1f);
                var center = new Vector2(0.5f, 0.5f);
                var circlePos = new Vector2(0f, -d * 0.5f);

                Image back = HudUiKit.NewImage("Back", slot.root, circle, new Color(0.04f, 0.07f, 0.1f, 0.85f));
                HudUiKit.Place(back.rectTransform, top, circlePos, new Vector2(d, d), center);

                slot.wipe = HudUiKit.NewImage("Wipe", slot.root, circle, new Color(1f, 1f, 1f, 0.28f));
                slot.wipe.type = Image.Type.Filled;
                slot.wipe.fillMethod = Image.FillMethod.Radial360;
                slot.wipe.fillOrigin = (int)Image.Origin360.Top;
                slot.wipe.fillClockwise = false;
                HudUiKit.Place(slot.wipe.rectTransform, top, circlePos, new Vector2(d, d), center);

                slot.ring = HudUiKit.NewImage("Ring", slot.root, ring, new Color(1f, 1f, 1f, 0.9f));
                HudUiKit.Place(slot.ring.rectTransform, top, circlePos, new Vector2(d, d), center);

                slot.icon = HudUiKit.NewImage("Icon", slot.root, null, Color.white);
                slot.icon.preserveAspect = true;
                HudUiKit.Place(slot.icon.rectTransform, top, circlePos + new Vector2(0f, 3f), new Vector2(d * 0.52f, d * 0.52f), center);

                slot.amount = HudUiKit.NewText("Amount", slot.root, "+20%", 13f, HudUiKit.Value, TextAlignmentOptions.Center);
                slot.amount.fontStyle = FontStyles.Bold;
                slot.amount.outlineWidth = 0.25f;
                slot.amount.outlineColor = new Color32(0, 0, 0, 220);
                HudUiKit.Place(slot.amount.rectTransform, top, new Vector2(0f, -d + 8f), new Vector2(d + 8f, 16f), center);

                slot.time = HudUiKit.NewText("Time", slot.root, "10s", 15f, HudUiKit.Label, TextAlignmentOptions.Center);
                HudUiKit.Place(slot.time.rectTransform, top, new Vector2(0f, -d - 10f), new Vector2(d + 8f, 18f), center);
                slots.Add(slot);
            }

            tip = HudUiKit.NewRect("EffectTip", transform);
            tip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            HudUiKit.Place(tip, new Vector2(0f, 1f), Vector2.zero, new Vector2(200f, 60f), new Vector2(0f, 1f));
            Image tipBack = tip.gameObject.AddComponent<Image>();
            tipBack.color = new Color(0.02f, 0.06f, 0.09f, 0.92f);
            tipBack.raycastTarget = false;
            var tipLayout = tip.gameObject.AddComponent<HorizontalLayoutGroup>();
            tipLayout.padding = new RectOffset(14, 14, 10, 10);
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
            tipText = HudUiKit.NewText("Text", tip, "공격 속도 +20%\n불 → 번개 · 7초", 18f, HudUiKit.Value, TextAlignmentOptions.MidlineLeft);
            tipText.lineSpacing = 6f;
            tipText.richText = true;
            tip.gameObject.SetActive(false);
#endif
        }

        public const string StatIconSetPath = "Assets/Members/HJH/05.Data/UI/StatIconSet.asset";
    }
}
