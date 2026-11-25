using Assets.Members.HJH._02.Scripts.UI;
using UnityEditor;
using UnityEngine;

namespace HJH.EditorTools
{
    // StatsMiniHud 의 Rows 를 코드에서 바꾸면(예: 치명타 줄 추가) PlayerRig 프리팹의 구운 줄이 옛것이라 안 보임.
    // 스크립트가 다시 컴파일될 때 구운 줄이 코드와 다르면 STATS 창만 자동으로 다시 굽는다 (다른 HUD 부품은 건드리지 않음).
    // 수동: HJH/UI/STATS 창만 굽기
    [InitializeOnLoad]
    public static class StatsHudAutoBake
    {
        private const string PlayerRigPath = "Assets/Members/HJH/05.Data/Prefab/PlayerRig.prefab";

        static StatsHudAutoBake() => EditorApplication.delayCall += () => Run(false);

        [MenuItem("HJH/UI/STATS 창만 굽기 (PlayerRig)")]
        private static void Manual() => Run(true);

        private static void Run(bool force)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRigPath) == null)
                return;

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerRigPath);
            try
            {
                bool dirty = false;
                StatsMiniHud stats = root.GetComponentInChildren<StatsMiniHud>(true);
                if (stats != null && (force || stats.IsBakeStale))
                {
                    stats.BakeLayout();
                    dirty = true;
                    Debug.Log("[StatsHudAutoBake] PlayerRig 의 STATS 창을 다시 구웠습니다 (치명타 줄 등).");
                }

                // 궁극기 초기화 규칙 설명/진행도: PlayerRig 에 없으면 한 번 만들어서 굽는다
                if (stats != null && root.GetComponentInChildren<UltChainHint>(true) == null)
                {
                    Canvas canvas = stats.GetComponentInParent<Canvas>(true);
                    Transform hud = canvas != null ? canvas.rootCanvas.transform : root.transform;
                    var go = new GameObject("UltChainHint", typeof(RectTransform));
                    go.transform.SetParent(hud, false);
                    go.AddComponent<UltChainHint>().BakeLayout();
                    dirty = true;
                    Debug.Log("[StatsHudAutoBake] PlayerRig 에 궁극기 초기화 안내(UltChainHint)를 추가했습니다.");
                }

                if (!dirty)
                    return;
                PrefabUtility.SaveAsPrefabAsset(root, PlayerRigPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
