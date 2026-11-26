using System;
using System.Collections;
using Assets.Members.HJH._02.Scripts.Tutorial;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    public class MainMenuScreen : MonoBehaviour, IUiBake
    {
        [SerializeField] private string gameTitle = "GAME TITLE";
        [SerializeField] private string subtitle = "원소 콤보 로그라이트";
        [SerializeField, Tooltip("픽 화면 씬 (빌드 설정에 있어야 함)")] private string pickScene = "CharacterPick";
        [SerializeField, Tooltip("처음인가요? 에서 '예' 가 튜토리얼 시작이면 켜 둠 (끄면 '아니요' 가 튜토리얼)")]
        private bool yesStartsTutorial = true;
        [SerializeField, Tooltip("켜면 게임을 켤 때 한 번 '처음인가요?' 를 먼저 물음. 끄면 바로 타이틀")] private bool askFirstTime;
        [SerializeField, Min(0.05f)] private float fadeTime = 0.45f;

        [Serializable]
        public sealed class MenuButton
        {
            public Button button;
            public Image back;
            public TMP_Text label;
            [Tooltip("고른 버튼 왼쪽에 켜지는 표시 (없어도 됨)")] public Image marker;
        }

        [Header("구워 둔 화면 (⋮ > 레이아웃 굽기)")]
        [SerializeField] private Image fade;
        [SerializeField] private RectTransform backGlow;
        [SerializeField] private CanvasGroup questionPage;
        [SerializeField] private TMP_Text questionSub;
        [SerializeField] private MenuButton[] questionButtons;
        [SerializeField] private CanvasGroup menuPage;
        [SerializeField] private MenuButton[] menuButtons;
        [SerializeField, Tooltip("고른 버튼 / 나머지 버튼 그림")] private Sprite focusedSprite;
        [SerializeField] private Sprite normalSprite;

        [SerializeField, Tooltip("설정 버튼을 눌렀을 때 (설정 화면 연결용)")] private UnityEngine.Events.UnityEvent onSettings = new();

        [Header("버튼 색 (정글 톤)")]
        [SerializeField] private Color focusedTint = Color.white;
        [SerializeField] private Color normalTint = new(0.75f, 0.85f, 1f, 1f);
        [SerializeField] private Color focusedLabel = new(0.84f, 0.72f, 0.47f, 1f);
        [SerializeField] private Color normalLabel = new(0.91f, 0.94f, 0.97f, 1f);

        private MenuButton[] _buttons = Array.Empty<MenuButton>();
        private bool _onQuestion;
        private int _focus;
        private bool _busy;

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            HudUiKit.EnsureEventSystem();

            questionPage.gameObject.SetActive(false);
            menuPage.gameObject.SetActive(false);
            if (backGlow != null)
                backGlow.DOScale(1.06f, 3f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(backGlow.gameObject);
            if (fade != null)
            {
                fade.color = Color.black;
                fade.raycastTarget = true;
                fade.DOFade(0f, fadeTime).SetUpdate(true).OnComplete(() => fade.raycastTarget = false);
            }

            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return PlayIntro();

            if (askFirstTime && !TutorialSession.AskedThisLaunch)
                ShowQuestion();
            else
                ShowMenu();
        }

        protected virtual IEnumerator PlayIntro()
        {
            yield break;
        }

        private void ShowQuestion()
        {
            string tutorialAnswer = yesStartsTutorial ? "예" : "아니요";
            questionSub.text = $"<color=#D6B878>{tutorialAnswer}</color>를 누르면 튜토리얼을 진행합니다.";
            ShowPage(questionPage, questionButtons, true);
        }

        private void ShowMenu()
        {
            ShowPage(menuPage, menuButtons, false);
        }

        private void ShowPage(CanvasGroup page, MenuButton[] buttons, bool question)
        {
            questionPage.gameObject.SetActive(page == questionPage);
            menuPage.gameObject.SetActive(page == menuPage);
            _onQuestion = question;
            _buttons = buttons ?? Array.Empty<MenuButton>();
            page.alpha = 0f;
            page.DOFade(1f, 0.3f).SetUpdate(true).SetLink(page.gameObject);
            SetFocus(0);
        }

        public void AnswerYes() => Answer(yesStartsTutorial);
        public void AnswerNo() => Answer(!yesStartsTutorial);

        private void Answer(bool startTutorial)
        {
            if (_busy)
                return;
            TutorialSession.AskedThisLaunch = true;
            if (startTutorial)
                StartTutorial();
            else
                ShowMenu();
        }

        public void StartTutorial()
        {
            if (_busy)
                return;
            TutorialSession.AskedThisLaunch = true;
            TutorialSession.Begin();
            Go(pickScene);
        }

        public void StartGame()
        {
            if (_busy)
                return;
            TutorialSession.Abort();
            Go(pickScene);
        }

        public void OpenSettings() => onSettings?.Invoke();

        public UnityEngine.Events.UnityEvent OnSettings => onSettings;

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Go(string scene)
        {
            _busy = true;
            if (fade == null)
            {
                SceneManager.LoadScene(scene);
                return;
            }
            Punch(_focus);
            fade.raycastTarget = true;
            fade.DOFade(1f, fadeTime).SetUpdate(true).OnComplete(() => SceneManager.LoadScene(scene));
        }

        public void DebugAnswer(bool yes) => Answer(yes == yesStartsTutorial);
        public void DebugStartGame() => StartGame();

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (_busy || k == null || _buttons.Length == 0 || UiFocusService.IsUiFocused)
                return;

            if (k.leftArrowKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame)
                SetFocus((_focus + _buttons.Length - 1) % _buttons.Length);
            if (k.rightArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame)
                SetFocus((_focus + 1) % _buttons.Length);
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)
                _buttons[_focus].button.onClick.Invoke();

            if (_onQuestion)
            {
                if (k.yKey.wasPressedThisFrame)
                    Answer(yesStartsTutorial);
                else if (k.nKey.wasPressedThisFrame)
                    Answer(!yesStartsTutorial);
            }
        }

        public void SetFocus(int index)
        {
            if (_buttons.Length == 0)
                return;
            _focus = Mathf.Clamp(index, 0, _buttons.Length - 1);
            for (int i = 0; i < _buttons.Length; i++)
            {
                bool on = i == _focus;
                MenuButton entry = _buttons[i];
                RectTransform root = entry.back.rectTransform;
                root.DOKill();
                root.DOScale(on ? 1.05f : 1f, 0.15f).SetUpdate(true);
                entry.back.sprite = on ? focusedSprite : normalSprite;
                entry.back.color = on ? focusedTint : normalTint;
                entry.label.color = on ? focusedLabel : normalLabel;
                if (entry.marker != null)
                    entry.marker.enabled = on;
            }
        }

        private void Punch(int index)
        {
            if (index < 0 || index >= _buttons.Length)
                return;
            RectTransform root = _buttons[index].back.rectTransform;
            root.DOKill();
            root.localScale = Vector3.one * 1.05f;
            root.DOPunchScale(Vector3.one * -0.08f, 0.2f, 6, 0.6f).SetUpdate(true).SetLink(root.gameObject);
        }

        [ContextMenu("레이아웃 굽기 (자식 다시 만들기)")]
        public void BakeLayout()
        {
#if UNITY_EDITOR
            HudUiKit.ClearChildren(transform);
            focusedSprite = HudUiKit.Load("button_hover");
            normalSprite = HudUiKit.Load("button_normal");

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.03f, 0.045f, 0.085f);
            }

            var canvasGo = new GameObject("MenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.5f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasGo.transform;

            Image glow = HudUiKit.NewImage("BackGlow", root, HudUiKit.Load("soft_glow"), new Color(0.3f, 0.6f, 1f, 0.12f));
            HudUiKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1800f, 1300f));
            backGlow = glow.rectTransform;

            questionPage = NewPage("Question", root);
            TMP_Text title = HudUiKit.NewText("Title", questionPage.transform, "처음인가요?", 60f, HudUiKit.Title, TextAlignmentOptions.Center, 8f);
            HudUiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1000f, 80f));
            Image line = HudUiKit.NewImage("Line", questionPage.transform, HudUiKit.Load("divider"), new Color(1f, 1f, 1f, 0.5f));
            HudUiKit.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -278f), new Vector2(560f, 14f));
            questionSub = HudUiKit.NewText("Sub", questionPage.transform, "<color=#D6B878>예</color>를 누르면 튜토리얼을 진행합니다.", 22f,
                HudUiKit.Label, TextAlignmentOptions.Center, 2f);
            questionSub.richText = true;
            HudUiKit.Place(questionSub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -304f), new Vector2(1000f, 34f));
            var bottom = new Vector2(0.5f, 0f);
            questionButtons = new[]
            {
                NewButton(questionPage.transform, "Yes", "예", bottom, new Vector2(-170f, 170f), new Vector2(300f, 84f), false, AnswerYes, 0),
                NewButton(questionPage.transform, "No", "아니요", bottom, new Vector2(170f, 170f), new Vector2(300f, 84f), false, AnswerNo, 1),
            };

            menuPage = NewPage("Menu", root);
            Transform menu = menuPage.transform;
            var middle = new Vector2(0.5f, 0.5f);
            Image eye = HudUiKit.NewImage("Eye", menu, HudUiKit.Load("eye_icon"), Color.white);
            HudUiKit.Place(eye.rectTransform, middle, new Vector2(0f, 250f), new Vector2(84f, 34f));
            TMP_Text gameName = HudUiKit.NewText("Title", menu, gameTitle, 84f, HudUiKit.Title, TextAlignmentOptions.Center, 14f);
            HudUiKit.Place(gameName.rectTransform, middle, new Vector2(0f, 170f), new Vector2(1400f, 110f));
            Image menuLine = HudUiKit.NewImage("Line", menu, HudUiKit.Load("divider"), new Color(1f, 1f, 1f, 0.5f));
            HudUiKit.Place(menuLine.rectTransform, middle, new Vector2(0f, 104f), new Vector2(640f, 14f));
            TMP_Text sub = HudUiKit.NewText("Sub", menu, subtitle, 22f, HudUiKit.Gold, TextAlignmentOptions.Center, 8f);
            HudUiKit.Place(sub.rectTransform, middle, new Vector2(0f, 76f), new Vector2(800f, 34f));
            menuButtons = new[]
            {
                NewButton(menu, "Start", "게임 시작", middle, new Vector2(0f, -40f), new Vector2(460f, 86f), true, StartGame, 0),
                NewButton(menu, "Tutorial", "튜토리얼", middle, new Vector2(0f, -140f), new Vector2(460f, 64f), false, StartTutorial, 1),
                NewButton(menu, "Quit", "종료", middle, new Vector2(0f, -220f), new Vector2(460f, 64f), false, Quit, 2),
            };
            TMP_Text done = HudUiKit.NewText("Done", menu, "튜토리얼 완료", 16f, HudUiKit.Label, TextAlignmentOptions.Center, 2f);
            HudUiKit.Place(done.rectTransform, middle, new Vector2(0f, -268f), new Vector2(300f, 26f));
            menuPage.gameObject.SetActive(false);

            fade = HudUiKit.NewImage("Fade", root, null, new Color(0f, 0f, 0f, 0f));
            HudUiKit.Stretch(fade.rectTransform);
#endif
        }

#if UNITY_EDITOR
        private static CanvasGroup NewPage(string name, Transform parent)
        {
            RectTransform page = HudUiKit.NewRect(name, parent);
            HudUiKit.Stretch(page);
            return page.gameObject.AddComponent<CanvasGroup>();
        }

        private MenuButton NewButton(Transform page, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, bool primary,
            UnityEngine.Events.UnityAction action, int index)
        {
            Image back = HudUiKit.NewImage(name, page, HudUiKit.Load(primary ? "button_hover" : "button_normal"), Color.white);
            HudUiKit.Place(back.rectTransform, anchor, pos, size, new Vector2(0.5f, 0.5f));
            back.raycastTarget = true;
            TMP_Text text = HudUiKit.NewText("Label", back.transform, label, primary ? 34f : 26f, HudUiKit.Title, TextAlignmentOptions.Center, 6f);
            HudUiKit.Stretch(text.rectTransform);

            var button = back.gameObject.AddComponent<Button>();
            button.targetGraphic = back;
            button.transition = Selectable.Transition.None;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, action);
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(back.gameObject.AddComponent<HoverSelect>().OnEnter, SetFocus, index);
            return new MenuButton { button = button, back = back, label = text };
        }
#endif
    }
}
