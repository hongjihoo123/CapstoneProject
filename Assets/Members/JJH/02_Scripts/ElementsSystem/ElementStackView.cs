using DG.Tweening;
using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Members.JJH._02_Scripts.ElementsSystem
{
    public class ElementStackView : MonoBehaviour
    {
        [SerializeField] private EventChannelSO systemChannel;
        [SerializeField] private ElementBuffController buffController;

        [Header("Slots")]
        [SerializeField] private Image[] slotImages;
        [SerializeField] private Slider timerSlider;

        [Header("Next")]
        [SerializeField] private Image nextSlotImage;
        [SerializeField] private Image nextSlotRing;
        [SerializeField] private TextMeshProUGUI hintText;

        [Header("Combo Banner")]
        [SerializeField] private RectTransform banner;
        [SerializeField] private CanvasGroup bannerGroup;
        [SerializeField] private Image bannerBar;
        [SerializeField] private TextMeshProUGUI bannerHeader;
        [SerializeField] private TextMeshProUGUI bannerTitle;
        [SerializeField] private TextMeshProUGUI bannerDetail;

        [Header("Combo Bracket")]
        [SerializeField] private RectTransform bracket;
        [SerializeField] private CanvasGroup bracketGroup;
        [SerializeField] private TextMeshProUGUI bracketLabel;

        [Header("Finisher")]
        [SerializeField] private Image screenFlash;
        [SerializeField] private Image[] stockPips;
        [SerializeField] private TextMeshProUGUI[] stockNames;
        [SerializeField] private TextMeshProUGUI finisherKey;
        [SerializeField] private TextMeshProUGUI readyLabel;
        [SerializeField] private int maxFinisherStock = 3;

        [Header("Combo List")]
        [SerializeField] private RectTransform listRowTemplate;
        [SerializeField] private RectTransform listRowParent;
        [SerializeField] private GameObject listBody;
        [SerializeField] private TextMeshProUGUI listTitle;
        [SerializeField] private TextMeshProUGUI listClosedTab;

        [Header("Sprites")]
        [Tooltip("ElementType 순서: Fire, Water, Wind, Electric, Earth")]
        [SerializeField] private Sprite[] elementSprites;

        [Header("Color")]
        [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color gold = new Color(1f, 0.83f, 0.3f);
        [SerializeField] private Color comboBarColor = new Color(0.42f, 0.28f, 0.03f, 0.92f);
        [SerializeField] private Color finisherBarColor = new Color(0.55f, 0.07f, 0.1f, 0.95f);
        [SerializeField] private Color pipOn = new Color(1f, 0.83f, 0.3f);
        [SerializeField] private Color pipOff = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField] private Color listRowColor = new Color(0.55f, 0.48f, 0.32f, 0.75f);

        [Tooltip("ElementType 순서: Fire, Water, Wind, Electric, Earth")]
        [SerializeField] private string[] elementNames = { "불", "물", "바람", "전기", "땅" };

        [Header("Setting")]
        [SerializeField] private float clearDelay = 0.35f;
        [SerializeField] private float clearScale = 1.3f;
        [SerializeField] private float bounceScale = 0.25f;

        private class ListRow
        {
            public string name;
            public ElementType[] sequence;
            public Image background;
            public Image[] steps;
            public TextMeshProUGUI[] stepTexts;
            public int progress;
        }

        private class FinisherStock
        {
            public string Name;
            public ElementType[] Sequence;
        }

        private readonly List<ListRow> _rows = new();
        private readonly List<FinisherStock> _finisherStock = new();

        private float _duration;
        private float _remaining;
        private int _filledCount;
        private Coroutine _clearRoutine;
        private Vector2 _bannerHome;
        private bool _listOpen;

        private void Awake()
        {
            if (banner != null)
                _bannerHome = banner.anchoredPosition;

            if (timerSlider != null)
            {
                timerSlider.minValue = 0f;
                timerSlider.wholeNumbers = false;
                timerSlider.interactable = false;
            }

            if (bracket != null)
                bracket.gameObject.SetActive(false);

            if (bracketGroup != null)
                bracketGroup.alpha = 0f;

            if (screenFlash != null)
                screenFlash.enabled = false;

            BuildComboList();
        }

        private void OnEnable()
        {
            systemChannel.AddListener<ElementStackChangedEvent>(HandleStackChanged);
            systemChannel.AddListener<ElementComboNextEvent>(HandleComboNext);
            systemChannel.AddListener<ElementComboCompleteEvent>(HandleComboComplete);

            ClearSlots();
            SetTimer(0f);
            HideBanner();
            DrawFinisherStock();
            SetListOpen(false);
        }

        private void OnDisable()
        {
            systemChannel.RemoveListener<ElementStackChangedEvent>(HandleStackChanged);
            systemChannel.RemoveListener<ElementComboNextEvent>(HandleComboNext);
            systemChannel.RemoveListener<ElementComboCompleteEvent>(HandleComboComplete);

            if (_clearRoutine != null)
            {
                StopCoroutine(_clearRoutine);
                _clearRoutine = null;
            }

            if (bracket != null)
            {
                bracket.DOKill();
                bracket.gameObject.SetActive(false);
            }

            if (bracketGroup != null)
            {
                bracketGroup.DOKill();
                bracketGroup.alpha = 0f;
            }

            if (screenFlash != null)
            {
                screenFlash.DOKill();
                screenFlash.enabled = false;
            }

            ResetSlotScale();
            HideBanner();
        }

        private void Update()
        {
            if (_duration > 0f)
            {
                _remaining -= Time.deltaTime;
                SetTimer(_remaining);
            }

            if (nextSlotRing != null && nextSlotRing.enabled)
                nextSlotRing.color = WithAlpha(nextSlotRing.color, 0.3f + 0.65f * Pulse(6f));

            if (readyLabel != null)
            {
                bool hasStock = _finisherStock.Count > 0;
                bool full = _finisherStock.Count >= maxFinisherStock;

                readyLabel.enabled = hasStock;
                readyLabel.text = full ? "MAX" : "READY";
                readyLabel.alpha = 0.45f + 0.55f * Pulse(full ? 10f : 4f);
            }

            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                SetListOpen(!_listOpen);
        }

        private void HandleStackChanged(ElementStackChangedEvent evt)
        {
            if (evt.Stacks == null || evt.Stacks.Count == 0)
            {
                _duration = 0f;
                _remaining = 0f;
                SetTimer(0f);

                if (_clearRoutine == null)
                    _clearRoutine = StartCoroutine(ClearAfterDelay());

                return;
            }

            if (_clearRoutine != null)
            {
                StopCoroutine(_clearRoutine);
                _clearRoutine = null;
            }

            Render(evt.Stacks);

            _filledCount = Mathf.Min(evt.Stacks.Count, slotImages.Length);

            int lastIndex = _filledCount - 1;

            if (lastIndex >= 0)
                Bounce(slotImages[lastIndex].rectTransform);

            _duration = evt.TimerDuration;
            _remaining = evt.TimerDuration;

            if (timerSlider != null)
                timerSlider.maxValue = _duration;

            SetTimer(_remaining);
        }

        private void HandleComboNext(ElementComboNextEvent evt)
        {
            if (nextSlotImage != null)
            {
                nextSlotImage.sprite = elementSprites[(int)evt.NextElement];
                nextSlotImage.enabled = true;
                nextSlotImage.color = Color.white;
            }

            if (nextSlotRing != null)
            {
                nextSlotRing.enabled = true;
                nextSlotRing.color = ElementColor(evt.NextElement);
            }

            if (hintText != null)
                hintText.text = $"<color=#{Hex(gold)}>{evt.ComboName}</color>  {evt.Progress}/{evt.ComboLength}   다음 {ElementText(evt.NextElement)}";

            RefreshComboList(evt.ComboName, evt.Progress);
        }

        private void HandleComboComplete(ElementComboCompleteEvent evt)
        {
            _duration = 0f;
            _remaining = 0f;
            SetTimer(0f);

            ShowBanner("COMBO!", evt.ComboName, ElementSequenceText(evt.Sequence), comboBarColor);

            int first = Mathf.Max(0, _filledCount - evt.Sequence.Count);
            int last = _filledCount - 1;

            for (int i = first; i <= last; i++)
            {
                if (i >= slotImages.Length)
                    continue;

                Bounce(slotImages[i].rectTransform);
                FlashRing(slotImages[i]);
            }

            ShowBracket(first, last, evt.ComboName);
            AddFinisherStock(evt.ComboName, evt.Sequence);
            FlashComboList(evt.ComboName);

            if (nextSlotImage != null)
                nextSlotImage.enabled = false;

            if (nextSlotRing != null)
                nextSlotRing.enabled = false;

            if (hintText != null)
                hintText.text = $"<color=#{Hex(gold)}>{evt.ComboName}</color> 발동!";
        }

        private void AddFinisherStock(string name, IReadOnlyList<ElementType> sequence)
        {
            if (_finisherStock.Count >= maxFinisherStock)
                return;

            var stock = new FinisherStock
            {
                Name = name,
                Sequence = new ElementType[sequence.Count]
            };

            for (int i = 0; i < sequence.Count; i++)
                stock.Sequence[i] = sequence[i];

            _finisherStock.Add(stock);
            DrawFinisherStock();
        }

        public void PlayFinisher()
        {
            if (_finisherStock.Count == 0)
                return;

            StringBuilder names = new StringBuilder();

            foreach (FinisherStock stock in _finisherStock)
            {
                if (names.Length > 0)
                    names.Append("  +  ");

                names.Append(stock.Name);
            }

            ShowBanner($"FINISH  x{_finisherStock.Count}", "피니시", names.ToString(), finisherBarColor);
            PlayScreenFlash();

            _finisherStock.Clear();
            DrawFinisherStock();
        }

        private void PlayScreenFlash()
        {
            if (screenFlash == null)
                return;

            screenFlash.DOKill();
            screenFlash.enabled = true;
            screenFlash.color = WithAlpha(screenFlash.color, 0.35f);

            screenFlash.DOFade(0f, 0.25f).SetUpdate(true).OnComplete(() => screenFlash.enabled = false);
        }

        private void BuildComboList()
        {
            if (buffController == null || listRowTemplate == null || listRowParent == null)
                return;

            foreach (ListRow row in _rows)
            {
                if (row.background != null)
                    Destroy(row.background.gameObject);
            }

            _rows.Clear();
            listRowTemplate.gameObject.SetActive(false);

            for (int i = 0; i < buffController.ComboCount; i++)
            {
                string comboName = buffController.GetComboName(i);
                IReadOnlyList<ElementType> source = buffController.GetComboSequence(i);

                if (source == null || source.Count == 0)
                    continue;

                RectTransform rowRoot = Instantiate(listRowTemplate, listRowParent);
                rowRoot.gameObject.SetActive(true);
                rowRoot.name = $"Row_{comboName}";

                TextMeshProUGUI nameText = rowRoot.Find("Name")?.GetComponent<TextMeshProUGUI>();

                if (nameText != null)
                    nameText.text = comboName;

                Transform stepsRoot = rowRoot.Find("Steps");

                if (stepsRoot == null || stepsRoot.childCount == 0)
                {
                    Destroy(rowRoot.gameObject);
                    continue;
                }

                Transform firstStep = stepsRoot.GetChild(0);

                var row = new ListRow
                {
                    name = comboName,
                    sequence = new ElementType[source.Count],
                    background = rowRoot.GetComponent<Image>(),
                    steps = new Image[source.Count],
                    stepTexts = new TextMeshProUGUI[source.Count]
                };

                for (int k = 0; k < source.Count; k++)
                    row.sequence[k] = source[k];

                if (row.background != null)
                    row.background.color = listRowColor;

                for (int k = 0; k < source.Count; k++)
                {
                    Transform step = k == 0 ? firstStep : Instantiate(firstStep, stepsRoot);

                    row.steps[k] = step.GetComponent<Image>();
                    row.stepTexts[k] = step.GetComponentInChildren<TextMeshProUGUI>();

                    if (row.steps[k] != null)
                    {
                        row.steps[k].sprite = elementSprites[(int)source[k]];
                        row.steps[k].enabled = true;
                        row.steps[k].color = ElementColor(source[k]);
                    }

                    if (row.stepTexts[k] != null)
                        row.stepTexts[k].text = ElementText(source[k]);
                }

                _rows.Add(row);
            }

            listRowTemplate.gameObject.SetActive(false);
        }

        private void RefreshComboList(string comboName, int progress)
        {
            foreach (ListRow row in _rows)
            {
                row.progress = row.name == comboName ? progress : 0;

                for (int i = 0; i < row.steps.Length; i++)
                {
                    Color color = ElementColor(row.sequence[i]);
                    float alpha = row.progress == 0 || i < row.progress ? 1f : 0.35f;

                    row.steps[i].color = new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, alpha);

                    if (row.stepTexts[i] != null)
                        row.stepTexts[i].text = ElementText(row.sequence[i]);
                }
            }

            if (listTitle != null)
                listTitle.text = "콤보 리스트";
        }

        private void FlashComboList(string comboName)
        {
            foreach (ListRow row in _rows)
            {
                if (row.name != comboName || row.background == null)
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

        private void Render(IReadOnlyList<ElementType> stacks)
        {
            for (int i = 0; i < slotImages.Length; i++)
            {
                if (i < stacks.Count)
                {
                    slotImages[i].sprite = elementSprites[(int)stacks[i]];
                    slotImages[i].color = Color.white;
                    slotImages[i].enabled = true;
                }
                else
                {
                    slotImages[i].sprite = null;
                    slotImages[i].color = emptyColor;
                    slotImages[i].enabled = false;
                }
            }
        }

        private void ClearSlots()
        {
            _filledCount = 0;
            ResetSlotScale();

            foreach (Image slot in slotImages)
            {
                slot.sprite = null;
                slot.color = emptyColor;
                slot.enabled = false;
            }

            if (nextSlotImage != null)
                nextSlotImage.enabled = false;

            if (nextSlotRing != null)
                nextSlotRing.enabled = false;

            if (bracket != null)
                bracket.gameObject.SetActive(false);

            if (hintText != null)
                hintText.text = "<color=#8A8A96>스킬을 이어 써서 콤보</color>";
        }

        private void PlayClearEffect()
        {
            for (int i = 0; i < _filledCount; i++)
            {
                RectTransform rt = slotImages[i].rectTransform;

                rt.DOKill();
                rt.localScale = Vector3.one;
                rt.DOScale(clearScale, clearDelay).SetEase(Ease.OutQuad);
            }
        }

        private IEnumerator ClearAfterDelay()
        {
            yield return new WaitForSeconds(clearDelay);
            ClearSlots();
            _clearRoutine = null;
        }

        private void ResetSlotScale()
        {
            foreach (Image slot in slotImages)
            {
                RectTransform rt = slot.rectTransform;
                rt.DOKill();
                rt.localScale = Vector3.one;
            }
        }

        private void SetTimer(float remainingSeconds)
        {
            if (timerSlider != null)
                timerSlider.value = Mathf.Max(0f, remainingSeconds);
        }

        private void ShowBanner(string header, string title, string detail, Color barColor)
        {
            if (banner == null || bannerGroup == null)
                return;

            if (bannerHeader != null)
                bannerHeader.text = header;

            if (bannerTitle != null)
                bannerTitle.text = title;

            if (bannerDetail != null)
                bannerDetail.text = detail;

            if (bannerBar != null)
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

        private void HideBanner()
        {
            if (banner == null || bannerGroup == null)
                return;

            banner.DOKill();
            bannerGroup.DOKill();
            bannerGroup.alpha = 0f;
        }

        private void ShowBracket(int first, int last, string comboName)
        {
            if (bracket == null || bracketGroup == null || slotImages.Length == 0)
                return;

            first = Mathf.Clamp(first, 0, slotImages.Length - 1);
            last = Mathf.Clamp(last, 0, slotImages.Length - 1);

            RectTransform parent = (RectTransform)bracket.parent;

            float left = parent.InverseTransformPoint(Edge(slotImages[first].rectTransform, true)).x;
            float right = parent.InverseTransformPoint(Edge(slotImages[last].rectTransform, false)).x;

            bracket.localPosition = new Vector3((left + right) * 0.5f, bracket.localPosition.y, 0f);
            bracket.sizeDelta = new Vector2(right - left, bracket.sizeDelta.y);

            if (bracketLabel != null)
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

            Color originalColor = Color.white;

            ring.color = gold;
            ring.DOColor(originalColor, 0.7f).SetUpdate(true);
        }

        private void Bounce(RectTransform target)
        {
            if (target == null)
                return;

            target.DOKill(true);
            target.DOPunchScale(Vector3.one * bounceScale, 0.3f).SetUpdate(true);
        }

        private void DrawFinisherStock()
        {
            for (int i = 0; i < stockPips.Length; i++)
            {
                bool active = i < _finisherStock.Count;

                stockPips[i].enabled = true;
                stockPips[i].color = active ? pipOn : pipOff;

                if (stockNames != null && i < stockNames.Length && stockNames[i] != null)
                    stockNames[i].text = active ? _finisherStock[i].Name : "";
            }

            if (finisherKey != null)
                finisherKey.text = _finisherStock.Count > 0 ? "F" : "";

            if (readyLabel != null)
            {
                readyLabel.enabled = _finisherStock.Count > 0;
                readyLabel.text = _finisherStock.Count >= maxFinisherStock ? "MAX" : "READY";
            }
        }

        private string ElementText(ElementType element)
        {
            return elementNames[(int)element];
        }

        private string ElementSequenceText(IReadOnlyList<ElementType> sequence)
        {
            StringBuilder text = new StringBuilder();

            for (int i = 0; i < sequence.Count; i++)
            {
                if (i > 0)
                    text.Append("  <color=#8A8A96>›</color>  ");

                text.Append($"<color=#{Hex(ElementColor(sequence[i]))}>{ElementText(sequence[i])}</color>");
            }

            return text.ToString();
        }

        private Color ElementColor(ElementType element)
        {
            return element switch
            {
                ElementType.Fire => new Color(1f, 0.25f, 0.15f),
                ElementType.Water => new Color(0.2f, 0.5f, 1f),
                ElementType.Wind => new Color(0.3f, 0.9f, 0.5f),
                ElementType.Electric => new Color(0.95f, 0.8f, 0.15f),
                ElementType.Earth => new Color(0.65f, 0.4f, 0.2f),
                _ => Color.white
            };
        }

        private static Vector3 Edge(RectTransform cell, bool left)
        {
            Rect rect = cell.rect;
            return cell.TransformPoint(new Vector3(left ? rect.xMin : rect.xMax, rect.center.y, 0f));
        }

        private static string Hex(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        private static float Pulse(float speed)
        {
            return 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
        }
    }
}