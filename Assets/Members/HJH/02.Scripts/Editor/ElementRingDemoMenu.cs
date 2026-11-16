using System.IO;
using Members.HJH.Test;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HJH.EditorTools
{
    // 속성 링 셰이더 확인용 데모 씬(카메라 + 데모 오브젝트 하나)을 만든다. 기존 씬은 건드리지 않음.
    public static class ElementRingDemoMenu
    {
        public const string ScenePath = "Assets/Members/HJH/01.Scene/UI_ElementRingDemo.unity";

        [MenuItem("HJH/UI/속성 링 데모 씬 만들기")]
        public static void BuildScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[ElementRingDemo] 플레이 중에는 만들 수 없습니다.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera", typeof(Camera));
            cam.tag = "MainCamera";
            var camera = cam.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.04f, 0.08f);

            new GameObject("ElementRingDemo", typeof(ElementRingDemo));

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[ElementRingDemo] 씬 저장: " + ScenePath);
        }
    }
}
