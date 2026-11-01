// 불티·바람줄기용 단순 발광 입자 (텍스처 없이 UV 로 부드러운 타원)
//  - 늘어난 빌보드(Stretch)면 길쭉한 줄기, 일반 빌보드면 동그란 불티
//  - 색 = 파티클 색 × 밝기. 더하기에 가까운 블렌드라 배경을 가리지 않음
Shader "HJH/FX/Streak"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 8)) = 2.5
        _Softness ("Softness", Range(0.5, 4)) = 1.6
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "Streak"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _Softness;
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
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                half a = pow(saturate(1.0 - dot(p, p)), _Softness) * i.color.a;
                half3 col = i.color.rgb * _Intensity * a;
                return half4(col, a * 0.15);
            }
            ENDHLSL
        }
    }
}
