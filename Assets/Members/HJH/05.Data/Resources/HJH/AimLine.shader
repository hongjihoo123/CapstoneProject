// 조준 보조선 (사거리 원·방향선, AttackAreaVisualizer 의 얇은 선)
//  - 보이는 부분은 그대로, 나무·바위 뒤에 가려진 부분만 연하게 (_OccludedAlpha)
//    (예전엔 ZTest Always 로 항상 맨 위에 그려서 나무 위에 선이 떠 보였음)
//  - 색 = 라인 렌더러의 정점 색
//  - Resources 폴더에 둬서 빌드에도 포함 (Shader.Find 로 찾음)
Shader "HJH/AimLine"
{
    Properties
    {
        _OccludedAlpha ("Alpha when hidden behind objects", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+100" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half _OccludedAlpha;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            half4 color : COLOR;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            half4 color : COLOR;
        };

        Varyings vert(Attributes v)
        {
            Varyings o;
            o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
            o.color = v.color;
            return o;
        }

        half4 FragVisible(Varyings i) : SV_Target { return i.color; }

        half4 FragHidden(Varyings i) : SV_Target { return half4(i.color.rgb, i.color.a * _OccludedAlpha); }
        ENDHLSL

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
