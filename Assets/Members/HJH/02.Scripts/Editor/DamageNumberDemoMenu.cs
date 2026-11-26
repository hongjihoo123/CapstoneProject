using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEditor;

namespace HJH.EditorTools
{
    public static class DamageNumberDemoMenu
    {
        private const string MenuPath = "HJH/디버그/데미지 숫자 샘플 (플레이 시)";

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            bool on = !SessionState.GetBool(DamageNumberDemo.SessionKey, false);
            SessionState.SetBool(DamageNumberDemo.SessionKey, on);
            UnityEngine.Debug.Log($"[DamageNumberDemo] {(on ? "켜짐" : "꺼짐")} - 다음 플레이부터 적용");
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, SessionState.GetBool(DamageNumberDemo.SessionKey, false));
            return true;
        }
    }
}
