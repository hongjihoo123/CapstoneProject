// 화면 전체 스타일라이즈 (불투명 물체만: 반투명인 물·안개·이펙트보다 먼저 그려짐)
//  1) 붓칠: 쿠와하라 필터 - 사실적인 잔무늬를 넓은 색 면으로 뭉갬
//  2) 명암 단계: 밝기를 몇 단계로 나눔 (색조는 유지)
//  3) 외곽선: 깊이가 갑자기 바뀌는 곳을 같은 색의 어두운 선으로
// 값은 Volume 의 HJH/Stylize (StylizeVolume) 에서 옴. 꺼져 있으면 그대로 통과.
Shader "Hidden/HJH/StylizeFullscreen"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "Stylize"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _HJH_StylizeOn;        // 0 = 꺼짐(그대로), 1 = 켜짐
            float _HJH_StylizeIntensity; // 원본과 섞는 정도
            float _HJH_PaintRadius;      // 0~4 픽셀
            float _HJH_Bands;            // 명암 단계 수
            float _HJH_BandStrength;
            float _HJH_Outline;
            float _HJH_OutlineDistance;  // 이 거리(m) 넘으면 외곽선 흐려짐

            half3 Load(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb; }
            half Luma(half3 c) { return dot(c, half3(0.299h, 0.587h, 0.114h)); }

            // 4 방향 사분면 중 색이 가장 고른 쪽의 평균 = 붓칠처럼 면이 생기고 경계는 유지됨
            half3 Kuwahara(float2 uv, int radius, float2 texel)
            {
                half3 bestMean = Load(uv);
                half bestVar = 1e5;
                [unroll] for (int q = 0; q < 4; q++)
                {
                    float2 dir = float2((q & 1) ? 1 : -1, (q & 2) ? 1 : -1);
                    half3 sum = 0; half3 sq = 0; half n = 0;
                    [loop] for (int y = 0; y <= radius; y++)
                    [loop] for (int x = 0; x <= radius; x++)
                    {
                        half3 c = Load(uv + float2(x, y) * dir * texel);
                        sum += c; sq += c * c; n += 1;
                    }
                    half3 mean = sum / n;
                    half3 var3 = abs(sq / n - mean * mean);
                    half v = var3.r + var3.g + var3.b;
                    if (v < bestVar) { bestVar = v; bestMean = mean; }
                }
                return bestMean;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
                if (_HJH_StylizeOn < 0.5)
                    return src;

                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (raw <= 0.000001) return src;    // 하늘
                #else
                    if (raw >= 0.999999) return src;
                #endif
                float depth = LinearEyeDepth(raw, _ZBufferParams);
                float2 texel = _ScreenSize.zw;

                // 1) 붓칠
                int radius = (int)round(_HJH_PaintRadius);
                half3 col = radius > 0 ? Kuwahara(uv, radius, texel) : src.rgb;

                // 2) 명암 단계 (경계는 살짝 부드럽게)
                half L = Luma(col);
                half B = max(_HJH_Bands, 1.0);
                half step = (floor(L * B) + smoothstep(0.4, 0.6, frac(L * B))) / B;
                col *= lerp(1.0, step / max(L, 0.0001), _HJH_BandStrength);

                // 3) 외곽선: 주변 4점과 깊이 차이가 크면 (가까운 쪽 기준 비율)
                float dl = LinearEyeDepth(SampleSceneDepth(uv - float2(texel.x, 0)), _ZBufferParams);
                float dr = LinearEyeDepth(SampleSceneDepth(uv + float2(texel.x, 0)), _ZBufferParams);
                float dd = LinearEyeDepth(SampleSceneDepth(uv - float2(0, texel.y)), _ZBufferParams);
                float du = LinearEyeDepth(SampleSceneDepth(uv + float2(0, texel.y)), _ZBufferParams);
                float diff = max(max(abs(dl - depth), abs(dr - depth)), max(abs(dd - depth), abs(du - depth)));
                float edge = smoothstep(0.04, 0.12, diff / max(depth, 0.01));
                edge *= 1.0 - smoothstep(_HJH_OutlineDistance * 0.6, _HJH_OutlineDistance, depth);
                col *= lerp(1.0, 0.28, edge * _HJH_Outline);

                return half4(lerp(src.rgb, col, _HJH_StylizeIntensity), src.a);
            }
            ENDHLSL
        }
    }
}
