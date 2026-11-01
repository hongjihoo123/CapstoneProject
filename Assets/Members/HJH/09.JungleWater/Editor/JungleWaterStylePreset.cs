using UnityEditor;
using UnityEngine;

namespace Assets.Members.HJH.Environment.EditorTools
{
    // 메뉴: HJH > Jungle Water > 카툰풍 물 스타일 적용
    // 피드백 "물이 너무 실사 게임 같다" → "조금 더 카툰풍" 기준으로 물 값을 한 번에 바꿈
    //  - 색: 밝은 하늘빛 청록(얕음) → 청록 → 진한 청록(깊음) 또렷한 4단계 띠 (셰이더의 JW_SoftBands)
    //  - 물가 거품: 또렷한 흰 띠 + 바깥에 끊어진 거품 줄 하나 더 (JW_ToonFoamLine)
    //  - 반짝임은 하얀 덩어리(계단), 굴절·반사는 거의 없앰 (사진 같은 질감 제거)
    // HLSL 머티리얼과 노드 머티리얼 둘 다 바꿈 (노드 그래프 다시 만들 때 HLSL 값을 복사해 가므로)
    public static class JungleWaterStylePreset
    {
        private static readonly string[] Materials =
        {
            "Assets/Members/HJH/09.JungleWater/Generated/Materials/M_JungleWater.mat",
            "Assets/Members/HJH/09.JungleWater/Generated/Materials/M_JungleWater_Node.mat",
        };

        [MenuItem("HJH/Jungle Water/카툰풍 물 스타일 적용")]
        public static void Apply()
        {
            int count = 0;
            foreach (string path in Materials)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                    continue;

                // 색 (배경의 붓터치 느낌 나뭇잎과 어울리는 청록~녹색)
                SetColor(m, "_ShallowTint", new Color(0.55f, 0.95f, 0.8f, 1f));
                SetColor(m, "_DeepColor", new Color(0.12f, 0.45f, 0.38f, 1f));
                SetFloat(m, "_Murkiness", 2.2f);   // 얕은 색 띠가 넓게 보이게

                // 물결: 크고 느리고 약하게
                SetFloat(m, "_WaveScale", 0.6f);
                SetFloat(m, "_WaveSpeed", 0.18f);
                SetFloat(m, "_WaveStrength", 0.04f);   // 카툰: 표면 잔무늬 거의 없이 평평한 색
                SetFloat(m, "_RefractionStrength", 0f);
                SetFloat(m, "_RippleStrength", 10f);   // 물결은 음영보다 흰 거품 고리로 보이게

                // 스킬·캐릭터 물결은 게임 피드백이라 거품을 더 진하게
                SetFloat(m, "_RippleFoam", 3.5f);

                // 물가 거품 띠
                SetColor(m, "_FoamColor", new Color(0.96f, 1f, 0.97f, 1f));
                SetFloat(m, "_FoamDistance", 0.07f);   // 물이 얕아서 넓히면 평평한 얕은 곳이 통째로 하얘짐 (하늘 반사처럼 보임)
                SetFloat(m, "_FoamNoiseScale", 2.2f);
                SetFloat(m, "_EdgeFade", 0.06f);

                // 반사·반짝임: 넓고 은은하게
                SetColor(m, "_ReflectionColor", new Color(0.6f, 0.88f, 0.86f, 1f));
                SetFloat(m, "_FresnelPower", 5f);
                SetFloat(m, "_ReflectionStrength", 0f);
                SetFloat(m, "_SpecularPower", 140f);   // 큰 흰 얼룩(하늘빛처럼 보임) → 작은 반짝임
                SetFloat(m, "_SpecularStrength", 0.55f);

                // 물에 비치는 구름: 또렷한 카툰 구름이 천천히 흘러감
                SetColor(m, "_CloudColor", new Color(0.88f, 0.97f, 0.94f, 1f));
                SetFloat(m, "_CloudStrength", 0.25f);   // 은은하게 비치는 정도
                SetFloat(m, "_CloudScale", 0.03f);   // 크고 드문드문
                SetFloat(m, "_CloudCoverage", 0.6f);
                SetFloat(m, "_CloudSoftness", 0.1f);

                EditorUtility.SetDirty(m);
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[JungleWater] 카툰풍 물 스타일 적용 ({count}개 머티리얼)");
        }

        private static void SetFloat(Material m, string name, float value)
        {
            if (m.HasProperty(name))
                m.SetFloat(name, value);
        }

        private static void SetColor(Material m, string name, Color value)
        {
            if (m.HasProperty(name))
                m.SetColor(name, value);
        }
    }
}
