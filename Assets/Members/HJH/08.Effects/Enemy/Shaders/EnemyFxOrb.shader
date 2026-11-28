Shader "HJH/FX/EnemyOrb"
{
    Properties
    {
        _NoiseTex ("Noise", 2D) = "gray" {}
        _Flow1 ("Flow1 Tiling(xy) Scroll(zw)", Vector) = (2, 1, 0.15, -1.1)
        _Flow2 ("Flow2 Tiling(xy) Scroll(zw)", Vector) = (3, 1.6, -0.25, -1.8)
        [HDR] _DarkColor ("Dark", Color) = (0.02, 0.18, 0.04, 1)
        [HDR] _MidColor ("Mid", Color) = (0.15, 0.8, 0.12, 1)
        [HDR] _CoreColor ("Core", Color) = (1.6, 2.2, 0.8, 1)
        _Threshold ("Threshold", Range(-0.5, 1)) = 0.2
        _Rim ("Rim", Range(0, 3)) = 1
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Wobble ("Vertex Wobble", Range(0, 0.3)) = 0.05
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Flow1;
                float4 _Flow2;
                half4 _DarkColor;
                half4 _MidColor;
                half4 _CoreColor;
                float _Threshold;
                float _Rim;
                float _Opacity;
                float _Wobble;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float2 uv = v.uv * float2(2, 1.5) + float2(0.1, -0.9) * _Time.y;
                float n = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, uv, 0).g - 0.5;
                float3 p = v.positionOS.xyz + v.normalOS * n * _Wobble * 2;
                o.positionWS = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                half n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv * _Flow1.xy + _Flow1.zw * t).r;
                half n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv * _Flow2.xy + _Flow2.zw * t).b;
                half flow = saturate((n1 + n2 - 1) * 2.2 + 0.5);
                float3 view = normalize(_WorldSpaceCameraPos - i.positionWS);
                half facing = saturate(abs(dot(normalize(i.normalWS), view)));
                half rim = pow(1 - facing, 2) * _Rim;
                half heat = saturate(flow * 0.9 + rim * 0.5);
                half3 col = lerp(_DarkColor.rgb, _MidColor.rgb, smoothstep(0.2, 0.55, heat));
                col = lerp(col, _CoreColor.rgb, smoothstep(0.65, 0.95, heat));
                half alpha = smoothstep(_Threshold - 0.12, _Threshold + 0.12, flow + rim * 0.55);
                alpha *= i.color.a * _Opacity;
                return half4(col * i.color.rgb * alpha, alpha * 0.85);
            }
            ENDHLSL
        }
    }
}
