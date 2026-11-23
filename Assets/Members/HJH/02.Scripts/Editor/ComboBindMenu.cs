using System.Collections.Generic;
using System.IO;
using Assets.Members.HJH._02.Scripts.Element;
using Assets.Members.HJH._02.Scripts.Tutorial;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01.Scripts.Player;
using Members.KYR._01.Scripts.Stats;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Mod = Assets.Members.HJH._02.Scripts.Element.ComboStatBuff.Mod;

namespace HJH.EditorTools
{
    // 콤보 바인딩 임시 세팅.
    //  - 원소 효과 표: Resources/HJH/ComboElementTable.asset (다시 실행하면 덮어씀. 숫자는 전부 임시)
    //  - Map_Shop 에 바인딩 상점 하나 놓기 (이미 있으면 그대로)
    public static class ComboBindMenu
    {
        private const string TablePath = "Assets/Members/HJH/05.Data/Resources/HJH/ComboElementTable.asset";
        private const string MaterialFolder = "Assets/Members/HJH/05.Data/Combo";
        private const string ShopScene = "Assets/Members/HJH/01.Scene/Map_Shop.unity";

        private const StatModifierType Pct = StatModifierType.PercentAdd, Flat = StatModifierType.Flat;

        // 원소마다 한 칸 효과
        private static Mod[] BaseOf(ElementType element) => element switch
        {
            ElementType.Fire => new[] { new Mod(PlayerStatId.Damage, Pct, 0.10f) },
            ElementType.Water => new[] { new Mod(PlayerStatId.MagicDamage, Pct, 0.10f) },
            ElementType.Wind => new[] { new Mod(PlayerStatId.MoveSpeed, Pct, 0.08f) },
            ElementType.Electric => new[] { new Mod(PlayerStatId.AttackSpeed, Pct, 0.10f) },
            ElementType.Earth => new[] { new Mod(PlayerStatId.ArmorDefense, Flat, 10f), new Mod(PlayerStatId.MagicDefense, Flat, 10f) },
            _ => new Mod[0]
        };

        // 상성: 물 > 불 > 바람 > 땅 > 번개 > 물
        private static ElementType Beats(ElementType element) => element switch
        {
            ElementType.Water => ElementType.Fire,
            ElementType.Fire => ElementType.Wind,
            ElementType.Wind => ElementType.Earth,
            ElementType.Earth => ElementType.Electric,
            _ => ElementType.Water,
        };

        // 이웃 보너스 (앞 → 뒤)
        //  같은 원소: 증폭 (그 원소 효과 한 번 더)
        //  뒤가 앞을 이김 (불→물): 상성  앞 -절반, 뒤 +한 칸
        //  앞이 뒤를 이김 (물→불): 역상성 앞 +절반, 뒤 -절반
        //  관계 없음: 연계  뒤 +절반
        private static ComboElementTable.PairBonus Pair(ElementType first, ElementType second)
        {
            var mods = new List<Mod>();
            string label;
            if (first == second)
            {
                label = "증폭";
                mods.AddRange(BaseOf(first));
            }
            else if (Beats(second) == first)
            {
                label = "상성";
                mods.AddRange(Scaled(BaseOf(first), -0.5f));
                mods.AddRange(BaseOf(second));
            }
            else if (Beats(first) == second)
            {
                label = "역상성";
                mods.AddRange(Scaled(BaseOf(first), 0.5f));
                mods.AddRange(Scaled(BaseOf(second), -0.5f));
            }
            else
            {
                label = "연계";
                mods.AddRange(Scaled(BaseOf(second), 0.5f));
            }

            return new ComboElementTable.PairBonus { first = first, second = second, label = label, mods = mods.ToArray() };
        }

        private static IEnumerable<Mod> Scaled(Mod[] mods, float scale)
        {
            foreach (Mod mod in mods)
                yield return new Mod(mod.stat, mod.type, mod.value * scale);
        }

