using System.IO;
using System.Text;
using Assets.Members.HJH._02.Scripts.Element;
using Assets.Members.HJH._02.Scripts.Quest;
using Assets.Members.HJH._02.Scripts.Tutorial;
using Assets.Members.HJH._02.Scripts.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HJH.EditorTools
{
    // 예전에 실행 중 코드로 만들던 UI 를 한 번 "구워서" 프리팹/씬/PlayerRig 하이어라키에 저장.
    // 구운 뒤에는 폰트·크기·위치를 인스펙터에서 직접 고치면 되고, 이 메뉴를 다시 돌리면 고친 것이 지워지니 주의.
    //   프리팹 6개 (05.Data/Prefab/UI): ComboBindPanel, ComboBindStation, ScreenNotice, PortalNotice, ComboBuffBar, PickComboRow
    //   PlayerRig.prefab : 체력·골드·시간·STATS·목표·사망 화면·콤보 배너 + 효과 바(프리팹) + 튜토리얼 화면(꺼짐)
    //   CharacterPick    : 픽 화면 전체 + 튜토리얼 화면(꺼짐)
    //   MainMenu         : 메인 화면
    //   Map_Shop         : 바인딩 상점을 프리팹으로 바꿈
    // 굽기 전에 씬·프리팹을 Backups 폴더에 복사해 둠
    public static class UiBakeMenu
    {
        private const string PrefabFolder = "Assets/Members/HJH/05.Data/Prefab/UI";
        private const string PlayerRigPath = "Assets/Members/HJH/05.Data/Prefab/PlayerRig.prefab";
        private const string ShopScene = "Assets/Members/HJH/01.Scene/Map_Shop.unity";
        private const string PickScene = "Assets/Members/HJH/01.Scene/CharacterPick.unity";
        private const string MenuScene = "Assets/Members/HJH/01.Scene/MainMenu.unity";
        private const string BackupFolder = "Assets/Members/HJH/Backups";
        private const string LogPath = "Captures/_ui_bake_log.txt";

        private static readonly StringBuilder Log = new();

        [MenuItem("HJH/UI/코드 UI 굽기 (프리팹 6개 + PlayerRig·픽·메인·상점)")]
        public static void BakeAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[UiBake] 플레이 중에는 굽지 않습니다.");
                return;
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogWarning("[UiBake] 저장 안 된 씬이 있어서 멈춤: " + SceneManager.GetSceneAt(i).path);
                    return;
                }
            }

            Log.Clear();
            string original = SceneManager.GetActiveScene().path;
            try
            {
                Backup();
                Directory.CreateDirectory(PrefabFolder);

                BakePrefab<ScreenNotice>("ScreenNotice");
                BakePrefab<ScreenNotice>("PortalNotice");
                BakePrefab<PickComboRowView>("PickComboRow");
                BakePrefab<ComboBindPanel>("ComboBindPanel");
                ComboBuffBarBakeMenu.EnsureStatIconSet();
                BakePrefab<ComboBuffBarView>("ComboBuffBar");

                BakeShop();
                BakePlayerRig();
                BakeScene<CharacterPickScreen>(PickScene, addTutorialView: true);
                BakeScene<MainMenuScreen>(MenuScene, addTutorialView: false);
                AssetDatabase.SaveAssets();
                Write("DONE");
            }
            catch (System.Exception e)
            {
                Write("FAIL " + e);
                Debug.LogException(e);
            }
            finally
            {
                if (!string.IsNullOrEmpty(original) && File.Exists(original))
                    EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            }
        }

        // 픽 화면만 다시 굽기 (스킬 수치 줄처럼 이 화면만 바뀌었을 때). 다른 화면·프리팹은 건드리지 않음
        [MenuItem("HJH/UI/캐릭터 픽 화면만 굽기")]
        public static void BakePickOnly()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[UiBake] 플레이 중에는 굽지 않습니다.");
                return;
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogWarning("[UiBake] 저장 안 된 씬이 있어서 멈춤: " + SceneManager.GetSceneAt(i).path);
                    return;
                }
            }

            Log.Clear();
            string original = SceneManager.GetActiveScene().path;
            try
            {
                Directory.CreateDirectory(BackupFolder);
                string backup = $"{BackupFolder}/CharacterPick_before_pickbake_{System.DateTime.Now:MMdd_HHmm}.unity.bak";
                File.Copy(PickScene, backup, true);
                Write("백업 " + backup);
                BakeScene<CharacterPickScreen>(PickScene, addTutorialView: true);
                AssetDatabase.SaveAssets();
                Write("DONE (픽 화면만)");
            }
            catch (System.Exception e)
            {
                Write("FAIL " + e);
                Debug.LogException(e);
            }
            finally
            {
                if (!string.IsNullOrEmpty(original) && File.Exists(original))
                    EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            }
        }

        private static void Write(string line)
        {
            Log.AppendLine(line);
            Debug.Log("[UiBake] " + line);
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, Log.ToString());
        }

        private static void Backup()
        {
            Directory.CreateDirectory(BackupFolder);
            string stamp = System.DateTime.Now.ToString("MMdd_HHmm");
            foreach (string path in new[] { PlayerRigPath, ShopScene, PickScene, MenuScene })
            {
                if (!File.Exists(path))
                    continue;
                string target = $"{BackupFolder}/{Path.GetFileNameWithoutExtension(path)}_before_uibake_{stamp}{Path.GetExtension(path)}.bak";
                File.Copy(path, target, true);
                Write("백업 " + target);
            }
        }

        // 빈 오브젝트에 컴포넌트를 붙이고 BakeLayout → 프리팹으로 저장
        private static T BakePrefab<T>(string name) where T : MonoBehaviour, IUiBake
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            var go = new GameObject(name, typeof(RectTransform));
            try
            {
                var component = go.AddComponent<T>();
                component.BakeLayout();
                go.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
            Write("프리팹 " + path);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        // 씬을 열면 쓰지 않는 에셋이 내려가서 예전 참조가 죽음 → 쓸 때마다 경로로 다시 읽음
        private static T Load<T>(string name) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>($"{PrefabFolder}/{name}.prefab");

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Write($"  (필드 없음) {target.GetType().Name}.{field}");
                return;
            }
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Map_Shop 의 상점 오브젝트 → 안내 글자 굽고, 창/안내 프리팹 연결, 상점 자체를 프리팹으로
        private static void BakeShop()
        {
            Scene scene = EditorSceneManager.OpenScene(ShopScene, OpenSceneMode.Single);
            var panel = Load<ComboBindPanel>("ComboBindPanel");
            var notice = Load<ScreenNotice>("ScreenNotice");
            ComboBindStation station = Object.FindFirstObjectByType<ComboBindStation>(FindObjectsInactive.Include);
            if (station == null)
            {
                Write("상점 없음 (Map_Shop) — HJH/콤보/Map_Shop 에 바인딩 상점 놓기 먼저");
                return;
            }
            station.BakeLayout();
            SetRef(station, "panelPrefab", panel);
            SetRef(station, "noticePrefab", notice);
            string path = $"{PrefabFolder}/ComboBindStation.prefab";
            if (PrefabUtility.IsPartOfPrefabInstance(station.gameObject))
                PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(station.gameObject), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAssetAndConnect(station.gameObject, path, InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Write("프리팹 " + path + " (Map_Shop 상점이 이 프리팹으로 바뀜)");
        }

        // PlayerRig: HUD 부품 굽기 + 배너 / 효과 바(프리팹) / 튜토리얼 화면 놓기
        private static void BakePlayerRig()
        {
            var buffBarPrefab = Load<ComboBuffBarView>("ComboBuffBar");
            var portalNotice = Load<ScreenNotice>("PortalNotice");
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerRigPath);
            try
            {
                foreach (IUiBake bake in root.GetComponentsInChildren<IUiBake>(true))
                {
                    if (bake is ComboBuffBarView || bake is TutorialView || bake is ComboBannerView)
                        continue;   // 아래에서 따로
                    bake.BakeLayout();
                    Write("PlayerRig 굽기 " + ((Component)bake).name + " (" + bake.GetType().Name + ")");
                }

                foreach (RoomPortalGate gate in root.GetComponentsInChildren<RoomPortalGate>(true))
                    SetRef(gate, "noticePrefab", portalNotice);

                StatsMiniHud stats = root.GetComponentInChildren<StatsMiniHud>(true);
                Transform hud = stats != null ? stats.GetComponentInParent<Canvas>(true).rootCanvas.transform : root.transform;

                ComboBannerView banner = root.GetComponentInChildren<ComboBannerView>(true);
                if (banner == null)
                {
                    var go = new GameObject("ComboBanner", typeof(RectTransform));
                    go.transform.SetParent(hud, false);
                    banner = go.AddComponent<ComboBannerView>();
                }
                banner.BakeLayout();
                Write("PlayerRig 배너 " + banner.name);

                foreach (ComboBuffBarView old in root.GetComponentsInChildren<ComboBuffBarView>(true))
                    Object.DestroyImmediate(old.gameObject);
                var bar = (GameObject)PrefabUtility.InstantiatePrefab(buffBarPrefab.gameObject, hud);
                bar.name = "ComboBuffBar";
                Write("PlayerRig 효과 바 프리팹 놓음");

                PlaceTutorialView(root.transform, root.GetComponentInChildren<TutorialView>(true));
                PrefabUtility.SaveAsPrefabAsset(root, PlayerRigPath);
                Write("저장 " + PlayerRigPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void PlaceTutorialView(Transform parent, TutorialView view)
        {
            if (view == null)
            {
                var go = new GameObject("TutorialView", typeof(RectTransform));
                go.transform.SetParent(parent, false);
                view = go.AddComponent<TutorialView>();
            }
            view.gameObject.SetActive(true);
            view.BakeLayout();   // 끝에 꺼짐
            Write("튜토리얼 화면 " + view.name + " (꺼진 채로)");
        }

        private static void BakeScene<T>(string path, bool addTutorialView) where T : MonoBehaviour, IUiBake
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            T screen = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (screen == null)
            {
                Write($"{typeof(T).Name} 없음 ({path})");
                return;
            }
            screen.BakeLayout();
            Write($"씬 굽기 {path} ({typeof(T).Name})");

            if (addTutorialView)
            {
                TutorialView view = Object.FindFirstObjectByType<TutorialView>(FindObjectsInactive.Include);
                if (view == null)
                {
                    var go = new GameObject("TutorialView", typeof(RectTransform));
                    SceneManager.MoveGameObjectToScene(go, scene);
                    view = go.AddComponent<TutorialView>();
                }
                PlaceTutorialView(null, view);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
