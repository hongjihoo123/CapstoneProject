// 캐릭터 만화풍 셰이더 (URP). 메시·뼈대는 그대로라 애니메이션에 영향 없음.
//  - 2단 명암: 빛 받는 면 / 그림자 면이 또렷하게 갈림 (경계만 살짝 부드럽게), 그림자는 회색 대신 색이 있는 그늘
//  - 테두리 빛(림): 실루엣 가장자리가 밝게 → 어두운 배경에서도 캐릭터가 또렷
//  - 외곽선: 뒤집은 껍데기(inverted hull) 방식. 두께는 화면 기준이라 멀어져도 일정
//  - 그림자 받기·드리우기, 깊이(외곽선 필터용)·노멀 패스 포함
// 텍스처는 KM SG_CharacterShader 의 _BaseColor(색 텍스처)를 _BaseMap 으로 복사해서 씀 (HJH/Character/만화풍 머티리얼 만들기)
Shader "HJH/Character/Toon"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Saturation ("Saturation", Range(0, 2)) = 1.15
        _Brightness ("Brightness", Range(0.5, 2)) = 1.05

        [Header(Shading)]
        _ShadowColor ("Shadow Tint", Color) = (0.62, 0.56, 0.74, 1)
        _ShadowThreshold ("Shadow Threshold", Range(-1, 1)) = 0.05
        _ShadowSoftness ("Shadow Edge Softness", Range(0.001, 0.5)) = 0.04
        _ReceiveShadow ("Receive Shadow", Range(0, 1)) = 0.85

        [Header(Rim)]
        _RimColor ("Rim Color", Color) = (1, 0.95, 0.85, 1)
        _RimPower ("Rim Width", Range(0.5, 8)) = 3.5
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.45

        [Header(Outline)]
        _OutlineColor ("Outline Color (x base color)", Color) = (0.25, 0.2, 0.28, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.01)) = 0.0025
        [Toggle] _OutlineSolid ("Outline Solid Color (적 표시용: 색 그대로, 1 = 빨강·흰 테두리)", Float) = 0

        [Header(Moss)]
        _MossColor ("Moss Color (위를 보는 면에 이끼, 적 리터칭)", Color) = (0.32, 0.5, 0.2, 1)
        _MossAmount ("Moss Amount", Range(0, 1)) = 0

        [Header(Swamp Paint)]   // 맵 풍으로 맞추기. 값이 0 이면 꺼짐
        _Simplify ("Simplify (텍스처 잔무늬 줄이기, 밉 바이어스)", Range(0, 4)) = 0
        _GradeColor ("Palette Grade (곱할 색)", Color) = (1, 1, 1, 1)
        _GradeAmount ("Palette Grade Amount", Range(0, 1)) = 0
        _BrushStrength ("Brush Strength (붓 터치: 명암 경계 흔들림 + 색 얼룩)", Range(0, 1)) = 0
        _BrushScale ("Brush Scale", Range(4, 120)) = 36
        _GlowColor ("Glow Color (청록·노랑 포인트가 스스로 빛남)", Color) = (0.3, 1, 0.95, 1)
        _GlowAmount ("Glow Amount", Range(0, 3)) = 0
        _GroundTint ("Ground Bounce (바닥 늪색 반사광, 아래를 보는 면)", Color) = (0.1, 0.3, 0.38, 1)
        _GroundTintAmount ("Ground Bounce Amount", Range(0, 1)) = 0

        [Header(Alpha)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Saturation;
            half _Brightness;
            half4 _ShadowColor;
            half _ShadowThreshold;
            half _ShadowSoftness;
            half _ReceiveShadow;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half4 _OutlineColor;
            float _OutlineWidth;
            half _OutlineSolid;
            half4 _MossColor;
            half _MossAmount;
            half _Cutoff;
            half _Simplify;
            half4 _GradeColor;
            half _GradeAmount;
            half _BrushStrength;
            half _BrushScale;
            half4 _GlowColor;
            half _GlowAmount;
            half4 _GroundTint;
            half _GroundTintAmount;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        float Hash21(float2 p)
        {
            return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
        }

        // 부드러운 값 노이즈 2겹 (UV 기준이라 움직여도 안 미끄러짐)
        half BrushNoise(float2 uv)
        {
            float2 p = uv * _BrushScale;
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            half n = lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x), lerp(Hash21(i + float2(0, 1)), Hash21(i + 1.0), f.x), f.y);
            float2 p2 = uv * _BrushScale * 2.7 + 7.3;
            float2 i2 = floor(p2);
            float2 f2 = frac(p2);
            f2 = f2 * f2 * (3.0 - 2.0 * f2);
            half n2 = lerp(lerp(Hash21(i2), Hash21(i2 + float2(1, 0)), f2.x), lerp(Hash21(i2 + float2(0, 1)), Hash21(i2 + 1.0), f2.x), f2.y);
            return n * 0.65 + n2 * 0.35;
        }

        half4 SampleBase(float2 uv)
        {
            half4 c = SAMPLE_TEXTURE2D_BIAS(_BaseMap, sampler_BaseMap, uv, _Simplify) * _BaseColor;
            #if defined(_ALPHATEST_ON)
                clip(c.a - _Cutoff);
            #endif
            return c;
        }
        ENDHLSL

        // ------------------------------------------------ 본 그림
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            // 추가 라이트 (캐릭터 전용 조명 CharacterLightLayer, 발광 꽃 점광원). 렌더링 레이어가 맞는 빛만 받음
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fog : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 base = SampleBase(i.uv);

                // 색 살짝 진하게 (애니풍)
                half luma = dot(base.rgb, half3(0.299, 0.587, 0.114));
                half3 albedo = lerp(luma.xxx, base.rgb, _Saturation) * _Brightness;

                // 맵 풍 맞추기: 팔레트를 맵 색(남색·청록)쪽으로 + 붓 얼룩 (옵션, 0 이면 그대로)
                albedo *= lerp(half3(1, 1, 1), _GradeColor.rgb, _GradeAmount);
                half brush = BrushNoise(i.uv);
                albedo *= 1.0 + (brush - 0.5) * 0.45 * _BrushStrength;

                // 청록·노랑 포인트(고글 렌즈, 장비 라인)는 스스로 빛남: 맵의 발광 식물과 같은 계열
                half glowMask = smoothstep(0.25, 0.45, min(base.g, base.b) - base.r)
                              + smoothstep(0.4, 0.6, min(base.r, base.g) - base.b);
                half3 glow = base.rgb * saturate(glowMask) * _GlowAmount * _GlowColor.rgb;

                float3 n = normalize(i.normalWS);
                // 이끼: 위를 보는 면 + UV 얼룩 (움직여도 안 미끄러짐) (정글 배경에 묻어나게)
                half moss = smoothstep(0.25, 0.6, n.y) * smoothstep(0.2, 0.5, frac(sin(dot(floor(i.uv * 20.0), float2(12.9898, 78.233))) * 43758.5453) * 0.5 + n.y * 0.5);
                albedo = lerp(albedo, _MossColor.rgb * (0.7 + luma * 0.6), moss * _MossAmount);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord);
                half shadow = lerp(1.0, light.shadowAttenuation, _ReceiveShadow);

                // 2단 명암: N·L 이 문턱을 넘으면 밝은 면, 아니면 색 있는 그늘
                half ndl = dot(n, light.direction) + (BrushNoise(i.uv * 0.7 + 3.1) - 0.5) * 0.35 * _BrushStrength;   // 명암 경계가 붓 터치처럼 울퉁불퉁
                half lit = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, ndl) * shadow;
                half3 lightColor = light.color;
                half3 ambient = SampleSH(n);
                half3 shade = albedo * _ShadowColor.rgb * (ambient * 0.6 + lightColor * 0.35);
                half3 bright = albedo * (lightColor + ambient * 0.35);
                half3 color = lerp(shade, bright, lit);

                // 늪 바닥 반사광: 아래를 보는 면(다리·소총 밑)에 청록빛이 번짐
                color += albedo * _GroundTint.rgb * saturate(-n.y * 0.8 + 0.2) * _GroundTintAmount;

                // 추가 라이트: 같은 2단 명암으로 밝은 면만 더함 (그늘 쪽은 그대로 → 툰 느낌 유지)
                #if defined(_ADDITIONAL_LIGHTS)
                {
                    uint meshLayers = GetMeshRenderingLayer();
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.positionWS;
                    inputData.normalWS = n;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                    uint pixelLightCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    {
                        Light extra = GetAdditionalLight(lightIndex, i.positionWS);
                        #ifdef _LIGHT_LAYERS
                        if (IsMatchingLightLayer(extra.layerMask, meshLayers))
                        #endif
                        {
                            half band = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, dot(n, extra.direction));
                            color += albedo * extra.color * extra.distanceAttenuation * band;
                        }
                    }
                    #endif
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light extra = GetAdditionalLight(lightIndex, i.positionWS);
                        #ifdef _LIGHT_LAYERS
                        if (IsMatchingLightLayer(extra.layerMask, meshLayers))
                        #endif
                        {
                            half band = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, dot(n, extra.direction));
                            color += albedo * extra.color * extra.distanceAttenuation * band;
                        }
                    LIGHT_LOOP_END
                }
                #endif

                // 테두리 빛: 밝은 쪽에서 더 세게 (그늘 쪽도 살짝)
                half rim = pow(saturate(1.0 - dot(n, v)), _RimPower);
                rim = smoothstep(0.35, 0.55, rim) * _RimStrength * lerp(0.4, 1.0, lit);
                color += _RimColor.rgb * rim * lightColor;
                color += glow;

                color = MixFog(color, i.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // ------------------------------------------------ 외곽선 (뒤집은 껍데기)
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fog : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float4 clip = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalCS = mul((float3x3)UNITY_MATRIX_VP, TransformObjectToWorldNormal(input.normalOS));
                float2 offset = normalize(normalCS.xy + 1e-5);
                offset.x *= _ScreenParams.y / _ScreenParams.x;   // 가로세로 비율 보정
                clip.xy += offset * _OutlineWidth * clip.w * 2.0;
                o.positionCS = clip;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fog = ComputeFogFactor(clip.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 base = SampleBase(i.uv);
                half3 color = lerp(base.rgb * _OutlineColor.rgb, _OutlineColor.rgb, _OutlineSolid);   // 같은 색의 어두운 선 (적은 단색 빨강·흰)
                return half4(MixFog(color, i.fog), 1);
            }
            ENDHLSL
        }

        // ------------------------------------------------ 그림자 드리우기
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                o.positionCS = ApplyShadowClamping(positionCS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                SampleBase(i.uv);
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------ 깊이 (외곽선 필터·뎁스 프리패스용)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half Frag(Varyings i) : SV_Target
            {
                SampleBase(i.uv);
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                SampleBase(i.uv);
                return half4(NormalizeNormalPerPixel(i.normalWS), 0);
            }
            ENDHLSL
        }
    }
}
