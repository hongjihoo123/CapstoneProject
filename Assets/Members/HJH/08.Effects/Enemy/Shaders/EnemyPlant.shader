Shader "HJH/FX/EnemyPlant"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade Color", Color) = (0.42, 0.48, 0.66, 1)
        _Ambient ("Ambient", Range(0, 2)) = 0.7
        [HDR] _GlowTint ("Glow Tint", Color) = (1, 1, 1, 1)
        _Glow ("Glow Strength", Range(0, 8)) = 2.2
        _Pulse ("Glow Pulse", Range(0, 1)) = 0.3
        _Rim ("Rim", Range(0, 2)) = 0.25
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half4 _ShadeColor;
            half4 _GlowTint;
            float _Ambient;
            float _Glow;
            float _Pulse;
            float _Rim;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half ndl = dot(n, light.direction) * light.shadowAttenuation;
                half lit = smoothstep(-0.05, 0.12, ndl);
                half3 albedo = i.color.rgb * _Tint.rgb;
                half3 col = albedo * (light.color * lerp(_ShadeColor.rgb * 0.5, 1, lit) + SampleSH(n) * _Ambient);
                float3 view = normalize(_WorldSpaceCameraPos - i.positionWS);
                col += albedo * pow(1 - saturate(dot(n, view)), 3) * _Rim * 3;
                half pulse = 1 - _Pulse * (0.5 + 0.5 * sin(_Time.y * 5 + i.positionWS.y * 2.5));
                col += albedo * _GlowTint.rgb * i.color.a * _Glow * pulse;
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            float4 vert(float4 positionOS : POSITION, float3 normalOS : NORMAL) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 direction = normalize(_LightPosition - positionWS);
            #else
                float3 direction = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, direction));
                return ApplyShadowClamping(positionCS);
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half4 frag(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings vert(float4 positionOS : POSITION, float3 normalOS : NORMAL)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
