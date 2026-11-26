using Assets.Members.HJH._02.Scripts.UI;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    public sealed class TutorialView : MonoBehaviour, IUiBake
    {
        private const float CardWidth = 600f;
        private static readonly Color Glass = new(0.02f, 0.035f, 0.07f, 0.86f);

        private TutorialContext _ctx;

        [SerializeField] private RectTransform _root;
        [SerializeField] private Image _dim;
        [SerializeField] private RectTransform _card;
        [SerializeField] private CanvasGroup _cardGroup;
        [SerializeField] private TMP_Text _tag, _count, _title, _body, _hint, _progress, _skip;
        [SerializeField] private Image _divider;
        [SerializeField] private RectTransform _continue;
        [SerializeField] private Image _skipFill;
        [SerializeField] private RectTransform _highlight;
        [SerializeField] private CanvasGroup _highlightGroup;
        [SerializeField] private RectTransform _marker;
        [SerializeField] private RectTransform _edgeArrow;
        [SerializeField] private TMP_Text _edgeDistance;

        private bool _full;
        private RectTransform _anchor;
        private Transform _worldTarget;
        private bool _guideMode;

        public static TutorialView Attach(TutorialContext ctx)
        {
            TutorialView view = FindFirstObjectByType<TutorialView>(FindObjectsInactive.Include);
            if (view == null)
            {
                Debug.LogError("[TutorialView] 씬에 TutorialView 가 없습니다 (픽 화면 / PlayerRig 에 꺼진 채로 있어야 함). HJH/UI 메뉴로 굽기.");
                return null;
            }
            view._ctx = ctx;
            view._cardGroup.alpha = 0f;
            view._highlightGroup.alpha = 0f;
            view.gameObject.SetActive(true);
            HudUiKit.EnsureEventSystem();
            return view;
        }

        public void Detach()
        {
            Hide();
            SetPaused(false);
            _ctx = null;
            if (_guidePoint != null)
                Destroy(_guidePoint.gameObject);
            gameObject.SetActive(false);
        }

        public void OnContinueClicked() => _ctx?.Input.Click();

        [ContextMenu("레이아웃 굽기 (자식 다시 만들기)")]
        public void BakeLayout()
        {
#if UNITY_EDITOR
            HudUiKit.ClearChildren(transform);
            var canvas = GetComponent<Canvas>() ? GetComponent<Canvas>() : gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 900;
            var scaler = GetComponent<CanvasScaler>() ? GetComponent<CanvasScaler>() : gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
            _root = (RectTransform)transform;

            _dim = HudUiKit.NewImage("Dim", _root, null, new Color(0f, 0.005f, 0.02f, 0f));
            HudUiKit.Stretch(_dim.rectTransform);

            BuildHighlight();
            BuildMarker();
            BuildCard();
            gameObject.SetActive(false);
#endif
        }

#if UNITY_EDITOR

        private void BuildCard()
        {
            _card = HudUiKit.NewRect("Card", _root);
            HudUiKit.Place(_card, new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(CardWidth, 230f), new Vector2(0.5f, 1f));
            _cardGroup = _card.gameObject.AddComponent<CanvasGroup>();
            _cardGroup.alpha = 0f;

            Image glow = HudUiKit.NewImage("Glow", _card, HudUiKit.Load("soft_glow"), new Color(0.3f, 0.6f, 1f, 0.08f));
            HudUiKit.Stretch(glow.rectTransform, -60f);
            Image panel = HudUiKit.NewImage("Panel", _card, HudUiKit.Load("panel_9slice"), Color.white, Image.Type.Sliced);
            HudUiKit.Stretch(panel.rectTransform);
            Image back = HudUiKit.NewImage("Glass", _card, null, Glass);
            HudUiKit.Stretch(back.rectTransform, 2f);
            back.transform.SetSiblingIndex(panel.transform.GetSiblingIndex());

            Image eye = HudUiKit.NewImage("Eye", _card, HudUiKit.Load("eye_icon"), Color.white);
            HudUiKit.Place(eye.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -16f), new Vector2(46f, 19f));

            _tag = HudUiKit.NewText("Tag", _card, "TUTORIAL", 15f, HudUiKit.Cyan, TextAlignmentOptions.MidlineLeft, 6f);
            HudUiKit.Place(_tag.rectTransform, new Vector2(0f, 1f), new Vector2(76f, -12f), new Vector2(300f, 26f));

            _count = HudUiKit.NewText("Count", _card, "", 16f, HudUiKit.Gold, TextAlignmentOptions.MidlineRight, 2f);
            HudUiKit.Place(_count.rectTransform, new Vector2(1f, 1f), new Vector2(-22f, -12f), new Vector2(64f, 26f));

            _title = HudUiKit.NewText("Title", _card, "", 30f, HudUiKit.Title, TextAlignmentOptions.MidlineLeft, 3f);
            _title.richText = true;
            HudUiKit.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -44f), new Vector2(CardWidth - 44f, 42f));

            _progress = HudUiKit.NewText("Progress", _card, "", 18f, HudUiKit.Cyan, TextAlignmentOptions.MidlineRight, 1f);
            _progress.richText = true;
            HudUiKit.Place(_progress.rectTransform, new Vector2(1f, 0f), new Vector2(-22f, 12f), new Vector2(360f, 30f));

            _divider = HudUiKit.NewImage("Line", _card, HudUiKit.Load("divider"), new Color(1f, 1f, 1f, 0.45f));
            HudUiKit.Place(_divider.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -88f), new Vector2(CardWidth - 44f, 12f));

            _body = HudUiKit.NewText("Body", _card, "", 20f, HudUiKit.Label, TextAlignmentOptions.TopLeft);
            _body.richText = true;
            _body.textWrappingMode = TextWrappingModes.Normal;
            _body.lineSpacing = 8f;
            HudUiKit.Place(_body.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -106f), new Vector2(CardWidth - 44f, 80f));

            _hint = HudUiKit.NewText("Hint", _card, "", 19f, HudUiKit.Title, TextAlignmentOptions.MidlineRight, 2f);
            _hint.richText = true;
            HudUiKit.Place(_hint.rectTransform, new Vector2(1f, 0f), new Vector2(-196f, 16f), new Vector2(380f, 36f));

            Image button = HudUiKit.NewImage("Continue", _card, HudUiKit.Load("button_hover"), Color.white);
            HudUiKit.Place(button.rectTransform, new Vector2(1f, 0f), new Vector2(-18f, 12f), new Vector2(160f, 44f));
            button.raycastTarget = true;
            TMP_Text label = HudUiKit.NewText("Label", button.transform, "계속", 20f, HudUiKit.Title, TextAlignmentOptions.Center, 4f);
            HudUiKit.Stretch(label.rectTransform);
            var click = button.gameObject.AddComponent<Button>();
            click.targetGraphic = button;
            click.transition = Selectable.Transition.None;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(click.onClick, OnContinueClicked);
            _continue = button.rectTransform;

            _skip = HudUiKit.NewText("Skip", _card, "<color=#D6B878>[Tab]</color> 건너뛰기 · 길게: 종료", 14f,
                new Color(HudUiKit.Label.r, HudUiKit.Label.g, HudUiKit.Label.b, 0.75f), TextAlignmentOptions.MidlineRight);
            _skip.richText = true;
            HudUiKit.Place(_skip.rectTransform, new Vector2(1f, 1f), new Vector2(-96f, -12f), new Vector2(240f, 26f));

            Image skipBack = HudUiKit.NewImage("SkipBar", _card, null, new Color(1f, 1f, 1f, 0.08f));
            HudUiKit.Place(skipBack.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(CardWidth, 3f));
            _skipFill = HudUiKit.NewImage("Fill", skipBack.transform, null, HudUiKit.Gold);
            HudUiKit.Stretch(_skipFill.rectTransform);
            _skipFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        }

        private void BuildHighlight()
        {
            _highlight = HudUiKit.NewRect("Highlight", _root);
            _highlight.pivot = new Vector2(0.5f, 0.5f);
            _highlight.anchorMin = _highlight.anchorMax = Vector2.zero;
            _highlightGroup = _highlight.gameObject.AddComponent<CanvasGroup>();
            _highlightGroup.alpha = 0f;

            Image glow = HudUiKit.NewImage("Glow", _highlight, HudUiKit.Load("soft_glow"), new Color(HudUiKit.Gold.r, HudUiKit.Gold.g, HudUiKit.Gold.b, 0.22f));
            HudUiKit.Stretch(glow.rectTransform, -40f);

            const float t = 2f;
            Line(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, t));
            Line(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, t));
            Line(new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(t, 0f));
            Line(new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(t, 0f));
            foreach (Vector2 corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
            {
                Image h = HudUiKit.NewImage("CornerH", _highlight, null, HudUiKit.Gold);
                HudUiKit.Place(h.rectTransform, corner, Vector2.zero, new Vector2(22f, 4f), corner);
                Image v = HudUiKit.NewImage("CornerV", _highlight, null, HudUiKit.Gold);
                HudUiKit.Place(v.rectTransform, corner, Vector2.zero, new Vector2(4f, 22f), corner);
            }
        }

        private void Line(Vector2 min, Vector2 max, Vector2 thickness)
        {
            Image line = HudUiKit.NewImage("Edge", _highlight, null, new Color(HudUiKit.Gold.r, HudUiKit.Gold.g, HudUiKit.Gold.b, 0.85f));
            RectTransform r = line.rectTransform;
            r.anchorMin = min;
            r.anchorMax = max;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = thickness;
            r.anchoredPosition = Vector2.zero;
        }

        private void BuildMarker()
        {
            _marker = HudUiKit.NewRect("TargetMarker", _root);
            _marker.anchorMin = _marker.anchorMax = Vector2.zero;
            _marker.sizeDelta = new Vector2(90f, 90f);
            Image ring = HudUiKit.NewImage("Ring", _marker, HudUiKit.Load("slot_circle_frame"), HudUiKit.Gold);
            HudUiKit.Stretch(ring.rectTransform);
            Image arrow = HudUiKit.NewImage("Arrow", _marker, null, HudUiKit.Gold);
            HudUiKit.Place(arrow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 26f), new Vector2(16f, 16f), new Vector2(0.5f, 0.5f));
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            TMP_Text label = HudUiKit.NewText("Label", _marker, "목표", 16f, HudUiKit.Gold, TextAlignmentOptions.Center, 3f);
            HudUiKit.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 54f), new Vector2(120f, 24f));
            _marker.gameObject.SetActive(false);

            _edgeArrow = HudUiKit.NewRect("EdgeArrow", _root);
            _edgeArrow.anchorMin = _edgeArrow.anchorMax = Vector2.zero;
            _edgeArrow.sizeDelta = new Vector2(60f, 60f);
            foreach (float sign in new[] { 1f, -1f })
            {
                Image bar = HudUiKit.NewImage("Chevron", _edgeArrow, null, HudUiKit.Gold);
                HudUiKit.Place(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, sign * 9f), new Vector2(30f, 5f));
                bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -sign * 38f);
            }
            _edgeDistance = HudUiKit.NewText("Distance", _root, "", 16f, HudUiKit.Gold, TextAlignmentOptions.Center, 1f);
            _edgeDistance.rectTransform.anchorMin = _edgeDistance.rectTransform.anchorMax = Vector2.zero;
            _edgeDistance.rectTransform.sizeDelta = new Vector2(100f, 24f);
            _edgeArrow.gameObject.SetActive(false);
            _edgeDistance.gameObject.SetActive(false);
        }

