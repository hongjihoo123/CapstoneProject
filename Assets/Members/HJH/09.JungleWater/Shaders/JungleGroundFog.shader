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

            #define FOG_STEPS 8

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

            float FogNoise(float3 p, float t)
            {
                float3 q = p * _NoiseScale;
                float n = JW_ValueNoise3(q + _Wind.xyz * t) * 0.65
                        + JW_ValueNoise3(q * 2.3 - _Wind.xyz * t * 1.3) * 0.35;
                return saturate((n - 0.5) * _NoiseContrast + 0.5);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 entry = i.positionWS;   // 안개층 윗면
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                float3 exit = JW_ReconstructWorldPos(screenUV, SampleSceneDepth(screenUV));

                float3 ray = exit - entry;
                float len = length(ray);
                if (len < 1e-3)
                    return 0;

                float3 dir = ray / len;
                len = min(len, _MaxMarchDistance);
                float stepLen = len / FOG_STEPS;
                float jitter = JW_Hash21(i.positionCS.xy);   // 띠무늬 줄이기
                float t = _Time.y;

                float transmittance = 1.0;
                [unroll] for (int s = 0; s < FOG_STEPS; s++)
                {
                    float3 p = entry + dir * ((s + jitter) * stepLen);
                    float heightFactor = saturate((entry.y - p.y) / _FogThickness);
                    float reveal = JW_PlayerReveal(p, _RevealRadius, _RevealSoftness, _RevealMin);
                    float density = _Density * heightFactor * FogNoise(p, t) * reveal;
                    transmittance *= exp(-density * stepLen);
                }

                Light mainLight = GetMainLight();
                half3 color = lerp(_FogColor.rgb, _FogColor.rgb * mainLight.color, _LightInfluence);
                return half4(color, (1.0 - transmittance) * _FogColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
