using System;
using System.Collections.Generic;
using System.Text;
using Assets.Members.HJH._02.Scripts.Element;
using DG.Tweening;
using Members.JJH._02_Scripts.ElementsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Mod = Assets.Members.HJH._02.Scripts.Element.ComboStatBuff.Mod;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 콤보 바인딩 창. 원소를 순서대로 2~4칸 채우고 "저장하기" → ComboLoadout.TryBind (기존 바인딩은 교체).
    //  - 원소 버튼: 빈 칸 맨 앞에 넣음 / 채운 칸 클릭: 그 칸 빼기 / 전체 초기화: 칸 전부 비우기
    //  - 넣거나 뺄 때마다 그 원소 효과와 바뀐 능력치를 보여주고, 아래에 효과 목록 + 합계
    //  - 열려 있는 동안 시간 멈춤. Esc 또는 닫기로 닫음
    // 모양은 HUD/픽 화면과 같은 말투: 눈 아이콘 머리말, 금색 선, 얇게 빛나는 원형 칸, 파란 그라데이션 버튼.
    // 프로젝트가 Linear 색 공간이라 반투명 판은 뒤 HUD 가 생각보다 많이 비침 → 창 판은 완전 불투명.
    // 상점 오브젝트(ComboBindStation)가 프리팹을 넘겨 열고, 나중에 레벨업 같은 곳에서도 Open(prefab) 만 부르면 됨
    // 모양은 프리팹(05.Data/Prefab/UI/ComboBindPanel)에 저장된 그대로 → 폰트·크기·위치는 프리팹에서 수정.
    // 실행 중에는 글자/아이콘/색만 바꿈. 버튼은 인스펙터 OnClick 에 연결돼 있음 (AddChoice, RemoveAt, Clear, Close, OnSaveClicked)
    public class ComboBindPanel : MonoBehaviour, IUiBake
    {
        private static readonly ElementType[] Choices =
            { ElementType.Fire, ElementType.Water, ElementType.Wind, ElementType.Electric, ElementType.Earth };

        private const float Width = 1060f;
        private const float Height = 1040f;
        private const float Pad = 56f;

        private static readonly Color PanelColor = new(0.035f, 0.05f, 0.08f, 1f);
        private static readonly Color EdgeColor = new(0.55f, 0.75f, 0.95f, 0.45f);
        private static readonly Color Faint = new(1f, 1f, 1f, 0.22f);
        private static readonly Color Warn = new(1f, 0.55f, 0.45f, 1f);
        private static readonly Color Good = new(0.55f, 0.95f, 0.6f, 1f);
        private const string GrayHex = "#8A8A96";

        [Serializable]
        public sealed class SlotView
        {
            public RectTransform root;
            public Image glow;
            public Image frame;
            public Image badge;
            public TMP_Text plus;
            public TMP_Text label;
            public TMP_Text effect;
        }

        public static ComboBindPanel Current { get; private set; }
        public static bool IsOpen => Current != null;

        // 프리팹에 구워 둔 부품 (BakeLayout 이 채움)
        [SerializeField] private ElementPalette palette;
        [SerializeField] private RectTransform window;
        [SerializeField] private CanvasGroup windowGroup;
        [SerializeField] private List<SlotView> slots = new();
        [SerializeField, Tooltip("칸 사이 보너스 글자")] private List<TMP_Text> pairs = new();
        [SerializeField] private TMP_Text changeText;
        [SerializeField] private TMP_Text totalText;
        [SerializeField] private TMP_Text totalNoteText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Image saveButton;
        [SerializeField, Tooltip("원소 효과 줄 (Choices 순서)")] private TMP_Text[] elementRows;
        [SerializeField] private TMP_Text bonusRules;
        [SerializeField] private TMP_Text lengthScale;

        private static ElementPalette _activePalette;
        private readonly List<ElementType> _sequence = new();
        private List<SlotView> _slots => slots;
        private List<TMP_Text> _pairs => pairs;
        private Action _onClosed;
        private float _timeScale;
        private bool _closing;
        private bool _saved;

        public IReadOnlyList<ElementType> Sequence => _sequence;
        public bool WasSaved => _saved;
        public static event Action Opened;
        public static event Action Closed;

        public static ComboBindPanel Open(ComboBindPanel prefab, Action onClosed = null)
        {
            if (Current != null)
                return Current;
            if (prefab == null)
            {
                Debug.LogError("[ComboBindPanel] 프리팹이 연결되지 않았습니다 (ComboBindStation 의 Panel Prefab).");
                return null;
            }

            ComboBindPanel panel = Instantiate(prefab);
            panel.name = prefab.name;
            panel._onClosed = onClosed;
            panel.Init();
            Opened?.Invoke();
            return panel;
        }

        private void Init()
        {
            Current = this;
            _timeScale = Time.timeScale;
            Time.timeScale = 0f;
            HudUiKit.EnsureEventSystem();
            _activePalette = palette;

            FillLegend();
            if (ComboLoadout.Bound != null)
                _sequence.AddRange(ComboLoadout.Bound);
            Refresh(null);

            // 열릴 때 살짝 떠오르게 (시간이 멈춰 있어서 unscaled)
            if (windowGroup != null)
            {
                windowGroup.alpha = 0f;
                windowGroup.DOFade(1f, 0.15f).SetUpdate(true);
            }
            Vector2 home = window.anchoredPosition;
            window.anchoredPosition = home + new Vector2(0f, -16f);
            window.DOAnchorPosY(home.y, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        // 표에서 읽는 안내 글자 (원소 효과 줄, 보너스 규칙, 길이 배율). 모양은 프리팹, 내용만 표 기준
        private void FillLegend()
        {
            ComboElementTable table = ComboElementTable.Default;
            if (elementRows != null)
            {
                for (int i = 0; i < elementRows.Length && i < Choices.Length; i++)
                {
                    if (elementRows[i] != null)
                        elementRows[i].text = ElementRowText(table, Choices[i]);
                }
            }
            if (bonusRules != null)
                bonusRules.text = BonusText(table);
            if (lengthScale != null)
                lengthScale.text = ScaleText(table);
        }

        // 버튼 OnClick 용 (인스펙터에서 정수 인자 = Choices 순서: 0 불, 1 물, 2 바람, 3 번개, 4 땅)
        public void AddChoice(int index)
        {
            if (index >= 0 && index < Choices.Length)
                Add(Choices[index]);
        }

        public void OnSaveClicked() => Save();

        // ---------------------------------------------------------------- 레이아웃 굽기 (에디터 전용)

        [ContextMenu("레이아웃 굽기 (자식 다시 만들기)")]
        public void BakeLayout()
        {
#if UNITY_EDITOR
            HudUiKit.ClearChildren(transform);
            slots = new List<SlotView>();
            pairs = new List<TMP_Text>();
            _activePalette = palette != null ? palette : palette = ElementPalette.Default;

            var canvas = GetComponent<Canvas>() ? GetComponent<Canvas>() : gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;   // HUD(3), 튜토리얼 카드(900) 위
            var scaler = GetComponent<CanvasScaler>() ? GetComponent<CanvasScaler>() : gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            Image dim = HudUiKit.NewImage("Dim", transform, null, new Color(0.01f, 0.015f, 0.03f, 0.82f));
            HudUiKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            // 창: 불투명 판 + 9슬라이스 테두리 + 위쪽 금색 선
            Image panel = HudUiKit.NewImage("Window", transform, null, PanelColor);
            HudUiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, Height));
            panel.raycastTarget = true;
            window = panel.rectTransform;
            windowGroup = panel.gameObject.AddComponent<CanvasGroup>();
            Image edge = HudUiKit.NewImage("Edge", _window, HudUiKit.Load("panel_9slice"), EdgeColor, Image.Type.Sliced);
            HudUiKit.Stretch(edge.rectTransform, -2f);
            Image topLine = HudUiKit.NewImage("TopLine", _window, HudUiKit.Load("divider"), HudUiKit.Gold);
            HudUiKit.Place(topLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 6f), new Vector2(Width - 40f, 14f));

            BuildHeaderRow();
            BuildSlots();
            BuildChoices();
            BuildPreview();
            BuildFooter();
            FillLegend();
            Refresh(null);   // 빈 칸 모양으로 저장
#endif
        }

