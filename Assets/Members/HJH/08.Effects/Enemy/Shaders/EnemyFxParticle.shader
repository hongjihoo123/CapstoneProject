Shader "HJH/FX/EnemyParticle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Color", Color) = (0.5, 0.5, 0.55, 1)
        _Additive ("Additive", Range(0, 1)) = 0
        _NoiseTex ("Noise", 2D) = "gray" {}
        _NoiseScroll ("Noise Tiling(xy) Scroll(zw)", Vector) = (1, 1, 0.1, 0.15)
        _Distort ("UV Distort", Range(0, 0.3)) = 0
        _Erode ("Erode By Particle Alpha", Range(0, 1)) = 0
        _Soft ("Erode Softness", Range(0.01, 0.6)) = 0.15
        _SoftDistance ("Soft Distance", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
        _DepthTolerance ("Ground Depth Tolerance", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _ShadowColor;
                float4 _NoiseScroll;
                float _Additive;
                float _Distort;
                float _Erode;
                float _Soft;
                float _SoftDistance;
                float _DepthTolerance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 noiseUV = i.uv * _NoiseScroll.xy + _NoiseScroll.zw * _Time.y + i.color.rg * 0.37;
                half3 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).rgb;
                float2 uv = i.uv + (noise.rg - 0.5) * _Distort;
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                half alpha = tex.a;
                half fade = i.color.a;
                if (_Erode > 0)
                {
                    half cut = (1 - i.color.a) * 1.15;
                    half eroded = smoothstep(cut - _Soft, cut + _Soft * 0.2, noise.b * 0.75 + tex.a * 0.35);
                    alpha *= lerp(1, eroded, _Erode);
                    fade = lerp(i.color.a, saturate(i.color.a * 3), _Erode);
                }
                alpha = saturate(alpha * fade * _Color.a);

                if (_SoftDistance > 0 || _DepthTolerance > 0)
                {
                    float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                    float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    float diff = sceneEye - LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                    if (_DepthTolerance > 0)
                        alpha *= smoothstep(-_DepthTolerance, -_DepthTolerance * 0.5, diff);
                    else
                        alpha *= saturate(diff / _SoftDistance);
                }

                half3 col = lerp(_ShadowColor.rgb, _Color.rgb, saturate((tex.r - 0.55) * 2.4)) * i.color.rgb;
                return half4(col * alpha, alpha * (1 - _Additive));
            }
            ENDHLSL
        }
    }
}
