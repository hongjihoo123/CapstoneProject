using System.IO;
using System.Linq;
using Assets.Members.HJH._02.Scripts.Tutorial;
using Assets.Members.HJH._02.Scripts.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HJH.EditorTools
{
    // 튜토리얼 만들기 메뉴
    //   기본 튜토리얼 에셋 만들기 : TutorialDefaults 의 순서를 에셋으로 저장 (Resources/HJH/Tutorial).
    //                               단계 하나 = 에셋 하나(Steps 폴더) → 인스펙터에서 문구·순서를 고치거나 새 단계를 끼워 넣으면 됨.
    //                               이미 있으면 덮어쓰지 않음 (고친 내용 보호). 다시 만들려면 폴더를 지우고 실행
    //   메인 화면 씬 만들기       : MainMenu 씬(카메라 + MainMenuScreen)을 만들고 빌드 설정 맨 앞에 넣음
    //   자동 진행 데모            : 다음 플레이에서 메인 화면 → 픽 → 스테이지 클리어까지 튜토리얼을 입력 없이 진행 (확인용)
    public static class TutorialMenu
    {
        private const string Folder = "Assets/Members/HJH/05.Data/Resources/HJH/Tutorial";
        private const string StepFolder = Folder + "/Steps";
        private const string TargetPrefab = "Assets/Members/JJH/03_Datas/02_Prefabs/Enemies/Slime_Enemy.prefab";
        public const string MainMenuPath = "Assets/Members/HJH/01.Scene/MainMenu.unity";
        private const string AutoFlag = "Captures/_tutorial_auto.txt";

        [MenuItem("HJH/튜토리얼/기본 튜토리얼 에셋 만들기")]
        public static void BuildAssets()
        {
            Directory.CreateDirectory(StepFolder);
            var target = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefab);
            if (target == null)
                Debug.LogWarning("[Tutorial] 연습용 적 프리팹을 못 찾음: " + TargetPrefab + " (데미지 단계는 적 없이 진행)");

            Save(TutorialDefaults.CreatePick(), TutorialDefaults.PickName);
            Save(TutorialDefaults.CreateStage(target), TutorialDefaults.StageName);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void Save(TutorialSequence sequence, string name)
        {
            string path = $"{Folder}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<TutorialSequence>(path) != null)
            {
                Debug.Log($"[Tutorial] 이미 있음, 그대로 둠: {path}");
                return;
            }

            // 단계부터 각각 에셋으로 저장 → 순서 에셋이 그 에셋들을 가리킴
            var so = new SerializedObject(sequence);
            SerializedProperty steps = so.FindProperty("steps");
            for (int i = 0; i < steps.arraySize; i++)
            {
                var step = (TutorialStep)steps.GetArrayElementAtIndex(i).objectReferenceValue;
                string stepPath = AssetDatabase.GenerateUniqueAssetPath($"{StepFolder}/{name}_{i + 1:00}_{Safe(step.Title)}.asset");
                AssetDatabase.CreateAsset(step, stepPath);
            }

            AssetDatabase.CreateAsset(sequence, path);
            Debug.Log($"[Tutorial] 저장: {path} (단계 {steps.arraySize}개)");
        }

        private static string Safe(string text)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                text = text.Replace(c, '_');
            return text.Replace(' ', '_');
        }

        [MenuItem("HJH/튜토리얼/메인 화면 씬 만들기")]
        public static void BuildMainMenuScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Tutorial] 플레이 중에는 만들 수 없습니다.");
                return;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogWarning("[Tutorial] 저장 안 된 장면이 있어 멈춤. 저장한 뒤 다시 실행하세요.");
                    return;
                }
            }

            string original = SceneManager.GetActiveScene().path;
            if (!File.Exists(MainMenuPath))
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cam = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
                var camera = cam.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.03f, 0.045f, 0.085f);
                new GameObject("MainMenuScreen", typeof(MainMenuScreen));
                EditorSceneManager.SaveScene(scene, MainMenuPath);
                Debug.Log("[Tutorial] 씬 저장: " + MainMenuPath);
            }

            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path == MainMenuPath);
            scenes.Insert(0, new EditorBuildSettingsScene(MainMenuPath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[Tutorial] 빌드 설정 맨 앞: MainMenu → " + string.Join(", ", scenes.Skip(1).Take(2).Select(s => Path.GetFileNameWithoutExtension(s.path))));

            if (!string.IsNullOrEmpty(original) && original != MainMenuPath && File.Exists(original))
                EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
        }

        [MenuItem("HJH/튜토리얼/자동 진행 데모 (다음 플레이 한 번)")]
        public static void ArmAutoPilot()
        {
            Directory.CreateDirectory("Captures");
            File.WriteAllText(AutoFlag, "on");
            Debug.Log("[Tutorial] 다음 플레이에서 자동 진행 (MainMenu 씬에서 시작). 결과: Captures/_tutorial_auto_log.txt");
        }

        [MenuItem("HJH/튜토리얼/완료 기록 지우기")]
        public static void ClearDone()
        {
            PlayerPrefs.DeleteKey("HJH.Tutorial.Done");
            Debug.Log("[Tutorial] 완료 기록 지움");
        }
    }
}
