// 피격 번쩍임: 적 메시를 한 번 더 그려서 색을 더한다(가산). 가장자리(림)가 더 밝게.
// HitFlash.cs 가 맞은 순간 렌더러에 이 머티리얼을 잠깐 덧붙이고 _FlashColor(알파 = 세기)를 줄여 가며 지운다.
Shader "HJH/HitFlash"
{
    Properties
    {
        _FlashColor ("Flash Color (A = 세기)", Color) = (1, 1, 1, 0)
        _RimPower ("Rim Power", Range(0.5, 6)) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+10" }

        Pass
        {
            Name "HitFlash"
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
                half4 _FlashColor;
                float _RimPower;
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
                half3 col = _FlashColor.rgb * _FlashColor.a * (0.45 + rim * 1.1);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
