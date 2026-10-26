#ifndef JUNGLE_WATER_COMMON_INCLUDED
#define JUNGLE_WATER_COMMON_INCLUDED

// 물/안개 셰이더가 같이 쓰는 HLSL.
//  - HLSL 버전(.shader): Core.hlsl (+ Lighting.hlsl) 다음에 include
//  - 노드 버전(.shadergraph): Custom Function 노드가 아래 *_float 함수를 부름
// 두 버전이 같은 함수를 쓰므로 결과가 똑같다.

// ---- WaterRippleSimulation.cs 가 매 프레임 넣어주는 전역 값 ----
TEXTURE2D(_JungleRippleTex);
SAMPLER(sampler_JungleRippleTex);
float4 _JungleRippleArea;   // xy = 영역 최소 XZ, z = 1/영역크기 (0이면 물결 없음), w = 텍셀 크기(UV)
float4 _JunglePlayerPos;    // xyz = 플레이어 위치, w = 1 이면 유효

// =====================================================================
// 노이즈 (노드 기본 노이즈와 무늬가 달라서 HLSL 하나로 통일)
// =====================================================================
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

// =====================================================================
// 깊이 → 바닥 위치
// 카메라에서 이 픽셀로 가는 광선을, 깊이 텍스처가 말하는 거리만큼 늘린다.
// (노드 버전 SGS_SceneBed 와 같은 계산)
// =====================================================================
float3 JW_CameraForward()
{
    return -UNITY_MATRIX_V[2].xyz;
}

float3 JW_BedFromRay(float3 surfaceWS, float sceneEyeDepth)
{
    float3 ray = surfaceWS - _WorldSpaceCameraPos;
    float surfaceEyeDepth = dot(ray, JW_CameraForward());
    return _WorldSpaceCameraPos + ray * (sceneEyeDepth / surfaceEyeDepth);
}

// =====================================================================
// 잔물결 높이 (두 겹의 흐르는 노이즈)
// =====================================================================
float JW_WaveHeight(float2 p, float t)
{
    return JW_ValueNoise(p + float2(t, t * 0.6)) * 0.6
         + JW_ValueNoise(p * 2.1 + float2(-t * 1.3, t * 0.8)) * 0.4;
}

// 옆 칸과의 높이 차이 = 기울기
float2 JW_WaveGradient(float2 p, float t)
{
    const float e = 0.08;
    float h = JW_WaveHeight(p, t);
    float hx = JW_WaveHeight(p + float2(e, 0), t);
    float hz = JW_WaveHeight(p + float2(0, e), t);
    return float2(hx - h, hz - h) / e;
}

// =====================================================================
// 상호작용 물결 (파동 시뮬레이션 텍스처 샘플링)
// =====================================================================
float JW_SampleRipple(float2 uv)
{
    return SAMPLE_TEXTURE2D_LOD(_JungleRippleTex, sampler_JungleRippleTex, uv, 0).r;
}

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

// =====================================================================
// 안개
// =====================================================================
float JW_PlayerReveal(float3 positionWS, float radius, float softness, float minValue)
{
    if (_JunglePlayerPos.w <= 0)
        return 1.0;
    float d = distance(positionWS.xz, _JunglePlayerPos.xz);
    return lerp(minValue, 1.0, smoothstep(radius, radius + softness, d));
}

float JW_FogNoise(float3 p, float t, float scale, float contrast, float3 wind)
{
    float3 q = p * scale;
    float n = JW_ValueNoise3(q + wind * t) * 0.65
            + JW_ValueNoise3(q * 2.3 - wind * t * 1.3) * 0.35;
    return saturate((n - 0.5) * contrast + 0.5);
}

#define JW_FOG_STEPS 8

// 안개층 윗면(entry) → 바닥(exit)까지 8걸음 걸으며 안개를 쌓는다.
// 노드에는 반복문이 없어서 이 부분만 HLSL.  반환값 = 안개 불투명도(0~1)
float JW_FogMarch(float3 entry, float3 exit, float2 screenUV,
                  float density, float thickness, float maxDistance,
                  float noiseScale, float noiseContrast, float3 wind,
                  float revealRadius, float revealSoftness, float revealMin)
{
    float3 ray = exit - entry;
    float len = length(ray);
    if (len < 1e-3)
        return 0;

    float3 dir = ray / len;
    len = min(len, maxDistance);
    float stepLen = len / JW_FOG_STEPS;
    float jitter = JW_Hash21(screenUV * _ScreenParams.xy);   // 띠무늬 줄이기
    float t = _Time.y;

    float transmittance = 1.0;
    [unroll] for (int s = 0; s < JW_FOG_STEPS; s++)
    {
        float3 p = entry + dir * ((s + jitter) * stepLen);
        float heightFactor = saturate((entry.y - p.y) / thickness);
        float reveal = JW_PlayerReveal(p, revealRadius, revealSoftness, revealMin);
        float d = density * heightFactor * JW_FogNoise(p, t, noiseScale, noiseContrast, wind) * reveal;
        transmittance *= exp(-d * stepLen);
    }
    return 1.0 - transmittance;
}

// =====================================================================
// 메인 라이트 (노드에는 라이트 색·그림자 노드가 없어서 HLSL)
// =====================================================================
void JW_MainLight(float3 positionWS, float3 normalWS, out float3 direction, out float3 color, out float shadow, out float3 ambient)
{
#if !defined(SHADERGRAPH_PREVIEW) && defined(UNIVERSAL_LIGHTING_INCLUDED)
    Light l = GetMainLight(TransformWorldToShadowCoord(positionWS));
    direction = l.direction;
    color = l.color;
    shadow = l.shadowAttenuation;
    ambient = SampleSH(normalWS);
#else
    direction = normalize(float3(0.4, 0.8, 0.3));
    color = float3(1, 1, 1);
    shadow = 1;
    ambient = float3(0.3, 0.3, 0.3);
#endif
}

// =====================================================================
// Shader Graph Custom Function 용 (함수 이름 + _float)
// =====================================================================
void JungleValueNoise_float(float2 P, out float Noise)
{
    Noise = JW_ValueNoise(P);
}

void JungleFbm_float(float2 P, out float Noise)
{
    Noise = JW_Fbm(P);
}

void JungleRipple_float(float3 PositionWS, float Strength, out float3 NormalWS, out float Height)
{
    JW_RippleNormal(PositionWS, Strength, NormalWS, Height);
}

void JungleMainLight_float(float3 PositionWS, float3 NormalWS, out float3 Direction, out float3 Color, out float Shadow, out float3 Ambient)
{
    JW_MainLight(PositionWS, NormalWS, Direction, Color, Shadow, Ambient);
}

void JungleFogMarch_float(float3 EntryWS, float3 ExitWS, float2 ScreenUV,
                          float Density, float Thickness, float MaxDistance,
                          float NoiseScale, float NoiseContrast, float3 Wind,
                          float RevealRadius, float RevealSoftness, float RevealMin,
                          out float Fog)
{
    Fog = JW_FogMarch(EntryWS, ExitWS, ScreenUV, Density, Thickness, MaxDistance,
                      NoiseScale, NoiseContrast, Wind, RevealRadius, RevealSoftness, RevealMin);
}

#endif
