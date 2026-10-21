using UnityEditor;
using UnityEngine;

namespace Members.JJH._02_Scripts.Systems.UtilSystems.FolderColor.Editor
{
    /// <summary>
    /// ProjectWindow의 각 항목 위에 지정된 색상을 그려주는 훅.
    /// 폴더 아이콘 틴트 + 행 배경 하이라이트를 함께 적용하며,
    /// One column(리스트)과 Two column(그리드) 레이아웃 모두에서 동작한다.
    /// </summary>
    [InitializeOnLoad]
    public static class FolderColorDrawer
    {
        // 리스트 행과 그리드 셀을 가르는 높이 기준값(리스트 행은 약 16px).
        private const float ListRowHeightThreshold = 20f;

        private static Texture2D _folderTexture;

        static FolderColorDrawer()
        {
            EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
        }

        private static Texture2D FolderTexture
        {
            get
            {
                if (_folderTexture == null)
                    _folderTexture = EditorGUIUtility.IconContent("Folder Icon").image as Texture2D;
                return _folderTexture;
            }
        }

        private static void OnProjectWindowItemGUI(string guid, Rect rect)
        {
            var data = FolderColorData.Instance;
            if (data == null) return;
            if (!data.TryGetColor(guid, out var color)) return;

            bool isListView = rect.height <= ListRowHeightThreshold;

            // 1) 행/셀 배경 하이라이트 (반투명)
            Color bg = color;
            bg.a = isListView ? 0.18f : 0.16f;
            EditorGUI.DrawRect(rect, bg);

            // 2) 폴더 아이콘 틴트
            Rect iconRect;
            if (isListView)
            {
                // 리스트 행: 좌측에 정사각형 아이콘(높이 = 한 변).
                iconRect = new Rect(rect.x, rect.y, rect.height, rect.height);
            }
            else
            {
                // 그리드 셀: 상단에 가로 중앙 정렬된 정사각형 아이콘, 하단은 라벨 영역.
                float size = Mathf.Min(rect.width, rect.height);
                iconRect = new Rect(rect.x + (rect.width - size) * 0.5f, rect.y, size, size);
            }

            DrawTintedFolder(iconRect, color);
        }

        private static void DrawTintedFolder(Rect rect, Color color)
        {
            var tex = FolderTexture;
            if (tex == null) return;

            var prev = GUI.color;
            // 알파는 무시하고 항상 불투명하게 그려 기본 아이콘을 덮는다.
            GUI.color = new Color(color.r, color.g, color.b, 1f);
            GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit, true);
            GUI.color = prev;
        }
    }
}
