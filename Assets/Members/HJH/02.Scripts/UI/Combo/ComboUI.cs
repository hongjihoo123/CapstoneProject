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
    public class ComboUI : MonoBehaviour
    {
        [Serializable]
        public struct Cell
        {
            public RectTransform root;
            public CanvasGroup group;
            public Image background;
            public Image ring;
            public Image badge;
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
        [SerializeField] private TMP_Text bannerHeader;
        [SerializeField] private TMP_Text bannerTitle;
        [SerializeField] private TMP_Text bannerDetail;
        [SerializeField] private Image screenFlash;

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
        [SerializeField] private RectTransform qIcon;
        [SerializeField] private RectTransform eIcon;
        [SerializeField] private RectTransform rmbIcon;
        [SerializeField] private RectTransform rIcon;
        [SerializeField] private RectTransform spaceIcon;

        [Header("색")]
        [SerializeField] private Color gold = new(1f, 0.83f, 0.3f);
        [SerializeField] private Color emptyCellColor = new(1f, 1f, 1f, 0.22f);
        [SerializeField] private Color comboBarColor = new(0.42f, 0.28f, 0.03f, 0.92f);
        [SerializeField] private Color finisherBarColor = new(0.55f, 0.07f, 0.1f, 0.95f);
        [SerializeField] private Color pipOn = new(1f, 0.83f, 0.3f);
        [SerializeField] private Color pipOff = new(1f, 1f, 1f, 0.18f);
        [SerializeField] private Color listRowColor = new(0.55f, 0.48f, 0.32f, 0.75f);

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
            _hud.FinisherArmed += OnFinisherArmed;
            _hud.FinisherExpired += OnFinisherExpired;
            _hud.LoadoutChanged += Redraw;
            _hud.ComboListChanged += RebuildList;
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
            _hud.FinisherArmed -= OnFinisherArmed;
            _hud.FinisherExpired -= OnFinisherExpired;
            _hud.LoadoutChanged -= Redraw;
            _hud.ComboListChanged -= RebuildList;
        }

        private void Update()
        {
            if (_hud == null)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
                SetListOpen(!_listOpen);

            UpdateGauge();

            if (nextCell.ring != null && nextCell.ring.enabled)
                nextCell.ring.color = WithAlpha(nextCell.ring.color, 0.3f + 0.65f * Pulse(6f));

            if (readyLabel != null)
            {
                int stock = _hud.ComboStock.Count;
                bool full = stock >= _hud.MaxComboStock;
                if (_hud.FinisherReady)
                {
                    readyLabel.enabled = true;
                    readyLabel.text = $"다음 스킬 ×{_hud.FinisherScale:0.0#}  {_hud.FinisherTimeLeft:0.0}초";
                    readyLabel.alpha = 0.6f + 0.4f * Pulse(12f);
                }
                else
                {
                    readyLabel.enabled = stock > 0;
                    readyLabel.text = $"{(full ? "MAX" : "READY")} ×{_hud.FinisherScaleFor(stock):0.0#}";
                    readyLabel.alpha = 0.45f + 0.55f * Pulse(full ? 10f : 4f);
                }
            }
        }

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
                finisherKey.text = $"[{_hud.FinisherKey}] 피니시";
        }

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

        private void OnFinisher(SkillSlotId slot, float scale)
        {
            ShowBanner($"FINISH  ×{scale:0.0#}", "피니시", "피해 강화", finisherBarColor);
            Bounce(IconOf(slot), 0.4f);

            if (screenFlash != null)
            {
                screenFlash.DOKill();
                screenFlash.enabled = true;
                screenFlash.color = WithAlpha(screenFlash.color, 0.35f);
                screenFlash.DOFade(0f, 0.25f).SetUpdate(true).OnComplete(() => screenFlash.enabled = false);
            }
        }

        private void OnFinisherArmed(int stacks, float scale)
        {
            ShowBanner("READY", "피니시 준비", $"다음 스킬 ×{scale:0.0#}", finisherBarColor);
            foreach (SkillSlotId slot in new[] { SkillSlotId.Basic1, SkillSlotId.Basic2, SkillSlotId.Weapon, SkillSlotId.Ultimate })
                Bounce(IconOf(slot), 0.2f);
        }

        private void OnFinisherExpired() => ShowBanner("MISS", "피니시 시간 끝", string.Empty, comboBarColor);

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

        private RectTransform IconOf(SkillSlotId slot) => slot switch
        {
            SkillSlotId.Basic1 => qIcon,
            SkillSlotId.Basic2 => eIcon,
            SkillSlotId.Weapon => rmbIcon,
            SkillSlotId.Ultimate => rIcon,
            SkillSlotId.Dash => spaceIcon,
            _ => null
        };

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

        private void RebuildList()
        {
            foreach (ListRow row in _rows)
            {
                if (row.background != null)
                    Destroy(row.background.gameObject);
            }
            _rows.Clear();
            BuildList();
            Redraw();
        }

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
                order[i].background.transform.SetSiblingIndex(i + 1);
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
