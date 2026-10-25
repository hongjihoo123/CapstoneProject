// 물결 파동 시뮬레이션 (Custom Render Texture, 더블 버퍼)
//  R = 현재 높이, G = 이전 높이
//  파동 방정식: 새 높이 = (상하좌우 합) / 2 - 이전 높이, 그리고 감쇠
//  _Drops[i] = (uv.x, uv.y, 반지름(uv), 세기) → 그 자리를 눌러서 물결을 만든다
Shader "Hidden/HJH/JungleWater/RippleSim"
{
    Properties
    {
        _Damping ("Damping", Range(0.9, 1)) = 0.985
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Simulate"

            CGPROGRAM
            #include "UnityCustomRenderTexture.cginc"
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag
            #pragma target 3.0

            #define MAX_DROPS 32

            float _Damping;
            float4 _Drops[MAX_DROPS];
            float _DropCount;

            float4 frag(v2f_customrendertexture IN) : SV_Target
            {
                float2 uv = IN.globalTexcoord.xy;
                float2 t = 1.0 / _CustomRenderTextureInfo.xy;

                float2 c = tex2D(_SelfTexture2D, uv).rg;
                float neighbors = tex2D(_SelfTexture2D, uv + float2(t.x, 0)).r
                                + tex2D(_SelfTexture2D, uv - float2(t.x, 0)).r
                                + tex2D(_SelfTexture2D, uv + float2(0, t.y)).r
                                + tex2D(_SelfTexture2D, uv - float2(0, t.y)).r;

                float h = (neighbors * 0.5 - c.g) * _Damping;

                int count = (int)_DropCount;
                [loop] for (int i = 0; i < MAX_DROPS; i++)
                {
                    if (i >= count)
                        break;
                    float4 d = _Drops[i];
                    float dist = length(uv - d.xy) / d.z;
                    if (dist < 1.0)
                        h += d.w * (cos(dist * 3.14159265) * 0.5 + 0.5);
                }

                // 가장자리에서 물결이 튕겨 쌓이지 않게 흡수
                float2 edge = min(uv, 1.0 - uv);
                h *= saturate(min(edge.x, edge.y) * 40.0);

                return float4(h, c.r, 0, 0);
            }
            ENDCG
        }
    }
}
