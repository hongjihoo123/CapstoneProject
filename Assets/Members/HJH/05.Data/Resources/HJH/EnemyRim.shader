// 적 가시성 (b) 림라이트: 적 메시를 한 번 더 그려서 가장자리만 밝게 더함 (가산). 원래 셰이더는 안 건드림.
// RimHighlightLayer 가 적 렌더러에 이 머티리얼을 덧붙임. HitFlash.shader 와 같은 방식.
Shader "HJH/EnemyRim"
{
    Properties
    {
        _RimColor ("Rim Color (A = 세기)", Color) = (1, 0.35, 0.25, 0.8)
        _RimPower ("Rim Power (클수록 가장자리만)", Range(0.5, 8)) = 3
        _Fill ("몸 전체에 더하는 밝기", Range(0, 0.5)) = 0.06
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+5" }

        Pass
        {
            Name "EnemyRim"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                float _RimPower;
                float _Fill;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float rim = pow(1.0 - saturate(dot(normalize(i.normalWS), normalize(i.viewWS))), _RimPower);
                half3 col = _RimColor.rgb * _RimColor.a * (_Fill + rim);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
