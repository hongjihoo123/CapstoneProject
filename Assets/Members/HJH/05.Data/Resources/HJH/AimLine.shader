// 조준 범위·공격 예고 선 (AttackAreaVisualizer)
//  - 깊이 검사 없이 항상 맨 위에 그림 → 언덕·바위·물가처럼 높낮이가 있는 맵에서도 선이 땅에 묻혀 잘리지 않음
//  - 색 = 라인 렌더러의 정점 색
//  - Resources 폴더에 둬서 빌드에도 포함 (Shader.Find 로 찾음)
Shader "HJH/AimLine"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+100" "IgnoreProjector" = "True" }

        Pass
        {
            Name "AimLine"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            half4 frag(Varyings i) : SV_Target
            {
                return i.color;
            }
            ENDHLSL
        }
    }
}
