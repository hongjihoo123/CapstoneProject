using System.Linq;
using Assets.Members.HJH._02.Scripts.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HJH.EditorTools
{
    public static class TitleCameraMenu
    {
        [MenuItem("HJH/UI/타이틀/배경 장면 같이 열기 (카메라 맞출 때)")]
        public static void OpenBackdrop()
        {
            if (EditorApplication.isPlaying)
                return;
            if (!SceneManager.GetSceneByPath(TitleBackdropMenu.BackdropPath).isLoaded)
                EditorSceneManager.OpenScene(TitleBackdropMenu.BackdropPath, OpenSceneMode.Additive);
        }

        public static void SnapCamera()
        {
            if (EditorApplication.isPlaying)
                return;
            Scene menu = SceneManager.GetSceneByPath(TitleBackdropMenu.MainMenuPath);
            bool openedMenu = !menu.isLoaded;
            if (openedMenu)
                menu = EditorSceneManager.OpenScene(TitleBackdropMenu.MainMenuPath, OpenSceneMode.Additive);

            Scene backdrop = SceneManager.GetSceneByPath(TitleBackdropMenu.BackdropPath);
            bool opened = !backdrop.isLoaded;
            if (opened)
                backdrop = EditorSceneManager.OpenScene(TitleBackdropMenu.BackdropPath, OpenSceneMode.Additive);

            TitleCameraAnchor anchor = backdrop.GetRootGameObjects().Select(r => r.GetComponentInChildren<TitleCameraAnchor>(true)).FirstOrDefault(a => a != null);
            Camera cam = menu.GetRootGameObjects().Select(r => r.GetComponentInChildren<Camera>(true)).FirstOrDefault(c => c != null && c.CompareTag("MainCamera"));
            if (anchor != null && cam != null)
            {
                Undo.RecordObject(cam.transform, "타이틀 카메라 맞추기");
                cam.transform.SetPositionAndRotation(anchor.transform.position, anchor.transform.rotation);
                cam.fieldOfView = anchor.FieldOfView;
                cam.clearFlags = CameraClearFlags.Skybox;
                EditorSceneManager.MarkSceneDirty(menu);
                EditorSceneManager.SaveScene(menu);
                Debug.Log("[TitleCamera] Main Camera 를 앵커 자리로 맞추고 저장함");
            }
            else
                Debug.LogWarning("[TitleCamera] 앵커 또는 Main Camera 를 찾지 못함");

            if (opened)
                EditorSceneManager.CloseScene(backdrop, true);
            if (openedMenu)
                EditorSceneManager.CloseScene(menu, true);
        }
    }
}
