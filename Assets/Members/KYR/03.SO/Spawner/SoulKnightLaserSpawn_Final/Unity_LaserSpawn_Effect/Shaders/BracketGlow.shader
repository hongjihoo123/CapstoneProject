Shader "Custom/BracketGlow" {
Properties { _Color ("Color", Color) = (1,0.6,0,1) _Emission ("Emission", Float)=2 }
SubShader {
 Tags { "Queue"="Transparent" "RenderType"="Transparent" }
 Blend SrcAlpha One ZWrite Off
 Pass {
  CGPROGRAM
  #pragma vertex vert
  #pragma fragment frag
  #include "UnityCG.cginc"
  fixed4 _Color; float _Emission;
  struct v2f { float4 pos:SV_POSITION; };
  v2f vert(appdata_base v){ v2f o; o.pos=UnityObjectToClipPos(v.vertex); return o; }
  fixed4 frag(v2f i):SV_Target { return _Color * _Emission; }
  ENDCG
 }
}
}
