// 탑뷰용 낮게 깔리는 늪 안개
// 안개층 "윗면"에 평평한 판을 하나 두고 이 셰이더를 쓴다.
// 픽셀마다 판 → 깊이 텍스처의 바닥까지 몇 걸음 걸어가며(레이마칭) 안개를 쌓는다.
//  - 낮은 곳(물가)일수록 짙고, 나무·언덕은 안개 위로 솟는다
//  - 3D 노이즈가 바람에 흘러간다
//  - 플레이어 주변은 걷힌다 (_JunglePlayerPos)
// 필요: URP Asset 의 Depth Texture 켜기
Shader "HJH/JungleWater/GroundFog"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.62, 0.7, 0.62, 1)
        _Density ("Density", Range(0, 4)) = 1.1
        _FogThickness ("Fog Thickness (m, 판에서 이만큼 내려가면 최대 농도)", Float) = 1.6
        _MaxMarchDistance ("Max March Distance (m)", Float) = 25
        _NoiseScale ("Noise Scale", Float) = 0.35
        _NoiseContrast ("Noise Contrast", Range(0.5, 4)) = 1.6
        _Wind ("Wind (xyz)", Vector) = (0.3, 0.05, 0.15, 0)
        _LightInfluence ("Light Influence", Range(0, 1)) = 0.4

        [Header(Player Reveal)]
        _RevealRadius ("Clear Radius", Float) = 3
        _RevealSoftness ("Clear Softness", Float) = 3
        _RevealMin ("Density Near Player", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            // 물(Transparent-50) 다음, 파티클(Transparent) 전 → 스킬 이펙트는 안개에 안 묻힘
            "Queue" = "Transparent-10"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GroundFog"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Include/JungleWaterCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                float _Density;
                float _FogThickness;
                float _MaxMarchDistance;
                float _NoiseScale;
                float _NoiseContrast;
                float4 _Wind;
                float _LightInfluence;
                float _RevealRadius;
                float _RevealSoftness;
                float _RevealMin;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            // 노드 버전(SG_JungleGroundFog)과 같은 순서 / 같은 공식
            half4 frag(Varyings i) : SV_Target
            {
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);

                // [SGS_SceneBed] 안개층 윗면(이 판) → 바닥
                float3 entry = i.positionWS;
                float3 exit = JW_BedFromRay(entry, LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams));

                // [JungleFogMarch] 반복문으로 안개 쌓기
                float fog = JW_FogMarch(entry, exit, screenUV, _Density, _FogThickness, _MaxMarchDistance,
                                        _NoiseScale, _NoiseContrast, _Wind.xyz,
                                        _RevealRadius, _RevealSoftness, _RevealMin);

                // [SGS_MainLight] 해 색을 살짝 받는 안개 색
                float3 lightDir, lightColor, ambient;
                float shadow;
                JW_MainLight(entry, float3(0, 1, 0), lightDir, lightColor, shadow, ambient);
                float3 color = lerp(_FogColor.rgb, _FogColor.rgb * lightColor, _LightInfluence);

                return half4(color, fog * _FogColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
