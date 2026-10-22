using UnityEditor;
using UnityEngine;

namespace Members.JJH._02_Scripts.Systems.UtilSystems.FolderColor.Editor
{
    /// <summary>
    /// 'Custom...' 메뉴 선택 시 뜨는 소형 색상 선택 창.
    /// 선택한 폴더(들)에 임의의 색상을 지정한다.
    /// </summary>
    public class FolderColorPickerPopup : EditorWindow
    {
        private string[] _guids;
        private Color _color = Color.white;

        public static void Open(string[] guids)
        {
            if (guids == null || guids.Length == 0) return;

            var window = CreateInstance<FolderColorPickerPopup>();
            window._guids = guids;

            // 단일 폴더 선택이고 기존 색상이 있으면 그 값으로 초기화한다.
            var data = FolderColorData.Instance;
            if (guids.Length == 1 && data != null && data.TryGetColor(guids[0], out var existing))
                window._color = existing;

            window.titleContent = new GUIContent("Folder Color");
            window.minSize = window.maxSize = new Vector2(260f, 84f);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);

            // 알파/HDR은 폴더 표시에 의미가 없으므로 색상만 노출한다.
            _color = EditorGUILayout.ColorField(new GUIContent("Color"), _color, true, false, false);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply", GUILayout.Height(24f)))
                {
                    var data = FolderColorData.GetOrCreate();
                    foreach (var guid in _guids)
                        data.SetColor(guid, _color);
                    Close();
                }

                if (GUILayout.Button("Cancel", GUILayout.Height(24f)))
                    Close();
            }
        }
    }
}
