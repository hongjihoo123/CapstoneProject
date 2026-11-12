// 폭포 물안개 입자: 텍스처 없이 노이즈로 뭉게뭉게한 덩어리를 만든다.
//  - 가장자리가 노이즈로 깎여서 동그란 얼룩이 아니라 구름 덩어리처럼 보임
//  - 파티클 색의 알파가 줄어들수록 덩어리가 안쪽부터 깎여 사라진다 (그냥 투명해지는 것보다 자연스러움)
//  - 위쪽이 밝고 아래쪽이 어두운 간이 음영
Shader "HJH/FX/WaterfallMist"
{
    Properties
    {
        _Softness ("Softness", Range(0.4, 4)) = 1.2
        _Billow ("Billow (noise strength)", Range(0, 1.5)) = 0.9
        _Intensity ("Intensity", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+8" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "Mist"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Softness;
                float _Billow;
                float _Intensity;
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
                o.color = v.color;
                o.uv = v.uv;
                // 파티클마다 다른 노이즈 모양: 월드 위치 기반 (빌보드라 정점마다 같은 값)
                float3 wp = TransformObjectToWorld(float3(0, 0, 0));
                o.seed = Hash21(wp.xz * 0.37 + wp.y * 0.11) * 40.0;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                float r2 = dot(p, p);
                float radial = pow(saturate(1.0 - r2), _Softness);

                float2 np = i.uv * 3.2 + i.seed;
                float n = VNoise(np) * 0.55 + VNoise(np * 2.3 + 7.0) * 0.3 + VNoise(np * 5.1 + 3.0) * 0.15;
                float shape = radial * lerp(1.0, n * 1.6, _Billow);

                // 알파가 줄수록 안쪽부터 깎임
                float erode = (1.0 - i.color.a) * 0.55;
                float a = saturate((shape - erode) / max(0.05, 1.0 - erode)) * saturate(i.color.a * 2.2);

                half3 col = i.color.rgb * lerp(0.78, 1.12, saturate(i.uv.y + n * 0.25 - 0.1));
                return half4(col * _Intensity, a * i.color.a);
            }
            ENDHLSL
        }
    }
}
