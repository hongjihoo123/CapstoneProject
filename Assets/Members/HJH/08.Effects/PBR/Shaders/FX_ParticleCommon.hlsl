#ifndef HJH_FX_PARTICLE_COMMON_INCLUDED
#define HJH_FX_PARTICLE_COMMON_INCLUDED

// 파티클 공통: 시트 애니메이션 프레임 섞기 + 소프트 파티클(바닥에 닿는 곳 부드럽게)
// 파티클 렌더러의 Custom Vertex Streams = Position, Normal, Color, UV, UV2, AnimBlend
//   → TEXCOORD0.xy = 지금 프레임 UV, TEXCOORD0.zw = 다음 프레임 UV, TEXCOORD1.x = 섞는 비율

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

struct FxAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    half4 color : COLOR;
    float4 uvs : TEXCOORD0;
    float blend : TEXCOORD1;
};

struct FxVaryings
{
    float4 positionCS : SV_POSITION;
    half4 color : COLOR;
    float4 uvs : TEXCOORD0;
    float blend : TEXCOORD1;
    float3 positionWS : TEXCOORD2;
    float3 normalWS : TEXCOORD3;
};

FxVaryings FxVert(FxAttributes v)
{
    FxVaryings o;
    o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
    o.positionCS = TransformWorldToHClip(o.positionWS);
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    o.color = v.color;
    o.uvs = v.uvs;
    o.blend = v.blend;
    return o;
}

// 두 프레임을 섞어서 샘플 (뚝뚝 끊기지 않게)
half4 FxSampleFlipbook(TEXTURE2D_PARAM(tex, samp), FxVaryings i)
{
    half4 a = SAMPLE_TEXTURE2D(tex, samp, i.uvs.xy);
    half4 b = SAMPLE_TEXTURE2D(tex, samp, i.uvs.zw);
    return lerp(a, b, saturate(i.blend));
}

// 바닥·벽과 겹치는 곳에서 0 → 멀어질수록 1
float FxSoftFade(FxVaryings i, float distance)
{
    if (distance <= 0.0)
        return 1.0;
    float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
    float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
    float selfEye = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
    return saturate((sceneEye - selfEye) / distance);
}

#endif
