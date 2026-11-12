// 폭포 물줄기 면 (텍스처 없음, 전부 절차 생성).
//  - 줄기 = 세로로 늘인 value noise 두 겹. 아래로 갈수록 가속해서 길게 늘어남
//  - 좌우로 일렁이는 굴곡, 밝은 심지, 반짝이는 물방울 점
//  - 윗변(턱)·아랫변(수면)에서는 거품이 뭉쳐서 흰색으로 덮임
// 메시 정점 색: r = 높이 비율(0 아래 ~ 1 위), g = 폭 비율, a = 가장자리/윗변 알파
// 위치(OS)는 미터 단위라 폭포 크기와 상관없이 줄기 굵기가 같다.
Shader "HJH/FX/WaterfallSheet"
{
    Properties
    {
        _DeepColor ("Deep Color", Color) = (0.30, 0.55, 0.75, 1)
        _LightColor ("Light Color", Color) = (0.85, 0.96, 1, 1)
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _Speed ("Fall Speed (m/s)", Float) = 6
        _StreakDensity ("Streak Density (per m)", Float) = 2.2
        _StreakStretch ("Streak Length (m)", Float) = 5
        _Coverage ("Coverage", Range(0, 1)) = 0.5
        _BaseAlpha ("Base Alpha (water film)", Range(0, 1)) = 0.3
        _Sway ("Sway (m)", Float) = 0.12
        _Intensity ("Intensity", Float) = 1
        _FoamTop ("Top Foam", Range(0, 1)) = 0.6
        _FoamBottom ("Bottom Foam", Range(0, 1)) = 0.7
        _Sparkle ("Sparkle", Range(0, 1)) = 0.5
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+5" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Waterfall"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _LightColor;
                half4 _FoamColor;
                float _Speed;
                float _StreakDensity;
                float _StreakStretch;
                float _Coverage;
                float _BaseAlpha;
                float _Sway;
                float _Intensity;
                float _FoamTop;
                float _FoamBottom;
                float _Sparkle;
                float _Seed;
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
                float3 posOS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

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
                float t = _Time.y;
                float3 p = v.positionOS.xyz;
                // 물줄기가 앞뒤로 출렁임 (아래쪽일수록 크게)
                float h = v.color.r;
                p.z += (sin(p.y * 0.55 + t * 1.7 + p.x * 0.8 + _Seed) + sin(p.y * 0.23 - t * 1.1 + p.x * 1.7)) * 0.5 * _Sway * (1.0 - h * 0.7);
                o.positionCS = TransformObjectToHClip(p);
                o.color = v.color;
                o.posOS = v.positionOS.xyz;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float h = i.color.r;                         // 0 아래, 1 위
                float x = i.posOS.x + _Seed * 3.7;
                float y = i.posOS.y;

                // 아래로 갈수록 빨라짐: 위쪽은 느리게 넘어오고 아래에서 쏟아짐
                // (시간에 높이별 배율을 곱하면 시간이 갈수록 줄기가 압축돼 가로 얼룩이 되므로, 가속은 '길이'로만 표현)
                float spd = _Speed * 1.7;                                   // 체감 속도
                float stretchH = _StreakStretch * lerp(0.7, 1.6, h);        // 아래쪽 줄기는 짧고 촘촘, 위쪽은 길게 (정적)
                float wob = (VNoise(float2(x * 0.45, y * 0.18 + t * 0.6)) - 0.5) * 1.6;   // 줄기가 옆으로 휘청
                float fall = (t * spd) / _StreakStretch;

                float2 q1 = float2(x * _StreakDensity + wob, y / stretchH + fall);
                float2 q2 = float2(x * _StreakDensity * 2.3 + wob * 1.7 + 11.0, y / (stretchH * 0.5) + fall * 1.35 + 3.0);
                float s = VNoise(q1) * 0.6 + VNoise(q2) * 0.4;

                float thr = 1.0 - _Coverage;
                float streak = smoothstep(thr - 0.12, thr + 0.16, s);
                float core = pow(saturate(s * 1.25), 3.0);

                half3 col = lerp(_DeepColor.rgb, _LightColor.rgb, saturate(s * 1.5 + core * 0.4));
                col = lerp(col, _LightColor.rgb * 1.15, core * 0.55);

                // 반짝이는 물방울 점
                float sp = VNoise(float2(x * 7.0, y * 1.3 + t * spd * 1.4));
                float spark = smoothstep(0.86, 0.97, sp) * _Sparkle;
                col += spark * 0.9;

                float alpha = lerp(_BaseAlpha, 1.0, streak);
                alpha = max(alpha, spark * 0.8);

                // 윗변 거품: 턱에서 하얗게 뭉쳐 있다가 아래로 풀어짐
                float topN = VNoise(float2(x * 1.3, y * 0.9 - t * 0.8));
                float topBand = saturate((h - (1.0 - 0.16 * _FoamTop - topN * 0.05)) / (0.07));
                // 아랫변 거품: 수면 닿기 직전 하얗게 부서짐
                float botN = VNoise(float2(x * 2.2 + t * 0.35, y * 1.4 + t * 0.9));
                float botBand = saturate(((0.1 * _FoamBottom + botN * 0.05) - h) / (0.09 * _FoamBottom + 0.02)) * 0.85;
                float foam = saturate(max(topBand * _FoamTop, botBand));
                col = lerp(col, _FoamColor.rgb, foam);
                alpha = max(alpha, foam * 0.95);

                // 수면과 만나는 맨 아랫줄은 부드럽게 (바닥 거품 패치와 이어짐)
                alpha *= smoothstep(0.0, 0.03, h + 0.02);
                // 윗변은 노이즈로 들쭉날쭉하게 사라짐 (직선으로 잘린 모서리 방지)
                alpha *= saturate((1.0 - h - 0.004 - topN * 0.012) / 0.04);
                alpha *= i.color.a;
                return half4(col * _Intensity, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
