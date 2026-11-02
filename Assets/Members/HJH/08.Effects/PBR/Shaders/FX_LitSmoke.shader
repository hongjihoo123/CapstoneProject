// 빛을 받는 연기·흙먼지 (PBR 배경에 섞이게)
//  - 시트 애니메이션(밀도 = 알파) + 노멀 시트로 해·주변광에 따라 밝고 어두운 면이 생김
//  - 소프트 파티클: 바닥과 겹치는 곳이 칼같이 잘리지 않음
//  - 색 = 머티리얼 색 × 파티클 색
Shader "HJH/FX/LitSmoke"
{
    Properties
    {
        _MainTex ("Density Flipbook (A)", 2D) = "white" {}
        _NormalTex ("Normal Flipbook", 2D) = "bump" {}
        _Color ("Color", Color) = (0.5, 0.48, 0.45, 1)
        _LightWrap ("Light Wrap (뒤쪽도 조금 밝게)", Range(0, 1)) = 0.45
        _Ambient ("Ambient", Range(0, 2)) = 1
        _Density ("Density", Range(0, 3)) = 1.2
        _SoftDistance ("Soft Distance (m)", Float) = 0.8
        [Header(Toon)]
        [Toggle] _Toon ("Toon (2단 음영 + 또렷한 외곽)", Float) = 0
        _ShadowColor ("Shadow Color (카툰 그림자 면)", Color) = (0.2, 0.18, 0.24, 1)
        _AlphaCut ("Alpha Cut (카툰 외곽 위치)", Range(0.05, 0.9)) = 0.35
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "LitSmoke"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex FxVert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "FX_ParticleCommon.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalTex); SAMPLER(sampler_NormalTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _LightWrap;
                float _Ambient;
                float _Density;
                float _SoftDistance;
                float _Toon;
                half4 _ShadowColor;
                float _AlphaCut;
            CBUFFER_END

            half4 frag(FxVaryings i) : SV_Target
            {
                half density = FxSampleFlipbook(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i).a;
                half3 nTex = FxSampleFlipbook(TEXTURE2D_ARGS(_NormalTex, sampler_NormalTex), i).rgb * 2.0 - 1.0;

                // 빌보드 면 기준 좌표계: 카메라의 오른쪽·위·뒤
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 back = UNITY_MATRIX_V[2].xyz;
                float3 n = normalize(right * nTex.x + up * nTex.y + back * max(nTex.z, 0.2));

                Light light = GetMainLight();
                float ndl = dot(n, light.direction);
                float diffuse = saturate(ndl * (1.0 - _LightWrap) + _LightWrap);
                half3 lit = light.color * diffuse + SampleSH(n) * _Ambient;
                // 자기 그림자: 짙은 속은 어둡고 얇은 가장자리는 빛이 비침 → 납작하지 않고 덩어리져 보임
                lit *= lerp(1.15, 0.55, saturate(density * _Density));

                half3 col = _Color.rgb * i.color.rgb * lit;
                half alpha = saturate(density * _Density) * i.color.a * _Color.a * FxSoftFade(i, _SoftDistance);

                if (_Toon > 0.5)
                {
                    // 카툰(젠존제 풍): 밝은 면·그림자 면 2단 + 칼같은 외곽
                    half litSide = smoothstep(0.48, 0.52, diffuse * lerp(1.1, 0.6, saturate(density * _Density)));
                    col = lerp(_ShadowColor.rgb, _Color.rgb * 1.25, litSide) * i.color.rgb;
                    alpha = smoothstep(_AlphaCut - 0.03, _AlphaCut + 0.03, alpha) * i.color.a;
                }
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
