using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEditor;

namespace HJH.EditorTools
{
    public static class TelegraphDemoMenu
    {
        private const string MenuPath = "HJH/디버그/조준·적 예고 데모 (플레이 시)";

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            bool on = !SessionState.GetBool(TelegraphDemo.SessionKey, false);
            SessionState.SetBool(TelegraphDemo.SessionKey, on);
            UnityEngine.Debug.Log($"[TelegraphDemo] {(on ? "켜짐" : "꺼짐")} - 다음 플레이부터 적용");
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, SessionState.GetBool(TelegraphDemo.SessionKey, false));
            return true;
        }
    }
}
