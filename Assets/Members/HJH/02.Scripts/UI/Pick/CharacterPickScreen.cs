using Assets.Members.HJH._02.Scripts.Char.Character;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 캐릭터 픽 화면. 캐릭터 3명의 카드가 나오고, 하나를 눌러 고른 뒤 "이 캐릭터로 시작"을 누르면
    //   고른 캐릭터를 CharacterSelection 에 기억시키고 → 런 상태를 정리하고 → 다음 씬으로 넘어감.
    // 이후 모든 씬의 플레이어는 이 캐릭터로 시작함 (죽을 때까지). 키보드: 1~3 고르기, Enter 시작.
    // 화면은 코드로 그려서, 씬에는 이 컴포넌트 하나와 캐릭터 목록만 있으면 됨. 초상화(CharacterData.Portrait)를 채우면 카드에 나옴.
    public class CharacterPickScreen : MonoBehaviour
    {
        [SerializeField] private CharacterData[] roster;
        [SerializeField, Tooltip("캐릭터를 고른 뒤 넘어갈 씬 (빌드 설정에 있어야 함)")]
        private string nextScene = "Map_Bridge_Normal";
        [SerializeField, Min(0.05f)] private float fadeTime = 0.5f;

        private sealed class Card
        {
            public RectTransform root;
            public Image panel;
            public Image glow;
            public CanvasGroup group;
        }

        private readonly System.Collections.Generic.List<Card> _cards = new();
        private Button _confirm;
        private CanvasGroup _confirmGroup;
        private Image _fade;
        private int _selected = -1;
        private bool _busy;

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Transform canvas = BuildCanvas();
            BuildTexts(canvas);
            BuildCards(canvas);
            BuildConfirm(canvas);

            _fade = HudUiKit.NewImage("Fade", canvas, null, new Color(0f, 0f, 0f, 1f));
            HudUiKit.Stretch(_fade.rectTransform);
            _fade.raycastTarget = true;
            _fade.DOFade(0f, fadeTime).SetUpdate(true).OnComplete(() => _fade.raycastTarget = false);

            RefreshCards(animate: false);
        }

        private void Update()
        {
            if (_busy || Keyboard.current == null)
                return;

            for (int i = 0; i < _cards.Count && i < 9; i++)
            {
                if (Keyboard.current[Key.Digit1 + i].wasPressedThisFrame)
                    Select(i);
            }

            if (_selected >= 0 && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
                Confirm();
        }

        // ---------------------------------------------------------------- 화면 만들기

        private Transform BuildCanvas()
        {
            var go = new GameObject("PickCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 위는 짙은 남색, 아래는 더 어둡게
            var tex = new Texture2D(1, 64) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
                tex.SetPixel(0, y, Color.Lerp(new Color(0.02f, 0.03f, 0.06f), new Color(0.07f, 0.1f, 0.17f), y / 63f));
            tex.Apply();
            var bg = new GameObject("Background", typeof(RectTransform), typeof(RawImage));
            bg.transform.SetParent(go.transform, false);
            HudUiKit.Stretch((RectTransform)bg.transform);
            bg.GetComponent<RawImage>().texture = tex;

            Image glow = HudUiKit.NewImage("BackGlow", go.transform, HudUiKit.Load("soft_glow"), new Color(0.3f, 0.6f, 1f, 0.22f));
            HudUiKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(2200f, 1100f));

            HudUiKit.EnsureEventSystem();

            return go.transform;
        }

        private void BuildTexts(Transform canvas)
        {
            TMP_Text title = HudUiKit.NewText("Title", canvas, "캐릭터 선택", 54f, HudUiKit.Title, TextAlignmentOptions.Center, 8f);
            HudUiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(900f, 80f));

            Image line = HudUiKit.NewImage("TitleLine", canvas, HudUiKit.Load("divider"), new Color(1f, 1f, 1f, 0.7f));
            HudUiKit.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(520f, 18f));

            TMP_Text sub = HudUiKit.NewText("Sub", canvas, "함께할 영웅을 고르세요. 쓰러지기 전까지 이 캐릭터로 모험합니다.", 22f, HudUiKit.Label, TextAlignmentOptions.Center);
            HudUiKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -176f), new Vector2(1100f, 36f));

            if (roster == null || roster.Length == 0)
            {
                TMP_Text warn = HudUiKit.NewText("Warn", canvas, "캐릭터 목록이 비어 있습니다 (CharacterPickScreen > Roster)", 26f, new Color(1f, 0.5f, 0.4f), TextAlignmentOptions.Center);
                HudUiKit.Place(warn.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 60f));
            }
        }

        private void BuildCards(Transform canvas)
        {
            if (roster == null)
                return;

            const float width = 440f, height = 600f, gap = 56f;
            int n = roster.Length;
            float start = -(n - 1) * (width + gap) * 0.5f;

            for (int i = 0; i < n; i++)
            {
                CharacterData data = roster[i];
                if (data == null)
                    continue;

                int index = i;
                Color theme = data.ThemeColor;

                RectTransform root = HudUiKit.NewRect("Card_" + i, canvas);
                HudUiKit.Place(root, new Vector2(0.5f, 0.5f), new Vector2(start + i * (width + gap), 10f), new Vector2(width, height));
                var group = root.gameObject.AddComponent<CanvasGroup>();

                Image glow = HudUiKit.NewImage("Glow", root, HudUiKit.Load("soft_glow"), new Color(theme.r, theme.g, theme.b, 0f));
                HudUiKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width + 360f, height + 300f));

                Image panel = HudUiKit.NewImage("Panel", root, HudUiKit.Load("panel_9slice"), Color.white, Image.Type.Sliced);
                HudUiKit.Stretch(panel.rectTransform);
                panel.raycastTarget = true;

                // 초상화: 있으면 그 그림, 없으면 테마색 원판 + 이름 첫 글자
                Image disc = HudUiKit.NewImage("PortraitBack", root, HudUiKit.Load("slot_circle_bg"), new Color(theme.r * 0.45f, theme.g * 0.45f, theme.b * 0.45f, 1f));
                HudUiKit.Place(disc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(230f, 230f));
                if (data.Portrait != null)
                {
                    Image portrait = HudUiKit.NewImage("Portrait", root, data.Portrait, Color.white);
                    portrait.preserveAspect = true;
                    HudUiKit.Place(portrait.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(190f, 190f));
                }
                else
                {
                    string initial = string.IsNullOrEmpty(data.DisplayName) ? "?" : data.DisplayName.Substring(0, 1);
                    TMP_Text letter = HudUiKit.NewText("Initial", root, initial, 100f, theme, TextAlignmentOptions.Center);
                    HudUiKit.Place(letter.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(200f, 150f));
                }
                Image ring = HudUiKit.NewImage("PortraitRing", root, HudUiKit.Load("slot_circle_frame"), theme);
                HudUiKit.Place(ring.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(230f, 230f));

                TMP_Text nameText = HudUiKit.NewText("Name", root, data.DisplayName, 38f, HudUiKit.Title, TextAlignmentOptions.Center, 2f);
                HudUiKit.Place(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -296f), new Vector2(width - 40f, 54f));

                TMP_Text role = HudUiKit.NewText("Role", root, data.Role, 18f, theme, TextAlignmentOptions.Center);
                HudUiKit.Place(role.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Vector2(width - 40f, 32f));

                Image divider = HudUiKit.NewImage("Divider", root, HudUiKit.Load("divider"), new Color(1f, 1f, 1f, 0.5f));
                HudUiKit.Place(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -372f), new Vector2(width - 80f, 16f));

                TMP_Text desc = HudUiKit.NewText("Description", root, data.Description, 19f, HudUiKit.Label, TextAlignmentOptions.Top);
                desc.textWrappingMode = TextWrappingModes.Normal;
                desc.overflowMode = TextOverflowModes.Ellipsis;
                desc.lineSpacing = 6f;
                HudUiKit.Place(desc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -392f), new Vector2(width - 70f, 170f));

                TMP_Text key = HudUiKit.NewText("Key", root, $"[ {i + 1} ]", 17f, HudUiKit.Gold, TextAlignmentOptions.Center, 2f);
                HudUiKit.Place(key.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(200f, 28f));

                var button = root.gameObject.AddComponent<Button>();
                button.targetGraphic = panel;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => Select(index));
                root.gameObject.AddComponent<HoverScale>();

                _cards.Add(new Card { root = root, panel = panel, glow = glow, group = group });
            }
        }

        private void BuildConfirm(Transform canvas)
        {
            Image back = HudUiKit.NewImage("Confirm", canvas, HudUiKit.Load("button_hover"), Color.white);
            HudUiKit.Place(back.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(480f, 72f));
            back.raycastTarget = true;
            _confirmGroup = back.gameObject.AddComponent<CanvasGroup>();

            TMP_Text label = HudUiKit.NewText("Label", back.transform, "이 캐릭터로 시작", 28f, HudUiKit.Title, TextAlignmentOptions.Center, 2f);
            HudUiKit.Stretch(label.rectTransform);

            _confirm = back.gameObject.AddComponent<Button>();
            _confirm.targetGraphic = back;
            _confirm.transition = Selectable.Transition.None;
            _confirm.onClick.AddListener(Confirm);
            back.gameObject.AddComponent<HoverScale>();
        }

        // ---------------------------------------------------------------- 선택 / 시작

        // 테스트 도구용: 마우스 없이 고르고 시작까지
        public void DebugPick(int index)
        {
            Select(index);
            Confirm();
        }

        private void Select(int index)
        {
            if (_busy || index < 0 || index >= _cards.Count)
                return;

            _selected = index;
            RefreshCards(animate: true);
        }

        private void RefreshCards(bool animate)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                Card card = _cards[i];
                bool selected = i == _selected;
                bool dimmed = _selected >= 0 && !selected;
                Color theme = roster[i].ThemeColor;

                float scale = selected ? 1.05f : 1f;
                float alpha = dimmed ? 0.6f : 1f;
                Color glow = new(theme.r, theme.g, theme.b, selected ? 0.55f : 0f);
                Color panel = selected ? Color.Lerp(Color.white, theme, 0.3f) : Color.white;

                card.root.DOKill();
                card.glow.DOKill();
                if (animate)
                {
                    card.root.DOScale(scale, 0.18f).SetEase(Ease.OutQuad).SetUpdate(true);
                    card.glow.DOColor(glow, 0.2f).SetUpdate(true);
                }
                else
                {
                    card.root.localScale = Vector3.one * scale;
                    card.glow.color = glow;
                }

                card.group.alpha = alpha;
                card.panel.color = panel;
            }

            if (_confirmGroup != null)
            {
                _confirmGroup.alpha = _selected >= 0 ? 1f : 0.35f;
                _confirm.interactable = _selected >= 0;
            }
        }

        private void Confirm()
        {
            if (_busy || _selected < 0)
                return;

            _busy = true;
            CharacterSelection.Pick(_selected);
            RunReset.Begin();

            _fade.raycastTarget = true;
            _fade.DOFade(1f, fadeTime).SetUpdate(true).OnComplete(() => SceneManager.LoadScene(nextScene));
        }

        // 마우스를 올리면 살짝 커지는 효과 (선택된 카드/버튼의 기본 크기 위에 곱함)
        private sealed class HoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            private Vector3 _base = Vector3.one;
            private bool _hover;

            private void Update()
            {
                // 다른 쪽(선택 연출)이 크기를 바꾸는 중이 아닐 때만 호버 보정
                if (_hover && transform.localScale.x < 1.04f && !DOTween.IsTweening(transform))
                    transform.localScale = Vector3.Lerp(transform.localScale, _base * 1.03f, Time.unscaledDeltaTime * 14f);
                else if (!_hover && transform.localScale.x > 1.0f && transform.localScale.x < 1.04f && !DOTween.IsTweening(transform))
                    transform.localScale = Vector3.Lerp(transform.localScale, _base, Time.unscaledDeltaTime * 14f);
            }

            public void OnPointerEnter(PointerEventData eventData) => _hover = true;
            public void OnPointerExit(PointerEventData eventData) => _hover = false;
        }
    }
}
