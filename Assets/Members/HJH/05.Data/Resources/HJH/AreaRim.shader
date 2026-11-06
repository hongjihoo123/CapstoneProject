// 스킬 범위·적 공격 예고 (AttackAreaVisualizer)
//  - 바닥에 깐 사각형 하나에, 범위 모양(원·사각형·부채꼴) 여러 개를 "합친" 테두리를 그림
//    → 사각형+부채꼴이 겹쳐도 안쪽 선은 사라지고 바깥 테두리만 하나로 이어짐
//  - 모양: 테두리는 밝은 선, 안쪽으로 갈수록 은은하게 사라지는 빛 (Circle116 텍스처 느낌)
//  - 나무·바위 뒤에 가려진 부분만 연하게 (_OccludedAlpha). 가리는 게 없으면(카메라에 더 가까우면) 그대로
//  - Resources 폴더에 둬서 빌드에도 포함
Shader "HJH/AreaRim"
{
    Properties
    {
        _Color ("Color", Color) = (0.4, 0.9, 1, 0.95)
        _LineWidth ("Edge line width (m)", Float) = 0.08
        _GlowWidth ("Inner glow width (m)", Float) = 0.5
        _GlowStrength ("Inner glow strength", Range(0, 1)) = 0.35
        _OccludedAlpha ("Alpha when hidden behind objects", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+100" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        #define MAX_SHAPES 8

        // MaterialPropertyBlock 으로 매 프레임 넣음 (모양 개수가 바뀌어서 SRP Batcher 는 안 씀)
        float4 _Color;
        float _LineWidth;
        float _GlowWidth;
        float _GlowStrength;
        float _OccludedAlpha;
        float _ShapeCount;
        float4 _ShapeA[MAX_SHAPES];   // x = 종류(0 원, 1 사각형, 2 부채꼴), y/z = 월드 x/z, w = yaw(라디안)
        float4 _ShapeB[MAX_SHAPES];   // 원: x = 반지름 / 사각형: x,y = 반폭·반길이 / 부채꼴: x = 반지름, y = 반각(라디안)

        struct Attributes { float4 positionOS : POSITION; };
        struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

        Varyings vert(Attributes v)
        {
            Varyings o;
            o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            return o;
        }

        // 부채꼴 (Inigo Quilez sdPie). p.y = 앞, c = (sin 반각, cos 반각)
        float SdPie(float2 p, float2 c, float r)
        {
            p.x = abs(p.x);
            float l = length(p) - r;
            float m = length(p - c * clamp(dot(p, c), 0.0, r));
            return max(l, m * sign(c.y * p.x - c.x * p.y));
        }

        float SdBox(float2 p, float2 halfSize)
        {
            float2 q = abs(p) - halfSize;
            return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
        }

        // 바깥 + / 안쪽 - 거리. 모양 여러 개는 min = 합집합
        float SceneDistance(float2 p)
        {
            float d = 1e5;
            [loop] for (int i = 0; i < MAX_SHAPES; i++)
            {
                if (i >= (int)_ShapeCount) break;
                float4 a = _ShapeA[i];
                float4 b = _ShapeB[i];
                float2 q = p - a.yz;
                float s, c;
                sincos(a.w, s, c);
                // Unity yaw: 앞 = (sin, cos), 오른쪽 = (cos, -sin)  (x, z)
                float2 local = float2(dot(q, float2(c, -s)), dot(q, float2(s, c)));

                float shape;
                if (a.x < 0.5)      shape = length(q) - b.x;
                else if (a.x < 1.5) shape = SdBox(local, b.xy);
                else
                {
                    float halfAngle = min(b.y, 3.14159);
                    shape = SdPie(local, float2(sin(halfAngle), cos(halfAngle)), b.x);
                }
                d = min(d, shape);
            }
            return d;
        }

        half RimAlpha(float3 positionWS)
        {
            float d = SceneDistance(positionWS.xz);
            float aa = max(fwidth(d), 1e-4);
            float halfLine = _LineWidth * 0.5;
            float edge = 1.0 - smoothstep(halfLine, halfLine + aa, abs(d));      // 테두리 선
            float inside = 1.0 - smoothstep(-aa, aa, d);
            float glow = inside * exp(-max(-d, 0.0) / max(_GlowWidth, 1e-3)) * _GlowStrength;   // 안쪽 은은한 빛
            return saturate(edge + glow) * _Color.a;
        }

        half4 FragVisible(Varyings i) : SV_Target
        {
            return half4(_Color.rgb, RimAlpha(i.positionWS));
        }

        half4 FragHidden(Varyings i) : SV_Target
        {
            return half4(_Color.rgb, RimAlpha(i.positionWS) * _OccludedAlpha);
        }
        ENDHLSL

        // 보이는 부분 (앞에 아무것도 없음)
        Pass
        {
            Name "Visible"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment FragVisible
            ENDHLSL
        }

        // 나무·바위 뒤에 가려진 부분 → 연하게
        Pass
        {
            Name "Hidden"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Greater
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment FragHidden
            ENDHLSL
        }
    }
}
