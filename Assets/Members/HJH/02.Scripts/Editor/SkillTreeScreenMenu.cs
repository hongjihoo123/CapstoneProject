using System;
using System.Reflection;
using Assets.Members.HJH._02.Scripts.Element.Upgrade;
using Assets.Members.HJH._02.Scripts.UI;
using Members.KYR._01.Scripts.Enchant.So;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.Editor
{
    // 스킬 트리 창 프리팹을 굽고, 캐릭터 선택 씬의 "아웃 업그레이드" 버튼에 연결.
    //  - 연결만 (이미 구워진 프리팹은 그대로 둠): 프리팹이 없으면 한 번 굽고, 씬의 필드/버튼 연결을 채움. 몇 번 눌러도 같은 결과.
    //  - 창 다시 굽기: 프리팹 안의 손본 모양이 지워지니 일부러 누를 때만.
    public static class SkillTreeScreenMenu
    {
        private const string PrefabPath = "Assets/Members/HJH/05.Data/Prefab/UI/SkillTreeScreen.prefab";
        private const string PickScenePath = "Assets/Members/HJH/01.Scene/CharacterPick.unity";
        private const string TreeFolder = "Assets/Members/HJH/05.Data/Enchant";

        [MenuItem("HJH/스킬 트리/창 굽기 + 캐릭터 선택창 연결")]
        public static void BakeAndConnect()
        {
            if (!CanEdit())
                return;

            if (AssetDatabase.LoadAssetAtPath<SkillTreeScreen>(PrefabPath) == null)
                Bake();
            ConnectPickScene();
        }

        [MenuItem("HJH/스킬 트리/창 다시 굽기 (손본 모양 지워짐)")]
        public static void Rebake()
        {
            if (!CanEdit())
                return;
            Bake();
            ConnectPickScene();
        }

        [MenuItem("HJH/스킬 트리/소울 100 받기 (테스트)")]
        public static void GrantSoul()
        {
            Assets.Members.HJH._02.Scripts.Element.Upgrade.SkillTreeProgress.Grant(100);
            Debug.Log("[SkillTree] 소울 100 지급. 지금 소울: " + Assets.Members.HJH._02.Scripts.Element.Upgrade.SkillTreeProgress.Soul);
        }

        [MenuItem("HJH/스킬 트리/저장 지우기 (처음 상태)")]
        public static void Wipe()
        {
            Assets.Members.HJH._02.Scripts.Element.Upgrade.SkillTreeProgress.WipeAll();
            Debug.Log("[SkillTree] 스킬 트리 저장을 지우고 처음 상태로 돌렸습니다.");
        }

        // ------------------------------------------------------------ 미리보기 (캡처용)
        // 메뉴로 장면을 정해 두고, 플레이를 시작하면 CharacterPick 에서 창을 열어 줌.
        // 쓰는 법: 메뉴 "미리보기 준비" -> capture.ps1 으로 CharacterPick 을 play 로 캡처 (pick_screen 설정 참고)
        private const string DemoKey = "HJH.SkillTreeDemo";

        [MenuItem("HJH/스킬 트리/미리보기 준비 (평타 탭, 일부 배움)")]
        public static void DemoFire() => EditorPrefs.SetString(DemoKey, "tab=평타;learn=atk1_entry,atk1_l*3,atk1_r*2,atk1_s1*2,atk1_a1,atk1_s2;select=atk1_a1");

        [MenuItem("HJH/스킬 트리/미리보기 준비 (궁극기 탭)")]
        public static void DemoCombo() => EditorPrefs.SetString(DemoKey, "tab=궁극기;learn=ult1_entry,ult1_l*5,ult1_s1*3;select=ult_key");

        [InitializeOnLoadMethod]
        private static void HookDemo()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !EditorPrefs.HasKey(DemoKey))
                    return;

                string spec = EditorPrefs.GetString(DemoKey);
                EditorPrefs.DeleteKey(DemoKey);
                double until = EditorApplication.timeSinceStartup + 3d;
                EditorApplication.CallbackFunction tick = null;
                tick = () =>
                {
                    if (!EditorApplication.isPlaying)
                    {
                        EditorApplication.update -= tick;
                        return;
                    }
                    if (EditorApplication.timeSinceStartup < until)
                        return;
                    EditorApplication.update -= tick;
                    RunDemo(spec);
                };
                EditorApplication.update += tick;
            };
        }

        private static void RunDemo(string spec)
        {
            var pick = UnityEngine.Object.FindFirstObjectByType<CharacterPickScreen>();   // 없으면(다른 씬에서 플레이) 창을 바로 엶
            var tree = AssetDatabase.LoadAssetAtPath<EnchantTree>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:EnchantTree", new[] { TreeFolder })[0]));
            string tab = null, select = null;
            foreach (string part in spec.Split(';'))
            {
                string[] kv = part.Split('=');
                if (kv[0] == "tab") tab = kv[1];
                else if (kv[0] == "select") select = kv[1];
                else if (kv[0] == "learn")
                {
                    SkillTreeProgress.Grant(200);
                    foreach (string item in kv[1].Split(','))
                    {
                        string[] n = item.Split('*');
                        for (int i = 0; i < (n.Length > 1 ? int.Parse(n[1]) : 1); i++)
                            SkillTreeProgress.TryLearn(tree, tree.FindNode(n[0]));
                    }
                }
            }

            if (pick != null)
                pick.OpenOutUpgrade();
            else
                SkillTreeScreen.Open(AssetDatabase.LoadAssetAtPath<SkillTreeScreen>(PrefabPath), tree, "미리보기");
            SkillTreeScreen screen = SkillTreeScreen.Current;
            if (screen == null)
                return;
            const BindingFlags f = BindingFlags.NonPublic | BindingFlags.Instance;
            if (tab != null) typeof(SkillTreeScreen).GetMethod("SelectTab", f).Invoke(screen, new object[] { tab });
            if (select != null) typeof(SkillTreeScreen).GetMethod("Select", f).Invoke(screen, new object[] { select });
        }

        private static bool CanEdit()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogWarning("[SkillTree] 플레이/컴파일 중에는 안 합니다.");
                return false;
            }
            return true;
        }

        private static void Bake()
        {
            var go = new GameObject("SkillTreeScreen", typeof(RectTransform));
            try
            {
                var screen = go.AddComponent<SkillTreeScreen>();
                screen.BakeLayout();
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[SkillTree] 창 프리팹을 구웠습니다: " + PrefabPath);
        }

        private static void ConnectPickScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<SkillTreeScreen>(PrefabPath);
            string[] guids = AssetDatabase.FindAssets("t:EnchantTree", new[] { TreeFolder });
            if (prefab == null || guids.Length == 0)
            {
                Debug.LogError("[SkillTree] 프리팹 또는 트리 에셋이 없습니다.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(PickScenePath);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            if (wasOpen && scene.isDirty)
            {
                Debug.LogWarning("[SkillTree] CharacterPick 씬에 저장 안 한 변경이 있어 건너뜁니다. 저장한 뒤 다시 눌러 주세요.");
                return;
            }
            if (!wasOpen)
                scene = EditorSceneManager.OpenScene(PickScenePath, OpenSceneMode.Additive);

            bool changed = false;
            try
            {
                CharacterPickScreen pick = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    pick = root.GetComponentInChildren<CharacterPickScreen>(true);
                    if (pick != null)
                        break;
                }
                if (pick == null)
                {
                    Debug.LogError("[SkillTree] CharacterPick 씬에서 CharacterPickScreen 을 못 찾았습니다.");
                    return;
                }

                var so = new SerializedObject(pick);
                SerializedProperty prefabField = so.FindProperty("skillTreeScreenPrefab");
                if (prefabField.objectReferenceValue != prefab)
                {
                    prefabField.objectReferenceValue = prefab;
                    changed = true;
                }

                SerializedProperty trees = so.FindProperty("skillTrees");
                for (int i = 0; i < guids.Length; i++)
                {
                    var tree = AssetDatabase.LoadAssetAtPath<EnchantTree>(AssetDatabase.GUIDToAssetPath(guids[i]));
                    bool has = false;
                    for (int k = 0; k < trees.arraySize; k++)
                        has |= trees.GetArrayElementAtIndex(k).objectReferenceValue == tree;
                    if (!has)
                    {
                        trees.arraySize++;
                        trees.GetArrayElementAtIndex(trees.arraySize - 1).objectReferenceValue = tree;
                        changed = true;
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                Transform upgrade = FindDeep(pick.transform, "OutUpgrade");
                Button button = upgrade != null ? upgrade.GetComponent<Button>() : null;
                if (button == null)
                {
                    Debug.LogError("[SkillTree] 'OutUpgrade' 버튼을 못 찾았습니다.");
                    return;
                }

                bool wired = false;
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    wired |= button.onClick.GetPersistentMethodName(i) == nameof(CharacterPickScreen.OpenOutUpgrade);
                if (!wired)
                {
                    UnityEventTools.AddPersistentListener(button.onClick, pick.OpenOutUpgrade);
                    EditorUtility.SetDirty(button);
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(pick);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("[SkillTree] CharacterPick 씬: 아웃 업그레이드 버튼을 스킬 트리 창에 연결하고 저장했습니다.");
                }
                else
                {
                    Debug.Log("[SkillTree] CharacterPick 씬은 이미 연결돼 있습니다.");
                }
            }
            finally
            {
                if (!wasOpen)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }
            return null;
        }
    }
}
