using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using UnityEditor;
using UnityEngine;

namespace Members.KYR._01.Scripts.SkillUpgrade.Editor
{
    [CustomEditor(typeof(SkillUpgradeTableSO))]
    public sealed class SkillUpgradeTableEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "스킬마다 레벨 목록을 적습니다. 레벨 하나 = 그 레벨로 올리는 비용(런 골드)과 추가되는 고정 데미지/배율.\n" +
                "고정 데미지는 타격 1회(틱 1회)마다 더해지고, 배율(0.1 = +10%)은 그 뒤에 곱해집니다.",
                MessageType.Info);

            if (GUILayout.Button("스킬 강화 편집기 열기", GUILayout.Height(28f)))
                SkillUpgradeEditorWindow.Open((SkillUpgradeTableSO)target);
            if (GUILayout.Button("프로젝트의 스킬 모두 추가 (없는 것만)"))
                AddMissingSkills((SkillUpgradeTableSO)target);

            EditorGUILayout.Space();
            DrawDefaultInspector();
            DrawPreview((SkillUpgradeTableSO)target);
        }

        internal static void AddMissingSkills(SkillUpgradeTableSO table)
        {
            SerializedObject so = new SerializedObject(table);
            SerializedProperty entries = so.FindProperty("entries");
            HashSet<SkillData> existing = new HashSet<SkillData>();
            for (int i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("skill").objectReferenceValue is SkillData skill)
                    existing.Add(skill);
            }

            int added = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:SkillData"))
            {
                SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                if (skill == null || skill is EmptySkillData || existing.Contains(skill))
                    continue;

                int index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("skill").objectReferenceValue = skill;

                // 시작값: 3레벨, 레벨마다 +10%. 스킬별로 고쳐 쓰면 된다.
                SerializedProperty levels = entry.FindPropertyRelative("levels");
                levels.ClearArray();
                for (int lv = 0; lv < 3; lv++)
                {
                    levels.InsertArrayElementAtIndex(lv);
                    SerializedProperty level = levels.GetArrayElementAtIndex(lv);
                    level.FindPropertyRelative("cost").intValue = 50 * (lv + 1);
                    level.FindPropertyRelative("flatDamage").floatValue = 0f;
                    level.FindPropertyRelative("percentDamage").floatValue = 0.1f;
                }

                existing.Add(skill);
                added++;
            }

            so.ApplyModifiedProperties();
            Debug.Log($"SkillUpgradeTable: 스킬 {added}개 추가");
        }

        private static void DrawPreview(SkillUpgradeTableSO table)
        {
            if (table.Entries == null || table.Entries.Count == 0)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("누적 미리보기 (최대 레벨 기준)", EditorStyles.boldLabel);
            foreach (SkillUpgradeTableSO.Entry entry in table.Entries)
            {
                if (entry == null || entry.skill == null)
                    continue;

                float flat = 0f;
                float percent = 0f;
                int cost = 0;
                if (entry.levels != null)
                {
                    foreach (SkillUpgradeTableSO.Level level in entry.levels)
                    {
                        if (level == null)
                            continue;
                        flat += level.flatDamage;
                        percent += level.percentDamage;
                        cost += level.cost;
                    }
                }

                EditorGUILayout.LabelField(
                    entry.skill.DisplayName,
                    $"Lv{entry.MaxLevel}  {SkillUpgradeService.Describe(flat, percent)}  총 {cost}G");
            }
        }
    }
}
