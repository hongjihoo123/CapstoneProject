using System.IO;
using Assets.Members.HJH._02.Scripts.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HJH.EditorTools
{
    public static class SettingsMenuBake
    {
        private const string PrefabPath = "Assets/Members/HJH/05.Data/Prefab/UI/SettingsMenu.prefab";
        private const string PlayerRigPath = "Assets/Members/HJH/05.Data/Prefab/PlayerRig.prefab";
        private const string MenuScene = "Assets/Members/HJH/01.Scene/MainMenu.unity";
        private const string BackupFolder = "Assets/Members/HJH/Backups";

        [MenuItem("HJH/UI/설정 창 만들기 (프리팹 + 메인 메뉴·PlayerRig 에 놓기)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Settings] 플레이 중에는 만들지 않습니다.");
                return;
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogWarning("[Settings] 저장 안 된 씬이 있어서 멈춤: " + SceneManager.GetSceneAt(i).path);
                    return;
                }
            }

            string original = SceneManager.GetActiveScene().path;
            try
            {
                Backup();
                BakePrefab();
                PlaceInPlayerRig();
                PlaceInMainMenu();
                AssetDatabase.SaveAssets();
                Debug.Log("[Settings] DONE");
            }
            finally
            {
                if (!string.IsNullOrEmpty(original) && File.Exists(original))
                    EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            }
        }

        private static void Backup()
        {
            Directory.CreateDirectory(BackupFolder);
            string stamp = System.DateTime.Now.ToString("MMdd_HHmm");
            foreach (string path in new[] { PlayerRigPath, MenuScene })
                File.Copy(path, $"{BackupFolder}/{Path.GetFileNameWithoutExtension(path)}_before_settings_{stamp}{Path.GetExtension(path)}.bak", true);
        }

        private static void BakePrefab()
        {
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("SettingsMenu");
            try
            {
                SettingsMenu menu = root.GetComponent<SettingsMenu>();
                if (menu == null)
                    menu = root.AddComponent<SettingsMenu>();
                menu.BakeLayout();
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (exists)
                    PrefabUtility.UnloadPrefabContents(root);
                else
                    Object.DestroyImmediate(root);
            }
        }

        private static void PlaceInPlayerRig()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerRigPath);
            try
            {
                if (root.GetComponentInChildren<SettingsMenu>(true) != null)
                    return;
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerRigPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void PlaceInMainMenu()
        {
            Scene scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
            MainMenuScreen screen = Object.FindFirstObjectByType<MainMenuScreen>(FindObjectsInactive.Include);
            if (screen == null)
            {
                Debug.LogWarning("[Settings] MainMenuScreen 이 없음: " + MenuScene);
                return;
            }

            SettingsMenu menu = Object.FindFirstObjectByType<SettingsMenu>(FindObjectsInactive.Include);
            if (menu == null)
                menu = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene))
                    .GetComponent<SettingsMenu>();

            for (int i = screen.OnSettings.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(screen.OnSettings, i);
            UnityEventTools.AddPersistentListener(screen.OnSettings, menu.Open);
            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
