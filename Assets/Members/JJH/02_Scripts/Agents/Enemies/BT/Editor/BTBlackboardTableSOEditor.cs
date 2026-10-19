using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Editor
{
    [CustomEditor(typeof(BTBlackboardTableSO))]
    public class BTBlackboardTableSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); //기본 인스펙터 그려주고

            BTBlackboardTableSO table = target as BTBlackboardTableSO;

            EditorGUILayout.Space(); //한줄 띄고
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Bake from Graph", GUILayout.Height(28)))
                {
                    Bake(table);
                }
            }

            EditorGUILayout.HelpBox(
                "그래프의 블랙보드 변수 전체를 이름 -> GUID로 구워줍니다.",
                MessageType.Info);
        }

        private void Bake(BTBlackboardTableSO table)
        {
            (bool isSuccess, string message, int count) = table.Bake();

            if (!isSuccess)
            {
                EditorUtility.DisplayDialog("베이크 실패", message, "확인");
                return;
            }

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssetIfDirty(table);

            //검증 BTVar에 const들이 실제 그래프에 존재하는지 검증
            HashSet<string> baked = table.GetBakedNames();
            List<string> missing = GetBTVarsNames().Where(n => !baked.Contains(n)).ToList();

            if (missing.Count > 0)
            {
                Debug.LogWarning($"Bake 완료 {count}개 -- 단 BTVars에 있으나 그래프에 없는 것 : {string.Join(",", missing)}");
            }
            else
            {
                Debug.Log($"Bake완료 : {message}, BTVars 전체 확인됨");
            }
        }

        private static IEnumerable<string> GetBTVarsNames()
        {
            return typeof(BTVars)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue());
        }
    }
}