#if UNITY_EDITOR
        private RectTransform _window => window;
        private readonly List<TMP_Text> _bakedRows = new();

        // 머리말: 눈 아이콘 + "콤보 바인딩" + 금색 선 / 오른쪽 비용 / 아래 안내 + 지금 콤보
        private void BuildHeaderRow()
        {
            RectTransform header = HudUiKit.NewRect("Header", _window);
            HudUiKit.Place(header, new Vector2(0f, 1f), new Vector2(Pad, -36f), new Vector2(Width - Pad * 2f, 40f));
            HudUiKit.BuildHeader(header, "콤보 바인딩", Width - Pad * 2f);

            Image coin = HudUiKit.NewImage("CostIcon", header, HudUiKit.Load("gold_icon"), Color.white);
            coin.preserveAspect = true;
            HudUiKit.Place(coin.rectTransform, new Vector2(1f, 1f), new Vector2(-190f, -6f), new Vector2(26f, 26f));
            costText = HudUiKit.NewText("Cost", header, "", 19f, HudUiKit.Gold, TextAlignmentOptions.MidlineRight);
            HudUiKit.Place(costText.rectTransform, new Vector2(1f, 1f), new Vector2(0f, -4f), new Vector2(180f, 30f));
        }

        // 칸 (최대 길이만큼). 칸 사이는 콤보 UI 처럼 금색 ›
        //  - 칸 아래: 그 칸 원소 효과 / 칸 사이 아래: 두 원소 보너스 → 어떤 효과가 어디서 왔는지 자리로 보임
        private void BuildSlots()
        {
            ComboElementTable table = ComboElementTable.Default;
            int max = table != null ? table.MaxLength : 4;
            int min = table != null ? table.MinLength : 2;
            const float step = 190f;
            float x0 = -(max - 1) * step * 0.5f;
            const float y = -180f;

            for (int i = 0; i < max; i++)
            {
                int index = i;
                float x = x0 + i * step;
                SlotView slot = BuildSlot(new Vector2(x, y), i + 1, i < min, b => Wire(b, RemoveAt, index));
                slot.effect = HudUiKit.NewText("Effect_" + (i + 1), _window, "", 15f, HudUiKit.Value, TextAlignmentOptions.Top);
                slot.effect.richText = true;
                HudUiKit.Place(slot.effect.rectTransform, new Vector2(0.5f, 1f), new Vector2(x, y - 98f), new Vector2(170f, 44f), new Vector2(0.5f, 1f));
                _slots.Add(slot);

                if (i < max - 1)
                {
                    float mid = x + step * 0.5f;
                    TMP_Text arrow = HudUiKit.NewText("Arrow", _window, "›", 44f, HudUiKit.Gold, TextAlignmentOptions.Center);
                    HudUiKit.Place(arrow.rectTransform, new Vector2(0.5f, 1f), new Vector2(mid, y + 4f), new Vector2(40f, 60f), new Vector2(0.5f, 0.5f));

                    TMP_Text pair = HudUiKit.NewText("Pair_" + (i + 1), _window, "", 14f, HudUiKit.Value, TextAlignmentOptions.Top);
                    pair.richText = true;
                    HudUiKit.Place(pair.rectTransform, new Vector2(0.5f, 1f), new Vector2(mid, y - 150f), new Vector2(180f, 84f), new Vector2(0.5f, 1f));
                    _pairs.Add(pair);
                }
            }
        }

        private SlotView BuildSlot(Vector2 pos, int number, bool required, Action<UnityEngine.UI.Button> wire)
        {
            RectTransform root = HudUiKit.NewRect("Slot_" + number, _window);
            HudUiKit.Place(root, new Vector2(0.5f, 1f), pos, new Vector2(112f, 112f), new Vector2(0.5f, 0.5f));

            Image glow = HudUiKit.NewImage("Glow", root, HudUiKit.Load("soft_glow"), Color.clear);
            HudUiKit.Stretch(glow.rectTransform, -34f);
            Image back = HudUiKit.NewImage("Back", root, HudUiKit.Load("slot_circle_bg"), new Color(0.07f, 0.1f, 0.15f, 1f));
            HudUiKit.Stretch(back.rectTransform);
            back.raycastTarget = true;
            Image badge = HudUiKit.NewImage("Badge", root, null, Color.white);
            badge.preserveAspect = true;
            HudUiKit.Stretch(badge.rectTransform, 22f);
            TMP_Text plus = HudUiKit.NewText("Plus", root, "+", 40f, Faint, TextAlignmentOptions.Center);
            HudUiKit.Stretch(plus.rectTransform);
            Image frame = HudUiKit.NewImage("Frame", root, HudUiKit.Load("slot_circle_frame"), Color.white);
            HudUiKit.Stretch(frame.rectTransform, -6f);

            // 칸 번호 (필수 칸은 금색)
            TMP_Text index = HudUiKit.NewText("Index", root, number.ToString(), 15f, required ? HudUiKit.Gold : HudUiKit.Label * 0.7f, TextAlignmentOptions.Center, 2f);
            HudUiKit.Place(index.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 30f), new Vector2(60f, 22f), new Vector2(0.5f, 1f));

            TMP_Text label = HudUiKit.NewText("Label", root, "", 19f, HudUiKit.Label, TextAlignmentOptions.Center);
            HudUiKit.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -8f), new Vector2(150f, 26f), new Vector2(0.5f, 1f));

            AddButton(back, wire);
            return new SlotView { root = root, glow = glow, frame = frame, badge = badge, plus = plus, label = label };
        }

        // 원소 고르기 줄 + 전체 초기화
        private void BuildChoices()
        {
            Divider(-408f);
            TMP_Text title = HudUiKit.NewText("ChoiceTitle", _window, "원소", 16f, HudUiKit.Gold, TextAlignmentOptions.Center, 6f);
            HudUiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -424f), new Vector2(200f, 24f));

            const float step = 112f;
            float x0 = -Choices.Length * step * 0.5f;
            const float y = -480f;
            for (int i = 0; i < Choices.Length; i++)
                BuildChoice(i, new Vector2(x0 + i * step, y));

            RectTransform reset = BuildCircleButton("Reset", new Vector2(x0 + Choices.Length * step, y), Warn * 0.8f, null, "×", "전체 초기화", HudUiKit.Label, b => Wire(b, Clear));
            reset.gameObject.AddComponent<HoverScale>();
        }

        private void BuildChoice(int index, Vector2 pos)
        {
            ElementType element = Choices[index];
            RectTransform root = BuildCircleButton("Choice_" + element, pos, Tint(element, 0.9f), Badge(element), null,
                ElementComboBook.KoreanName(element), Tint(element, 1f), b => Wire(b, AddChoice, index));
            root.gameObject.AddComponent<HoverScale>();
        }

        private RectTransform BuildCircleButton(string name, Vector2 pos, Color frameColor, Sprite icon, string glyph, string caption, Color captionColor, Action<UnityEngine.UI.Button> wire)
        {
            RectTransform root = HudUiKit.NewRect(name, _window);
            HudUiKit.Place(root, new Vector2(0.5f, 1f), pos, new Vector2(72f, 72f), new Vector2(0.5f, 0.5f));

            Image back = HudUiKit.NewImage("Back", root, HudUiKit.Load("slot_circle_bg"), new Color(0.07f, 0.1f, 0.15f, 1f));
            HudUiKit.Stretch(back.rectTransform);
            back.raycastTarget = true;
            if (icon != null)
            {
                Image image = HudUiKit.NewImage("Icon", root, icon, Color.white);
                image.preserveAspect = true;
                HudUiKit.Stretch(image.rectTransform, 12f);
            }
            if (glyph != null)
            {
                TMP_Text text = HudUiKit.NewText("Glyph", root, glyph, 30f, HudUiKit.Title, TextAlignmentOptions.Center);
                HudUiKit.Stretch(text.rectTransform);
            }
            Image frame = HudUiKit.NewImage("Frame", root, HudUiKit.Load("slot_circle_frame"), frameColor);
            HudUiKit.Stretch(frame.rectTransform, -4f);

            TMP_Text label = HudUiKit.NewText("Label", root, caption, 16f, captionColor, TextAlignmentOptions.Center);
            HudUiKit.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -8f), new Vector2(110f, 24f), new Vector2(0.5f, 1f));

            AddButton(back, wire);
            return root;
        }

        // 바뀐 효과 한 줄 + 금색 선 + 아래 안내(항상): 왼쪽 상성 오각형 / 가운데 원소 효과 / 오른쪽 합계
        private void BuildPreview()
        {
            changeText = HudUiKit.NewText("Change", _window, "", 18f, HudUiKit.Value, TextAlignmentOptions.Center);
            changeText.richText = true;
            HudUiKit.Place(changeText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -560f), new Vector2(Width - Pad * 2f, 52f));

            Divider(-620f);

            const float top = -640f;
            ComboElementTable table = ComboElementTable.Default;

            // 왼쪽 위: 상성 오각형 (화살표 = 이김)
            SectionTitle("상성", new Vector2(Pad, top), 80f);
            TMP_Text arrowNote = HudUiKit.NewText("BeatsNote", _window, "화살표 → 이김", 14f, HudUiKit.Label * 0.8f, TextAlignmentOptions.MidlineLeft);
            HudUiKit.Place(arrowNote.rectTransform, new Vector2(0f, 1f), new Vector2(Pad + 52f, top), new Vector2(150f, 24f));
            BuildBeatsWheel(table, new Vector2(Pad + 160f - Width * 0.5f, -770f));

            // 오른쪽 위: 원소마다 효과 (배지 + 이름 + 값)
            const float midX = Pad + 340f;
            SectionTitle("원소 효과", new Vector2(midX, top), 200f);
            _bakedRows.Clear();
            for (int i = 0; i < Choices.Length; i++)
                BuildElementRow(table, Choices[i], new Vector2(midX, -680f - i * 37f));
            elementRows = _bakedRows.ToArray();

            // 아래: 보너스 스탯 제목 + 규칙 한 줄씩
            SectionTitle("보너스 스탯", new Vector2(Pad, -858f), 110f);
            TMP_Text bonusNote = HudUiKit.NewText("BonusNote", _window, "% 는 그 원소 효과 기준", 14f, HudUiKit.Label * 0.8f, TextAlignmentOptions.MidlineLeft);
            HudUiKit.Place(bonusNote.rectTransform, new Vector2(0f, 1f), new Vector2(Pad + 112f, -858f), new Vector2(240f, 24f));
            TMP_Text bonus = HudUiKit.NewText("BonusRules", _window, BonusText(table), 15f, HudUiKit.Label, TextAlignmentOptions.TopLeft);
            bonus.richText = true;
            bonus.lineSpacing = 6f;
            HudUiKit.Place(bonus.rectTransform, new Vector2(0f, 1f), new Vector2(Pad + 14f, -884f), new Vector2(490f, 90f));
            bonusRules = bonus;

            // 오른쪽: 합계 + 길이 배율
            const float rightX = Pad + 720f;
            float rightWidth = Width - rightX - Pad;
            SectionTitle("합계", new Vector2(rightX, top), 80f);
            totalNoteText = HudUiKit.NewText("TotalNote", _window, "", 15f, HudUiKit.Label, TextAlignmentOptions.MidlineRight);
            totalNoteText.richText = true;
            HudUiKit.Place(totalNoteText.rectTransform, new Vector2(0f, 1f), new Vector2(rightX + 50f, top), new Vector2(rightWidth - 50f, 24f));
            totalText = HudUiKit.NewText("Total", _window, "", 20f, HudUiKit.Value, TextAlignmentOptions.TopLeft);
            totalText.richText = true;
            HudUiKit.Place(totalText.rectTransform, new Vector2(0f, 1f), new Vector2(rightX, -676f), new Vector2(rightWidth, 150f));

            TMP_Text scale = HudUiKit.NewText("LengthScale", _window, ScaleText(table), 15f, HudUiKit.Label, TextAlignmentOptions.TopLeft);
            scale.richText = true;
            scale.lineSpacing = 8f;
            HudUiKit.Place(scale.rectTransform, new Vector2(0f, 1f), new Vector2(rightX, -840f), new Vector2(rightWidth, 60f));
            lengthScale = scale;
        }

        private void SectionTitle(string text, Vector2 pos, float width)
        {
            TMP_Text title = HudUiKit.NewText("Title_" + text, _window, text, 16f, HudUiKit.Gold, TextAlignmentOptions.MidlineLeft, 6f);
            HudUiKit.Place(title.rectTransform, new Vector2(0f, 1f), pos, new Vector2(width, 24f));
        }

        private void Divider(float y)
        {
            Image line = HudUiKit.NewImage("Line", _window, HudUiKit.Load("divider"), new Color(1f, 1f, 1f, 0.4f));
            HudUiKit.Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(Pad, y), new Vector2(Width - Pad * 2f, 14f));
        }

        // 원소 한 줄: 배지 + 원소 이름(색) + "공격력 +10%"
        private void BuildElementRow(ComboElementTable table, ElementType element, Vector2 pos)
        {
            Image badge = HudUiKit.NewImage("RowBadge_" + element, _window, Badge(element), Color.white);
            badge.preserveAspect = true;
            HudUiKit.Place(badge.rectTransform, new Vector2(0f, 1f), pos, new Vector2(32f, 32f));

            TMP_Text text = HudUiKit.NewText("Row_" + element, _window, ElementRowText(table, element), 18f, HudUiKit.Value, TextAlignmentOptions.MidlineLeft);
            text.richText = true;
            HudUiKit.Place(text.rectTransform, new Vector2(0f, 1f), pos + new Vector2(44f, 0f), new Vector2(330f, 32f));
            _bakedRows.Add(text);
        }

        // 상성 오각형: 이기는 원소 → 지는 원소 화살표. 원소 순서는 "상성" 보너스 줄에서 읽음 (물 맨 위, 시계 방향)
        private void BuildBeatsWheel(ComboElementTable table, Vector2 center)
        {
            Dictionary<ElementType, ElementType> beats = BeatsOf(table);   // 이기는 쪽 → 지는 쪽
            var order = new List<ElementType>();
            ElementType at = beats.ContainsKey(ElementType.Water) ? ElementType.Water : Choices[0];
            while (!order.Contains(at))
            {
                order.Add(at);
                if (!beats.TryGetValue(at, out at))
                    break;
            }
            foreach (ElementType element in Choices)
            {
                if (!order.Contains(element))
                    order.Add(element);
            }

            RectTransform wheel = HudUiKit.NewRect("BeatsWheel", _window);
            HudUiKit.Place(wheel, new Vector2(0.5f, 1f), center, Vector2.zero, new Vector2(0.5f, 0.5f));

            const float radius = 76f;
            const float node = 48f;
            var points = new Dictionary<ElementType, Vector2>();
            for (int i = 0; i < order.Count; i++)
            {
                float angle = Mathf.PI * 0.5f - i * Mathf.PI * 2f / order.Count;   // 맨 위부터 시계 방향
                points[order[i]] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            var arrowColor = new Color(0.55f, 0.82f, 1f, 0.9f);
            foreach (KeyValuePair<ElementType, ElementType> pair in beats)
            {
                if (points.TryGetValue(pair.Key, out Vector2 from) && points.TryGetValue(pair.Value, out Vector2 to))
                    Arrow(wheel, from, to, node * 0.5f + 5f, arrowColor);
            }

            foreach (KeyValuePair<ElementType, Vector2> point in points)
            {
                RectTransform root = HudUiKit.NewRect("Node_" + point.Key, wheel);
                HudUiKit.Place(root, new Vector2(0.5f, 0.5f), point.Value, new Vector2(node, node), new Vector2(0.5f, 0.5f));
                Image back = HudUiKit.NewImage("Back", root, HudUiKit.Load("slot_circle_bg"), new Color(0.07f, 0.1f, 0.15f, 1f));
                HudUiKit.Stretch(back.rectTransform);
                Image badge = HudUiKit.NewImage("Badge", root, Badge(point.Key), Color.white);
                badge.preserveAspect = true;
                HudUiKit.Stretch(badge.rectTransform, 6f);
                Image frame = HudUiKit.NewImage("Frame", root, HudUiKit.Load("slot_circle_frame"), Tint(point.Key, 1f));
                HudUiKit.Stretch(frame.rectTransform, -3f);
            }
        }

        // 선 + 끝에 꺾쇠 두 개
        private static void Arrow(RectTransform parent, Vector2 from, Vector2 to, float inset, Color color)
        {
            Vector2 dir = (to - from).normalized;
            Vector2 start = from + dir * inset;
            Vector2 tip = to - dir * inset;
            Line(parent, start, tip, 3f, color);
            foreach (float side in new[] { 30f, -30f })
            {
                Vector2 back = (Vector2)(Quaternion.Euler(0f, 0f, side) * -dir);
                Line(parent, tip + dir * 1f, tip + back * 12f, 3f, color);
            }
        }

        private static void Line(RectTransform parent, Vector2 a, Vector2 b, float thickness, Color color)
        {
            Image line = HudUiKit.NewImage("Line", parent, null, color);
            RectTransform rect = line.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = a;
            rect.sizeDelta = new Vector2((b - a).magnitude, thickness);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        }
#endif

        // "상성" 보너스 = 뒤 원소가 앞 원소를 이김 → 이기는 쪽(second) → 지는 쪽(first)
        private static Dictionary<ElementType, ElementType> BeatsOf(ComboElementTable table)
        {
            var beats = new Dictionary<ElementType, ElementType>();
            if (table == null)
                return beats;
            foreach (ComboElementTable.PairBonus pair in table.Pairs)
            {
                if (pair.label == "상성")
                    beats[pair.second] = pair.first;
            }
            return beats;
        }

        // 보너스 규칙 한 줄씩 (이름 | 언제 | 무엇). 예시 원소는 표의 실제 상성 한 쌍에서 고름.
        // 수치는 ComboBindMenu 임시 규칙(증폭 +100%, 연계 뒤 +50%, 상성 앞 -50%/뒤 +100%, 역상성 앞 +50%/뒤 -50%)과 맞춤
        private static string BonusText(ComboElementTable table)
        {
            ElementType loser = ElementType.Fire, winner = ElementType.Water;
            foreach (KeyValuePair<ElementType, ElementType> pair in BeatsOf(table))
            {
                winner = pair.Key;
                loser = pair.Value;
                break;
            }
            const string gold = "#D6B878";
            string dot = $"  <color={GrayHex}>·</color>  ";
            string Up(string v) => $"<color=#8CF29A>{v}</color>";
            string Down(string v) => $"<color=#FF8C73>{v}</color>";
            string Row(string name, string when, string what) => $"<color={gold}>{name}</color><pos=64>{when}{dot}{what}";

            return string.Join("\n",
                Row("증폭", $"같은 원소 연속 ({Colored(loser)}›{Colored(loser)})", $"그 원소 효과 {Up("+100%")}"),
                Row("연계", $"그 외 순서 <color={GrayHex}>(증폭·상성 아님)</color>", $"뒤 원소 효과 {Up("+50%")}"),
                Row("상성", $"지는 원소 › 이기는 원소 ({Colored(loser)}›{Colored(winner)})", $"앞 {Down("−50%")}  뒤 {Up("+100%")}"),
                Row("역상성", $"이기는 원소 › 지는 원소 ({Colored(winner)}›{Colored(loser)})", $"앞 {Up("+50%")}  뒤 {Down("−50%")}"));
        }

        private static string ScaleText(ComboElementTable table)
        {
            if (table == null)
                return "";
            var scales = new List<string>();
            for (int length = table.MinLength; length <= table.MaxLength; length++)
                scales.Add($"{length}칸 <color=#D6B878>×{table.ScaleFor(length):0.##}</color>");
            return $"<color=#D6B878>길이 배율</color> <color={GrayHex}>(합계에 곱함)</color>\n{string.Join("   ", scales)}";
        }

        // 원소 효과 한 줄: "불   공격력 +10%"
        private static string ElementRowText(ComboElementTable table, ElementType element)
        {
            ComboElementTable.ElementEffect effect = table != null ? table.EffectOf(element) : null;
            var stats = new List<string>();
            if (effect != null)
            {
                foreach (Mod mod in effect.mods)
                    stats.Add($"{ComboEffect.StatName(mod.stat)} <color=#D6B878>{ComboEffect.Amount(mod)}</color>");
            }
            return $"<nobr>{Colored(element)}   {string.Join($"  <color={GrayHex}>·</color>  ", stats)}</nobr>";
        }

#if UNITY_EDITOR
        // 아래: 안내/오류 + 닫기 · 저장하기 (픽 화면 버튼과 같은 그림)
        private void BuildFooter()
        {
            messageText = HudUiKit.NewText("Message", _window, "", 17f, Warn, TextAlignmentOptions.MidlineLeft);
            HudUiKit.Place(messageText.rectTransform, new Vector2(0f, 0f), new Vector2(Pad, 34f), new Vector2(480f, 30f));

            Image close = TextButton("Close", "닫기  ESC", "button_normal", new Vector2(-Pad - 270f, 26f), new Vector2(170f, 54f), 20f, b => Wire(b, Close));
            close.color = new Color(1f, 1f, 1f, 0.7f);
            saveButton = TextButton("Save", "저장하기", "button_hover", new Vector2(-Pad, 26f), new Vector2(250f, 54f), 24f, b => Wire(b, OnSaveClicked));
        }

        private Image TextButton(string name, string label, string sprite, Vector2 pos, Vector2 size, float fontSize, Action<UnityEngine.UI.Button> wire)
        {
            Image image = HudUiKit.NewImage(name, _window, HudUiKit.Load(sprite), Color.white);
            HudUiKit.Place(image.rectTransform, new Vector2(1f, 0f), pos, size);
            image.raycastTarget = true;
            TMP_Text text = HudUiKit.NewText("Label", image.transform, label, fontSize, HudUiKit.Title, TextAlignmentOptions.Center, 4f);
            HudUiKit.Stretch(text.rectTransform);
            AddButton(image, wire);
            image.gameObject.AddComponent<HoverScale>();
            return image;
        }
#endif

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                Close();
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        // ---------------------------------------------------------------- 넣기 / 빼기

        public bool Add(ElementType element)
        {
            if (_sequence.Count >= _slots.Count)
            {
                Shake(_slots[_slots.Count - 1].root);
                ShowMessage($"최대 {_slots.Count}칸까지입니다.", Warn);
                return false;
            }

            ComboEffect before = ComboLoadout.Evaluate(_sequence);
            _sequence.Add(element);
            Refresh(Describe(true, element, before));
            Pop(_slots[_sequence.Count - 1].root);
            return true;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _sequence.Count)
                return;

            ComboEffect before = ComboLoadout.Evaluate(_sequence);
            ElementType element = _sequence[index];
            _sequence.RemoveAt(index);
            Refresh(Describe(false, element, before));
        }

        public void RemoveLast() => RemoveAt(_sequence.Count - 1);

        // 칸 전부 비우기
        public void Clear()
        {
            if (_sequence.Count == 0)
                return;
            _sequence.Clear();
            Refresh($"전체 초기화  <color={GrayHex}>·  아래 원소를 눌러 다시 채우세요.</color>");
        }

        public bool Save()
        {
            if (!ComboLoadout.TryBind(_sequence, out string reason))
            {
                ShowMessage(reason, Warn);
                Shake(saveButton.rectTransform);
                return false;
            }

            _saved = true;
            Refresh(null);
            ShowMessage("바인딩 완료!", Good);
            DOVirtual.DelayedCall(0.5f, Close).SetUpdate(true);
            return true;
        }

        public void Close()
        {
            if (_closing)
                return;
            _closing = true;
            Time.timeScale = _timeScale > 0f ? _timeScale : 1f;
            Destroy(gameObject);
            _onClosed?.Invoke();
            Closed?.Invoke();
        }

        // ---------------------------------------------------------------- 화면

        private void Refresh(string change)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                SlotView slot = _slots[i];
                bool filled = i < _sequence.Count;
                slot.badge.enabled = filled;
                slot.plus.enabled = !filled && i == _sequence.Count;   // 다음에 들어갈 칸만 +
                slot.glow.color = filled ? Tint(_sequence[i], 0.45f) : Color.clear;
                slot.frame.color = filled ? Tint(_sequence[i], 1f) : Faint;
                slot.label.text = filled ? ElementComboBook.KoreanName(_sequence[i]) : "비어 있음";
                slot.label.color = filled ? Tint(_sequence[i], 1f) : HudUiKit.Label * 0.6f;
                if (filled)
                    slot.badge.sprite = Badge(_sequence[i]);
            }

            if (change != null)
                changeText.text = change;
            else if (_sequence.Count == 0)
                changeText.text = $"<color={GrayHex}>아래 원소를 눌러 칸을 채우세요. 채운 칸을 누르면 뺍니다.</color>";

            FillPreview();
            costText.text = ComboLoadout.Gate.CostLabel(_sequence);

            bool ok = ComboLoadout.CanBind(_sequence, out string reason);
            saveButton.color = ok ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            if (!_saved)
                ShowMessage(ok ? "" : reason, Warn);
        }

        private void ShowMessage(string text, Color color)
        {
            messageText.color = color;
            messageText.text = text;
        }

        // "물 추가 · 원소 효과 마법력 +10%" + 바뀐 능력치 "공격력 +10% → +5%"
        private string Describe(bool added, ElementType element, ComboEffect before)
        {
            ComboEffect after = ComboLoadout.Evaluate(_sequence);
            ComboElementTable table = ComboElementTable.Default;
            ComboElementTable.ElementEffect own = table != null ? table.EffectOf(element) : null;

            var text = new StringBuilder();
            text.Append($"{Colored(element)} {(added ? "추가" : "취소")}");
            if (own != null && own.mods.Length > 0)
            {
                text.Append($"  <color={GrayHex}>·</color>  원소 효과 ");
                for (int i = 0; i < own.mods.Length; i++)
                    text.Append(i > 0 ? ", " : "").Append(ComboEffect.Describe(own.mods[i]));
            }

            var diffs = new List<string>();
            foreach (Mod mod in Union(before.Total, after.Total))
            {
                float a = ValueOf(before.Total, mod), b = ValueOf(after.Total, mod);
                if (Mathf.Approximately(a, b))
                    continue;
                string color = b > a ? "#8CF29A" : "#FF8C73";
                diffs.Add($"{ComboEffect.StatName(mod.stat)} <color={GrayHex}>{Amount(mod, a)} →</color> <color={color}>{Amount(mod, b)}</color>");
            }
            if (diffs.Count > 0)
                text.Append('\n').Append(string.Join("      ", diffs));
            return text.ToString();
        }

        // 칸 아래: 원소 효과 / 칸 사이 아래: 보너스 (+ 초록, - 빨강) / 합계: 스탯마다 한 줄 (길이 배율까지)
        private void FillPreview()
        {
            ComboElementTable table = ComboElementTable.Default;
            for (int i = 0; i < _slots.Count; i++)
            {
                ComboElementTable.ElementEffect basic = i < _sequence.Count && table != null ? table.EffectOf(_sequence[i]) : null;
                _slots[i].effect.text = basic != null ? ModLines(basic.mods, false) : "";
            }

            for (int i = 0; i < _pairs.Count; i++)
            {
                ComboElementTable.PairBonus pair = i + 1 < _sequence.Count && table != null ? table.PairOf(_sequence[i], _sequence[i + 1]) : null;
                _pairs[i].text = pair != null && pair.mods.Length > 0
                    ? $"<color=#D6B878>{pair.label}</color>\n{ModLines(pair.mods, true)}"
                    : "";
            }

            if (_sequence.Count == 0)
            {
                totalText.text = $"<color={GrayHex}>—</color>";
                totalNoteText.text = "";
                return;
            }

            ComboEffect effect = ComboLoadout.Evaluate(_sequence);
            var total = new StringBuilder();
            foreach (Mod mod in effect.Total)
            {
                if (total.Length > 0)
                    total.Append('\n');
                string color = mod.value >= 0f ? "#D6B878" : "#FF8C73";
                total.Append($"{ComboEffect.StatName(mod.stat)}  <color={color}>{ComboEffect.Amount(mod)}</color>");
            }
            totalText.text = total.Length > 0 ? total.ToString() : "효과 없음";
            totalNoteText.text = $"<color=#D6B878>×{effect.Scale:0.##}</color> ({_sequence.Count}칸)  ·  {(table != null ? table.Duration : 10f):0}초";
        }

        // "공격력 +10%" 한 줄씩. signColor 면 + 초록 / - 빨강
        private static string ModLines(Mod[] mods, bool signColor)
        {
            var lines = new List<string>();
            foreach (Mod mod in mods)
            {
                string amount = ComboEffect.Amount(mod);
                if (signColor)
                    amount = $"<color={(mod.value >= 0f ? "#8CF29A" : "#FF8C73")}>{amount}</color>";
                lines.Add($"{ComboEffect.StatName(mod.stat)} {amount}");
            }
            return string.Join("\n", lines);
        }

        private static string Colored(ElementType element) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(Tint(element, 1f))}>{ElementComboBook.KoreanName(element)}</color>";

        // ---------------------------------------------------------------- 도우미

