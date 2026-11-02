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

// 반실사 색 덩어리: 연속으로 변하는 값(0~1)을 부드러운 계단으로
//  steps = 계단 수 (2 → 얕음/중간/깊음 3단계), softness = 계단 경계 부드러움 (0 = 칼같이, 0.5 = 거의 연속)
float JW_SoftBands(float x, float steps, float softness)
{
    float s = saturate(x) * steps;
    float stepped = floor(s) + smoothstep(0.5 - softness, 0.5 + softness, frac(s));
    return saturate(stepped / steps);
}

void JungleSoftBands_float(float In, float Steps, float Softness, out float Out)
{
    Out = JW_SoftBands(In, Steps, Softness);
}

// 물에 비치는 구름: 하늘을 실제로 그리지 않고, 반사 방향으로 위쪽 "구름 평면"의 노이즈를 읽음
//  탑뷰라 하늘은 화면에 안 보이고 반사만 보이므로 이걸로 충분. 구름은 천천히 흘러감
//  scale = 구름 크기(작을수록 큼), coverage = 구름 양(작을수록 많음), softness = 구름 가장자리 (작을수록 또렷 = 카툰)
float JW_CloudReflection(float3 positionWS, float3 normalWS, float scale, float coverage, float softness)
{
    float3 view = normalize(_WorldSpaceCameraPos - positionWS);
    float3 r = reflect(-view, normalize(normalWS));
    r.y = max(r.y, 0.15);
    float2 p = positionWS.xz + r.xz / r.y * 30.0;          // 30m 위 구름 평면에 닿는 곳
    p = p * scale + float2(_Time.y * 0.015, _Time.y * 0.008); // 바람에 흐름
    float n = JW_Fbm(p) * 0.7 + JW_Fbm(p * 2.7 + 13.1) * 0.3;
    return smoothstep(coverage, coverage + softness, n);
}

void JungleCloudReflection_float(float3 PositionWS, float3 NormalWS, float Scale, float Coverage, float Softness, out float Cloud)
{
    Cloud = JW_CloudReflection(PositionWS, NormalWS, Scale, Coverage, Softness);
}

// 카툰 물의 두 번째 거품 줄: 물가 거품 띠 바깥에 얇은 선 하나 더 (노이즈로 군데군데 끊김)
float JW_ToonFoamLine(float thickness, float distance, float noise)
{
    float d = thickness / max(distance * 1.8, 1e-4) + (noise - 0.5) * 0.15;
    float ring = smoothstep(0.86, 0.9, d) * (1.0 - smoothstep(0.95, 0.99, d));   // 얇은 선
    return ring * smoothstep(0.45, 0.55, noise);   // 군데군데 끊김
}

void JungleToonFoamLine_float(float Thickness, float Distance, float Noise, out float Line)
{
    Line = JW_ToonFoamLine(Thickness, Distance, Noise);
}

// 스킬 충격으로 뚫린 안개 구멍 (JungleFogController 가 채움)
// xy = 중심 xz, z = 반지름, w = 세기 (1 = 완전히 걷힘 → 0 = 다시 참)
#define JW_FOG_HOLES 8
float4 _JungleFogHoles[JW_FOG_HOLES];
float _JungleFogHoleCount;

// 1 = 안개 그대로. 구멍 안은 옅어지고, 테두리는 밀려난 안개가 살짝 짙어짐
float JW_FogHoles(float3 positionWS)
{
    float k = 1.0;
    [unroll] for (int h = 0; h < JW_FOG_HOLES; h++)
    {
        if (h >= (int)_JungleFogHoleCount)
            break;
        float4 hole = _JungleFogHoles[h];
        float d = distance(positionWS.xz, hole.xy);
        float inside = 1.0 - smoothstep(hole.z * 0.55, hole.z, d);
        float rim = smoothstep(hole.z * 0.8, hole.z, d) * (1.0 - smoothstep(hole.z, hole.z * 1.45, d));
        k *= (1.0 - inside * hole.w) * (1.0 + rim * 0.8 * hole.w);
    }
    return k;
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
        float reveal = JW_PlayerReveal(p, revealRadius, revealSoftness, revealMin) * JW_FogHoles(p);
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
