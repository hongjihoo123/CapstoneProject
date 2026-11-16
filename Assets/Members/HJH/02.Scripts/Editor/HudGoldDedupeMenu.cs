using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HJH.EditorTools
{
    // _HJH_Map 에는 RunGold 가 장면에 직접 하나 + PlayerRig 안에 하나, 총 두 개라서 골드 표시가 겹쳐 보였음.
    // 장면 쪽 예전 골드 판(GoldPlate)을 꺼서 새 GoldHud 만 보이게 한다. RunGold 오브젝트(골드 지급)는 지우지 않음.
    // 하기 전에 장면을 Backups 에 복사해 둔다. 저장 안 된 변경이 있으면 아무것도 안 하고 멈춤.
    public static class HudGoldDedupeMenu
    {
        private const string ScenePath = "Assets/Members/HJH/01.Scene/_HJH_Map.unity";
        private const string BackupPath = "Assets/Members/HJH/Backups/_HJH_Map_before_gold_hud.unity";

        [MenuItem("HJH/UI/_HJH_Map 예전 골드 표시 끄기")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[GoldDedupe] 플레이 중에는 할 수 없습니다.");
                return;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogWarning("[GoldDedupe] 저장 안 된 장면이 있어 멈춤. 저장한 뒤 다시 실행하세요.");
                    return;
                }
            }

            string original = SceneManager.GetActiveScene().path;
            if (!File.Exists(BackupPath))
                File.Copy(ScenePath, BackupPath);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int hidden = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "GoldPlate" && t.gameObject.activeSelf)
                    {
                        t.gameObject.SetActive(false);
                        hidden++;
                    }
                }
            }

            if (hidden > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (!string.IsNullOrEmpty(original) && original != ScenePath)
                EditorSceneManager.OpenScene(original, OpenSceneMode.Single);

            Debug.Log($"[GoldDedupe] 꺼둔 GoldPlate {hidden}개 (백업: {BackupPath})");
        }
    }
}
