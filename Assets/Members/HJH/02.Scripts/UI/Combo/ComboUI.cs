using System;
using System.Collections.Generic;
using System.Text;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Element;
using Assets.Members.HJH._02.Scripts.Hud;
using DG.Tweening;
using Members.JJH._02_Scripts.ElementsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 콤보 UI 전부 여기 한 파일임. 예전에 7개로 쪼개져 있던 거 합친 거여, 여기서 건들면 됨
    //  - 아래 입력 칸: 스킬 쓸 때마다 속성/키가 왼쪽부터 쌓임 + 다음에 뭐 누르면 콤보인지(NEXT) + 안내 문구
    //  - 콤보 완성: 가운데 COMBO! 문구, 쓰인 칸들 튕기고 밑에 콤보 이름, 맨 아래 스킬바에서 그 콤보에 쓴 스킬 칸도 튕김
    //  - 남은 시간 게이지: 이거 다 떨어지면 콤보 끊김
    //  - 피니시 스톡: 완성한 콤보 모아두는 다이아 (R 누르면 한 번에 터짐)
    //  - 콤보 리스트: Tab 누르면 열리고 닫힘
    // 값은 전부 PlayerHudSource 에서 꺼내 씀. 게임 로직 코드는 몰라도 됨
    // 콤보 순간 화면 멈춤/흔들림은 여기 말고 ElementComboChain 에 있음 (UI 지워도 손맛은 안 없어지게)
    public class ComboUI : MonoBehaviour
    {
        // 입력 칸 하나에 들어가는 것들
        [Serializable]
        public struct Cell
        {
            public RectTransform root;
            public CanvasGroup group;
            public Image background;
            public Image ring;      // 테두리. NEXT 칸 깜빡일 때랑 콤보 완성 때 금색으로 번쩍
            public Image badge;     // 속성 아이콘
            public TMP_Text key;
            public TMP_Text element;
        }

        [Header("아래 입력 칸 왼쪽부터 순서대로임")]
        [SerializeField] private Cell[] cells;
        [SerializeField] private Cell nextCell;
        [SerializeField] private TMP_Text hintText;

        [Header("콤보 완성 때 칸 밑에 뜨는 이름")]
        [SerializeField] private RectTransform bracket;
        [SerializeField] private CanvasGroup bracketGroup;
        [SerializeField] private TMP_Text bracketLabel;

        [Header("가운데 문구")]
        [SerializeField] private RectTransform banner;
        [SerializeField] private CanvasGroup bannerGroup;
        [SerializeField] private Image bannerBar;
        [SerializeField] private TMP_Text bannerHeader;   // COMBO! / FINISH
        [SerializeField] private TMP_Text bannerTitle;    // 콤보 이름
        [SerializeField] private TMP_Text bannerDetail;   // 누른 키 순서
        [SerializeField] private Image screenFlash;       // 피니시 때 화면 번쩍 (없어도 됨)

        [Header("남은 시간 게이지")]
        [SerializeField] private CanvasGroup gaugeGroup;
        [SerializeField] private Image gaugeFill;

        [Header("피니시 스톡")]
        [SerializeField] private Image[] stockPips;
        [SerializeField] private TMP_Text[] stockNames;
        [SerializeField] private TMP_Text finisherKey;
        [SerializeField] private TMP_Text readyLabel;

        [Header("콤보 리스트 (Tab)")]
        [SerializeField] private RectTransform listRowTemplate;
        [SerializeField] private RectTransform listRowParent;
        [SerializeField] private GameObject listBody;
        [SerializeField] private TMP_Text listTitle;
        [SerializeField] private TMP_Text listClosedTab;

        [Header("맨 아래 스킬바 칸들 (콤보 완성되면 그 콤보에 쓴 스킬 칸이 톡 튐)")]
        [SerializeField] private RectTransform qIcon;       // Q 칸
        [SerializeField] private RectTransform eIcon;       // E 칸
        [SerializeField] private RectTransform rmbIcon;     // 우클릭 칸 (캐릭터 전용 스킬)
        [SerializeField] private RectTransform rIcon;       // R 칸 (궁극기)
        [SerializeField] private RectTransform spaceIcon;   // Space 칸 (대쉬)

        [Header("색")]
        [SerializeField] private Color gold = new(1f, 0.83f, 0.3f);
        [SerializeField] private Color emptyCellColor = new(1f, 1f, 1f, 0.22f);
        [SerializeField] private Color comboBarColor = new(0.42f, 0.28f, 0.03f, 0.92f);
        [SerializeField] private Color finisherBarColor = new(0.55f, 0.07f, 0.1f, 0.95f);
        [SerializeField] private Color pipOn = new(1f, 0.83f, 0.3f);
        [SerializeField] private Color pipOff = new(1f, 1f, 1f, 0.18f);
        [SerializeField] private Color listRowColor = new(0.55f, 0.48f, 0.32f, 0.75f);

        // 콤보 리스트 한 줄
        private class ListRow
        {
            public ElementComboBook.Recipe combo;
            public Image background;
            public Image[] steps;
            public TMP_Text[] stepKeys;
            public int progress;
        }

        private PlayerHudSource _hud;
        private Vector2 _bannerHome;
        private bool _breaking;
        private bool _listOpen;
        private readonly List<ListRow> _rows = new();
        private readonly List<ElementType> _elements = new();

        private void Start()
        {
            _hud = PlayerHudSource.Instance;
            if (_hud == null)
                return;

            if (banner != null)
                _bannerHome = banner.anchoredPosition;
            if (bannerGroup != null)
                bannerGroup.alpha = 0f;
            if (screenFlash != null)
                screenFlash.enabled = false;
            if (bracket != null)
                bracket.gameObject.SetActive(false);

            BuildList();
            SetListOpen(false);

            _hud.ComboInputAdded += OnInputAdded;
            _hud.ComboChanged += Redraw;
            _hud.ComboLanded += OnComboLanded;
            _hud.ComboBroken += OnComboBroken;
            _hud.FinisherReleased += OnFinisher;
            _hud.LoadoutChanged += Redraw;   // 스킬 바꾸거나 캐릭터 바꾸면 칸에 적힌 키도 바뀌어야 해서
            Redraw();
        }

        private void OnDestroy()
        {
            if (_hud == null)
                return;

            _hud.ComboInputAdded -= OnInputAdded;
            _hud.ComboChanged -= Redraw;
            _hud.ComboLanded -= OnComboLanded;
            _hud.ComboBroken -= OnComboBroken;
            _hud.FinisherReleased -= OnFinisher;
            _hud.LoadoutChanged -= Redraw;
        }

        private void Update()
        {
            if (_hud == null)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
                SetListOpen(!_listOpen);

            UpdateGauge();

            // NEXT 칸 테두리는 계속 깜빡이게
            if (nextCell.ring != null && nextCell.ring.enabled)
                nextCell.ring.color = WithAlpha(nextCell.ring.color, 0.3f + 0.65f * Pulse(6f));

            // 스톡 하나라도 있으면 READY, 꽉 차면 MAX (더 빨리 깜빡)
            if (readyLabel != null)
            {
                int stock = _hud.ComboStock.Count;
                bool full = stock >= _hud.MaxComboStock;
                readyLabel.enabled = stock > 0;
                readyLabel.text = full ? "MAX" : "READY";
                readyLabel.alpha = 0.45f + 0.55f * Pulse(full ? 10f : 4f);
            }
        }

        // ------------------------------------------------------------ 평소 화면 (칸, NEXT, 스톡, 리스트)

        // 지금 상태 그대로 다시 그림. 콤보 상태가 바뀔 때마다 불림
        private void Redraw()
        {
            if (_breaking)
                return;

            IReadOnlyList<ElementComboChain.ComboInput> inputs = _hud.ComboInputs;
            _elements.Clear();
            foreach (ElementComboChain.ComboInput input in inputs)
                _elements.Add(input.Element);

            for (int i = 0; i < cells.Length; i++)
            {
                if (i < inputs.Count)
                    DrawCell(cells[i], inputs[i].Element, _hud.KeyLabel(inputs[i].Slot), 1f);
                else
                    ClearCell(cells[i]);
            }

            bool onRoute = _hud.TryGetNextCombo(out ElementType next, out ElementComboBook.Recipe route, out int progress);
            if (onRoute)
            {
                DrawCell(nextCell, next, _hud.KeyFor(next), 0.5f);
                nextCell.background.color = emptyCellColor;
                if (nextCell.ring != null)
                {
                    nextCell.ring.enabled = true;
                    nextCell.ring.color = _hud.ColorOf(next);
                }
            }
            else
            {
                ClearCell(nextCell);
            }

            if (hintText != null)
            {
                if (onRoute)
                    hintText.text = $"<color=#{Hex(gold)}>{route.Name}</color>  {progress}/{route.Length}   다음 {KeyAndElement(next)}";
                else
                    hintText.text = inputs.Count == 0
                        ? "<color=#8A8A96>스킬을 이어 써서 콤보</color>"
                        : "<color=#8A8A96>이어지는 콤보 없음</color>";
            }

            DrawStock();
            RefreshList();
        }

        private void DrawCell(Cell cell, ElementType element, string key, float alpha)
        {
            Color color = _hud.ColorOf(element);
            cell.background.color = new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f, 1f);

            Sprite badge = _hud.Palette != null ? _hud.Palette.BadgeOf(element) : null;
            if (cell.badge != null)
            {
                cell.badge.enabled = badge != null;
                cell.badge.sprite = badge;
                cell.badge.color = WithAlpha(Color.white, alpha);
            }

            if (cell.key != null)
            {
                cell.key.text = key;
                cell.key.alpha = alpha;
            }

            if (cell.element != null)
            {
                cell.element.text = _hud.ElementName(element);
                cell.element.color = WithAlpha(color, alpha);
            }
        }

        private void ClearCell(Cell cell)
        {
            cell.background.color = emptyCellColor;
            if (cell.badge != null)
                cell.badge.enabled = false;
            if (cell.ring != null && !DOTween.IsTweening(cell.ring))
                cell.ring.enabled = false;
            if (cell.key != null)
                cell.key.text = string.Empty;
            if (cell.element != null)
                cell.element.text = string.Empty;
        }

        // 남은 시간 비율만큼 게이지 채움. 얼마 안 남으면 깜빡깜빡
        private void UpdateGauge()
        {
            if (gaugeGroup == null || gaugeFill == null)
                return;

            bool running = _hud.ComboInputs.Count > 0 && !_breaking && _hud.ComboWindow > 0f;
            if (!running)
            {
                gaugeGroup.alpha = 0f;
                return;
            }

            float left = _hud.ComboTimeLeft;
            gaugeFill.fillAmount = Mathf.Clamp01(left / _hud.ComboWindow);
            gaugeGroup.alpha = left < 0.8f ? 0.55f + 0.45f * Pulse(16f) : 1f;
        }

        private void DrawStock()
        {
            IReadOnlyList<ElementComboBook.Recipe> stock = _hud.ComboStock;

            if (stockPips != null)
            {
                for (int i = 0; i < stockPips.Length; i++)
                    stockPips[i].color = i < stock.Count ? pipOn : pipOff;
            }

            if (stockNames != null)
            {
                for (int i = 0; i < stockNames.Length; i++)
                    stockNames[i].text = i < stock.Count ? stock[i].Name : string.Empty;
            }

            if (finisherKey != null)
                finisherKey.text = $"[{_hud.FinisherKey}] FINISH";
        }

        // ------------------------------------------------------------ 이벤트 (입력, 완성, 끊김, 피니시)

        // 방금 쓴 스킬 칸이 쾅 하고 들어오는 느낌
        private void OnInputAdded()
        {
            if (_breaking)
            {
                _breaking = false;
                ResetCellAlpha();
            }

            int last = _hud.ComboInputs.Count - 1;
            if (last >= 0 && last < cells.Length)
                Bounce(cells[last].root, 0.35f);
        }

        private void OnComboLanded(ElementComboBook.Recipe combo)
        {
            ShowBanner("COMBO!", combo.Name, KeySequence(combo.Sequence), comboBarColor);

            // 콤보는 항상 마지막 입력들로 완성됨
            // 예) 입력이 4개고 콤보 길이가 2면 -> 2번, 3번 칸이 그 콤보
            IReadOnlyList<ElementComboChain.ComboInput> inputs = _hud.ComboInputs;
            int first = Mathf.Max(0, inputs.Count - combo.Length);
            for (int i = first; i < inputs.Count; i++)
            {
                if (i < cells.Length)
                {
                    Bounce(cells[i].root, 0.25f);
                    FlashRing(cells[i].ring);
                }

                Bounce(IconOf(inputs[i].Slot), 0.25f);
            }

            ShowBracket(first, inputs.Count - 1, combo.Name);
            FlashListRow(combo);
        }

        private void OnFinisher(IReadOnlyList<ElementComboBook.Recipe> combos)
        {
            var names = new StringBuilder();
            foreach (ElementComboBook.Recipe combo in combos)
            {
                if (names.Length > 0)
                    names.Append("  +  ");
                names.Append(combo.Name);
            }

            ShowBanner($"FINISH  x{combos.Count}", "피니시", names.ToString(), finisherBarColor);

            if (screenFlash != null)
            {
                screenFlash.DOKill();
                screenFlash.enabled = true;
                screenFlash.color = WithAlpha(screenFlash.color, 0.35f);
                screenFlash.DOFade(0f, 0.25f).SetUpdate(true).OnComplete(() => screenFlash.enabled = false);
            }
        }

        // 시간 다 돼서 콤보 끊기면 칸들이 스르륵 사라졌다가 빈 칸으로 돌아옴
        private void OnComboBroken()
        {
            _breaking = true;
            foreach (Cell cell in cells)
            {
                if (cell.group == null)
                    continue;
                cell.group.DOKill();
                cell.group.DOFade(0f, 0.3f).SetUpdate(true);
            }

            DOVirtual.DelayedCall(0.3f, () =>
            {
                if (!_breaking)
                    return;
                _breaking = false;
                ResetCellAlpha();
                Redraw();
            }).SetUpdate(true).SetLink(gameObject);
        }

        private void ResetCellAlpha()
        {
            foreach (Cell cell in cells)
            {
                if (cell.group == null)
                    continue;
                cell.group.DOKill();
                cell.group.alpha = 1f;
            }
        }

        // ------------------------------------------------------------ 연출 도우미

        // 크게 나왔다가 제자리 -> 잠깐 멈췄다가 오른쪽으로 빠지며 사라짐, 수치는 조절 알아서 하시고요잉
        // SetUpdate(true): 콤보 순간 게임이 잠깐 멈춰도(히트스톱) UI는 계속 움직이게 해주는 거임
        private void ShowBanner(string header, string title, string detail, Color barColor)
        {
            if (banner == null || bannerGroup == null)
                return;

            bannerHeader.text = header;
            bannerTitle.text = title;
            bannerDetail.text = detail;
            bannerBar.color = barColor;

            banner.DOKill();
            bannerGroup.DOKill();
            banner.anchoredPosition = _bannerHome + Vector2.left * 420f;
            banner.localScale = Vector3.one * 1.2f;
            bannerGroup.alpha = 1f;

            banner.DOAnchorPosX(_bannerHome.x, 0.12f).SetEase(Ease.OutQuad).SetUpdate(true);
            banner.DOScale(1f, 0.12f).SetUpdate(true);
            banner.DOAnchorPosX(_bannerHome.x + 210f, 0.2f).SetDelay(0.87f).SetEase(Ease.InQuad).SetUpdate(true);
            bannerGroup.DOFade(0f, 0.2f).SetDelay(0.87f).SetUpdate(true);
        }

        // 콤보 만든 칸들 밑에 괄호 + 이름. 첫 칸 왼쪽 끝 ~ 마지막 칸 오른쪽 끝에 맞춰서 늘어남
        private void ShowBracket(int first, int last, string comboName)
        {
            if (bracket == null || bracketGroup == null || cells.Length == 0)
                return;

            first = Mathf.Clamp(first, 0, cells.Length - 1);
            last = Mathf.Clamp(last, 0, cells.Length - 1);

            var parent = (RectTransform)bracket.parent;
            float left = parent.InverseTransformPoint(Edge(cells[first].root, true)).x;
            float right = parent.InverseTransformPoint(Edge(cells[last].root, false)).x;
            bracket.localPosition = new Vector3((left + right) * 0.5f, bracket.localPosition.y, 0f);
            bracket.sizeDelta = new Vector2(right - left, bracket.sizeDelta.y);
            bracketLabel.text = comboName;

            bracketGroup.DOKill();
            bracket.gameObject.SetActive(true);
            bracketGroup.alpha = 1f;
            bracketGroup.DOFade(0f, 0.35f).SetDelay(0.55f).SetUpdate(true)
                .OnComplete(() => bracket.gameObject.SetActive(false));
        }

        private void FlashRing(Image ring)
        {
            if (ring == null)
                return;

            ring.DOKill();
            ring.enabled = true;
            ring.color = gold;
            ring.DOFade(0f, 0.7f).SetUpdate(true).OnComplete(() => ring.enabled = false);
        }

        private static void Bounce(RectTransform target, float amount)
        {
            if (target == null)
                return;

            target.DOKill(true);
            target.DOPunchScale(Vector3.one * amount, 0.3f).SetUpdate(true);
        }

        // 이제 Q/E 말고 우클릭, R, 대쉬도 속성이 있어서 다 콤보에 들어갈 수 있음
        private RectTransform IconOf(SkillSlotId slot) => slot switch
        {
            SkillSlotId.Basic1 => qIcon,
            SkillSlotId.Basic2 => eIcon,
            SkillSlotId.Weapon => rmbIcon,
            SkillSlotId.Ultimate => rIcon,
            SkillSlotId.Dash => spaceIcon,
            _ => null
        };

        // ------------------------------------------------------------ 콤보 리스트 (Tab)

        // 템플릿 한 줄 복사해서 콤보마다 한 줄씩 만듦. 템플릿 안에 "Name" 텍스트, "Steps" 밑에 칸 하나 있어야 함
        private void BuildList()
        {
            if (listRowTemplate == null || listRowParent == null)
                return;

            listRowTemplate.gameObject.SetActive(false);
            foreach (ElementComboBook.Recipe combo in _hud.ComboList)
            {
                RectTransform rowRoot = Instantiate(listRowTemplate, listRowParent);
                rowRoot.gameObject.SetActive(true);
                rowRoot.name = $"Row_{combo.Name}";
                rowRoot.Find("Name").GetComponent<TMP_Text>().text = combo.Name;

                var row = new ListRow
                {
                    combo = combo,
                    background = rowRoot.GetComponent<Image>(),
                    steps = new Image[combo.Length],
                    stepKeys = new TMP_Text[combo.Length]
                };
                row.background.color = listRowColor;

                Transform firstStep = rowRoot.Find("Steps").GetChild(0);
                for (int i = 0; i < combo.Length; i++)
                {
                    Transform step = i == 0 ? firstStep : Instantiate(firstStep, firstStep.parent);
                    row.steps[i] = step.GetComponent<Image>();
                    row.stepKeys[i] = step.GetComponentInChildren<TMP_Text>();
                }

                _rows.Add(row);
            }
        }

        // 콤보 진행 중이면 이어갈 수 있는 것만 (많이 진행한 순서로), 아니면 전부 보여줌
        private void RefreshList()
        {
            if (_rows.Count == 0)
                return;

            bool anyOnRoute = false;
            foreach (ListRow row in _rows)
            {
                row.progress = row.combo.TailProgress(_elements);
                anyOnRoute |= row.progress > 0;
            }

            foreach (ListRow row in _rows)
            {
                for (int i = 0; i < row.steps.Length; i++)
                {
                    ElementType element = row.combo.Sequence[i];
                    float alpha = !anyOnRoute || i < row.progress ? 1f : 0.35f;
                    Color color = _hud.ColorOf(element);
                    row.steps[i].color = new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, alpha);
                    row.stepKeys[i].text = _hud.KeyFor(element);
                    row.stepKeys[i].alpha = alpha;
                }
            }

            var order = new List<ListRow>(_rows);
            if (anyOnRoute)
                order.Sort((a, b) => b.progress.CompareTo(a.progress));

            for (int i = 0; i < order.Count; i++)
            {
                order[i].background.gameObject.SetActive(!anyOnRoute || order[i].progress > 0);
                order[i].background.transform.SetSiblingIndex(i + 1); // 0번은 숨겨둔 템플릿
            }

            if (listTitle != null)
                listTitle.text = anyOnRoute ? "이어갈 수 있는 콤보" : "콤보 리스트";
        }

        private void FlashListRow(ElementComboBook.Recipe combo)
        {
            foreach (ListRow row in _rows)
            {
                if (row.combo.Name != combo.Name)
                    continue;

                row.background.DOKill();
                row.background.color = WithAlpha(gold, 0.9f);
                row.background.DOColor(listRowColor, 0.8f).SetUpdate(true);
            }
        }

        private void SetListOpen(bool open)
        {
            _listOpen = open;
            if (listBody != null)
                listBody.SetActive(open);
            if (listTitle != null)
                listTitle.gameObject.SetActive(open);
            if (listClosedTab != null)
                listClosedTab.gameObject.SetActive(!open);
        }

        // ------------------------------------------------------------ 글자/색 도우미

        // "Q 불 › E 물" 처럼 키 + 속성 이름을 속성 색으로
        private string KeySequence(IReadOnlyList<ElementType> sequence)
        {
            var text = new StringBuilder();
            for (int i = 0; i < sequence.Count; i++)
            {
                if (i > 0)
                    text.Append("  <color=#8A8A96>›</color>  ");
                text.Append(KeyAndElement(sequence[i]));
            }
            return text.ToString();
        }

        private string KeyAndElement(ElementType element) =>
            $"<color=#{Hex(_hud.ColorOf(element))}>{_hud.KeyFor(element)} {_hud.ElementName(element)}</color>";

        private static Vector3 Edge(RectTransform cell, bool left)
        {
            Rect r = cell.rect;
            return cell.TransformPoint(new Vector3(left ? r.xMin : r.xMax, r.center.y, 0f));
        }

        private static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);
        private static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);
        private static float Pulse(float speed) => 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
    }
}
