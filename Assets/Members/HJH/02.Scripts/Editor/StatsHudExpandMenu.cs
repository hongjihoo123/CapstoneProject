using UnityEditor;

namespace Assets.Members.HJH._02.Scripts.Editor
{
    // 스탯 HUD 를 Ctrl 없이 펼친 채로 두는 캡처/확인용 스위치 (StatsMiniHud 가 EditorPrefs 를 읽음)
    internal static class StatsHudExpandMenu
    {
        private const string Key = "HJH.StatsHud.ForceExpand";

        [MenuItem("HJH/UI/스탯 HUD 펼침 강제 켜기")]
        private static void On() => EditorPrefs.SetBool(Key, true);

        [MenuItem("HJH/UI/스탯 HUD 펼침 강제 끄기")]
        private static void Off() => EditorPrefs.SetBool(Key, false);
    }
}
