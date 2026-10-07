using System.Collections;
using System.Collections.Generic;
using System.Text;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using Assets.Members.HJH._02.Scripts.Element;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01_Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // Combo HUD for ElementComboChain. Owns the input trail above the skill bar and routes chain events
    // to the other pieces (counter / stock views subscribe on their own):
    //  - trail: the last inputs as key caps (slides left when the oldest drops), NEXT cap + hint for
    //    the combo you are furthest into,
    //  - combo landed: its caps flash gold under a bracket, banner call-out, move-list flash, light hit-stop,
    //  - finisher: big red banner + screen flash + heavier hit-stop.
    // Effects are still Debug.Log on JJH's side; this only makes them visible and readable.
    public class ElementComboHud : MonoBehaviour
    {
        [SerializeField] private ElementComboChain chain;
        [SerializeField] private PlayerAgent player;
        [SerializeField] private ElementPalette palette;

        [Header("Input trail")]
        [SerializeField] private ComboInputCell[] cells;
        [SerializeField] private ComboInputCell nextCell;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private RectTransform bracket;
        [SerializeField] private TMP_Text bracketLabel;
        [SerializeField] private float shiftDuration = 0.12f;
        [SerializeField] private float breakDuration = 0.3f;
        [SerializeField] private float bracketDuration = 0.9f;
        [SerializeField] private Color comboColor = new Color(1f, 0.83f, 0.3f);

        [Header("Call-outs")]
        [SerializeField] private ComboBanner banner;
        [SerializeField] private ComboListView comboList;

        [Header("Feel")]
        [SerializeField] private float comboHitStop = 0.03f;
        [SerializeField] private float comboShake = 0.12f;
        [SerializeField] private float finisherHitStop = 0.08f;
        [SerializeField] private float finisherShake = 0.35f;

        private float _cellStep;
        private float _shiftTime = -1f;
        private float _bracketTime = -1f;
        private Coroutine _break;
        private Dictionary<ElementType, string> _keys = new();

        private void Start()
        {
            if (cells.Length > 1)
                _cellStep = cells[1].Home.x - cells[0].Home.x;

            if (comboList != null)
                comboList.Build(chain.Book, palette);
            bracket.gameObject.SetActive(false);
            Render(slamLast: false);
        }

        private void OnEnable()
        {
            chain.InputAdded += HandleInputAdded;
            chain.ComboLanded += HandleComboLanded;
            chain.ChainBroken += HandleChainBroken;
            chain.FinisherReleased += HandleFinisher;
        }

        private void OnDisable()
        {
            chain.InputAdded -= HandleInputAdded;
            chain.ComboLanded -= HandleComboLanded;
            chain.ChainBroken -= HandleChainBroken;
            chain.FinisherReleased -= HandleFinisher;
        }

        private void Update()
        {
            UpdateShift();
            UpdateBracket();
        }

        private void HandleInputAdded(bool shifted)
        {
            StopBreak();
            if (shifted)
            {
                _shiftTime = 0f;
                _bracketTime = -1f;
                bracket.gameObject.SetActive(false);
            }
            Render(slamLast: true);
        }

        private void HandleComboLanded(ElementComboChain.LandedCombo landed)
        {
            int first = landed.StartIndex;
            int last = landed.StartIndex + landed.Combo.Length - 1;
            for (int i = first; i <= last; i++)
            {
                if (i >= 0 && i < cells.Length)
                    cells[i].Flash(comboColor);
            }
            ShowBracket(first, last, landed.Combo.Name);

            banner.ShowCombo(landed.Combo.Name, KeySequence(landed.Combo.Sequence));
            if (comboList != null)
                comboList.FlashLanded(landed.Combo);
            HitFeel.Play(comboHitStop, comboShake);
        }

        private void HandleFinisher(IReadOnlyList<ElementComboBook.Recipe> released)
        {
            var names = new StringBuilder();
            foreach (ElementComboBook.Recipe combo in released)
            {
                if (names.Length > 0)
                    names.Append("  +  ");
                names.Append(combo.Name);
            }

            banner.ShowFinisher(released.Count, names.ToString());
            HitFeel.Play(finisherHitStop, finisherShake);
        }

        private void HandleChainBroken(int finalCount)
        {
            StopBreak();
            _break = StartCoroutine(BreakTrail());
        }

        private void Render(bool slamLast)
        {
            _keys = ComboKeyMap.Build(player);
            IReadOnlyList<ElementComboChain.ComboInput> inputs = chain.Inputs;

            for (int i = 0; i < cells.Length; i++)
            {
                if (i < inputs.Count && palette.TryGet(inputs[i].Element, out ElementPalette.Entry entry))
                    cells[i].ShowInput(entry, ComboKeyMap.Key(player, inputs[i].Slot), ElementComboBook.KoreanName(inputs[i].Element),
                        slamLast && i == inputs.Count - 1);
                else
                    cells[i].ShowEmpty();
            }

            bool onRoute = chain.TryGetNext(out ElementType nextElement, out ElementComboBook.Recipe route, out int progress);
            if (onRoute && palette.TryGet(nextElement, out ElementPalette.Entry next))
                nextCell.ShowGhost(next, ComboKeyMap.KeyFor(_keys, nextElement), ElementComboBook.KoreanName(nextElement));
            else
                nextCell.ShowEmpty();

            hintText.text = Hint(inputs.Count, onRoute, route, progress);
            if (comboList != null)
                comboList.Refresh(chain.Elements, _keys);
        }

        private string Hint(int inputCount, bool onRoute, ElementComboBook.Recipe route, int progress)
        {
            if (onRoute)
            {
                ElementType next = route.Sequence[progress];
                string gold = ColorUtility.ToHtmlStringRGB(comboColor);
                return $"<color=#{gold}>{route.Name}</color>  {progress}/{route.Length}   다음 {Colored(next, ComboKeyMap.KeyFor(_keys, next))}";
            }

            return inputCount == 0
                ? "<color=#8A8A96>스킬을 이어 써서 콤보</color>"
                : "<color=#8A8A96>이어지는 콤보 없음</color>";
        }

        // Gold underline under the inputs that formed the combo, with its name; fades out.
        private void ShowBracket(int first, int last, string comboName)
        {
            first = Mathf.Clamp(first, 0, cells.Length - 1);
            last = Mathf.Clamp(last, 0, cells.Length - 1);
            float left = cells[first].Home.x - _cellStep * 0.45f;
            float right = cells[last].Home.x + _cellStep * 0.45f;

            bracket.anchoredPosition = new Vector2((left + right) * 0.5f, bracket.anchoredPosition.y);
            bracket.sizeDelta = new Vector2(right - left, bracket.sizeDelta.y);
            bracketLabel.text = comboName;
            bracket.gameObject.SetActive(true);
            _bracketTime = 0f;
        }

        private void UpdateBracket()
        {
            if (_bracketTime < 0f)
                return;

            _bracketTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_bracketTime / bracketDuration);
            float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            foreach (Graphic graphic in bracket.GetComponentsInChildren<Graphic>())
            {
                Color c = graphic.color;
                graphic.color = new Color(c.r, c.g, c.b, alpha);
            }

            if (t >= 1f)
            {
                _bracketTime = -1f;
                bracket.gameObject.SetActive(false);
            }
        }

        // The inputs list was full: every input moved one cell left, so slide the cells in from the right.
        private void UpdateShift()
        {
            if (_shiftTime < 0f)
                return;

            _shiftTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_shiftTime / shiftDuration);
            float offset = _cellStep * (1f - t) * (1f - t);
            foreach (ComboInputCell cell in cells)
                cell.SetPose(new Vector2(offset, 0f), 1f, 1f);

            if (t >= 1f)
                _shiftTime = -1f;
        }

        // Chain broke: the inputs drop and fade, then the empty trail is drawn.
        private IEnumerator BreakTrail()
        {
            for (float time = 0f; time < breakDuration; time += Time.unscaledDeltaTime)
            {
                float t = time / breakDuration;
                foreach (ComboInputCell cell in cells)
                    cell.SetPose(new Vector2(0f, -24f * t * t), 1f, 1f - t);
                yield return null;
            }

            _break = null;
            foreach (ComboInputCell cell in cells)
                cell.ResetPose();
            Render(slamLast: false);
        }

        private void StopBreak()
        {
            if (_break == null)
                return;

            StopCoroutine(_break);
            _break = null;
            foreach (ComboInputCell cell in cells)
                cell.ResetPose();
        }

        private string KeySequence(IReadOnlyList<ElementType> sequence)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < sequence.Count; i++)
            {
                if (i > 0)
                    sb.Append("  <color=#8A8A96>›</color>  ");
                sb.Append(Colored(sequence[i], ComboKeyMap.KeyFor(_keys, sequence[i])));
            }
            return sb.ToString();
        }

        private string Colored(ElementType element, string key)
        {
            Color color = palette.TryGet(element, out ElementPalette.Entry entry) ? entry.color : Color.white;
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{key} {ElementComboBook.KoreanName(element)}</color>";
        }
    }
}
