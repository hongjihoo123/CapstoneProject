// 맵 만화풍 공용 코드 (HJH/Map/Toon, HJH/Map/ToonRock)
//  캐릭터 만화풍(HJH/Character/Toon)과 같은 규칙: 2단 명암 + 색 있는 그늘 + 테두리 빛
//  + 맵용: 노멀맵(약하게, 명암 경계를 붓 터치처럼 흔듦), 텍스처 잔무늬 줄이기(밉 바이어스), 잎 바람, 디더 반투명
#ifndef HJH_MAP_TOON_INCLUDED
#define HJH_MAP_TOON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _Saturation;
    half _Brightness;
    half _Simplify;
    half _NormalStrength;
    half4 _ShadowColor;
    half _ShadowThreshold;
    half _ShadowSoftness;
    half _ReceiveShadow;
    half4 _RimColor;
    half _RimPower;
    half _RimStrength;
    half4 _OutlineColor;
    float _OutlineWidth;
    half _Cutoff;
    float _WindSpeed;
    float _WindStrength;
    float4 _WindDirection;
    half _Fade;
    half _GreenKey;
CBUFFER_END

TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);  SAMPLER(sampler_BumpMap);

// 바위(ToonRock)는 _Fade 가 없고 _BaseColor.a 로 반투명 (가림 처리 스크립트가 _BaseColor 알파를 내림)
#if defined(HJH_ROCK)
    #define HJH_FADE _BaseColor.a
#else
    #define HJH_FADE _Fade
#endif

// 4x4 바이엘 디더: 반투명을 구멍 뚫기로 (그리는 순서 문제 없음)
void DitherClip(float4 positionCS)
{
    half fade = HJH_FADE;
    if (fade >= 0.999)
        return;
    uint2 p = uint2(positionCS.xy) % 4;
    const half bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    clip(fade - (bayer[p.y * 4 + p.x] + 0.5) / 16.0);
}

half4 SampleBase(float2 uv)
{
    half4 c = SAMPLE_TEXTURE2D_BIAS(_BaseMap, sampler_BaseMap, uv, _Simplify);
    c.rgb *= _BaseColor.rgb;
    #if defined(_ALPHATEST_ON)
        half4 sharp = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);   // 잎 외곽은 또렷한 원본 밉으로
        half a = lerp(sharp.a, smoothstep(0.06, 0.16, sharp.g), _GreenKey);   // 잎 그림은 알파 없이 검정 배경 → 초록이 어두우면 잘라냄 (원래 잎 셰이더와 같게)
        clip(a - _Cutoff);
    #endif
    return c;
}

// 잎 바람 (원래 잎 셰이더와 같은 식): 오브젝트 X 로 sin(시간 × 속도 + 월드 x) × 세기 × UV 중심에서의 거리
//  가운데(가지)는 고정, 카드 가장자리일수록 크게 흔들림
float3 Wind(float3 positionWS, float2 uv)
{
    #if defined(_WIND_ON)
        float sway = sin(_Time.y * _WindSpeed + positionWS.x) * _WindStrength * distance(uv, float2(0.5, 0.5));
        positionWS += TransformObjectToWorldDir(float3(1, 0, 0), false) * sway;
    #endif
    return positionWS;
}

#endif
