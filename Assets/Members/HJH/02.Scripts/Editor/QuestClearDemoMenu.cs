using Assets.Members.HJH._02.Scripts.Quest;
using UnityEditor;

namespace HJH.EditorTools
{
    // 적 자동 처치 데모 켜고 끄기 (다음 플레이부터 적용, 에디터 세션 동안만 기억)
    public static class QuestClearDemoMenu
    {
        private const string MenuPath = "HJH/디버그/적 자동 처치 데모 (플레이 시)";

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            bool on = !SessionState.GetBool(QuestClearDemo.SessionKey, false);
            SessionState.SetBool(QuestClearDemo.SessionKey, on);
            UnityEngine.Debug.Log($"[QuestClearDemo] {(on ? "켜짐" : "꺼짐")} - 다음 플레이부터 적용");
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, SessionState.GetBool(QuestClearDemo.SessionKey, false));
            return true;
        }
    }
}
