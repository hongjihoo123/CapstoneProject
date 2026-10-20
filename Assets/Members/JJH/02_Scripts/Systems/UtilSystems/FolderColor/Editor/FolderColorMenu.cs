using System.Collections.Generic;
using UnityEditor;

namespace Members.JJH._02_Scripts.Systems.UtilSystems.FolderColor.Editor
{
    /// <summary>
    /// ProjectWindow 우클릭 컨텍스트 메뉴(Assets/Folder Color)에서
    /// 선택한 폴더(들)에 프리셋/커스텀 색상을 지정하거나 해제한다.
    /// 메뉴는 폴더가 하나라도 선택된 경우에만 활성화된다.
    /// </summary>
    public static class FolderColorMenu
    {
        private const string Root = "Assets/Folder Color/";

        // ─────────────────────────── 프리셋 10종 ───────────────────────────
        // 라벨과 우선순위(priority)는 FolderColorPalette.Presets 배열 순서와 대응한다.

        [MenuItem(Root + "Red", false, 0)] private static void Red() => ApplyPreset(0);
        [MenuItem(Root + "Red", true)] private static bool RedV() => HasFolderSelection();

        [MenuItem(Root + "Orange", false, 1)] private static void Orange() => ApplyPreset(1);
        [MenuItem(Root + "Orange", true)] private static bool OrangeV() => HasFolderSelection();

        [MenuItem(Root + "Yellow", false, 2)] private static void Yellow() => ApplyPreset(2);
        [MenuItem(Root + "Yellow", true)] private static bool YellowV() => HasFolderSelection();

        [MenuItem(Root + "Green", false, 3)] private static void Green() => ApplyPreset(3);
        [MenuItem(Root + "Green", true)] private static bool GreenV() => HasFolderSelection();

        [MenuItem(Root + "Mint", false, 4)] private static void Mint() => ApplyPreset(4);
        [MenuItem(Root + "Mint", true)] private static bool MintV() => HasFolderSelection();

        [MenuItem(Root + "Teal", false, 5)] private static void Teal() => ApplyPreset(5);
        [MenuItem(Root + "Teal", true)] private static bool TealV() => HasFolderSelection();

        [MenuItem(Root + "Blue", false, 6)] private static void Blue() => ApplyPreset(6);
        [MenuItem(Root + "Blue", true)] private static bool BlueV() => HasFolderSelection();

        [MenuItem(Root + "Indigo", false, 7)] private static void Indigo() => ApplyPreset(7);
        [MenuItem(Root + "Indigo", true)] private static bool IndigoV() => HasFolderSelection();

        [MenuItem(Root + "Purple", false, 8)] private static void Purple() => ApplyPreset(8);
        [MenuItem(Root + "Purple", true)] private static bool PurpleV() => HasFolderSelection();

        [MenuItem(Root + "Pink", false, 9)] private static void Pink() => ApplyPreset(9);
        [MenuItem(Root + "Pink", true)] private static bool PinkV() => HasFolderSelection();

        // ─────────────────────────── 커스텀 / 해제 ──────────────────────────
        // priority 간격(9 → 20)이 11 이상이므로 메뉴에 구분선이 자동으로 들어간다.

        [MenuItem(Root + "Custom...", false, 20)]
        private static void Custom() => FolderColorPickerPopup.Open(GetSelectedFolderGuids());
        [MenuItem(Root + "Custom...", true)]
        private static bool CustomV() => HasFolderSelection();

        [MenuItem(Root + "Clear", false, 21)]
        private static void Clear()
        {
            var data = FolderColorData.Instance;
            if (data == null) return;
            foreach (var guid in GetSelectedFolderGuids())
                data.ClearColor(guid);
        }
        [MenuItem(Root + "Clear", true)]
        private static bool ClearV() => HasFolderSelection();

        // ─────────────────────────────── 내부 ──────────────────────────────

        private static void ApplyPreset(int index)
        {
            var color = FolderColorPalette.Presets[index].Color;
            var data = FolderColorData.GetOrCreate();
            foreach (var guid in GetSelectedFolderGuids())
                data.SetColor(guid, color);
        }

        private static bool HasFolderSelection()
        {
            foreach (var guid in Selection.assetGUIDs)
                if (IsFolder(guid)) return true;
            return false;
        }

        private static string[] GetSelectedFolderGuids()
        {
            var result = new List<string>();
            foreach (var guid in Selection.assetGUIDs)
                if (IsFolder(guid)) result.Add(guid);
            return result.ToArray();
        }

        private static bool IsFolder(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path);
        }
    }
}
