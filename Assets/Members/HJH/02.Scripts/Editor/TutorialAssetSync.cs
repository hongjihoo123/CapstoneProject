using System.Collections.Generic;
using System.IO;
using Assets.Members.HJH._02.Scripts.Tutorial;
using UnityEditor;
using UnityEngine;

namespace HJH.EditorTools
{
    // TutorialDefaults 의 문구·순서를 저장된 튜토리얼 에셋에 덮어씀 (GUID 유지, 종류가 바뀐 단계만 새로 만듦).
    // 인스펙터에서 직접 고친 문구는 사라지므로, 덮어쓰기 전에 _RemovedBackup 에 복사해 둠.
    public static class TutorialAssetSync
    {
        private const string Root = "Assets/Members/HJH/05.Data/Resources/" + TutorialDefaults.ResourceFolder;
        private const string StepDir = Root + "Steps/";

        public static void Sync(GameObject dummy)
        {
            if (dummy == null)
            {
                Debug.LogError("[Tutorial] 훈련 더미 프리팹이 없어 튜토리얼 에셋을 갱신하지 않았습니다.");
                return;
            }

            Directory.CreateDirectory(StepDir);
            Backup();
            Apply(TutorialDefaults.PickName, TutorialDefaults.CreatePick());
            Apply(TutorialDefaults.StageName, TutorialDefaults.CreateStage(dummy));
            AssetDatabase.SaveAssets();
        }

        private static void Backup()
        {
            string target = "_RemovedBackup/" + System.DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + "/Tutorial/";
            foreach (string file in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
            {
                string to = target + file.Substring(Root.Length);
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Copy(file, to, true);
            }
        }

        private static void Apply(string name, TutorialSequence fresh)
        {
            string path = Root + name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<TutorialSequence>(path);
            bool createSequence = saved == null;
            if (createSequence)
                saved = ScriptableObject.CreateInstance<TutorialSequence>();

            var freshSteps = new List<TutorialStep>(fresh.Steps);
            var oldSteps = new List<TutorialStep>(saved.Steps ?? new TutorialStep[0]);
            var result = new List<TutorialStep>();

            for (int i = 0; i < freshSteps.Count; i++)
            {
                TutorialStep step = freshSteps[i];
                TutorialStep old = i < oldSteps.Count ? oldSteps[i] : null;
                if (old != null && old.GetType() == step.GetType())
                {
                    string assetName = old.name;
                    EditorUtility.CopySerialized(step, old);
                    old.name = assetName;
                    EditorUtility.SetDirty(old);
                    result.Add(old);
                    continue;
                }

                string fileName = $"{name}_{i + 1:00}_{step.Title.Replace(' ', '_')}";
                step.name = fileName;
                AssetDatabase.CreateAsset(step, StepDir + fileName + ".asset");
                result.Add(step);
            }

            foreach (TutorialStep old in oldSteps)
                if (old != null && !result.Contains(old))
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(old));

            if (createSequence)
            {
                EditorUtility.CopySerialized(fresh, saved);
                saved.name = name;
                AssetDatabase.CreateAsset(saved, path);
            }

            var serialized = new SerializedObject(saved);
            SerializedProperty list = serialized.FindProperty("steps");
            list.arraySize = result.Count;
            for (int i = 0; i < result.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = result[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(saved);
        }
    }
}
