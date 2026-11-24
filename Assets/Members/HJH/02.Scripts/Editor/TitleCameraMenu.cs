using System.Linq;
using Assets.Members.HJH._02.Scripts.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HJH.EditorTools
{
    // 타이틀 카메라를 에디터에서 직접 맞추기 위한 메뉴 (MainMenu 가 안 열려 있어도 앵커 맞추기는 알아서 열고 닫음).
    //   배경 장면 같이 열기 : MainMenu 를 연 상태에서 실행 → TitleBackdrop 이 하이어라키에 함께 열려서 Main Camera 를 옮기면 Game 뷰에 바로 보임.
    //                         (플레이하면 이미 열려 있는 배경은 다시 읽지 않음. 저장할 땐 MainMenu 의 Main Camera 만 저장하면 됨)
    //   (SnapCamera) 앵커 자리로 : MainMenu 의 Main Camera 를 TitleBackdrop 의 TitleCameraAnchor 자리로 한 번 맞춤 (처음 잡을 때용)
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

        // 'MainMenu 의 Main Camera 를 TitleBackdrop 의 TitleCameraAnchor 자리로 맞추고 저장' ("처음부터 다시 만들기" 가 호출)
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