#endif

        public void ShowFull(TutorialStep step, bool waiting = true)
        {
            _full = waiting;
            SetTexts(step);
            _body.gameObject.SetActive(true);
            _divider.gameObject.SetActive(true);
            _hint.gameObject.SetActive(waiting);
            _continue.gameObject.SetActive(waiting);
            _hint.text = waiting ? step.ResumeHint(_ctx) : "";
            _progress.text = "";

            float bodyHeight = Mathf.Max(30f, _body.GetPreferredValues(_body.text, CardWidth - 44f, 0f).y);
            _body.rectTransform.sizeDelta = new Vector2(CardWidth - 44f, bodyHeight);
            _card.sizeDelta = new Vector2(CardWidth, 106f + bodyHeight + (waiting ? 74f : 46f));

            PopIn();
        }

        public void ShowObjective(TutorialStep step, bool pop)
        {
            _full = false;
            SetTexts(step);
            _body.gameObject.SetActive(false);
            _divider.gameObject.SetActive(false);
            _hint.gameObject.SetActive(false);
            _continue.gameObject.SetActive(false);
            _card.sizeDelta = new Vector2(CardWidth, 156f);

            _hint.gameObject.SetActive(true);
            _hint.alignment = TextAlignmentOptions.MidlineLeft;
            _hint.rectTransform.anchorMin = _hint.rectTransform.anchorMax = new Vector2(0f, 0f);
            _hint.rectTransform.pivot = new Vector2(0f, 0f);
            _hint.rectTransform.anchoredPosition = new Vector2(22f, 40f);
            _hint.rectTransform.sizeDelta = new Vector2(CardWidth - 44f, 30f);
            _hint.fontSize = 18f;
            _hint.color = HudUiKit.Label;
            _hint.text = FirstLine(step.BodyText(_ctx));

            if (pop)
                PopIn();
        }

        public void DockBottom(float y)
        {
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0f);
            _card.pivot = new Vector2(0.5f, 0f);
            _card.anchoredPosition = new Vector2(0f, y);
        }

        public void SetProgress(string text)
        {
            text ??= "";
            if (_progress.text != text)
                _progress.text = text;
        }

        public void SetPaused(bool paused)
        {
            _dim.DOKill();
            _dim.DOFade(paused ? 0.42f : 0f, 0.2f).SetUpdate(true);
        }

        public void SetAnchor(RectTransform anchor) => _anchor = anchor;

        public void SetWorldTarget(Transform target, bool guide)
        {
            _worldTarget = target;
            _guideMode = guide;
        }

        public void SetGuidePoint(Vector3? point)
        {
            if (point.HasValue)
            {
                if (_guidePoint == null)
                    _guidePoint = new GameObject("TutorialGuidePoint").transform;
                _guidePoint.position = point.Value;
                _worldTarget = _guidePoint;
                _guideMode = true;
            }
            else if (_worldTarget == _guidePoint)
            {
                _worldTarget = null;
            }
        }

        private Transform _guidePoint;

        public void SetSkipFill(float fill) =>
            _skipFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fill), 1f);

        public void FlashDone()
        {
            _tag.text = "COMPLETE";
            _tag.color = HudUiKit.Gold;
            _card.DOKill(true);
            _card.DOPunchScale(Vector3.one * 0.04f, 0.3f, 6, 0.6f).SetUpdate(true);
        }

        public void Hide()
        {
            _cardGroup.DOKill();
            _cardGroup.DOFade(0f, 0.2f).SetUpdate(true);
            _anchor = null;
            _worldTarget = null;
        }

        private void LateUpdate()
        {
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f);
            UpdateHighlight(pulse);
            UpdateMarker(pulse);

            if (_full && _hint != null)
                _hint.alpha = 0.6f + 0.4f * pulse;
        }

        private void UpdateHighlight(float pulse)
        {
            if (_anchor == null || !_anchor.gameObject.activeInHierarchy)
            {
                _highlightGroup.alpha = Mathf.MoveTowards(_highlightGroup.alpha, 0f, Time.unscaledDeltaTime * 6f);
                return;
            }

            Canvas source = _anchor.GetComponentInParent<Canvas>();
            Camera cam = source != null && source.renderMode != RenderMode.ScreenSpaceOverlay ? source.worldCamera : null;
            var corners = new Vector3[4];
            _anchor.GetWorldCorners(corners);
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, corner);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out Vector2 local);
                local += _root.rect.size * 0.5f;
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            const float pad = 10f;
            _highlight.anchoredPosition = (min + max) * 0.5f;
            _highlight.sizeDelta = max - min + Vector2.one * pad * 2f;
            _highlight.SetAsLastSibling();
            _card.SetAsLastSibling();
            _highlightGroup.alpha = Mathf.MoveTowards(_highlightGroup.alpha, 0.5f + 0.5f * pulse, Time.unscaledDeltaTime * 8f);
        }

        private void UpdateMarker(float pulse)
        {
            Camera cam = Camera.main;
            bool show = _worldTarget != null && cam != null;
            if (!show)
            {
                _marker.gameObject.SetActive(false);
                _edgeArrow.gameObject.SetActive(false);
                _edgeDistance.gameObject.SetActive(false);
                return;
            }

            Vector3 world = _worldTarget.position + Vector3.up * 1.2f;
            Vector3 screen = cam.WorldToScreenPoint(world);
            Vector2 size = _root.rect.size;
            float scale = size.x / Mathf.Max(1f, Screen.width);
            Vector2 pos = new Vector2(screen.x, screen.y) * scale;
            bool inFront = screen.z > 0f;
            bool onScreen = inFront && pos.x > 60f && pos.x < size.x - 60f && pos.y > 60f && pos.y < size.y - 60f;

            _marker.gameObject.SetActive(onScreen);
            if (onScreen)
            {
                _marker.anchoredPosition = pos;
                _marker.localScale = Vector3.one * (0.9f + 0.1f * pulse);
            }

            bool edge = !onScreen && _guideMode;
            _edgeArrow.gameObject.SetActive(edge);
            _edgeDistance.gameObject.SetActive(edge);
            if (!edge)
                return;

            Vector2 center = size * 0.5f;
            Vector2 dir = pos - center;
            if (!inFront)
                dir = -dir;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector2.up;
            dir.Normalize();

            float margin = 70f;
            float tx = dir.x != 0f ? (center.x - margin) / Mathf.Abs(dir.x) : float.MaxValue;
            float ty = dir.y != 0f ? (center.y - margin) / Mathf.Abs(dir.y) : float.MaxValue;
            Vector2 edgePos = center + dir * Mathf.Min(tx, ty);
            _edgeArrow.anchoredPosition = edgePos;
            _edgeArrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            _edgeArrow.localScale = Vector3.one * (0.9f + 0.15f * pulse);
            _edgeDistance.rectTransform.anchoredPosition = edgePos - dir * 46f;

            float meters = _ctx.Player != null ? Vector3.Distance(_ctx.Player.transform.position, _worldTarget.position) : 0f;
            _edgeDistance.text = $"{meters:0}m";
        }

        private void SetTexts(TutorialStep step)
        {
            _tag.text = "TUTORIAL";
            _tag.color = HudUiKit.Cyan;
            _count.text = $"{_ctx.StepIndex + 1} / {_ctx.StepCount}";
            _title.text = step.TitleText(_ctx);
            _body.text = step.BodyText(_ctx);

            _hint.alignment = TextAlignmentOptions.MidlineRight;
            _hint.rectTransform.anchorMin = _hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            _hint.rectTransform.pivot = new Vector2(1f, 0f);
            _hint.rectTransform.anchoredPosition = new Vector2(-196f, 16f);
            _hint.rectTransform.sizeDelta = new Vector2(380f, 36f);
            _hint.fontSize = 19f;
            _hint.color = HudUiKit.Title;
        }

        private void PopIn()
        {
            _cardGroup.DOKill();
            _card.DOKill(true);
            _cardGroup.alpha = 0f;
            _cardGroup.DOFade(1f, 0.22f).SetUpdate(true);
            _card.localScale = Vector3.one * 0.96f;
            _card.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            int n = text.IndexOf('\n');
            return n < 0 ? text : text.Substring(0, n);
        }

        private void OnDestroy()
        {
            if (_guidePoint != null)
                Destroy(_guidePoint.gameObject);
            DOTween.Kill(_card);
            DOTween.Kill(_cardGroup);
            DOTween.Kill(_dim);
        }
    }
}
