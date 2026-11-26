using Assets.Members.HJH._02.Scripts.Char.Character;
using UnityEditor;

namespace HJH.EditorTools
{
    public static class PickFlowDemoMenu
    {
        private const string MenuPath = "HJH/디버그/캐릭터 픽 순환 데모 (플레이 시)";

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            bool on = !SessionState.GetBool(PickFlowDemo.SessionKey, false);
            SessionState.SetBool(PickFlowDemo.SessionKey, on);
            UnityEngine.Debug.Log($"[PickFlowDemo] {(on ? "켜짐" : "꺼짐")} - 다음 플레이부터 적용");
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, SessionState.GetBool(PickFlowDemo.SessionKey, false));
            return true;
        }
    }
}
