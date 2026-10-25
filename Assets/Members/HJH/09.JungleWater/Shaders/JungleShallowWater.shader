// 탑뷰 정글 얕은 물
//  - 물 두께(깊이 텍스처 기반)로 바닥 비침 → 탁한 물 색 전환
//  - 절차적 잔물결 + 파동 시뮬레이션 물결(_JungleRippleTex) 노멀
//  - 굴절(Opaque Texture), 물가 거품, 물결 거품, 반사색, 하이라이트, 그림자
// 필요: URP Asset 의 Depth Texture / Opaque Texture 켜기
Shader "HJH/JungleWater/ShallowWater"
{
    Properties
    {
        [Header(Color)]
        _ShallowTint ("Shallow Tint (바닥 비침 색)", Color) = (0.78, 0.86, 0.68, 1)
        _DeepColor ("Deep Color (탁한 물 색)", Color) = (0.09, 0.16, 0.11, 1)
        _Murkiness ("Murkiness (흐림 정도)", Range(0.1, 12)) = 3.5

        [Header(Surface Waves)]
        _WaveScale ("Wave Scale", Float) = 1.4
        _WaveSpeed ("Wave Speed", Float) = 0.25
        _WaveStrength ("Wave Normal Strength", Range(0, 2)) = 0.3
        _RefractionStrength ("Refraction", Range(0, 0.1)) = 0.02

        [Header(Interactive Ripples)]
        _RippleStrength ("Ripple Normal Strength", Range(0, 60)) = 18
        _RippleFoam ("Ripple Foam", Range(0, 10)) = 2.5

        [Header(Foam)]
        _FoamColor ("Foam Color", Color) = (0.82, 0.86, 0.74, 0.9)
        _FoamDistance ("Shore Foam Distance (m)", Range(0.01, 1)) = 0.12
        _FoamNoiseScale ("Foam Noise Scale", Float) = 3
        _EdgeFade ("Shore Edge Fade (m)", Range(0.001, 0.3)) = 0.05

        [Header(Lighting)]
        _ReflectionColor ("Reflection Color", Color) = (0.55, 0.66, 0.6, 1)
        _FresnelPower ("Fresnel Power", Range(1, 10)) = 4
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.5
        _SpecularPower ("Specular Power", Range(8, 512)) = 180
        _SpecularStrength ("Specular Strength", Range(0, 3)) = 0.7
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-50"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ShallowWater"
            Tags { "LightMode" = "UniversalForward" }

            // 바닥색은 Opaque Texture 에서 직접 섞고, 물가에서만 알파로 녹여 경계를 없앤다
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Include/JungleWaterCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowTint;
                half4 _DeepColor;
                float _Murkiness;
                float _WaveScale;
                float _WaveSpeed;
                float _WaveStrength;
                float _RefractionStrength;
                float _RippleStrength;
                float _RippleFoam;
                half4 _FoamColor;
                float _FoamDistance;
                float _FoamNoiseScale;
                float _EdgeFade;
                half4 _ReflectionColor;
                float _FresnelPower;
                float _ReflectionStrength;
                float _SpecularPower;
                float _SpecularStrength;
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

            // 두 겹의 흐르는 노이즈로 만든 잔물결 높이
            float WaveHeight(float2 p, float t)
            {
                return JW_ValueNoise(p + float2(t, t * 0.6)) * 0.6
                     + JW_ValueNoise(p * 2.1 + float2(-t * 1.3, t * 0.8)) * 0.4;
            }

            float2 WaveGradient(float2 p, float t)
            {
                const float e = 0.08;
                float hx = WaveHeight(p + float2(e, 0), t) - WaveHeight(p - float2(e, 0), t);
                float hz = WaveHeight(p + float2(0, e), t) - WaveHeight(p - float2(0, e), t);
                return float2(hx, hz) / (2.0 * e);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 posWS = i.positionWS;
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                float t = _Time.y * _WaveSpeed;

                // ---- 노멀: 잔물결 + 상호작용 물결 ----
                float2 waveGrad = WaveGradient(posWS.xz * _WaveScale, t) * _WaveStrength;
                float3 rippleN;
                float rippleH;
                JW_RippleNormal(posWS, _RippleStrength, rippleN, rippleH);
                float3 n = normalize(float3(-waveGrad.x + rippleN.x, 1.0, -waveGrad.y + rippleN.z));

                // ---- 물 두께: 수면 높이 - 바닥 높이 ----
                float3 bedWS = JW_ReconstructWorldPos(screenUV, SampleSceneDepth(screenUV));
                float depth = max(posWS.y - bedWS.y, 0.0);

                // ---- 굴절: 노멀만큼 UV 를 밀되, 물 위에 있는 물체를 끌어오면 취소 ----
                float2 refrUV = screenUV + n.xz * _RefractionStrength * saturate(depth * 2.0);
                float3 refrBed = JW_ReconstructWorldPos(refrUV, SampleSceneDepth(refrUV));
                if (refrBed.y > posWS.y)
                    refrUV = screenUV;
                else
                    depth = max(posWS.y - refrBed.y, 0.0);

                half3 sceneCol = SampleSceneColor(refrUV);

                // ---- 조명 ----
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(posWS));
                half shadow = mainLight.shadowAttenuation;
                half3 ambient = SampleSH(n);
                half3 diffuse = mainLight.color * saturate(dot(n, mainLight.direction)) * shadow + ambient;

                // ---- 색: 얕으면 바닥이 비치고, 깊을수록 탁한 물 색 ----
                float absorb = 1.0 - exp(-depth * _Murkiness);
                half3 transmitted = sceneCol * lerp(half3(1, 1, 1), _ShallowTint.rgb, saturate(depth * _Murkiness * 2.0));
                half3 col = lerp(transmitted, _DeepColor.rgb * diffuse, absorb);

                // ---- 반사 (비스듬히 볼수록 강함) ----
                float3 V = normalize(GetWorldSpaceViewDir(posWS));
                float fresnel = pow(1.0 - saturate(dot(n, V)), _FresnelPower);
                col = lerp(col, _ReflectionColor.rgb, fresnel * _ReflectionStrength);

                // ---- 거품: 물가 + 물결 마루 ----
                float foamNoise = JW_Fbm(posWS.xz * _FoamNoiseScale + float2(t * 0.6, -t * 0.4));
                float shore = 1.0 - saturate(depth / _FoamDistance);
                float foam = smoothstep(0.5, 0.7, shore + foamNoise * 0.6 - 0.3);
                foam += saturate(abs(rippleH) * _RippleFoam - 0.15) * smoothstep(0.3, 0.6, foamNoise);
                col = lerp(col, _FoamColor.rgb * diffuse, saturate(foam) * _FoamColor.a);

                // ---- 하이라이트 ----
                float3 H = normalize(mainLight.direction + V);
                col += mainLight.color * pow(saturate(dot(n, H)), _SpecularPower) * _SpecularStrength * shadow * (1.0 - saturate(foam));

                half alpha = saturate(depth / _EdgeFade + saturate(foam) * _FoamColor.a);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
