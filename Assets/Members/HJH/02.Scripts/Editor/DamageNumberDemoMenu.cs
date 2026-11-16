using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEditor;

namespace HJH.EditorTools
{
    // 데미지 숫자 샘플 데모 켜고 끄기 (다음 플레이부터 적용, 에디터 세션 동안만 기억)
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
