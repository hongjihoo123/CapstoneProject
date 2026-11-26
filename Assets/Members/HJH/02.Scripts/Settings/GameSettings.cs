using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Settings
{
    public enum ScreenModeOption
    {
        Fullscreen,   // 전체 화면 (모니터를 독점)
        Borderless,   // 전체 창 화면 (테두리 없는 창이 화면을 덮음. Alt+Tab 이 빠름)
        Windowed      // 창 화면
    }

    // 화면 설정 한 벌 (설정 창에서 고르고 "적용" 하면 저장 + 반영)
    public struct DisplayOptions
    {
        public ScreenModeOption Mode;
        public Vector2Int Size;
        public bool VSync;
        public int FpsCap;          // -1 = 제한 없음
    }

    // 화면 설정의 저장·적용 (소리 음량은 SoundManager 가 저장·적용).
    // 처음 켜면 1920 x 1080 + 전체 창 화면. 프레임은 저장한 적이 없으면 FrameRateCap 의 60
    public static class GameSettings
    {
        private const string ModeKey = "Screen.Mode";
        private const string WidthKey = "Screen.Width";
        private const string HeightKey = "Screen.Height";
        private const string VSyncKey = "Screen.VSync";
        private const string FpsKey = "Screen.FpsCap";

        public static readonly Vector2Int DefaultSize = new(1920, 1080);

        // 고를 수 있는 해상도 (화면 비율은 해상도에 따라 정해짐)
        public static readonly Vector2Int[] Sizes =
        {
            new(2560, 1600),
            new(2048, 1536),
            new(1920, 1200),
            DefaultSize,
        };

        public static readonly int[] FpsCaps = { 30, 60, 120, 144, -1 };

        // 저장된 값. 없으면 기본값
        public static DisplayOptions Current()
        {
            DisplayOptions defaults = Defaults();
            var size = new Vector2Int(PlayerPrefs.GetInt(WidthKey, defaults.Size.x), PlayerPrefs.GetInt(HeightKey, defaults.Size.y));
            return new DisplayOptions
            {
                Mode = (ScreenModeOption)PlayerPrefs.GetInt(ModeKey, (int)defaults.Mode),
                Size = System.Array.IndexOf(Sizes, size) >= 0 ? size : DefaultSize,
                VSync = PlayerPrefs.GetInt(VSyncKey, defaults.VSync ? 1 : 0) != 0,
                FpsCap = PlayerPrefs.GetInt(FpsKey, defaults.FpsCap),
            };
        }

        public static DisplayOptions Defaults() => new()
        {
            Mode = ScreenModeOption.Borderless,
            Size = DefaultSize,
            VSync = false,
            FpsCap = Char.FrameRateCap.DefaultFps,
        };

        public static void SaveAndApply(DisplayOptions options)
        {
            PlayerPrefs.SetInt(ModeKey, (int)options.Mode);
            PlayerPrefs.SetInt(WidthKey, options.Size.x);
            PlayerPrefs.SetInt(HeightKey, options.Size.y);
            PlayerPrefs.SetInt(VSyncKey, options.VSync ? 1 : 0);
            PlayerPrefs.SetInt(FpsKey, options.FpsCap);
            PlayerPrefs.Save();
            ApplyScreen(options);
            ApplyFrameRate(options);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySaved()
        {
            DisplayOptions options = Current();
            ApplyScreen(options);
            if (PlayerPrefs.HasKey(FpsKey))
                ApplyFrameRate(options);
        }

        private static void ApplyScreen(DisplayOptions options) =>
            Screen.SetResolution(options.Size.x, options.Size.y, ToUnity(options.Mode));

        private static void ApplyFrameRate(DisplayOptions options)
        {
            // 수직동기화가 켜지면 targetFrameRate 는 무시됨
            QualitySettings.vSyncCount = options.VSync ? 1 : 0;
            Application.targetFrameRate = options.FpsCap;
        }

        private static FullScreenMode ToUnity(ScreenModeOption mode) => mode switch
        {
            ScreenModeOption.Fullscreen => FullScreenMode.ExclusiveFullScreen,
            ScreenModeOption.Windowed => FullScreenMode.Windowed,
            _ => FullScreenMode.FullScreenWindow,
        };
    }
}
