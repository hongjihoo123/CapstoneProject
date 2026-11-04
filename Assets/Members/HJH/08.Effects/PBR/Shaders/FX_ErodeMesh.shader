// 입체 이펙트용 메시 셰이더 (불기둥, 회오리, 공기 가르는 막, 폭발 껍질, 바닥 불길)
//  - 납작한 판 대신 원기둥·반구·포물면 메시에 흐르는 노이즈를 입혀서 어느 각도에서 봐도 부피가 있음
//  - 노이즈 두 겹이 흐르고, 파티클 알파(수명)가 줄면 노이즈가 갉아먹히듯 사라짐 (erosion)
//  - _Mode 0 = 불 (열 → 심지/중간/가장자리 색), 1 = 색 (바람·먼지: 해·주변광을 받음)
//  - 실루엣 강조(프레넬): 가장자리가 진하고 가운데가 옅어서 껍질이 공처럼 보임
//  - 메시 UV: u = 둘레, v = 아래→위 (바닥 원판은 v = 가운데→바깥)
Shader "HJH/FX/ErodeMesh"
{
    Properties
    {
        _NoiseTex ("Noise (R, G, B = 줄무늬)", 2D) = "gray" {}
        _Noise1 ("Noise 1 Tiling(xy) Scroll(zw)", Vector) = (2, 1, 0, -1.2)
        _Noise2 ("Noise 2 Tiling(xy) Scroll(zw)", Vector) = (3, 1.5, 0.15, -2)
        _Streak ("Streak (B) Amount", Range(0, 1)) = 0
        [Enum(Fire,0,Tint,1)] _Mode ("Mode", Float) = 0
        [HDR] _CoreColor ("Core Color", Color) = (2.2, 1.5, 0.7, 1)
        [HDR] _MidColor ("Mid Color", Color) = (1.6, 0.5, 0.08, 1)
        _EdgeColor ("Edge Color", Color) = (0.3, 0.05, 0.02, 1)
        _TintColor ("Tint Color (Mode 1)", Color) = (0.8, 0.85, 0.8, 1)
        _Lit ("Lit (Mode 1)", Range(0, 1)) = 0.6
        _Threshold ("Threshold (구멍 크기)", Range(0, 1)) = 0.25
        _Erode ("Erode Softness", Range(0.02, 1)) = 0.25
        _Density ("Density", Range(0, 4)) = 1.4
        _Opacity ("Opacity", Range(0, 2)) = 1
        _Occlusion ("Occlusion (뒤를 가리는 정도)", Range(0, 1)) = 0.5
        _FadeBottom ("Fade Bottom (v)", Range(0, 1)) = 0.15
        _FadeTop ("Fade Top (v)", Range(0, 1)) = 0.4
        _VMask ("Height Mask (아래가 진함)", Range(0, 1)) = 0.5
        _Fresnel ("Fresnel Amount", Range(0, 1)) = 0.5
        _FresnelPower ("Fresnel Power", Range(0.5, 6)) = 1.5
        _SoftDistance ("Soft Distance (m)", Float) = 0.4
        [Toggle] _Toon ("Toon (색 계단 + 또렷한 외곽)", Float) = 0
        _StepSoft ("Toon Step Softness", Range(0.005, 0.25)) = 0.03
        [Header(Toon Lighting)]
        _LitShade ("Light Shade (빛 받는 면 / 그늘 면, 입체감)", Range(0, 1)) = 0
        _ShadeColor ("Shade Color (그늘 면 곱)", Color) = (0.55, 0.62, 0.8, 1)
        _Highlight ("Highlight (물 반짝임 띠)", Range(0, 2)) = 0
        _HighlightSize ("Highlight Size", Range(0.8, 0.999)) = 0.93
        _Solid ("Solid (살아 있는 동안 구멍 없이 꽉 참, 사라질 때만 갉아먹힘)", Range(0, 1)) = 0
        [Header(Ground Layer)]
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest (바닥 원판 = Always)", Float) = 4
        _DepthTolerance ("Depth Tolerance (m, 0 = 끔). 이만큼 땅에 묻혀도 보임", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ErodeMesh"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                float4 _Noise1;
                float4 _Noise2;
                float _Streak;
                float _Mode;
                half4 _CoreColor;
                half4 _MidColor;
                half4 _EdgeColor;
                half4 _TintColor;
                float _Lit;
                float _Threshold;
                float _Erode;
                float _Density;
                float _Opacity;
                float _Occlusion;
                float _FadeBottom;
                float _FadeTop;
                float _VMask;
                float _Fresnel;
                float _FresnelPower;
                float _SoftDistance;
                float _DepthTolerance;
                float _Toon;
                float _StepSoft;
                float _LitShade;
                half4 _ShadeColor;
                float _Highlight;
                float _HighlightSize;
                float _Solid;
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
                float seed : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.uv = v.uv;
                // 메시 렌더러는 오브젝트 위치로, 파티클은 색 값으로 노이즈를 조금씩 다르게
                float3 pivot = TransformObjectToWorld(float3(0, 0, 0));
                o.seed = frac(dot(pivot, float3(0.137, 0.271, 0.193)) + v.color.r * 0.31);
                return o;
            }

            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float t = _Time.y;
                float2 uv1 = i.uv * _Noise1.xy + _Noise1.zw * t + i.seed * 3.7;
                float2 uv2 = i.uv * _Noise2.xy + _Noise2.zw * t + i.seed * 5.3;
                half n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv1).r;
                half3 n2s = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv2).gbr;
                half noise = n1 * (0.55 + n2s.x * 0.9);
                noise = lerp(noise, noise * (0.4 + n2s.y * 1.2), _Streak);

                // 높이 마스크 + 위아래 가장자리 페이드
                half v = i.uv.y;
                half mask = lerp(1.0, saturate(1.15 - v), _VMask);
                mask *= smoothstep(0.0, max(_FadeBottom, 0.001), v) * smoothstep(1.0, 1.0 - max(_FadeTop, 0.001), v);

                // 실루엣 강조
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.positionWS);
                half facing = abs(dot(normalize(i.normalWS), viewDir));
                half rim = pow(saturate(1.0 - facing), _FresnelPower);
                mask *= lerp(1.0, 0.25 + rim * 1.5, _Fresnel);

                // 수명이 줄면 갉아먹히며 사라짐
                half life = i.color.a;
                half value = noise * mask * _Density;
                half cut = (1.0 - life) * 1.1;
                half h = saturate((value - _Threshold - cut) / _Erode);

                half3 col;
                if (_Mode < 0.5)
                {
                    col = lerp(_EdgeColor.rgb, _MidColor.rgb, smoothstep(0.05, 0.45, h));
                    col = lerp(col, _CoreColor.rgb, smoothstep(0.5, 0.95, h));
                }
                else
                {
                    Light light = GetMainLight();
                    half3 n = normalize(i.normalWS);
                    half diffuse = saturate(dot(n, light.direction) * 0.5 + 0.5);
                    half3 lit = light.color * diffuse + SampleSH(n);
                    col = _TintColor.rgb * lerp(1.0, lit, _Lit) * (0.75 + h * 0.5);
                }
                half alpha = saturate(h * _Opacity);
                if (_Toon > 0.5)
                {
                    // 카툰(젠존제 풍): 색 계단 + 칼같은 외곽. h 를 그대로 계단으로
                    half w = _StepSoft;
                    half shape = smoothstep(0.12 - w, 0.12 + w, h);
                    if (_Mode < 0.5)
                    {
                        col = lerp(_EdgeColor.rgb, _MidColor.rgb, smoothstep(0.35 - w, 0.35 + w, h));
                        col = lerp(col, _CoreColor.rgb, smoothstep(0.7 - w, 0.7 + w, h));
                    }
                    else
                    {
                        col = lerp(_TintColor.rgb * 0.75, _TintColor.rgb * 1.35, smoothstep(0.55 - w, 0.55 + w, h));
                    }
                    alpha = max(shape, _Solid * smoothstep(0.3, 0.6, life)) * saturate(_Opacity);
                    if (_LitShade > 0.001)
                    {
                        // 만화풍 명암: 빛 받는 면 / 그늘 면 2단 + 물 반짝임 띠 → 막이 아니라 덩어리로 보이게
                        Light mainLight = GetMainLight();
                        float3 nrm = normalize(i.normalWS) * (front ? 1.0 : -1.0);
                        half ndl = dot(nrm, mainLight.direction) * 0.5 + 0.5;
                        half litStep = smoothstep(0.45 - w, 0.45 + w, ndl);
                        col *= lerp(lerp(1.0, _ShadeColor.rgb, _LitShade), 1.0, litStep);
                        float3 vdir = normalize(_WorldSpaceCameraPos - i.positionWS);
                        half spec = dot(nrm, normalize(mainLight.direction + vdir));
                        col += _Highlight * smoothstep(_HighlightSize - w * 0.3, _HighlightSize + w * 0.3, spec) * mainLight.color;
                    }
                }
                col *= i.color.rgb;
                if (_SoftDistance > 0.0 || _DepthTolerance > 0.0)
                {
                    float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                    float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    float selfEye = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                    float diff = sceneEye - selfEye;   // 음수 = 이 픽셀이 땅·물체 뒤(아래)에 있음
                    if (_DepthTolerance > 0.0)
                        alpha *= smoothstep(-_DepthTolerance, -_DepthTolerance * 0.5, diff);   // 낮은 둔덕 정도는 뚫고 보이고, 캐릭터·벽 뒤는 가려짐
                    else
                        alpha *= saturate(diff / _SoftDistance);
                }
                return half4(col * alpha, alpha * _Occlusion);
            }
            ENDHLSL
        }
    }
}
