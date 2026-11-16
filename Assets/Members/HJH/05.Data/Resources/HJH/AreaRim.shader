// 바닥 범위 표시 (AttackAreaVisualizer)
//  - 바닥에 깐 사각형 하나에, 범위 모양(원·사각형·부채꼴) 여러 개를 "합친" 실루엣을 그림
//    → 사각형(목)+부채꼴이 겹쳐도 안쪽 선은 사라지고 바깥 테두리 하나로 이어짐
//  - _Style 로 그리는 방식이 바뀜
//      0 디버그(F1 판정 표시): 예전 테두리 + 안쪽 빛
//      1 플레이어 조준: 얇은 글로우 테두리 + 아주 옅은 채움 + 안쪽 동심원 호 가이드 + 다이아몬드 마커 + 링(발밑·사거리)
//      2 적 공격 예고: 빨강/분홍 얇은 테두리 + 옅은 채움 + 안쪽에서 바깥으로 차오르는 충전(진행도) + 막바지 깜빡임
//  - 나무·바위 뒤에 가려진 부분만 연하게 (_OccludedAlpha)
//  - Resources 폴더에 둬서 빌드에도 포함 (Shader.Find)
Shader "HJH/AreaRim"
{
    Properties
    {
        _Color ("Color", Color) = (0.35, 0.72, 1, 1)
        _LineWidth ("Edge line width (m)", Float) = 0.05
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
        #define MAX_EXTRAS 4

        // MaterialPropertyBlock 으로 매 프레임 넣음 (모양 개수가 바뀌어서 SRP Batcher 는 안 씀)
        float4 _Color;
        float _Style;
        float _LineWidth;
        float _GlowWidth;
        float _GlowStrength;
        float _OuterGlow;      // 테두리 바깥으로 번지는 빛 폭 (m)
        float _FillAlpha;      // 안쪽 전체 채움
        float _OccludedAlpha;

        float _ShapeCount;
        float4 _ShapeA[MAX_SHAPES];   // x = 종류(0 원, 1 사각형, 2 부채꼴), y/z = 월드 x/z, w = yaw(라디안)
        float4 _ShapeB[MAX_SHAPES];   // 원: x = 반지름 / 사각형: x,y = 반폭·반길이 / 부채꼴: x = 반지름, y = 반각(라디안)
                                      // z = 적 예고 충전 진행도(0~1)

        // 조준(Style 1) 전용
        float4 _Guide;        // xy = 동심원 중심, z = 안쪽 반지름, w = 바깥 반지름
        float4 _GuideInfo;    // x = 가이드를 제한할 모양 번호(-1 = 없음), y = 칸 수, z = 방사선 yaw, w = 방사선 반각(0 = 없음)
        float _RingCount;
        float4 _Rings[MAX_EXTRAS];    // xy = 중심, z = 반지름, w = 진하기
        float _MarkerCount;
        float4 _Markers[MAX_EXTRAS];  // xy = 위치, z = 크기(m), w = yaw

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

        // Unity yaw: 앞 = (sin, cos), 오른쪽 = (cos, -sin)  (x, z)
        float2 ToLocal(float2 q, float yaw)
        {
            float s, c;
            sincos(yaw, s, c);
            return float2(dot(q, float2(c, -s)), dot(q, float2(s, c)));
        }

        // 모양 하나의 거리(바깥 +). u = 충전 좌표 (원·부채꼴: 중심에서 바깥으로, 사각형: 뒤에서 앞으로) 0~1
        float ShapeDistance(int i, float2 p, out float u)
        {
            float4 a = _ShapeA[i];
            float4 b = _ShapeB[i];
            float2 q = p - a.yz;
            float2 local = ToLocal(q, a.w);

            if (a.x < 0.5)
            {
                u = length(q) / max(b.x, 1e-3);
                return length(q) - b.x;
            }
            if (a.x < 1.5)
            {
                u = (local.y + b.y) / max(2.0 * b.y, 1e-3);
                return SdBox(local, b.xy);
            }
            float halfAngle = min(b.y, 3.14159);
            u = length(local) / max(b.x, 1e-3);
            return SdPie(local, float2(sin(halfAngle), cos(halfAngle)), b.x);
        }

        // 선 하나 (거리 dist, 반폭 w) 를 안티에일리어싱해서
        float Line(float dist, float halfWidth, float aa)
        {
            return 1.0 - smoothstep(halfWidth, halfWidth + aa, dist);
        }

        // 다이아몬드(마름모) 마커: 테두리 + 옅은 속
        float Diamond(float2 p, float4 m, float aa)
        {
            float2 local = ToLocal(p - m.xy, m.w);
            float d = (abs(local.x) * 1.35 + abs(local.y)) - m.z;    // 앞뒤로 살짝 긴 마름모
            float rim = Line(abs(d), _LineWidth * 0.6, aa);
            float fill = (1.0 - smoothstep(-aa, aa, d)) * 0.35;
            return max(rim, fill);
        }

        // rgb 는 그대로 _Color, 알파 + 흰빛(심) 정도를 같이 돌려줌
        float4 Shade(float3 positionWS)
        {
            float2 p = positionWS.xz;

            float d = 1e5;
            float charged = 0.0;   // 적 예고: 충전된 영역
            float front = 0.0;     // 적 예고: 차오르는 앞머리 선
            float progress = 0.0;
            [loop] for (int i = 0; i < MAX_SHAPES; i++)
            {
                if (i >= (int)_ShapeCount) break;
                float u;
                float s = ShapeDistance(i, p, u);
                d = min(d, s);
                float aaU = max(fwidth(u), 1e-4);   // 분기 밖에서 (미분은 분기 안에서 쓰면 안 됨)

                if (_Style > 1.5 && s < 0.0)
                {
                    float prog = saturate(_ShapeB[i].z);
                    charged = max(charged, 1.0 - smoothstep(prog - aaU, prog + aaU, u));
                    front = max(front, Line(abs(u - prog), aaU * 1.5, aaU) * step(0.02, prog) * step(prog, 0.995));
                    progress = max(progress, prog);
                }
            }

            float aa = max(fwidth(d), 1e-4);
            float inside = 1.0 - smoothstep(-aa, aa, d);
            float edge = Line(abs(d), _LineWidth * 0.5, aa);
            float core = Line(abs(d), _LineWidth * 0.18, aa);                       // 테두리 한가운데 하얀 심
            float outer = (1.0 - inside) * exp(-max(d, 0.0) / max(_OuterGlow, 1e-3)) * 0.35 * step(d, _OuterGlow * 4.0);
            float inner = inside * exp(-max(-d, 0.0) / max(_GlowWidth, 1e-3)) * _GlowStrength;
            float alpha = edge + outer + inner + inside * _FillAlpha;
            float white = core * 0.45;

            if (_Style > 0.5 && _Style < 1.5)
            {
                // 동심원 호 가이드 + 방사선 (지정한 모양 안쪽에서만)
                int gi = (int)_GuideInfo.x;
                if (gi >= 0 && _Guide.w > _Guide.z)
                {
                    float gu;
                    float gs = ShapeDistance(gi, p, gu);
                    float guideMask = (1.0 - smoothstep(-_LineWidth * 2.0, -_LineWidth, gs));
                    float2 gq = p - _Guide.xy;
                    float r = length(gq);
                    float span = _Guide.w - _Guide.z;
                    float cells = max(_GuideInfo.y, 1.0);
                    float t = (r - _Guide.z) / span * cells;
                    float aaR = max(fwidth(r), 1e-4);
                    float arcDist = abs(frac(t + 0.5) - 0.5) * span / cells;
                    float arcs = Line(arcDist, 0.012, aaR) * step(0.5, t) * step(t, cells - 0.5);

                    float rays = 0.0;
                    if (_GuideInfo.w > 0.0)
                    {
                        [unroll] for (int k = -1; k <= 1; k += 2)
                        {
                            float yaw = _GuideInfo.z + k * _GuideInfo.w / 3.0;
                            float2 dir = float2(sin(yaw), cos(yaw));
                            float along = dot(gq, dir);
                            float perp = abs(gq.x * dir.y - gq.y * dir.x);
                            rays = max(rays, Line(perp, 0.01, aaR) * step(_Guide.z, along));
                        }
                    }
                    alpha += max(arcs, rays * 0.6) * 0.2 * guideMask;
                }

                // 링 (발밑 링·사거리 원)
                [loop] for (int ri = 0; ri < MAX_EXTRAS; ri++)
                {
                    if (ri >= (int)_RingCount) break;
                    float4 ring = _Rings[ri];
                    float rd = abs(length(p - ring.xy) - ring.z);
                    float raa = max(fwidth(rd), 1e-4);
                    float ringLine = Line(rd, _LineWidth * 0.5, raa);
                    float ringGlow = exp(-rd / max(_OuterGlow, 1e-3)) * 0.3 * step(rd, _OuterGlow * 4.0);
                    alpha = max(alpha, (ringLine + ringGlow) * ring.w);
                    white = max(white, Line(rd, _LineWidth * 0.18, raa) * 0.45 * ring.w);
                }

                // 다이아몬드 마커
                [loop] for (int mi = 0; mi < MAX_EXTRAS; mi++)
                {
                    if (mi >= (int)_MarkerCount) break;
                    float dm = Diamond(p, _Markers[mi], aa);
                    alpha = max(alpha, dm);
                    white = max(white, dm * 0.35);
                }
            }
            else if (_Style > 1.5)
            {
                // 충전: 안쪽에서 차오름 + 앞머리 밝은 선. 거의 다 차면 테두리가 빨라지는 깜빡임
                float pulseSpeed = lerp(4.0, 22.0, progress * progress);
                float pulse = lerp(1.0, 0.65 + 0.35 * sin(_Time.y * pulseSpeed), smoothstep(0.6, 1.0, progress));
                alpha = (edge + outer) * pulse + inner + inside * _FillAlpha
                      + inside * charged * 0.12 + inside * front * 0.5;
                white = max(white * pulse, front * 0.25);
            }

            return float4(saturate(white), 0, 0, saturate(alpha) * _Color.a);
        }

        half4 Compose(float3 positionWS, float alphaScale)
        {
            float4 s = Shade(positionWS);
            return half4(lerp(_Color.rgb, 1.0, s.x), s.w * alphaScale);
        }

        half4 FragVisible(Varyings i) : SV_Target { return Compose(i.positionWS, 1.0); }
        half4 FragHidden(Varyings i) : SV_Target { return Compose(i.positionWS, _OccludedAlpha); }
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
