using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HJH.EditorTools
{
    // PlayerRig 안에 들어 있는 옛 능력치 창(KYR PlayerStatPanel, Tab) 을 PlayerRig 쪽에서만 꺼 둔다.
    // KYR 프리팹/스크립트는 그대로 두고 PlayerRig 의 오버라이드(꺼짐)만 저장 → 새 STATS(Ctrl 펼침) 로 대체
    internal static class HideOldStatPanelMenu
    {
        private const string PlayerRigPath = "Assets/Members/HJH/05.Data/Prefab/PlayerRig.prefab";

        [MenuItem("HJH/UI/PlayerRig 옛 능력치 창 끄기")]
        private static void Hide() => SetActive(false);

        [MenuItem("HJH/UI/PlayerRig 옛 능력치 창 다시 켜기")]
        private static void Show() => SetActive(true);

        private static void SetActive(bool active)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[StatPanel] 플레이 중에는 안 함");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerRigPath);
            try
            {
                var panels = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "PlayerStatPanel").ToArray();
                foreach (Transform t in panels)
                    t.gameObject.SetActive(active);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerRigPath);
                Debug.Log($"[StatPanel] PlayerStatPanel {panels.Length}개 active={active}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
