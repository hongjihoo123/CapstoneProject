// 불꽃·화염구 (시트 애니메이션 열 → 색)
//  - 텍스처의 알파 = 열(0~1). 열이 높을수록 심지 색, 낮을수록 가장자리 색 (부드러운 그라데이션)
//  - 프리멀티플라이 블렌드: 밝은 심지는 빛을 더하고, 가장자리는 살짝 가림 → 흰색으로 날아가지 않음
//  - 파티클 알파가 줄면 열 기준이 올라가 가장자리부터 식어 사라짐
Shader "HJH/FX/Fire"
{
    Properties
    {
        _MainTex ("Heat Flipbook (A)", 2D) = "white" {}
        [HDR] _CoreColor ("Core Color", Color) = (1.4, 0.9, 0.4, 1)
        [HDR] _MidColor ("Mid Color", Color) = (1.2, 0.42, 0.08, 1)
        _EdgeColor ("Edge Color", Color) = (0.35, 0.06, 0.02, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.85
        _SoftDistance ("Soft Distance (m)", Float) = 0.6
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "Fire"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex FxVert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "FX_ParticleCommon.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _CoreColor;
                half4 _MidColor;
                half4 _EdgeColor;
                float _Opacity;
                float _SoftDistance;
            CBUFFER_END

            half4 frag(FxVaryings i) : SV_Target
            {
                half heat = FxSampleFlipbook(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i).a;

                // 식어 가며 사라짐: 파티클 알파가 줄수록 열 기준을 올림
                half cool = 1.0 - i.color.a;
                half h = saturate((heat - cool * 0.6) / max(1.0 - cool * 0.6, 0.05));

                half3 col = lerp(_EdgeColor.rgb, _MidColor.rgb, smoothstep(0.1, 0.5, h));
                col = lerp(col, _CoreColor.rgb, smoothstep(0.45, 0.9, h));
                col *= i.color.rgb;

                half alpha = smoothstep(0.02, 0.35, h) * _Opacity * FxSoftFade(i, _SoftDistance);
                // 프리멀티플라이: 색은 알파만큼, 가림은 알파의 절반만 (빛나는 느낌 유지)
                return half4(col * alpha, alpha * 0.5);
            }
            ENDHLSL
        }
    }
}
