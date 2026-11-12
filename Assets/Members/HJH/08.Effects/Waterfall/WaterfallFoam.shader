// 수면에 떠 있는 포말 (레이스처럼 얽힌 거품). 입자(수평 빌보드)와 바닥 패치 쿼드 양쪽에 쓴다.
//  - Voronoi 셀 경계 = 거품 알갱이가 서로 맞닿은 선, 거기에 덩어리 노이즈를 섞음
//  - _Radial 1 = 입자(원형 페이드), 0 = 패치(사각형 페이드, 폭포 반대쪽으로 흘러나가며 사라짐)
Shader "HJH/FX/WaterfallFoam"
{
    Properties
    {
        _Radial ("Radial (1) / Box (0)", Range(0, 1)) = 1
        _Scale ("Cell Scale", Float) = 7
        _FlowSpeed ("Flow Speed", Float) = 0.6
        _Intensity ("Intensity", Float) = 1
        _Coverage ("Coverage", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+6" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "Foam"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Radial;
                float _Scale;
                float _FlowSpeed;
                float _Intensity;
                float _Coverage;
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
                float seed : TEXCOORD1;
            };

            float2 Hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

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

            // x = F2 - F1 (셀 경계에서 0), y = F1, z = 가장 가까운 셀의 난수(0~1)
            float3 Voronoi(float2 p, float tt)
            {
                float2 ip = floor(p), fp = frac(p);
                float f1 = 8.0, f2 = 8.0, id = 0.0;
                for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 g = float2(x, y);
                        float2 o = Hash22(ip + g);
                        o = 0.5 + 0.45 * sin(tt + 6.2831 * o);   // 셀이 살살 움직임
                        float d = length(g + o - fp);
                        if (d < f1) { f2 = f1; f1 = d; id = Hash21(ip + g + 3.7); }
                        else if (d < f2) f2 = d;
                    }
                return float3(f2 - f1, f1, id);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                float3 wp = TransformObjectToWorld(float3(0, 0, 0));
                o.seed = Hash21(wp.xz * 0.41 + wp.y * 0.13) * 30.0;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float2 uv = i.uv;
                // 패치일 때는 폭포 반대쪽(+v)으로 천천히 흘러나감
                float2 flow = float2(0, -t * _FlowSpeed * (1.0 - _Radial) * 0.35);
                float2 p = (uv + flow) * _Scale + i.seed;

                // 도메인을 일그러뜨려 셀이 벌집처럼 규칙적으로 보이지 않게
                p += (float2(VNoise(p * 0.55 + t * 0.12), VNoise(p * 0.55 + 17.0 - t * 0.1)) - 0.5) * 1.8;

                float3 vor = Voronoi(p, t * 0.8);
                float3 vor2 = Voronoi(p * 2.1 + 5.0, t * 1.1);                             // 잔 거품 한 겹 더
                float lace = (1.0 - smoothstep(0.0, 0.10, vor.x)) * lerp(0.2, 1.0, vor.z);   // 일부 셀 경계만 살아 있음
                float lace2 = (1.0 - smoothstep(0.0, 0.12, vor2.x)) * lerp(0.1, 1.0, vor2.z) * 0.6;
                lace = max(lace, lace2);
                float bubble = (1.0 - smoothstep(0.10, 0.30, vor.y)) * step(0.55, vor.z);   // 듬성듬성 큰 거품 하이라이트
                float blob = VNoise(p * 0.35 + t * 0.1) * 0.6 + VNoise(p * 0.9 - 4.0) * 0.4;

                float cover = smoothstep(1.0 - _Coverage - 0.10, 1.0 - _Coverage + 0.12, blob);
                float foam = saturate(lace * 0.9 + bubble * 0.35) * cover + cover * 0.22;

                // 가장자리 페이드
                float2 c = (uv - 0.5) * 2.0;
                float radial = saturate(1.0 - dot(c, c));
                float box = smoothstep(0.0, 0.22, uv.x) * smoothstep(0.0, 0.22, 1.0 - uv.x)
                          * smoothstep(0.0, 0.12, uv.y) * smoothstep(0.0, 0.75, 1.0 - uv.y);
                float falloff = lerp(box, radial * radial, _Radial);

                float a = saturate(foam * falloff * i.color.a * 1.35);
                half3 col = lerp(half3(0.78, 0.9, 0.96), half3(1, 1, 1), saturate(lace + bubble * 0.5)) * i.color.rgb;
                return half4(col * _Intensity, a);
            }
            ENDHLSL
        }
    }
}
