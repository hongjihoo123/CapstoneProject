using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 임시 (11-13 반딧불이 작업용). 4개 맵 적용이 끝나면 삭제.
namespace HJH.EditorTools
{
    public static class FireflyTempOps
    {
        private const string HjhMap = "Assets/Members/HJH/01.Scene/_HJH_Map.unity";

        // _HJH_Map 이 혼자 열려 있을 때만 저장 (반딧불이 구역 이동을 저장하려는 용도)
        [MenuItem("HJH/맵/반딧불이/(임시) _HJH_Map 저장")]
        private static void SaveHjhMap()
        {
            if (EditorApplication.isPlaying) return;
            var s = SceneManager.GetActiveScene();
            Debug.Log($"[FireflyTmp] active={s.path} dirty={s.isDirty} count={SceneManager.sceneCount}");
            if (s.path != HjhMap || SceneManager.sceneCount != 1) { Debug.LogWarning("[FireflyTmp] _HJH_Map 하나만 열려 있지 않아 저장 안 함"); return; }
            EditorSceneManager.SaveScene(s);
            Debug.Log("[FireflyTmp] saved " + HjhMap);
        }
    }
}
