using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 궁극기 초기화(치명 연쇄) 규칙을 플레이어에게 알려주는 부분.
    //  - 궁극기 칸에 마우스를 올리면 규칙 설명 상자 ("파편이 3초 안에 치명타를 8번 터뜨리면 궁극기가 초기화돼요")
    //  - 파편 치명을 세는 동안 칸 위에 "치명 3/8" 진행도
    // 숫자는 전부 PlayerHudSource.UltimateRule / TryGetUltimateProgress 에서 읽음 (스킬 트리 강화가 바뀌어도 글이 맞음).
    // PlayerRig 하이어라키에 구워 둔 오브젝트. 글자 크기·색·상자 모양은 인스펙터에서 수정
    public class UltChainHint : MonoBehaviour, IUiBake
    {
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private RectTransform tip;
        [SerializeField] private TMP_Text tipText;
        [SerializeField, Tooltip("칸 위 끝에서 진행도 글자까지 띄울 간격(px)")] private float progressGap = 6f;
        [SerializeField, Tooltip("칸 위 끝에서 설명 상자까지 띄울 간격(px)")] private float tipGap = 44f;
        [SerializeField] private Color progressColor = new(0.69f, 0.75f, 0.82f, 1f);
        [SerializeField] private Color progressFullColor = new(1f, 0.82f, 0.3f, 1f);

        private PlayerHudSource _hud;
        private RectTransform _slot;
        private SkillSlotView _slotView;
        private Canvas _canvas;
        private bool _found;

        private void Start()
        {
            _hud = PlayerHudSource.Instance;
            Canvas canvas = GetComponentInParent<Canvas>();
            _canvas = canvas != null ? canvas.rootCanvas : null;
            if (progressText != null)
                progressText.enabled = false;
            if (tip != null)
                tip.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_hud == null || _canvas == null)
                return;

            if (!_found)
                _found = FindSlot();
            if (!_found || _slot == null || !_slot.gameObject.activeInHierarchy)
            {
                Hide();
                return;
            }

            UpdateProgress();
            UpdateTip();
        }

        private bool FindSlot()
        {
            SkillBarView bar = FindFirstObjectByType<SkillBarView>(FindObjectsInactive.Include);
            if (bar == null || !bar.TryGetView(SkillSlotId.Ultimate, out _slotView))
                return false;

            _slot = (RectTransform)_slotView.transform;
            return true;
        }

        private void Hide()
        {
            if (progressText != null)
                progressText.enabled = false;
            if (tip != null && tip.gameObject.activeSelf)
                tip.gameObject.SetActive(false);
        }

        // 칸 위쪽 가운데의 화면 좌표(월드). 캔버스 배율을 곱해서 px 간격이 어느 해상도에서도 같게
        private Vector3 TopCenter(float gap)
        {
            Vector3[] corners = new Vector3[4];
            _slot.GetWorldCorners(corners);
            Vector3 top = (corners[1] + corners[2]) * 0.5f;
            return top + Vector3.up * (gap * _canvas.scaleFactor);
        }

        private void UpdateProgress()
        {
            if (progressText == null)
                return;

            if (!_hud.TryGetUltimateProgress(out int crits, out int need, out float left))
            {
                progressText.enabled = false;
                return;
            }

            float fill = Mathf.Clamp01(crits / (float)Mathf.Max(1, need));
            progressText.enabled = true;
            progressText.color = Color.Lerp(progressColor, progressFullColor, fill * fill);
            progressText.text = $"치명 {crits}/{need}  <size=70%>{left:0.0}s</size>";
            progressText.rectTransform.position = TopCenter(progressGap);
        }

        private void UpdateTip()
        {
            if (tip == null || tipText == null)
                return;

            Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1f, -1f);
            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (!RectTransformUtility.RectangleContainsScreenPoint(_slot, mouse, cam))
            {
                if (tip.gameObject.activeSelf)
                    tip.gameObject.SetActive(false);
                return;
            }

            PlayerHudSource.UltRule rule = _hud.UltimateRule;
            string gold = ColorUtility.ToHtmlStringRGB(HudUiKit.Gold);
            string label = ColorUtility.ToHtmlStringRGB(HudUiKit.Label);
            tipText.text =
                $"<color=#{gold}>치명 연쇄</color>\n" +
                $"궁극기 파편이 <color=#{gold}>{rule.Window:0.#}초</color> 안에 치명타를 <color=#{gold}>{rule.Need}번</color> 터뜨리면 궁극기가 초기화돼요.\n" +
                $"<size=80%><color=#{label}>초기화 뒤 {rule.ChainTime:0.#}초 안에 다시 쓰면 연쇄가 이어져요  ·  연쇄 {rule.Stage}/{rule.Cap}</color></size>";

            tip.gameObject.SetActive(true);
            tip.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(tip);
            tip.position = TopCenter(tipGap);
        }

        [ContextMenu("레이아웃 굽기 (자식 다시 만들기)")]
        public void BakeLayout()
        {
#if UNITY_EDITOR
            HudUiKit.ClearChildren(transform);
            var root = (RectTransform)transform;
            HudUiKit.Stretch(root);

            progressText = HudUiKit.NewText("Progress", root, "치명 3/8", 22f, progressColor, TextAlignmentOptions.Center, 2f);
            progressText.fontStyle = FontStyles.Bold;
            progressText.outlineWidth = 0.2f;
            progressText.outlineColor = new Color32(0, 0, 0, 220);
            progressText.raycastTarget = false;
            HudUiKit.Place(progressText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 34f), new Vector2(0.5f, 0f));

            tip = HudUiKit.NewRect("RuleTip", root);
            HudUiKit.Place(tip, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 90f), new Vector2(0.5f, 0f));
            Image back = tip.gameObject.AddComponent<Image>();
            back.color = new Color(0.02f, 0.06f, 0.09f, 0.94f);
            back.raycastTarget = false;
            var layout = tip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 12, 12);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            var fit = tip.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image edge = HudUiKit.NewImage("Edge", tip, null, new Color(1f, 0.82f, 0.3f, 0.7f));
            edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            edge.raycastTarget = false;
            edge.rectTransform.anchorMin = new Vector2(0f, 0f);
            edge.rectTransform.anchorMax = new Vector2(0f, 1f);
            edge.rectTransform.pivot = new Vector2(0f, 0.5f);
            edge.rectTransform.sizeDelta = new Vector2(2f, 0f);
            edge.rectTransform.anchoredPosition = Vector2.zero;

            tipText = HudUiKit.NewText("Text", tip, "치명 연쇄\n규칙 설명", 19f, HudUiKit.Value, TextAlignmentOptions.TopLeft);
            tipText.lineSpacing = 6f;
            tipText.richText = true;
            tipText.textWrappingMode = TextWrappingModes.Normal;
            tipText.raycastTarget = false;
            var element = tipText.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 360f;
            tip.gameObject.SetActive(false);
#endif
        }
    }
}
