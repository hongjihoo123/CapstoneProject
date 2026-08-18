using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RobotWeapons.Editor
{
    [CustomEditor(typeof(UpgradeData))]
    public class UpgradeDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var data = (UpgradeData)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("요약", EditorStyles.boldLabel);

            string targets = (data.applicableTypes == null || data.applicableTypes.Length == 0)
                ? "전체 무기"
                : string.Join(", ", data.applicableTypes.Select(t => t.ToString()));

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField($"적용 대상: {targets}");
                if (data.damageAdd != 0) EditorGUILayout.LabelField($"데미지 +{data.damageAdd}");
                if (data.healAdd != 0) EditorGUILayout.LabelField($"회복량 +{data.healAdd}");
                if (data.resourceAdd != 0) EditorGUILayout.LabelField($"자원 최대치 +{data.resourceAdd}");
                if (!string.IsNullOrEmpty(data.unlockSkillId)) EditorGUILayout.LabelField($"스킬 해금: {data.unlockSkillId}");
            }
        }
    }
}
