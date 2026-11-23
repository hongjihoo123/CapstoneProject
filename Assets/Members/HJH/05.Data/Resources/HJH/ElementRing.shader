// 스킬 슬롯 테두리 속성 링 (UI)
//  - 이미지 한 장 없이 수식(노이즈)으로 링 + 번지는 줄기 + 입자를 그림
//  - 칸보다 큰 정사각형 Image(칸 지름 x 1.9 정도)에 씌워서 쓴다. 스프라이트는 비워 둘 것(uv 0~1 가정)
//  - _Element 로 속성별 모양이 달라짐 (ElementType 순서와 같음)
//      0 Fire  : 위로 치솟는 불길 줄기 + 불씨
//      1 Water : 두툼한 물결 리본 + 흩날리는 물방울
//      2 Wind  : 링 둘레를 감아 도는 소용돌이 줄기
//      3 Electric : 지직거리는 링 + 갈라지는 번개 줄기 (끊어서 깜빡임)
//      4 Earth : 끊어진 바위 호 + 먼지 + 부스러기
//  - _Intensity: 0.5 약함 / 1 기본 / 2.4 "이거 쓰세요" (줄기가 길어지고 밝아지고 입자 많아짐)
//  - Resources 폴더에 둬서 빌드에도 포함 (Shader.Find)
Shader "HJH/UI/ElementRing"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (unused)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Element ("Element (0 Fire, 1 Water, 2 Wind, 3 Electric, 4 Earth)", Float) = 0
        _Intensity ("Intensity", Range(0, 3)) = 1
        _Seed ("Seed", Float) = 0
        _Speed ("Speed", Float) = 1
        _RingRadius ("Ring radius (quad half = 1)", Range(0.2, 0.9)) = 0.53

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Element;
            float _Intensity;
            float _Seed;
            float _Speed;
            float _RingRadius;

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float noise3(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float a = lerp(lerp(hash31(i), hash31(i + float3(1, 0, 0)), f.x),
                               lerp(hash31(i + float3(0, 1, 0)), hash31(i + float3(1, 1, 0)), f.x), f.y);
                float b = lerp(lerp(hash31(i + float3(0, 0, 1)), hash31(i + float3(1, 0, 1)), f.x),
                               lerp(hash31(i + float3(0, 1, 1)), hash31(i + float3(1, 1, 1)), f.x), f.y);
                return lerp(a, b, f.z);
            }

            float fbm(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    v += a * noise3(p);
                    p = p * 2.03 + float3(17.1, 9.2, 3.7);
                    a *= 0.5;
                }
                return v;
            }

            // 링 바깥으로 흩어지는 입자. 각도는 정수 칸으로 나눠서 이음새가 안 보이게 함
            float Sparks(float ang, float d, float t, float density, float size, float speed)
            {
                float2 g = float2(ang * 8.9127, d * 26.0 - t * speed);
                float2 id = floor(g);
                float2 f = frac(g) - 0.5;
                float h = hash31(float3(id, _Seed));
                float2 off = float2(hash31(float3(id, 3.1)), hash31(float3(id, 7.7))) - 0.5;
                float on = step(1.0 - density, h);
                float dist = length(f - off * 0.6);
                return on * smoothstep(size, 0.0, dist) * smoothstep(-0.01, 0.04, d) * exp(-max(d, 0.0) * 6.0);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                float r = length(p);
                float2 dir = p / max(r, 0.0001);
                float ang = atan2(p.y, p.x);

                float I = _Intensity;
                float k = saturate(I / 3.0);                       // 0~1: 얼마나 세게 보여줄지
                // 울렁임 완화: 전체 시간 속도 + 링 반지름 흔들림을 줄임 (두 상수 모두 1 = 예전 느낌, 낮을수록 차분)
                const float CALM_TIME = 0.35;
                const float CALM_WOBBLE = 0.12;
                float soft = 0.8;                                  // 전체 투명도 배율 (낮을수록 연함)
                float t = _Time.y * _Speed * CALM_TIME + _Seed * 9.7;
                float R0 = _RingRadius;

                float disp = (fbm(float3(dir * 1.6 + _Seed, t * 0.4)) - 0.5) * 0.07 * CALM_WOBBLE * (0.6 + 0.5 * I);
                float d = r - (R0 + disp);
                float w = 0.024 * (0.8 + 0.4 * I);

                float core = 0.0;
                float wisp = 0.0;
                float part = 0.0;
                float3 cA = float3(1, 0.4, 0.1);
                float3 cB = float3(1, 0.9, 0.5);

                if (_Element < 0.5)            // Fire
                {
                    cA = float3(1.0, 0.36, 0.07);
                    cB = float3(1.0, 0.90, 0.48);
                    core = exp(-d * d / (w * w * 1.4));
                    float up = 1.0 + 0.7 * dir.y;
                    float n = fbm(float3(dir * 5.0, d * 4.0 - t * 1.6));
                    float len = (0.07 + 0.17 * k) * up;
                    float fall = exp(-max(d, 0.0) / len) * smoothstep(-0.05, 0.0, d);
                    wisp = smoothstep(0.44, 0.86, n) * fall * (1.5 + 1.2 * k);
                    part = Sparks(ang, d, t, 0.05 + 0.16 * k, 0.32, 1.8);
                }
                else if (_Element < 1.5)       // Water
                {
                    cA = float3(0.12, 0.62, 1.0);
                    cB = float3(0.85, 1.0, 1.0);
                    core = exp(-d * d / (w * w * 2.4));
                    float n = fbm(float3(dir * 2.3, d * 5.0 - t * 0.55));
                    float ribbon = smoothstep(0.42, 0.78, n);
                    wisp = ribbon * exp(-abs(d - 0.03) / (0.055 + 0.11 * k)) * (1.1 + 0.8 * k);
                    part = Sparks(ang, d, t, 0.06 + 0.2 * k, 0.28, 1.0);
                }
                else if (_Element < 2.5)       // Wind
                {
                    cA = float3(0.18, 0.95, 0.42);
                    cB = float3(0.86, 1.0, 0.86);
                    core = exp(-d * d / (w * w * 0.8)) * 0.85;
                    float a2 = ang + d * (5.0 + 3.0 * k) + t * 0.9;
                    float n = fbm(float3(cos(a2) * 3.5, sin(a2) * 3.5, d * 8.0));
                    float streak = smoothstep(0.44, 0.8, n);
                    wisp = streak * exp(-abs(d) / (0.07 + 0.09 * k)) * (1.1 + 0.8 * k);
                    float arcs = pow(saturate(sin(a2 * 3.0 - t * 2.0) * 0.5 + 0.5), 6.0);
                    wisp += arcs * exp(-abs(d - 0.03) / 0.02) * (0.5 + 0.5 * k);
                    part = Sparks(ang, d, t, 0.04 + 0.10 * k, 0.22, 0.8) * 0.8;
                }
                else if (_Element < 3.5)       // Electric
                {
                    cA = float3(1.0, 0.80, 0.12);
                    cB = float3(1.0, 1.0, 0.86);
                    // 끊어서 튀지 않고 연속으로 흐르는 시간 -> 지직거리되 부드럽게 움직임
                    soft = 0.5;                                    // 전기는 유독 강해 보여서 더 연하게
                    float te = _Time.y * _Speed * 1.0;
                    float jag = (fbm(float3(dir * 5.0, te)) - 0.5) * 0.012 * (1.0 + 0.6 * I);
                    float dd = d + jag;
                    float flick = 0.94 + 0.12 * noise3(float3(te * 1.6, _Seed, 1.0));
                    core = exp(-dd * dd / (w * w * 0.4)) * flick;
                    float n = fbm(float3(dir * 4.0, d * 3.5 + te * 0.9));
                    float bolt = pow(saturate(1.0 - abs(n - 0.5) * 8.0), 3.0);
                    float gate = smoothstep(0.26, 0.55, noise3(float3(dir * 2.6, te * 0.45)));
                    wisp = bolt * gate * exp(-max(d, 0.0) / (0.10 + 0.16 * k)) * step(-0.02, d) * (1.6 + 1.2 * k);
                    part = Sparks(ang, d, te * 0.35, 0.05 + 0.14 * k, 0.2, 2.5);
                }
                else                           // Earth
                {
                    cA = float3(0.86, 0.46, 0.10);
                    cB = float3(1.0, 0.80, 0.42);
                    float seg = smoothstep(0.30, 0.5, fbm(float3(dir * 2.2 + _Seed, 3.0)));
                    core = exp(-d * d / (w * w * 3.0)) * (0.35 + 0.65 * seg);
                    float n = fbm(float3(dir * 3.0, d * 2.0 - t * 0.3));
                    float gate = smoothstep(-0.01, 0.03, d);
                    wisp = smoothstep(0.45, 0.85, n) * exp(-max(d, 0.0) / (0.08 + 0.14 * k)) * gate * (1.0 + 1.1 * k);
                    part = Sparks(ang, d, t, 0.06 + 0.16 * k, 0.42, 0.5);
                }

                // 사각형 끝에서 잘려 보이지 않게 가장자리로 갈수록 줄기·입자를 지움
                float edge = smoothstep(0.98, 0.70, r);
                // 덜 튀게: 줄기/입자/번지는 빛을 크게 줄이고 링 본체 위주로 보이게 함
                wisp *= edge * (1.15 + 1.1 * k) * 0.4;
                part *= edge * (1.2 + 1.3 * k) * 0.3;
                // 링 바깥으로 번지는 빛 (세질수록 넓고 진하게)
                core += exp(-max(d, 0.0) * (14.0 - 5.0 * k)) * (0.05 + 0.14 * k) * edge;

                float vis = saturate(0.25 + 0.75 * min(I, 1.0));
                float tot = (core * (0.9 + 0.4 * k) + wisp + part) * vis * soft;
                float a = saturate(tot);
                float hot = saturate(core * 1.1 + wisp * 0.4 + part);
                // 색도 덜 쨍하게: 흰색 쪽으로 살짝 섞고 밝기 보정을 낮춤
                float3 col = lerp(cA, cB, hot * hot) * (0.8 + 0.3 * k);
                col = lerp(col, dot(col, float3(0.33, 0.33, 0.33)).xxx, 0.18);
                return fixed4(col, a) * i.color;
            }
            ENDCG
        }
    }
}
