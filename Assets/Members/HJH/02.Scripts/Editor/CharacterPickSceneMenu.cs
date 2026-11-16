using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Assets.Members.HJH._02.Scripts.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HJH.EditorTools
{
    // 캐릭터 픽 화면 씬(CharacterPick)을 만들고 빌드 설정 맨 앞에 넣는다. 기존 씬은 건드리지 않음.
    // 씬에는 카메라와 CharacterPickScreen 하나뿐 (화면은 실행할 때 코드로 그려짐).
    // 캐릭터 3명은 Char1_Gunner / Char2_Chain / Char3_RuneMage 에셋에서 가져온다.
    public static class CharacterPickSceneMenu
    {
        public const string ScenePath = "Assets/Members/HJH/01.Scene/CharacterPick.unity";
        private static readonly string[] RosterPaths =
        {
            "Assets/Members/HJH/05.Data/Character/Char1_Gunner.asset",
            "Assets/Members/HJH/05.Data/Character/Char2_Chain.asset",
            "Assets/Members/HJH/05.Data/Character/Char3_RuneMage.asset",
        };

        [MenuItem("HJH/캐릭터/픽 화면 씬 만들기")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[CharacterPick] 플레이 중에는 만들 수 없습니다.");
                return;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogWarning("[CharacterPick] 저장 안 된 장면이 있어 멈춤. 저장한 뒤 다시 실행하세요.");
                    return;
                }
            }

            string original = SceneManager.GetActiveScene().path;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera", typeof(Camera));
            cam.tag = "MainCamera";
            var camera = cam.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.04f, 0.08f);

            var go = new GameObject("CharacterPickScreen", typeof(CharacterPickScreen));
            var roster = RosterPaths.Select(p => AssetDatabase.LoadAssetAtPath<CharacterData>(p)).Where(c => c != null).ToArray();
            var so = new SerializedObject(go.GetComponent<CharacterPickScreen>());
            SerializedProperty list = so.FindProperty("roster");
            list.arraySize = roster.Length;
            for (int i = 0; i < roster.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = roster[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);

            AddToBuildSettingsFirst(ScenePath);

            if (!string.IsNullOrEmpty(original) && original != ScenePath)
                EditorSceneManager.OpenScene(original, OpenSceneMode.Single);

            Debug.Log($"[CharacterPick] 씬 저장: {ScenePath} (캐릭터 {roster.Length}명, 빌드 설정 맨 앞)");
        }

        private static void AddToBuildSettingsFirst(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
