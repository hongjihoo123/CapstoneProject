using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HJH.EditorTools
{
    // 타이틀 배경으로 쓸 맵 장면 만들기: Map_Bridge_Normal 을 복사해서 플레이어/적 스포너/포탈/내비메시 등 게임 요소를 뺀 TitleBackdrop.unity.
    // 원본 맵은 건드리지 않음. 카메라 자리는 TitleCameraAnchor 오브젝트(씬에서 옮기면 구도가 바뀜).
    // 이어서 UI 장식(TitleDecorMenu)과 카메라 맞추기(TitleCameraMenu.SnapCamera)까지 한 번에 실행.
    public static class TitleBackdropMenu
    {
        public const string MainMenuPath = "Assets/Members/HJH/01.Scene/MainMenu.unity";
        public const string BackdropPath = "Assets/Members/HJH/01.Scene/TitleBackdrop.unity";
        private const string SourcePath = "Assets/Members/HJH/01.Scene/Map_Bridge_Normal.unity";
        private static readonly string[] RemovePrefixes =
        {
            "PlayerRig", "Portal", "TestEnemySpawner", "NavMesh", "CharacterHighlighter", "altar", "Altar",
        };

        
        [MenuItem("HJH/UI/타이틀/처음부터 다시 만들기 (맵 복사 + UI + 카메라, 손본 것 전부 초기화)")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[TitleBackdrop] 플레이 중에는 실행하지 않음");
                return;
            }

            AssetDatabase.DeleteAsset(BackdropPath);
            if (!AssetDatabase.CopyAsset(SourcePath, BackdropPath))
            {
                Debug.LogError("[TitleBackdrop] 맵 복사 실패");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(BackdropPath, OpenSceneMode.Additive);
            try
            {
                var removed = new List<string>();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (string prefix in RemovePrefixes)
                        if (root.name.StartsWith(prefix))
                        {
                            removed.Add(root.name);
                            Object.DestroyImmediate(root);
                            break;
                        }
                }

                // 폭포 정면 먼 곳 (캡처로 고른 구도: 대상 기준 +18,+16,-34 에서 (0,3,0) 쪽)
                Transform fall = Find(scene, "폭포");
                if (fall != null)
                {
                    Vector3 pos = fall.position + new Vector3(20f, 17f, -37f);
                    Vector3 look = fall.position + new Vector3(-14f, 9f, -8f);
                    var anchor = new GameObject("TitleCameraAnchor", typeof(TitleCameraAnchor));
                    SceneManager.MoveGameObjectToScene(anchor, scene);
                    anchor.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
                }
                else
                    Debug.LogWarning("[TitleBackdrop] '폭포' 를 찾지 못함 — TitleCameraAnchor 를 직접 놓아야 함");

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[TitleBackdrop] 완료. 뺀 것: {string.Join(", ", removed)}");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            AddToBuildSettings(BackdropPath);
            TitleDecorMenu.Decorate();
            TitleCameraMenu.SnapCamera();
        }

        private static Transform Find(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name)
                        return t;
            return null;
        }

        private static void AddToBuildSettings(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (list.Exists(s => s.path == path))
                return;
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
