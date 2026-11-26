using System;
using Assets.Members.HJH._02.Scripts.Char.Audio;
using Assets.Members.HJH._02.Scripts.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    public class SettingsMenu : MonoBehaviour, IUiBake
    {
        [Serializable]
        public sealed class VolumeRow
        {
            public Slider slider;
            public TMP_Text value;
        }

        [SerializeField, Tooltip("끄면 ESC 로는 안 열림")] private bool openWithEscape = true;

        [Header("구워 둔 화면 (HJH/UI/설정 창 만들기)")]
        [SerializeField] private GameObject window;
        [SerializeField] private TMP_Text modeText;
        [SerializeField] private TMP_Text sizeText;
        [SerializeField] private TMP_Text vsyncText;
        [SerializeField] private TMP_Text fpsText;
        [SerializeField] private TMP_Text noticeText;
        [SerializeField] private VolumeRow master;
        [SerializeField] private VolumeRow effects;
        [SerializeField] private VolumeRow ambience;
        [SerializeField] private VolumeRow music;

        private static SettingsMenu _open;

        private DisplayOptions _pending;
        private float _timeScaleBefore = 1f;
        private CursorLockMode _lockBefore;
        private bool _blocked;

        public bool IsOpen => _open == this;

        private void Start()
        {
            window.SetActive(false);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;

            if (IsOpen)
                Close();
            else if (openWithEscape && !_blocked)
                Open();
        }

        private void LateUpdate()
        {
            _blocked = Time.timeScale <= 0f || UiFocusService.IsUiFocused;
        }

        public void Open()
        {
            if (_open != null)
                return;

            _open = this;
            _timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;
            _lockBefore = Cursor.lockState;
            HudUiKit.EnsureEventSystem();
            UiFocusService.Acquire(this);

            _pending = GameSettings.Current();
            window.SetActive(true);
            Load(master, SoundManager.GetMasterBusVolume());
            Load(effects, SoundManager.GetBusVolume(SoundBus.Sfx));
            Load(ambience, SoundManager.GetBusVolume(SoundBus.Ambience));
            Load(music, SoundManager.GetBusVolume(SoundBus.Music));
            Refresh(Application.isEditor ? "에디터에서는 화면 모드·해상도가 안 바뀝니다 (빌드에서 확인)" : string.Empty);
        }

        public void Close()
        {
            if (_open != this)
                return;

            _open = null;
            window.SetActive(false);
            Time.timeScale = _timeScaleBefore;
            UiFocusService.Release(this);
            Cursor.lockState = _lockBefore;
            CursorService.Refresh();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            PlayerPrefs.Save();
        }

        private void OnDisable() => Close();

        public void StepMode(int step)
        {
            _pending.Mode = (ScreenModeOption)Wrap((int)_pending.Mode + step, 3);
            Refresh(null);
        }

        public void StepSize(int step)
        {
            int index = Array.IndexOf(GameSettings.Sizes, _pending.Size);
            _pending.Size = GameSettings.Sizes[Wrap(index + step, GameSettings.Sizes.Length)];
            Refresh(null);
        }

        public void StepVSync(int step)
        {
            _pending.VSync = !_pending.VSync;
            Refresh(null);
        }

        public void StepFps(int step)
        {
            int index = Array.IndexOf(GameSettings.FpsCaps, _pending.FpsCap);
            _pending.FpsCap = GameSettings.FpsCaps[Wrap((index < 0 ? 1 : index) + step, GameSettings.FpsCaps.Length)];
            Refresh(null);
        }

        public void Apply()
        {
            GameSettings.SaveAndApply(_pending);
            Refresh("적용했습니다");
        }

        public void ResetToDefaults()
        {
            _pending = GameSettings.Defaults();
            master.slider.value = 1f;
            effects.slider.value = 1f;
            ambience.slider.value = 1f;
            music.slider.value = 1f;
            GameSettings.SaveAndApply(_pending);
            Refresh("기본값으로 되돌렸습니다");
        }

        public void SetMaster(float value)
        {
            SoundManager.SetMasterBusVolume(value);
            Show(master, value);
        }

        public void SetEffects(float value)
        {
            SoundManager.SetBusVolume(SoundBus.Sfx, value);
            SoundManager.SetBusVolume(SoundBus.Enemy, value);
            SoundManager.SetBusVolume(SoundBus.Ui, value);
            Show(effects, value);
        }

        public void SetAmbience(float value)
        {
            SoundManager.SetBusVolume(SoundBus.Ambience, value);
            Show(ambience, value);
        }

        public void SetMusic(float value)
        {
            SoundManager.SetBusVolume(SoundBus.Music, value);
            Show(music, value);
        }

        private static void Load(VolumeRow row, float value)
        {
            row.slider.SetValueWithoutNotify(value);
            Show(row, value);
        }

        private static void Show(VolumeRow row, float value) => row.value.text = Mathf.RoundToInt(value * 100f) + "%";

        private void Refresh(string notice)
        {
            modeText.text = _pending.Mode switch
            {
                ScreenModeOption.Fullscreen => "전체 화면",
                ScreenModeOption.Windowed => "창 화면",
                _ => "전체 창 화면",
            };
            sizeText.text = $"{_pending.Size.x} x {_pending.Size.y}";
            vsyncText.text = _pending.VSync ? "켬" : "끔";
            fpsText.text = _pending.VSync ? "모니터에 맞춤" : _pending.FpsCap < 0 ? "제한 없음" : _pending.FpsCap + " FPS";
            noticeText.text = notice ?? "\"적용\" 을 눌러야 화면 설정이 바뀝니다";
        }

        private static int Wrap(int value, int count) => ((value % count) + count) % count;

        [ContextMenu("레이아웃 굽기 (자식 다시 만들기)")]
        public void BakeLayout()
        {
#if UNITY_EDITOR
            HudUiKit.ClearChildren(transform);

            var canvasGo = new GameObject("SettingsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            window = canvasGo;

            Image dim = HudUiKit.NewImage("Dim", canvasGo.transform, null, new Color(0f, 0.01f, 0.03f, 0.75f));
            HudUiKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            var middle = new Vector2(0.5f, 0.5f);
            Image panel = HudUiKit.NewImage("Panel", canvasGo.transform, null, new Color(0.035f, 0.055f, 0.1f, 0.97f));
            HudUiKit.Place(panel.rectTransform, middle, Vector2.zero, new Vector2(920f, 880f));
            Transform root = panel.transform;

            Text(root, "Title", "설정", 48f, HudUiKit.Title, TextAlignmentOptions.Center, 0f, -26f, 920f, 64f);

            Section(root, "화면", -108f);
            modeText = Stepper(root, "Mode", "화면 모드", -170f, StepMode);
            sizeText = Stepper(root, "Size", "해상도", -228f, StepSize);
            vsyncText = Stepper(root, "VSync", "수직동기화", -286f, StepVSync);
            fpsText = Stepper(root, "Fps", "프레임 제한", -344f, StepFps);
            noticeText = Text(root, "Notice", string.Empty, 18f, HudUiKit.Label, TextAlignmentOptions.Center, 0f, -408f, 920f, 28f);

            Section(root, "소리", -456f);
            master = Volume(root, "Master", "전체", -518f, SetMaster);
            effects = Volume(root, "Effects", "효과음", -576f, SetEffects);
            ambience = Volume(root, "Ambience", "환경음", -634f, SetAmbience);
            music = Volume(root, "Music", "배경음악", -692f, SetMusic);

            var bottom = new Vector2(0.5f, 0f);
            Button(root, "Reset", "기본값", bottom, new Vector2(-260f, 40f), new Vector2(220f, 64f), 26f, ResetToDefaults);
            Button(root, "Apply", "적용", bottom, new Vector2(0f, 40f), new Vector2(220f, 64f), 28f, Apply);
            Button(root, "Close", "닫기 (ESC)", bottom, new Vector2(260f, 40f), new Vector2(220f, 64f), 26f, Close);

            canvasGo.SetActive(false);
#endif
        }

#if UNITY_EDITOR
        private static TMP_Text Text(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align, float x, float y, float width, float height)
        {
            TMP_Text label = HudUiKit.NewText(name, parent, text, size, color, align);
            HudUiKit.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, height));
            return label;
        }

        private static void Section(Transform root, string title, float y)
        {
            Text(root, title, title, 30f, HudUiKit.Gold, TextAlignmentOptions.MidlineLeft, 60f, y, 300f, 40f);
            Image line = HudUiKit.NewImage(title + "Line", root, null, new Color(0.84f, 0.72f, 0.47f, 0.35f));
            HudUiKit.Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(60f, y - 44f), new Vector2(800f, 2f));
        }

        private static void Button(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size,
            float fontSize, UnityEngine.Events.UnityAction action)
        {
            Button button = NewButton(parent, name, label, anchor, pos, size, anchor, fontSize);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, action);
        }

        private static Button NewButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size,
            Vector2 pivot, float fontSize)
        {
            Image back = HudUiKit.NewImage(name, parent, null, new Color(0.13f, 0.2f, 0.32f, 1f));
            HudUiKit.Place(back.rectTransform, anchor, pos, size, pivot);
            back.raycastTarget = true;
            TMP_Text text = HudUiKit.NewText("Label", back.transform, label, fontSize, HudUiKit.Title, TextAlignmentOptions.Center);
            HudUiKit.Stretch(text.rectTransform);

            var button = back.gameObject.AddComponent<Button>();
            button.targetGraphic = back;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.5f, 1.5f, 1.5f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            button.colors = colors;
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            return button;
        }

        private static TMP_Text Stepper(Transform root, string name, string label, float y, UnityEngine.Events.UnityAction<int> step)
        {
            Text(root, name + "Label", label, 26f, HudUiKit.Label, TextAlignmentOptions.MidlineLeft, 60f, y, 280f, 48f);
            var topLeft = new Vector2(0f, 1f);
            Button prev = NewButton(root, name + "Prev", "<", topLeft, new Vector2(400f, y), new Vector2(60f, 48f), topLeft, 28f);
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(prev.onClick, step, -1);
            TMP_Text value = Text(root, name + "Value", "-", 26f, HudUiKit.Value, TextAlignmentOptions.Center, 470f, y, 320f, 48f);
            Button next = NewButton(root, name + "Next", ">", topLeft, new Vector2(800f, y), new Vector2(60f, 48f), topLeft, 28f);
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(next.onClick, step, 1);
            return value;
        }

        private static VolumeRow Volume(Transform root, string name, string label, float y, UnityEngine.Events.UnityAction<float> changed)
        {
            Text(root, name + "Label", label, 26f, HudUiKit.Label, TextAlignmentOptions.MidlineLeft, 60f, y, 280f, 48f);

            RectTransform area = HudUiKit.NewRect(name + "Slider", root);
            HudUiKit.Place(area, new Vector2(0f, 1f), new Vector2(400f, y), new Vector2(360f, 48f));
            var hit = area.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            Image back = HudUiKit.NewImage("Background", area, null, new Color(0.13f, 0.2f, 0.32f, 1f));
            Band(back.rectTransform, 10f);

            RectTransform fillArea = HudUiKit.NewRect("FillArea", area);
            Band(fillArea, 10f);
            Image fill = HudUiKit.NewImage("Fill", fillArea, null, HudUiKit.Cyan);
            HudUiKit.Stretch(fill.rectTransform);

            RectTransform handleArea = HudUiKit.NewRect("HandleArea", area);
            Band(handleArea, 32f);
            Image handle = HudUiKit.NewImage("Handle", handleArea, null, HudUiKit.Title);
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(16f, 0f);

            var slider = area.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            var navigation = slider.navigation;
            navigation.mode = Navigation.Mode.None;
            slider.navigation = navigation;
            slider.SetValueWithoutNotify(1f);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(slider.onValueChanged, changed);

            TMP_Text value = Text(root, name + "Value", "100%", 24f, HudUiKit.Value, TextAlignmentOptions.MidlineRight, 770f, y, 90f, 48f);
            return new VolumeRow { slider = slider, value = value };
        }

        private static void Band(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(0f, -height * 0.5f);
            rect.offsetMax = new Vector2(0f, height * 0.5f);
        }
#endif
    }
}
