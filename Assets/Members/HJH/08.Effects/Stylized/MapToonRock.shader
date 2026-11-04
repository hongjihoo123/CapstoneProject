// 맵 만화풍 바위 셰이더: HJH/Map/Toon 과 같은 그림, _Fade 대신 _BaseColor 알파로 디더 반투명
//  (_Fade 가 있으면 가림 처리 스크립트가 나무처럼 상자 검사로 다뤄서 큰 절벽이 늘 투명해짐 → 바위는 광선 검사 경로를 타도록 _Fade 를 뺌)
//  가림 처리가 바꿔 끼우는 Resources/HJH/RockFade/<이름>_Fade 도 이 셰이더 복사본
Shader "HJH/Map/ToonRock"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 1)) = 0.4
        _Saturation ("Saturation", Range(0, 2)) = 1.2
        _Brightness ("Brightness", Range(0.5, 2)) = 1.05
        _Simplify ("Simplify (텍스처 잔무늬 줄이기, 밉 바이어스)", Range(0, 4)) = 1.5

        [Header(Shading)]
        _ShadowColor ("Shadow Tint", Color) = (0.55, 0.58, 0.75, 1)
        _ShadowThreshold ("Shadow Threshold", Range(-1, 1)) = 0.0
        _ShadowSoftness ("Shadow Edge Softness", Range(0.001, 0.5)) = 0.06
        _ReceiveShadow ("Receive Shadow", Range(0, 1)) = 0.85

        [Header(Rim)]
        _RimColor ("Rim Color", Color) = (1, 0.97, 0.88, 1)
        _RimPower ("Rim Width", Range(0.5, 8)) = 4
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.25

        [Header(Outline)]
        _OutlineColor ("Outline Color (x base color)", Color) = (0.3, 0.26, 0.3, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.01)) = 0.0015

        [Header(Alpha and Wind)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Cutoff", Range(0, 1)) = 0.5
        [Toggle] _GreenKey ("Green Key (검정 배경 잎 그림)", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle(_WIND_ON)] _Wind ("Wind", Float) = 0
        _WindSpeed ("Wind Speed", Float) = 1.6
        _WindStrength ("Wind Strength", Float) = 0.12
        _WindDirection ("Wind Direction", Vector) = (1, 0, 0.4, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #define HJH_ROCK
        #include "MapToon.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WIND_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                half fog : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                float3 positionWS = Wind(TransformObjectToWorld(input.positionOS.xyz), input.uv);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                VertexNormalInputs n = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                o.normalWS = n.normalWS;
                o.tangentWS = float4(n.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                DitherClip(i.positionCS);
                half4 base = SampleBase(i.uv);
                half luma = dot(base.rgb, half3(0.299, 0.587, 0.114));
                half3 albedo = lerp(luma.xxx, base.rgb, _Saturation) * _Brightness;

                float3 n = normalize(i.normalWS) * (front ? 1.0 : -1.0);   // 양면 잎의 뒷면
                half3 tn = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _NormalStrength);
                float3 bitangent = i.tangentWS.w * cross(i.normalWS, i.tangentWS.xyz);
                n = normalize(TransformTangentToWorld(tn, float3x3(i.tangentWS.xyz, bitangent, n)));
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));

                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half shadow = lerp(1.0, light.shadowAttenuation, _ReceiveShadow);
                half ndl = dot(n, light.direction);
                half lit = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, ndl) * shadow;
                half3 ambient = SampleSH(n);
                half3 shade = albedo * _ShadowColor.rgb * (ambient * 0.7 + light.color * 0.3);
                half3 bright = albedo * (light.color + ambient * 0.4);
                half3 color = lerp(shade, bright, lit);

                half rim = pow(saturate(1.0 - dot(n, v)), _RimPower);
                color += _RimColor.rgb * smoothstep(0.35, 0.55, rim) * _RimStrength * lerp(0.3, 1.0, lit) * light.color;

                return half4(MixFog(color, i.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WIND_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float4 clip = TransformWorldToHClip(Wind(TransformObjectToWorld(input.positionOS.xyz), input.uv));
                float3 normalCS = mul((float3x3)UNITY_MATRIX_VP, TransformObjectToWorldNormal(input.normalOS));
                float2 offset = normalize(normalCS.xy + 1e-5);
                offset.x *= _ScreenParams.y / _ScreenParams.x;
                clip.xy += offset * _OutlineWidth * clip.w * 2.0;
                o.positionCS = clip;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fog = ComputeFogFactor(clip.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                DitherClip(i.positionCS);
                half4 base = SampleBase(i.uv);
                return half4(MixFog(base.rgb * _OutlineColor.rgb, i.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WIND_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = Wind(TransformObjectToWorld(input.positionOS.xyz), input.uv);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir)));
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                SampleBase(i.uv);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WIND_ON
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformWorldToHClip(Wind(TransformObjectToWorld(input.positionOS.xyz), input.uv));
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half Frag(Varyings i) : SV_Target
            {
                DitherClip(i.positionCS);
                SampleBase(i.uv);
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WIND_ON
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformWorldToHClip(Wind(TransformObjectToWorld(input.positionOS.xyz), input.uv));
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                DitherClip(i.positionCS);
                SampleBase(i.uv);
                return half4(NormalizeNormalPerPixel(i.normalWS), 0);
            }
            ENDHLSL
        }
    }
}
