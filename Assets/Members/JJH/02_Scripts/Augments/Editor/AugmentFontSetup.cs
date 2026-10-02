#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Members.JJH._02_Scripts.Augments.Editor
{
    // Pretendard-Medium(한글 지원 오픈소스 폰트)으로 TMP Font Asset을 만들고
    // Augment UI 프리팹의 텍스트들에 자동으로 적용한다.
    public static class AugmentFontSetup
    {
        private const string SourceFontPath = "Assets/TextMesh Pro/Fonts/Pretendard-Medium.otf";
        private const string FontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Pretendard-Medium SDF.asset";
        private const string AugmentUiPrefabPath = "Assets/Members/JJH/03_Datas/02_Prefabs/UI/AugmentUI.prefab";

        [MenuItem("Tools/Augment/Setup Korean Font (Pretendard) On Augment UI")]
        public static void Setup()
        {
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"[AugmentFontSetup] 폰트 파일을 찾을 수 없습니다: {SourceFontPath} (Unity가 아직 임포트하지 않았을 수 있음)");
                return;
            }

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                // Dynamic 아틀라스: 필요한 글자만 런타임에 채워 넣어서 한글 전체를 미리 구울 필요가 없음
                fontAsset = TMP_FontAsset.CreateFontAsset(
                    sourceFont,
                    90,
                    9,
                    GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic,
                    true);

                if (fontAsset == null)
                {
                    Debug.LogError("[AugmentFontSetup] TMP_FontAsset 생성에 실패했습니다.");
                    return;
                }

                fontAsset.name = "Pretendard-Medium SDF";
                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

                if (fontAsset.atlasTexture != null)
                {
                    fontAsset.atlasTexture.name = fontAsset.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
                }
                if (fontAsset.material != null)
                {
                    fontAsset.material.name = fontAsset.name + " Material";
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }

                EditorUtility.SetDirty(fontAsset);
                AssetDatabase.SaveAssets();
            }

            ApplyToAugmentUiPrefab(fontAsset);

            AssetDatabase.Refresh();
            Debug.Log("[AugmentFontSetup] Pretendard-Medium SDF 생성 + Augment UI 텍스트 적용 완료");
        }

        private static void ApplyToAugmentUiPrefab(TMP_FontAsset fontAsset)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(AugmentUiPrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogWarning($"[AugmentFontSetup] Augment UI 프리팹을 찾지 못해 폰트 에셋만 생성했습니다: {AugmentUiPrefabPath}");
                return;
            }

            bool changed = false;
            foreach (TMP_Text text in prefabRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = fontAsset;
                changed = true;
            }

            if (changed)
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, AugmentUiPrefabPath);

            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
}
#endif
