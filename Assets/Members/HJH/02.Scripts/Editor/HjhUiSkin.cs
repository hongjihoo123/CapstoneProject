using TMPro;
using UnityEditor;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // Shared look for the HJH HUD builders: Kenney CC0 packs under Assets/Members/HJH/UI (white art,
    // tinted per use, frames 9-sliced) and optional display fonts. Load() once per build.
    // Missing files fall back to Unity's rounded sprite / the default font, so builds never fail on art.
    internal static class HjhUiSkin
    {
        private const string BordersRoot = "Assets/Members/HJH/UI/kenney_fantasy-ui-borders/PNG/Default";
        private const string RpgRoot = "Assets/Members/HJH/UI/UIpack_RPG/PNG";
        private const string BodyFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Pretendard-Medium SDF.asset";
        // Drop the .ttf/.otf anywhere under Assets and rebuild; "<font> SDF.asset" is created next to it.
        private static readonly string[] DisplayFontNames = { "Cinzel" };
        private static readonly string[] KoreanDisplayFontNames = { "BlackHanSans", "Black Han Sans" };

        public static Sprite Rounded { get; private set; }
        // Octagon-cornered frame with a half-transparent center (key caps, skill icons).
        public static Sprite CapFrame { get; private set; }
        // Same shape, outline only (glows, flashes).
        public static Sprite CapOutline { get; private set; }
        public static Sprite RowFrame { get; private set; }
        public static Sprite SmallFrame { get; private set; }
        public static Sprite BannerFrame { get; private set; }
        public static Sprite ThinOutline { get; private set; }
        // Ornament on the right end only: mirror a copy for the left end.
        public static Sprite Divider { get; private set; }
        public static Sprite DividerFade { get; private set; }
        public static Sprite Arrow { get; private set; }
        // Our own Space key cap (white, tint it); shown instead of the "Space" text.
        public static Sprite SpaceKey { get; private set; }

        public static TMP_FontAsset BodyFont { get; private set; }
        // Latin display face (numbers, COMBO!, ranks); falls back to the body font.
        public static TMP_FontAsset DisplayFont { get; private set; }
        // Korean display face (combo names, titles); falls back to the body font.
        public static TMP_FontAsset KoreanDisplayFont { get; private set; }

        public static void Load()
        {
            Rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            CapFrame = Slice($"{BordersRoot}/Transparent center/panel-transparent-center-005.png", new Vector4(16f, 16f, 16f, 16f));
            CapOutline = Slice($"{BordersRoot}/Border/panel-border-005.png", new Vector4(16f, 16f, 16f, 16f));
            RowFrame = Slice($"{BordersRoot}/Transparent center/panel-transparent-center-001.png", new Vector4(14f, 14f, 14f, 14f));
            SmallFrame = Slice($"{BordersRoot}/Transparent center/panel-transparent-center-015.png", new Vector4(6f, 6f, 6f, 6f));
            BannerFrame = Slice($"{BordersRoot}/Transparent center/panel-transparent-center-022.png", new Vector4(16f, 16f, 16f, 16f));
            ThinOutline = Slice($"{BordersRoot}/Border/panel-border-015.png", new Vector4(4f, 4f, 4f, 4f));
            Divider = Slice($"{BordersRoot}/Divider/divider-003.png", new Vector4(0f, 0f, 24f, 0f));
            DividerFade = Slice($"{BordersRoot}/Divider Fade/divider-fade-003.png", new Vector4(0f, 0f, 24f, 0f));
            Arrow = Slice($"{RpgRoot}/arrowSilver_right.png", Vector4.zero);
            SpaceKey = Slice("Assets/Members/HJH/UI/Icons/Keys/Space.png", Vector4.zero);

            BodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
            DisplayFont = FindFont(DisplayFontNames) ?? BodyFont;
            KoreanDisplayFont = FindFont(KoreanDisplayFontNames) ?? BodyFont;
        }

        private static Sprite Slice(string path, Vector4 border)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogWarning($"[HjhUiSkin] {path} 가 없어 기본 스프라이트로 대신합니다.");
                return Rounded;
            }

            if (importer.textureType != TextureImporterType.Sprite || importer.spriteBorder != border)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Dynamic TMP font asset for the first font file matching one of the names; null when none is in the project.
        private static TMP_FontAsset FindFont(string[] names)
        {
            foreach (string fontName in names)
            {
                foreach (string guid in AssetDatabase.FindAssets($"t:Font {fontName}"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string sdfPath = System.IO.Path.ChangeExtension(path, null) + " SDF.asset";

                    var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(sdfPath);
                    if (existing != null)
                        return existing;

                    var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                    if (font == null)
                        continue;

                    TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font);
                    asset.name = System.IO.Path.GetFileNameWithoutExtension(sdfPath);
                    AssetDatabase.CreateAsset(asset, sdfPath);
                    asset.material.name = $"{asset.name} Material";
                    AssetDatabase.AddObjectToAsset(asset.material, asset);
                    asset.atlasTextures[0].name = $"{asset.name} Atlas";
                    AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[HjhUiSkin] 폰트 에셋 생성: {sdfPath}");
                    return asset;
                }
            }
            return null;
        }
    }
}
