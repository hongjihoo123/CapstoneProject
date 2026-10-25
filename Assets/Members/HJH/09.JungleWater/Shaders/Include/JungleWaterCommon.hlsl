#ifndef JUNGLE_WATER_COMMON_INCLUDED
#define JUNGLE_WATER_COMMON_INCLUDED

// 물/안개 셰이더가 같이 쓰는 함수 모음.
// Core.hlsl 을 먼저 include 한 뒤에 include 할 것.
// 아래쪽 *_float 함수들은 Shader Graph 의 Custom Function 노드에서 그대로 쓸 수 있음.

// ---- WaterRippleSimulation.cs 가 매 프레임 넣어주는 전역 값 ----
TEXTURE2D(_JungleRippleTex);
SAMPLER(sampler_JungleRippleTex);
float4 _JungleRippleArea;   // xy = 영역 최소 XZ, z = 1/영역크기 (0이면 물결 없음), w = 텍셀 크기(UV)
float4 _JunglePlayerPos;    // xyz = 플레이어 위치, w = 1 이면 유효

// ---- 노이즈 ----
float JW_Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float JW_Hash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float JW_ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = JW_Hash21(i);
    float b = JW_Hash21(i + float2(1, 0));
    float c = JW_Hash21(i + float2(0, 1));
    float d = JW_Hash21(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float JW_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float n000 = JW_Hash31(i);
    float n100 = JW_Hash31(i + float3(1, 0, 0));
    float n010 = JW_Hash31(i + float3(0, 1, 0));
    float n110 = JW_Hash31(i + float3(1, 1, 0));
    float n001 = JW_Hash31(i + float3(0, 0, 1));
    float n101 = JW_Hash31(i + float3(1, 0, 1));
    float n011 = JW_Hash31(i + float3(0, 1, 1));
    float n111 = JW_Hash31(i + float3(1, 1, 1));
    float x00 = lerp(n000, n100, u.x);
    float x10 = lerp(n010, n110, u.x);
    float x01 = lerp(n001, n101, u.x);
    float x11 = lerp(n011, n111, u.x);
    return lerp(lerp(x00, x10, u.y), lerp(x01, x11, u.y), u.z);
}

float JW_Fbm(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    [unroll] for (int i = 0; i < 4; i++)
    {
        v += a * JW_ValueNoise(p);
        p = p * 2.03 + 17.1;
        a *= 0.5;
    }
    return v;
}

// ---- 깊이 텍스처 → 월드 좌표 ----
float3 JW_ReconstructWorldPos(float2 screenUV, float rawDepth)
{
#if !UNITY_REVERSED_Z
    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
#endif
    return ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
}

// ---- 물결(파동 시뮬레이션) 샘플링 ----
float JW_SampleRipple(float2 uv)
{
    return SAMPLE_TEXTURE2D_LOD(_JungleRippleTex, sampler_JungleRippleTex, uv, 0).r;
}

// 월드 위치의 물결 높이와 노멀. strength = 노멀 기울기 배율
void JW_RippleNormal(float3 positionWS, float strength, out float3 normalWS, out float height)
{
    normalWS = float3(0, 1, 0);
    height = 0;
    if (_JungleRippleArea.z <= 0)
        return;

    float2 uv = (positionWS.xz - _JungleRippleArea.xy) * _JungleRippleArea.z;
    float2 inside = step(0.0, uv) * step(uv, 1.0);
    if (inside.x * inside.y <= 0)
        return;

    float t = _JungleRippleArea.w;
    float hL = JW_SampleRipple(uv - float2(t, 0));
    float hR = JW_SampleRipple(uv + float2(t, 0));
    float hD = JW_SampleRipple(uv - float2(0, t));
    float hU = JW_SampleRipple(uv + float2(0, t));
    height = JW_SampleRipple(uv);

    // 높이가 +x 로 갈수록 높으면 노멀은 -x 로 기운다
    normalWS = normalize(float3((hL - hR) * strength, 1.0, (hD - hU) * strength));
}

// 플레이어 주변 0 → 멀어질수록 1 (안개 걷기용)
float JW_PlayerReveal(float3 positionWS, float radius, float softness, float minValue)
{
    if (_JunglePlayerPos.w <= 0)
        return 1.0;
    float d = distance(positionWS.xz, _JunglePlayerPos.xz);
    return lerp(minValue, 1.0, smoothstep(radius, radius + softness, d));
}

// ---- Shader Graph Custom Function 용 래퍼 ----
void JungleRipple_float(float3 PositionWS, float Strength, out float3 NormalWS, out float Height)
{
    JW_RippleNormal(PositionWS, Strength, NormalWS, Height);
}

void JungleNoise3D_float(float3 Position, out float Noise)
{
    Noise = JW_ValueNoise3(Position);
}

void JunglePlayerReveal_float(float3 PositionWS, float Radius, float Softness, float MinValue, out float Reveal)
{
    Reveal = JW_PlayerReveal(PositionWS, Radius, Softness, MinValue);
}

#endif
