// 폭포가 수면에 닿는 곳에 쌓이는 만화풍 거품 더미 (구름처럼 울퉁불퉁한 하얀 덩어리).
//  - 윗선이 큰 혹 + 잔 혹이 겹친 구름 모양 (톱니 X, 둥글게 부푼 모양)
//  - 혹 높이가 초당 몇 번씩 뚝뚝 바뀌어서 거품이 들썩이는 손맛
//  - 안쪽에 회청색 그늘 덩이가 얼룩져서 부피가 느껴지고, 바닥은 물빛에 섞임
// 쿼드(uv 0~1)에 그린다: u = 폭, v = 높이
Shader "HJH/FX/WaterfallCrown"
{
    Properties
    {
        _Count ("Big bumps across width", Float) = 12
        _FlickerRate ("Bob rate (per sec)", Float) = 7
        _Seed ("Seed", Float) = 0
        _LowColor ("Water Tint", Color) = (0.55, 0.85, 1, 1)
        _HighColor ("Foam Color", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade Color", Color) = (0.60, 0.72, 0.90, 1)
        _Intensity ("Intensity", Float) = 1.1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+9" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "Crown"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Count;
                float _FlickerRate;
                float _Seed;
                half4 _LowColor;
                half4 _HighColor;
                half4 _ShadeColor;
                float _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float VNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i), b = Hash21(i + float2(1, 0)), c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            // 둥근 혹 한 줄: 칸마다 반원, 높이는 rate 에 맞춰 뚝뚝 바뀜 (스타일라이즈 프레임 점프)
            float Bumps(float u, float count, float seedOff, float rate)
            {
                float x = u * count;
                float cell = floor(x);
                float f = frac(x);
                float stepT = floor(_Time.y * rate + Hash21(float2(cell, seedOff)) * 3.0);
                float h = lerp(0.35, 1.0, Hash21(float2(cell + seedOff, stepT)));
                float d = 2.0 * f - 1.0;
                return h * sqrt(saturate(1.0 - d * d));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float u = i.uv.x, v = i.uv.y;
                float ux = u + _Seed * 0.173;

                // 윗선: 바닥 높이 + 큰 혹 + 잔 혹 (겹쳐서 구름 윤곽)
                float profile = 0.36 + 0.28 * Bumps(ux, _Count, 1.0 + _Seed, _FlickerRate * 0.35)
                                     + 0.18 * Bumps(ux, _Count * 3.4, 5.0 + _Seed, _FlickerRate);
                float alpha = smoothstep(profile, profile - 0.045, v);

                // 안쪽 회청색 그늘 덩이 (프레임 점프로 자글자글하게 움직임)
                float tj = floor(_Time.y * 6.0) * 0.37;
                float n = VNoise(float2(ux * _Count * 2.4 + tj, v * 6.0 + _Seed)) * 0.6
                        + VNoise(float2(ux * _Count * 5.0 - tj, v * 11.0 + 3.0)) * 0.4;
                float shade = smoothstep(0.52, 0.72, n) * smoothstep(0.05, 0.35, v) * (1.0 - smoothstep(profile * 0.7, profile, v));

                half3 col = lerp(_HighColor.rgb, _ShadeColor.rgb, shade * 0.6);
                // 윗가장자리는 한 번 더 하얗게 (거품 끝이 반짝)
                col = lerp(col, _HighColor.rgb, smoothstep(profile - 0.12, profile, v) * 0.7);
                // 바닥은 물빛에 섞임
                col = lerp(col, _LowColor.rgb, (1.0 - smoothstep(0.0, 0.16, v)) * 0.45);

                float sideFade = smoothstep(0.0, 0.03, u) * smoothstep(0.0, 0.03, 1.0 - u);
                return half4(col * _Intensity, alpha * sideFade);
            }
            ENDHLSL
        }
    }
}
