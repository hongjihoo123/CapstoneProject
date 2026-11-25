using System.IO;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace HJH.EditorTools
{
    // 캐릭터 선택 화면의 스킬 미리보기 영상을 스킬 에셋에 연결.
    // 영상 파일(mp4)을 05.Data/SkillPreview 폴더에 "스킬 에셋 이름.mp4" 로 넣고 메뉴를 한 번 누르면
    // 이름이 같은 SkillData(없으면 표시 이름이 같은 것)의 PreviewClip 에 자동으로 들어감.
    public static class SkillPreviewLink
    {
        private const string Folder = "Assets/Members/HJH/05.Data/SkillPreview";

        [MenuItem("HJH/스킬/미리보기 영상 연결")]
        public static void Link()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();

            int linked = 0;
            int missing = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:VideoClip", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(path);
                string key = Path.GetFileNameWithoutExtension(path);

                SkillData skill = Find(key);
                if (skill == null)
                {
                    Debug.LogWarning($"[SkillPreviewLink] 이름이 맞는 스킬이 없음: {key}");
                    missing++;
                    continue;
                }

                var so = new SerializedObject(skill);
                SerializedProperty property = so.FindProperty("previewClip");
                if (property == null || property.objectReferenceValue == clip)
                    continue;
                property.objectReferenceValue = clip;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(skill);
                linked++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SkillPreviewLink] 연결 {linked}개, 못 찾음 {missing}개 (영상 폴더: {Folder})");
        }

        private static SkillData Find(string key)
        {
            SkillData byDisplay = null;
            foreach (string guid in AssetDatabase.FindAssets("t:SkillData"))
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                if (skill == null)
                    continue;
                if (skill.name == key)
                    return skill;
                if (byDisplay == null && skill.DisplayName == key)
                    byDisplay = skill;
            }
            return byDisplay;
        }
    }
}