#if UNITY_EDITOR
        // 버튼을 만들고, 클릭은 인스펙터에 보이는 영구 연결(Persistent listener)로
        private static void AddButton(Image target, Action<UnityEngine.UI.Button> wire)
        {
            var button = target.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = target;
            button.transition = Selectable.Transition.None;
            wire?.Invoke(button);
        }

        private static void Wire(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action) =>
            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, action);

        private static void Wire(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction<int> action, int argument) =>
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(button.onClick, action, argument);
#endif

        // 원소 색·배지는 프리팹에 연결된 팔레트에서 (안 넣었으면 기본 팔레트)
        private static ElementPalette Palette => _activePalette != null ? _activePalette : ElementPalette.Default;

        private static Sprite Badge(ElementType element) =>
            Palette != null ? Palette.BadgeOf(element) : null;

        private static Color Tint(ElementType element, float alpha)
        {
            Color color = Palette != null ? Palette.ColorOf(element) : Color.white;
            return new Color(color.r, color.g, color.b, alpha);
        }

        private static IEnumerable<Mod> Union(IReadOnlyList<Mod> a, IReadOnlyList<Mod> b)
        {
            var seen = new List<Mod>();
            foreach (IReadOnlyList<Mod> list in new[] { a, b })
            {
                foreach (Mod mod in list)
                {
                    if (!seen.Exists(m => m.stat == mod.stat && m.type == mod.type))
                        seen.Add(mod);
                }
            }
            return seen;
        }

        private static float ValueOf(IReadOnlyList<Mod> list, Mod key)
        {
            foreach (Mod mod in list)
            {
                if (mod.stat == key.stat && mod.type == key.type)
                    return mod.value;
            }
            return 0f;
        }

        private static string Amount(Mod key, float value) => ComboEffect.Amount(new Mod(key.stat, key.type, value));

        private static void Pop(RectTransform rect)
        {
            rect.DOKill(true);
            rect.DOPunchScale(Vector3.one * 0.15f, 0.2f, 8).SetUpdate(true);
        }

        private static void Shake(RectTransform rect)
        {
            rect.DOKill(true);
            rect.DOShakeAnchorPos(0.25f, new Vector2(8f, 0f), 18).SetUpdate(true);
        }
    }
}
