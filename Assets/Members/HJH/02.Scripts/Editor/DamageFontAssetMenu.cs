using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace HJH.EditorTools
{
    // 데미지 숫자용 TMP 폰트 에셋 만들기 (Chakra Petch Bold = 일반/마법/약함, Russo One = 크리). 둘 다 OFL 무료 글꼴.
    // 숫자와 몇 개 기호만 미리 구워 둔 정적 아틀라스. 결과는 Resources 폴더에 둬서 코드에서 불러 씀.
    public static class DamageFontAssetMenu
    {
        private const string FontDir = "Assets/TextMesh Pro/Fonts/";
        private const string OutDir = "Assets/TextMesh Pro/Resources/Fonts & Materials/";
        private const string Chars = "0123456789,.+-%x×!?";

        [MenuItem("HJH/UI/데미지 숫자 폰트 에셋 만들기")]
        public static void Build()
        {
            Make("ChakraPetch-Bold.ttf", "ChakraPetch-Bold SDF");
            Make("RussoOne-Regular.ttf", "RussoOne SDF");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DamageFont] 완료: " + OutDir);
        }

        private static void Make(string ttf, string assetName)
        {
            string assetPath = OutDir + assetName + ".asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontDir + ttf);
            if (font == null)
            {
                Debug.LogWarning("[DamageFont] 글꼴 파일 없음: " + FontDir + ttf);
                return;
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 90, 14, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, false);
            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, assetPath);

            asset.material.name = assetName + " Material";
            // 새 재질엔 외곽선 키워드가 꺼져 있어서 코드로 outlineWidth 를 줘도 안 보임 → 켜 둔다
            asset.material.EnableKeyword("OUTLINE_ON");
            asset.material.SetFloat("_OutlineWidth", 0.15f);
            asset.material.SetColor("_OutlineColor", new Color(0.12f, 0.12f, 0.15f, 1f));
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.atlasTextures[0].name = assetName + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);

            asset.TryAddCharacters(Chars, out string missing);
            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning($"[DamageFont] {assetName}: 없는 글자 '{missing}'");

            asset.atlasPopulationMode = AtlasPopulationMode.Static;   // 구워 둔 글자만 쓰는 고정 아틀라스
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(asset.material);
            EditorUtility.SetDirty(asset.atlasTextures[0]);
        }
    }
}
