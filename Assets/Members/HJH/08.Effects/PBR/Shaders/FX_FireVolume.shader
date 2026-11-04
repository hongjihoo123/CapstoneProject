// 반실사 불·폭발 (구워 둔 볼륨 시트)
//  시트 채널: R = 연기 음영(위에서 받은 빛, 자기 그림자 포함), G = 온도, A = 밀도
//  - 뜨거운 곳은 온도 → 흑체 색(검붉음 → 주황 → 노랑)으로 빛나고, 식은 곳은 검은 그을음 연기로 뒤를 가림
//  - 그래서 솜뭉치처럼 다 빛나지 않고, 불 둘레에 어두운 연기가 붙은 사실적인 화구가 됨
//  - 파티클 알파가 줄면 온도도 같이 떨어져 식어 가며 사라짐
Shader "HJH/FX/FireVolume"
{
    Properties
    {
        _MainTex ("Flipbook (R shade, G temp, A density)", 2D) = "black" {}
        _SmokeColor ("Soot Color", Color) = (0.075, 0.065, 0.06, 1)
        _SmokeLight ("Soot Lit", Range(0, 3)) = 1.2
        [HDR] _HotColor ("Hot", Color) = (2.4, 1.55, 0.7, 1)
        [HDR] _MidColor ("Mid", Color) = (1.6, 0.45, 0.07, 1)
        _CoolColor ("Cool (검붉은 불씨)", Color) = (0.4, 0.04, 0.01, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1.4
        _TempCurve ("Temp Curve", Range(0.5, 3)) = 1.3
        _Cooling ("Cool With Fade", Range(0, 1)) = 0.6
        _Opacity ("Opacity", Range(0, 2)) = 1
        _SoftDistance ("Soft Distance (m)", Float) = 0.6
        [Header(Toon)]
        [Toggle] _Toon ("Toon (계단 색 + 또렷한 외곽)", Float) = 0
        _SootShadow ("Soot Shadow (카툰 연기 그림자 면)", Color) = (0.1, 0.07, 0.11, 1)
        _AlphaCut ("Alpha Cut (카툰 외곽 위치)", Range(0.05, 0.9)) = 0.3
        _GlowCut ("Glow Cut", Range(0.02, 0.6)) = 0.08
        _StepSoft ("Toon Step Softness (계단 경계 부드럽게)", Range(0.005, 0.2)) = 0.02
        _HotCut ("Toon Hot Cut (흰 심지 시작 온도)", Range(0.2, 0.95)) = 0.55
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "FireVolume"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex FxVert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "FX_ParticleCommon.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _SmokeColor;
                float _SmokeLight;
                half4 _HotColor;
                half4 _MidColor;
                half4 _CoolColor;
                float _Intensity;
                float _TempCurve;
                float _Cooling;
                float _Opacity;
                float _SoftDistance;
                float _Toon;
                half4 _SootShadow;
                float _AlphaCut;
                float _GlowCut;
                float _StepSoft;
                float _HotCut;
            CBUFFER_END

            half4 frag(FxVaryings i) : SV_Target
            {
                half4 s = FxSampleFlipbook(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i);
                half shade = s.r;
                half temp = saturate(s.g * lerp(1.0, i.color.a, _Cooling));
                half density = s.a;

                half alpha = saturate(density * _Opacity * i.color.a * FxSoftFade(i, _SoftDistance));
                half3 soot, fire;
                half glow;
                if (_Toon > 0.5)
                {
                    // 카툰(젠존제 풍): 색이 계단으로 끊기고 가장자리가 또렷함
                    //  불 = 흰노랑 심지 / 주황 / 진한 빨강 3단, 연기 = 밝은 면·그림자 면 2단
                    half w = _StepSoft;
                    fire = _CoolColor.rgb;
                    fire = lerp(fire, _MidColor.rgb, smoothstep(0.22 - w, 0.22 + w, temp));
                    fire = lerp(fire, _HotColor.rgb, smoothstep(_HotCut - w, _HotCut + w, temp));
                    fire *= _Intensity * i.color.rgb;
                    glow = smoothstep(_GlowCut - w, _GlowCut + w, temp);
                    soot = lerp(_SootShadow.rgb, _SmokeColor.rgb, smoothstep(0.45 - w, 0.45 + w, shade));
                    alpha = smoothstep(_AlphaCut - 0.03 - w, _AlphaCut + 0.03 + w, alpha);   // 만화처럼 칼같은 외곽
                }
                else
                {
                    // 그을음: 주변광 + 위에서 오는 햇빛 (시트에 구운 자기 그림자 음영)
                    Light light = GetMainLight();
                    soot = _SmokeColor.rgb * (SampleSH(half3(0, 1, 0)) * 0.9 + light.color * shade * _SmokeLight);

                    // 불: 온도 → 흑체 색
                    fire = lerp(_CoolColor.rgb, _MidColor.rgb, smoothstep(0.08, 0.5, temp));
                    fire = lerp(fire, _HotColor.rgb, smoothstep(0.55, 0.95, temp));
                    fire *= pow(temp, _TempCurve) * _Intensity * i.color.rgb;
                    glow = smoothstep(0.05, 0.4, temp);
                }
                half3 col = (soot * (1.0 - glow) + fire) * alpha;
                // 카툰은 불도 만화처럼 꽉 찬 색 (반투명이면 빨간 테두리가 분홍으로 뜸)
                return half4(col, alpha * (_Toon > 0.5 ? 1.0 : lerp(1.0, 0.55, glow)));
            }
            ENDHLSL
        }
    }
}
