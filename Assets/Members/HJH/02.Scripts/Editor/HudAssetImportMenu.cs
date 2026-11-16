using System.IO;
using UnityEditor;
using UnityEngine;

namespace HJH.EditorTools
{
    // ArtSource/UI_Concept 의 HUD PNG 를 Resources/HJH/UI 로 복사해서 스프라이트로 가져온다.
    // 이미 있으면 덮어쓰고 설정만 다시 맞춤. 원본(ArtSource)은 그대로.
    public static class HudAssetImportMenu
    {
        private const string SourceDir = "ArtSource/UI_Concept";
        private const string TargetDir = "Assets/Members/HJH/05.Data/Resources/HJH/UI";

        // 파일 이름, 9-slice 테두리 (0 이면 없음)
        private static readonly (string file, int border)[] Files =
        {
            ("slot_circle_frame.png", 0), ("slot_circle_bg.png", 0),
            ("hpbar_slant_bg.png", 0), ("hpbar_slant_frame.png", 0), ("hpbar_slant_fill.png", 0),
            ("minimap_ring.png", 0), ("panel_9slice.png", 24),
            ("button_normal.png", 0), ("button_hover.png", 0),
            ("divider.png", 0), ("quest_icon.png", 0), ("soft_glow.png", 0), ("eye_icon.png", 0), ("gold_icon.png", 0),
            ("minimap_player.png", 0), ("minimap_enemy.png", 0), ("minimap_portal.png", 0), ("minimap_vignette.png", 0),
        };

        [MenuItem("HJH/UI/HUD 에셋 가져오기")]
        public static void Import()
        {
            Directory.CreateDirectory(TargetDir);
            foreach (var (file, border) in Files)
            {
                string from = Path.Combine(SourceDir, file);
                if (!File.Exists(from))
                {
                    Debug.LogWarning("[HudAssetImport] 없음: " + from);
                    continue;
                }

                string to = TargetDir + "/" + file;
                File.Copy(from, to, true);
                AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceUpdate);

                var importer = (TextureImporter)AssetImporter.GetAtPath(to);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spritePixelsPerUnit = 100f;
                importer.spriteBorder = border > 0 ? new Vector4(border, border, border, border) : Vector4.zero;
                importer.SaveAndReimport();
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[HudAssetImport] 완료: " + TargetDir);
        }
    }
}