        [MenuItem("HJH/콤보/원소 효과 표 만들기")]
        public static ComboElementTable BuildTable()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TablePath)!);
            var elements = new List<ComboElementTable.ElementEffect>();
            var pairs = new List<ComboElementTable.PairBonus>();
            var all = (ElementType[])System.Enum.GetValues(typeof(ElementType));
            foreach (ElementType first in all)
            {
                elements.Add(new ComboElementTable.ElementEffect { element = first, mods = BaseOf(first) });
                foreach (ElementType second in all)
                    pairs.Add(Pair(first, second));
            }

            var table = AssetDatabase.LoadAssetAtPath<ComboElementTable>(TablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<ComboElementTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            table.Set(2, 4, 10f, new[] { 1.25f, 1.75f, 2.5f, 4f }, elements, pairs);
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ComboBind] 원소 효과 표 저장: {TablePath} (원소 {elements.Count}, 보너스 {pairs.Count})");
            return table;
        }

        [MenuItem("HJH/콤보/Map_Shop 에 바인딩 상점 놓기")]
        public static void PlaceInShop()
        {
            if (ComboElementTable.Default == null)
                BuildTable();

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ShopScene)
            {
                if (scene.isDirty)
                {
                    Debug.LogWarning("[ComboBind] 지금 씬에 저장 안 한 변경이 있어서 Map_Shop 을 열지 않음");
                    return;
                }
                scene = EditorSceneManager.OpenScene(ShopScene);
            }

            if (Object.FindFirstObjectByType<ComboBindStation>(FindObjectsInactive.Include) != null)
            {
                Debug.Log("[ComboBind] Map_Shop 에 이미 바인딩 상점이 있음");
                return;
            }

            PlayerAgent player = Object.FindFirstObjectByType<PlayerAgent>(FindObjectsInactive.Include);
            Vector3 origin = player != null ? player.transform.position : Vector3.zero;
            Vector3 position = origin + new Vector3(4f, 0f, 3f);
            if (Physics.Raycast(position + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
                position = hit.point;

            var root = new GameObject("ComboBindStation");
            root.transform.position = position;
            root.AddComponent<ComboBindStation>();
            BuildLook(root.transform);

            Undo.RegisterCreatedObjectUndo(root, "Place combo bind station");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log($"[ComboBind] 바인딩 상점 배치: {position} (플레이어 {origin})");
        }

        [MenuItem("HJH/콤보/바인딩 자동 확인 (다음 플레이 한 번)")]
        public static void ArmSelfTest()
        {
            Directory.CreateDirectory("Captures");
            File.WriteAllText(ComboBindSelfTest.Flag, "on");
            Debug.Log("[ComboBind] 다음 플레이(Map_Shop)에서 자동 확인. 결과: Captures/_combo_bind_log.txt");
        }

        // 이미 만들어 둔 튜토리얼 에셋에 반영: 픽 화면 콤보 단계 문구 교체 + 스테이지 콤보 단계 뒤에 바인딩 단계 넣기
        // (튜토리얼 에셋 만들기 메뉴는 있는 에셋을 안 건드려서 따로 둠. 다른 단계는 그대로)
        [MenuItem("HJH/콤보/튜토리얼에 콤보 단계 반영")]
        public static void UpdateTutorial()
        {
            const string folder = "Assets/Members/HJH/05.Data/Resources/HJH/Tutorial";
            var pick = AssetDatabase.LoadAssetAtPath<TutorialSequence>($"{folder}/{TutorialDefaults.PickName}.asset");
            var stage = AssetDatabase.LoadAssetAtPath<TutorialSequence>($"{folder}/{TutorialDefaults.StageName}.asset");
            if (pick == null || stage == null)
            {
                Debug.LogWarning("[ComboBind] 튜토리얼 에셋이 없음 → 메뉴 HJH/튜토리얼/기본 튜토리얼 에셋 만들기 (새 기본값에 이미 반영돼 있음)");
                return;
            }

            foreach (TutorialStep step in Steps(pick))
            {
                if (step is PickScreenStep screen && screen.Task == PickScreenStep.PickTask.Combo)
                {
                    screen.Configure(TutorialDefaults.PickComboTitle, TutorialDefaults.PickComboBody, TutorialAnchorIds.PickCombos, pauseTime: false);
                    EditorUtility.SetDirty(screen);
                }
            }

            var so = new SerializedObject(stage);
            SerializedProperty steps = so.FindProperty("steps");
            int comboAt = -1;
            for (int i = 0; i < steps.arraySize; i++)
            {
                Object step = steps.GetArrayElementAtIndex(i).objectReferenceValue;
                if (step is ComboBindStep)
                {
                    Debug.Log("[ComboBind] 스테이지 튜토리얼에 바인딩 단계가 이미 있음 (픽 문구만 갱신)");
                    AssetDatabase.SaveAssets();
                    return;
                }
                if (step is ComboStep)
                    comboAt = i;
            }

            var bind = ScriptableObject.CreateInstance<ComboBindStep>();
            bind.Configure(TutorialDefaults.BindTitle, TutorialDefaults.BindBody);
            AssetDatabase.CreateAsset(bind, $"{folder}/Steps/Tutorial_Stage_07b_콤보_바인딩.asset");

            int at = comboAt >= 0 ? comboAt + 1 : steps.arraySize;
            steps.InsertArrayElementAtIndex(at);
            steps.GetArrayElementAtIndex(at).objectReferenceValue = bind;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ComboBind] 스테이지 튜토리얼 {at + 1}번째에 콤보 바인딩 단계 추가");
        }

        private static IEnumerable<TutorialStep> Steps(TutorialSequence sequence)
        {
            SerializedProperty steps = new SerializedObject(sequence).FindProperty("steps");
            for (int i = 0; i < steps.arraySize; i++)
            {
                if (steps.GetArrayElementAtIndex(i).objectReferenceValue is TutorialStep step)
                    yield return step;
            }
        }

        // 임시 모양: 돌 받침 + 떠 있는 수정 (나중에 모델로 바꾸면 됨)
        private static void BuildLook(Transform root)
        {
            Directory.CreateDirectory(MaterialFolder);
            Material stone = LitMaterial("ComboStation_Stone", new Color(0.22f, 0.24f, 0.28f), Color.black);
            Material crystal = LitMaterial("ComboStation_Crystal", new Color(0.55f, 0.85f, 1f), new Color(0.5f, 1.2f, 2f));

            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.SetParent(root, false);
            pedestal.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            pedestal.transform.localScale = new Vector3(1.4f, 0.4f, 1.4f);
            pedestal.GetComponent<Renderer>().sharedMaterial = stone;

            GameObject gem = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gem.name = "Crystal";
            gem.transform.SetParent(root, false);
            gem.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            gem.transform.localScale = new Vector3(0.5f, 0.8f, 0.5f);
            gem.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            Object.DestroyImmediate(gem.GetComponent<Collider>());
            gem.GetComponent<Renderer>().sharedMaterial = crystal;

            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.85f, 1f);
            light.range = 4f;
            light.intensity = 2f;
        }

        private static Material LitMaterial(string name, Color color, Color emission)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.color = color;
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
