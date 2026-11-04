// 지형 만화풍 셰이더 (터레인 머티리얼 템플릿). 레이어 4장까지 섞고 맵·캐릭터와 같은 2단 명암.
//  - 터레인이 _Control(섞는 비율), _Splat0~3(레이어 색), _Splat0_ST(타일) 를 넣어 줌
//  - 'Draw Instanced' 는 꺼야 함 (HJH/Map/만화풍 맵 적용 메뉴가 끔)
//  - 텍스처 잔무늬를 밉 바이어스로 뭉개서 사실적 흙·풀 사진 느낌 대신 칠한 면처럼
Shader "HJH/Map/ToonTerrain"
{
    Properties
    {
        [HideInInspector] _Control ("Control", 2D) = "red" {}
        [HideInInspector] _Splat0 ("Layer 0", 2D) = "grey" {}
        [HideInInspector] _Splat1 ("Layer 1", 2D) = "grey" {}
        [HideInInspector] _Splat2 ("Layer 2", 2D) = "grey" {}
        [HideInInspector] _Splat3 ("Layer 3", 2D) = "grey" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Saturation ("Saturation", Range(0, 2)) = 1.15
        _Brightness ("Brightness", Range(0.5, 2)) = 1.0
        _Simplify ("Simplify (밉 바이어스)", Range(0, 4)) = 2
        _ShadowColor ("Shadow Tint", Color) = (0.55, 0.58, 0.75, 1)
        _ShadowThreshold ("Shadow Threshold", Range(-1, 1)) = 0.1
        _ShadowSoftness ("Shadow Edge Softness", Range(0.001, 0.5)) = 0.06
        _ReceiveShadow ("Receive Shadow", Range(0, 1)) = 0.9
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry-100" "TerrainCompatible" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Control_ST;
            float4 _Splat0_ST, _Splat1_ST, _Splat2_ST, _Splat3_ST;
            half4 _Tint;
            half _Saturation;
            half _Brightness;
            half _Simplify;
            half4 _ShadowColor;
            half _ShadowThreshold;
            half _ShadowSoftness;
            half _ReceiveShadow;
        CBUFFER_END

        TEXTURE2D(_Control); SAMPLER(sampler_Control);
        TEXTURE2D(_Splat0); SAMPLER(sampler_Splat0);
        TEXTURE2D(_Splat1);
        TEXTURE2D(_Splat2);
        TEXTURE2D(_Splat3);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half3 Layer(TEXTURE2D_PARAM(tex, smp), float4 st, float2 uv)
            {
                return SAMPLE_TEXTURE2D_BIAS(tex, smp, uv * st.xy + st.zw, _Simplify).rgb;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 w = SAMPLE_TEXTURE2D(_Control, sampler_Control, i.uv);
                w /= max(dot(w, 1.0), 1e-3);
                half3 base = Layer(TEXTURE2D_ARGS(_Splat0, sampler_Splat0), _Splat0_ST, i.uv) * w.r
                           + Layer(TEXTURE2D_ARGS(_Splat1, sampler_Splat0), _Splat1_ST, i.uv) * w.g
                           + Layer(TEXTURE2D_ARGS(_Splat2, sampler_Splat0), _Splat2_ST, i.uv) * w.b
                           + Layer(TEXTURE2D_ARGS(_Splat3, sampler_Splat0), _Splat3_ST, i.uv) * w.a;
                base *= _Tint.rgb;
                half luma = dot(base, half3(0.299, 0.587, 0.114));
                half3 albedo = lerp(luma.xxx, base, _Saturation) * _Brightness;

                float3 n = normalize(i.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half shadow = lerp(1.0, light.shadowAttenuation, _ReceiveShadow);
                half lit = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, dot(n, light.direction)) * shadow;
                half3 ambient = SampleSH(n);
                half3 shade = albedo * _ShadowColor.rgb * (ambient * 0.7 + light.color * 0.3);
                half3 bright = albedo * (light.color + ambient * 0.4);
                return half4(MixFog(lerp(shade, bright, lit), i.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            float4 Vert(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir)));
            }
            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half Frag(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
