Shader "Custom/SoulKnightLaserBeam"
{
    Properties
    {
        _MainTex ("Noise Tex", 2D) = "white" {}
        _InnerColor ("Inner Color", Color) = (1, 0.9, 0.9, 1)
        _OuterColor ("Outer Color", Color) = (1, 0.05, 0.05, 1)
        _BeamWidth ("Beam Width", Float) = 0.5
        _Speed ("Flow Speed", Float) = 2.0
        _TopMelt ("Top Melt Amount", Float) = 0.3
        _GlowPower ("Glow Power", Float) = 3.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };
            sampler2D _MainTex; float4 _InnerColor, _OuterColor; float _BeamWidth, _Speed, _TopMelt, _GlowPower;
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            v2f vert (appdata v)
            {
                v2f o;
                float hf = v.uv.y;
                float melt = hf * _TopMelt;
                float n = hash(v.vertex.xz + _Time.y) * melt;
                v.vertex.x += (n - 0.5) * melt;
                v.vertex.z += (hash(v.vertex.zx) - 0.5) * melt;
                v.vertex.x *= _BeamWidth; v.vertex.z *= _BeamWidth;
                o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float flowUV = uv.y - _Time.y * _Speed;
                float noise = tex2D(_MainTex, float2(uv.x * 2.0, flowUV * 0.5)).r;
                float distFromCenter = abs(uv.x - 0.5) * 2.0;
                float core = 1.0 - smoothstep(0.0, 0.8, distFromCenter);
                core = pow(core, _GlowPower);
                float verticalFade = smoothstep(0.0, 0.15, uv.y) * (1.0 - smoothstep(0.85, 1.0, uv.y) * 0.5);
                float topDrip = noise * step(0.8, uv.y);
                float4 col = lerp(_OuterColor, _InnerColor, core + noise * 0.3);
                col.a = (core + noise * 0.2 + topDrip) * verticalFade * 1.5;
                return col;
            }
            ENDCG
        }
    }
}
