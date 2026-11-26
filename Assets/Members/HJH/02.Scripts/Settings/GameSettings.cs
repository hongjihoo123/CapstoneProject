using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Settings
{
    public enum ScreenModeOption
    {
        Fullscreen,
        Borderless,
        Windowed
    }

    public struct DisplayOptions
    {
        public ScreenModeOption Mode;
        public Vector2Int Size;
        public bool VSync;
        public int FpsCap;
    }

    public static class GameSettings
    {
        private const string ModeKey = "Screen.Mode";
        private const string WidthKey = "Screen.Width";
        private const string HeightKey = "Screen.Height";
        private const string VSyncKey = "Screen.VSync";
        private const string FpsKey = "Screen.FpsCap";

        public static readonly Vector2Int DefaultSize = new(1920, 1080);

        public static readonly Vector2Int[] Sizes =
        {
            new(2560, 1600),
            new(2048, 1536),
            new(1920, 1200),
            DefaultSize,
        };

        public static readonly int[] FpsCaps = { 30, 60, 120, 144, -1 };

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
