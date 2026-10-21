// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "SH_Healing_Flames"
{
	Properties
	{
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		[ASEBegin][NoScaleOffset]_MainTex("MainTex", 2D) = "white" {}
		_Flipbook_Columns("Flipbook_Columns", Float) = 0
		_Flipbook_Rows("Flipbook_Rows", Float) = 0
		[HDR]_InsideColor("InsideColor", Color) = (0,0.8624213,1,0)
		[NoScaleOffset]_WPOTexture("WPOTexture", 2D) = "white" {}
		_WPOPower("WPOPower", Float) = 1
		_WPOPannerSpeed("WPOPannerSpeed", Vector) = (0.1,0,0,0)
		[Toggle(_USEMOTIONVECTOR_ON)] _UseMotionVector("UseMotionVector", Float) = 0
		[NoScaleOffset]_MotionVectorTex("MotionVectorTex", 2D) = "white" {}
		_MotionVectorStrenght("MotionVectorStrenght", Float) = 0
		[ASEEnd]_DepthFade("DepthFade", Float) = 0

		[HideInInspector]_QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector]_QueueControl("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
		//_TessPhongStrength( "Tess Phong Strength", Range( 0, 1 ) ) = 0.5
		//_TessValue( "Tess Max Tessellation", Range( 1, 32 ) ) = 16
		//_TessMin( "Tess Min Distance", Float ) = 10
		//_TessMax( "Tess Max Distance", Float ) = 25
		//_TessEdgeLength ( "Tess Edge length", Range( 2, 50 ) ) = 16
		//_TessMaxDisp( "Tess Max Displacement", Float ) = 25
	}

	SubShader
	{
		LOD 0

		
		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
		
		Cull Off
		AlphaToMask Off
		
		HLSLINCLUDE
		#pragma target 3.0

		#pragma prefer_hlslcc gles
		#pragma exclude_renderers d3d11_9x 

		#ifndef ASE_TESS_FUNCS
		#define ASE_TESS_FUNCS
		float4 FixedTess( float tessValue )
		{
			return tessValue;
		}
		
		float CalcDistanceTessFactor (float4 vertex, float minDist, float maxDist, float tess, float4x4 o2w, float3 cameraPos )
		{
			float3 wpos = mul(o2w,vertex).xyz;
			float dist = distance (wpos, cameraPos);
			float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
			return f;
		}

		float4 CalcTriEdgeTessFactors (float3 triVertexFactors)
		{
			float4 tess;
			tess.x = 0.5 * (triVertexFactors.y + triVertexFactors.z);
			tess.y = 0.5 * (triVertexFactors.x + triVertexFactors.z);
			tess.z = 0.5 * (triVertexFactors.x + triVertexFactors.y);
			tess.w = (triVertexFactors.x + triVertexFactors.y + triVertexFactors.z) / 3.0f;
			return tess;
		}

		float CalcEdgeTessFactor (float3 wpos0, float3 wpos1, float edgeLen, float3 cameraPos, float4 scParams )
		{
			float dist = distance (0.5 * (wpos0+wpos1), cameraPos);
			float len = distance(wpos0, wpos1);
			float f = max(len * scParams.y / (edgeLen * dist), 1.0);
			return f;
		}

		float DistanceFromPlane (float3 pos, float4 plane)
		{
			float d = dot (float4(pos,1.0f), plane);
			return d;
		}

		bool WorldViewFrustumCull (float3 wpos0, float3 wpos1, float3 wpos2, float cullEps, float4 planes[6] )
		{
			float4 planeTest;
			planeTest.x = (( DistanceFromPlane(wpos0, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[0]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.y = (( DistanceFromPlane(wpos0, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[1]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.z = (( DistanceFromPlane(wpos0, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[2]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.w = (( DistanceFromPlane(wpos0, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[3]) > -cullEps) ? 1.0f : 0.0f );
			return !all (planeTest);
		}

		float4 DistanceBasedTess( float4 v0, float4 v1, float4 v2, float tess, float minDist, float maxDist, float4x4 o2w, float3 cameraPos )
		{
			float3 f;
			f.x = CalcDistanceTessFactor (v0,minDist,maxDist,tess,o2w,cameraPos);
			f.y = CalcDistanceTessFactor (v1,minDist,maxDist,tess,o2w,cameraPos);
			f.z = CalcDistanceTessFactor (v2,minDist,maxDist,tess,o2w,cameraPos);

			return CalcTriEdgeTessFactors (f);
		}

		float4 EdgeLengthBasedTess( float4 v0, float4 v1, float4 v2, float edgeLength, float4x4 o2w, float3 cameraPos, float4 scParams )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;
			tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
			tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
			tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
			tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			return tess;
		}

		float4 EdgeLengthBasedTessCull( float4 v0, float4 v1, float4 v2, float edgeLength, float maxDisplacement, float4x4 o2w, float3 cameraPos, float4 scParams, float4 planes[6] )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;

			if (WorldViewFrustumCull(pos0, pos1, pos2, maxDisplacement, planes))
			{
				tess = 0.0f;
			}
			else
			{
				tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
				tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
				tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
				tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			}
			return tess;
		}
		#endif //ASE_TESS_FUNCS

		ENDHLSL

		
		Pass
		{
			
			Name "Forward"
			Tags { "LightMode"="UniversalForward" }
			
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA
			

			HLSLPROGRAM
			
			#pragma multi_compile_instancing
			#define _RECEIVE_SHADOWS_OFF 1
			#define ASE_ABSOLUTE_VERTEX_POS 1
			#define ASE_SRP_VERSION 999999
			#define REQUIRE_DEPTH_TEXTURE 1

			
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma shader_feature _ _SAMPLE_GI
			#pragma multi_compile _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile _ DEBUG_DISPLAY
			#define SHADERPASS SHADERPASS_UNLIT


			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"


			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_FRAG_COLOR
			#pragma shader_feature_local _USEMOTIONVECTOR_ON
			#define REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				float4 ase_texcoord1 : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 worldPos : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				float4 shadowCoord : TEXCOORD1;
				#endif
				#ifdef ASE_FOG
				float fogFactor : TEXCOORD2;
				#endif
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_color : COLOR;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _InsideColor;
			float2 _WPOPannerSpeed;
			float _WPOPower;
			float _Flipbook_Columns;
			float _Flipbook_Rows;
			float _MotionVectorStrenght;
			float _DepthFade;
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END
			sampler2D _WPOTexture;
			sampler2D _MainTex;
			sampler2D _MotionVectorTex;
			#ifndef UNITY_DECLARE_DEPTH_TEXTURE_INCLUDED // URP 14+ declares it already
			uniform float4 _CameraDepthTexture_TexelSize;
			#endif


						
			VertexOutput VertexFunction ( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float2 texCoord22 = v.ase_texcoord * float2( 1,1 ) + float2( 0,0 );
				float2 panner27 = ( 1.0 * _Time.y * _WPOPannerSpeed + texCoord22);
				
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord5 = screenPos;
				
				o.ase_texcoord3 = v.ase_texcoord;
				o.ase_color = v.ase_color;
				o.ase_texcoord4 = v.ase_texcoord1;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = ( v.vertex.xyz + ( v.ase_normal * ( (tex2Dlod( _WPOTexture, float4( panner27, 0, 0.0) )).rgb * _WPOPower ) ) );
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float4 positionCS = TransformWorldToHClip( positionWS );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				o.worldPos = positionWS;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				VertexPositionInputs vertexInput = (VertexPositionInputs)0;
				vertexInput.positionWS = positionWS;
				vertexInput.positionCS = positionCS;
				o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				#ifdef ASE_FOG
				o.fogFactor = ComputeFogFactor( positionCS.z );
				#endif
				o.clipPos = positionCS;
				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				float4 ase_texcoord1 : TEXCOORD1;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_texcoord = v.ase_texcoord;
				o.ase_color = v.ase_color;
				o.ase_texcoord1 = v.ase_texcoord1;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				o.ase_texcoord1 = patch[0].ase_texcoord1 * bary.x + patch[1].ase_texcoord1 * bary.y + patch[2].ase_texcoord1 * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag ( VertexOutput IN , FRONT_FACE_TYPE ase_vface : FRONT_FACE_SEMANTIC ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 WorldPosition = IN.worldPos;
				#endif
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif
				float CVS_Y88 = IN.ase_texcoord3.w;
				float4 color55 = IsGammaSpace() ? float4(0.5,0,1,0) : float4(0.2140411,0,1,0);
				float2 texCoord12 = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float2 Input_UVs84 = texCoord12;
				float FB_Columns78 = _Flipbook_Columns;
				float temp_output_4_0_g38 = FB_Columns78;
				float FB_Rows79 = _Flipbook_Rows;
				float temp_output_5_0_g38 = FB_Rows79;
				float2 appendResult7_g38 = (float2(temp_output_4_0_g38 , temp_output_5_0_g38));
				float totalFrames39_g38 = ( temp_output_4_0_g38 * temp_output_5_0_g38 );
				float2 appendResult8_g38 = (float2(totalFrames39_g38 , temp_output_5_0_g38));
				float CVS_X87 = IN.ase_texcoord3.z;
				float clampResult42_g38 = clamp( 0.0 , 0.0001 , ( totalFrames39_g38 - 1.0 ) );
				float temp_output_35_0_g38 = frac( ( ( CVS_X87 + clampResult42_g38 ) / totalFrames39_g38 ) );
				float2 appendResult29_g38 = (float2(temp_output_35_0_g38 , ( 1.0 - temp_output_35_0_g38 )));
				float2 temp_output_15_0_g38 = ( ( Input_UVs84 / appendResult7_g38 ) + ( floor( ( appendResult8_g38 * appendResult29_g38 ) ) / appendResult7_g38 ) );
				float2 UVs38_g41 = Input_UVs84;
				float Flipbook_Columns41_g41 = FB_Columns78;
				float temp_output_4_0_g42 = Flipbook_Columns41_g41;
				float Flipbook_Rows44_g41 = FB_Rows79;
				float temp_output_5_0_g42 = Flipbook_Rows44_g41;
				float2 appendResult7_g42 = (float2(temp_output_4_0_g42 , temp_output_5_0_g42));
				float totalFrames39_g42 = ( temp_output_4_0_g42 * temp_output_5_0_g42 );
				float2 appendResult8_g42 = (float2(totalFrames39_g42 , temp_output_5_0_g42));
				float Current_Frame47_g41 = CVS_X87;
				float clampResult42_g42 = clamp( 0.0 , 0.0001 , ( totalFrames39_g42 - 1.0 ) );
				float temp_output_35_0_g42 = frac( ( ( Current_Frame47_g41 + clampResult42_g42 ) / totalFrames39_g42 ) );
				float2 appendResult29_g42 = (float2(temp_output_35_0_g42 , ( 1.0 - temp_output_35_0_g42 )));
				float2 temp_output_15_0_g42 = ( ( UVs38_g41 / appendResult7_g42 ) + ( floor( ( appendResult8_g42 * appendResult29_g42 ) ) / appendResult7_g42 ) );
				float2 temp_output_9_0_g41 = temp_output_15_0_g42;
				float4 tex2DNode16_g41 = tex2D( _MotionVectorTex, temp_output_9_0_g41 );
				float2 appendResult18_g41 = (float2(tex2DNode16_g41.r , tex2DNode16_g41.g));
				float2 temp_cast_0 = (1.0).xx;
				float Current_Frame_Frac57_g41 = frac( Current_Frame47_g41 );
				float temp_output_36_0_g41 = _MotionVectorStrenght;
				float temp_output_4_0_g43 = Flipbook_Columns41_g41;
				float temp_output_5_0_g43 = Flipbook_Rows44_g41;
				float2 appendResult7_g43 = (float2(temp_output_4_0_g43 , temp_output_5_0_g43));
				float totalFrames39_g43 = ( temp_output_4_0_g43 * temp_output_5_0_g43 );
				float2 appendResult8_g43 = (float2(totalFrames39_g43 , temp_output_5_0_g43));
				float clampResult42_g43 = clamp( 0.0 , 0.0001 , ( totalFrames39_g43 - 1.0 ) );
				float temp_output_35_0_g43 = frac( ( ( ( Current_Frame47_g41 + 1.0 ) + clampResult42_g43 ) / totalFrames39_g43 ) );
				float2 appendResult29_g43 = (float2(temp_output_35_0_g43 , ( 1.0 - temp_output_35_0_g43 )));
				float2 temp_output_15_0_g43 = ( ( UVs38_g41 / appendResult7_g43 ) + ( floor( ( appendResult8_g43 * appendResult29_g43 ) ) / appendResult7_g43 ) );
				float2 temp_output_3_0_g41 = temp_output_15_0_g43;
				float4 tex2DNode25_g41 = tex2D( _MotionVectorTex, temp_output_3_0_g41 );
				float2 appendResult28_g41 = (float2(tex2DNode25_g41.r , tex2DNode25_g41.g));
				float2 temp_cast_1 = (1.0).xx;
				float4 lerpResult7_g41 = lerp( tex2D( _MainTex, ( temp_output_9_0_g41 - ( ( ( ( appendResult18_g41 * 2.0 ) - temp_cast_0 ) * Current_Frame_Frac57_g41 ) * temp_output_36_0_g41 ) ) ) , tex2D( _MainTex, ( temp_output_3_0_g41 + ( temp_output_36_0_g41 * ( ( ( 2.0 * appendResult28_g41 ) - temp_cast_1 ) * ( 1.0 - Current_Frame_Frac57_g41 ) ) ) ) ) , Current_Frame_Frac57_g41);
				#ifdef _USEMOTIONVECTOR_ON
				float4 staticSwitch24 = lerpResult7_g41;
				#else
				float4 staticSwitch24 = tex2D( _MainTex, temp_output_15_0_g38 );
				#endif
				float4 break28 = staticSwitch24;
				float TX_B77 = break28.b;
				float3 VertexColor_RGB112 = (IN.ase_color).rgb;
				float3 temp_output_69_0 = ( ( TX_B77 * (_InsideColor).rgb ) + ( VertexColor_RGB112 * ( 1.0 - TX_B77 ) ) );
				float3 lerpResult56 = lerp( ( 0.6 * temp_output_69_0 ) , temp_output_69_0 , ase_vface);
				float2 texCoord30 = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_34_0 = ( 1.0 - texCoord30.y );
				float3 lerpResult53 = lerp( ( 0.5 * lerpResult56 ) , lerpResult56 , saturate( ( temp_output_34_0 * temp_output_34_0 ) ));
				float CVS_Z89 = IN.ase_texcoord4.x;
				float3 lerpResult59 = lerp( (color55).rgb , lerpResult53 , CVS_Z89);
				float TX_G76 = break28.g;
				float3 lerpResult51 = lerp( lerpResult59 , lerpResult53 , TX_G76);
				
				float VertexColor_A113 = IN.ase_color.a;
				float TX_R75 = break28.r;
				float4 screenPos = IN.ase_texcoord5;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float screenDepth33 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm.xy ),_ZBufferParams);
				float distanceDepth33 = abs( ( screenDepth33 - LinearEyeDepth( ase_screenPosNorm.z,_ZBufferParams ) ) / ( _DepthFade ) );
				
				float3 BakedAlbedo = 0;
				float3 BakedEmission = 0;
				float3 Color = ( CVS_Y88 * lerpResult51 );
				float Alpha = ( ( ( temp_output_34_0 * ( ( VertexColor_A113 * TX_G76 ) + TX_R75 ) ) * saturate( distanceDepth33 ) ) * VertexColor_A113 );
				float AlphaClipThreshold = 0.5;
				float AlphaClipThresholdShadow = 0.5;

				#ifdef _ALPHATEST_ON
					clip( Alpha - AlphaClipThreshold );
				#endif

				#if defined(_DBUFFER)
					ApplyDecalToBaseColor(IN.clipPos, Color);
				#endif

				#if defined(_ALPHAPREMULTIPLY_ON)
				Color *= Alpha;
				#endif


				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif

				#ifdef ASE_FOG
					Color = MixFog( Color, IN.fogFactor );
				#endif

				return half4( Color, Alpha );
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "DepthOnly"
			Tags { "LightMode"="DepthOnly" }

			ZWrite On
			ColorMask 0
			AlphaToMask Off

			HLSLPROGRAM
			
			#pragma multi_compile_instancing
			#define _RECEIVE_SHADOWS_OFF 1
			#define ASE_ABSOLUTE_VERTEX_POS 1
			#define ASE_SRP_VERSION 999999
			#define REQUIRE_DEPTH_TEXTURE 1

			
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#pragma shader_feature_local _USEMOTIONVECTOR_ON
			#define REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 worldPos : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				float4 shadowCoord : TEXCOORD1;
				#endif
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				float4 ase_texcoord3 : TEXCOORD3;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _InsideColor;
			float2 _WPOPannerSpeed;
			float _WPOPower;
			float _Flipbook_Columns;
			float _Flipbook_Rows;
			float _MotionVectorStrenght;
			float _DepthFade;
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END
			sampler2D _WPOTexture;
			sampler2D _MainTex;
			sampler2D _MotionVectorTex;
			#ifndef UNITY_DECLARE_DEPTH_TEXTURE_INCLUDED // URP 14+ declares it already
			uniform float4 _CameraDepthTexture_TexelSize;
			#endif


			
			VertexOutput VertexFunction( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float2 texCoord22 = v.ase_texcoord * float2( 1,1 ) + float2( 0,0 );
				float2 panner27 = ( 1.0 * _Time.y * _WPOPannerSpeed + texCoord22);
				
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord3 = screenPos;
				
				o.ase_texcoord2 = v.ase_texcoord;
				o.ase_color = v.ase_color;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = ( v.vertex.xyz + ( v.ase_normal * ( (tex2Dlod( _WPOTexture, float4( panner27, 0, 0.0) )).rgb * _WPOPower ) ) );
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif

				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				o.worldPos = positionWS;
				#endif

				o.clipPos = TransformWorldToHClip( positionWS );
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					VertexPositionInputs vertexInput = (VertexPositionInputs)0;
					vertexInput.positionWS = positionWS;
					vertexInput.positionCS = o.clipPos;
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_texcoord = v.ase_texcoord;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag(VertexOutput IN  ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 WorldPosition = IN.worldPos;
				#endif
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float2 texCoord30 = IN.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_34_0 = ( 1.0 - texCoord30.y );
				float VertexColor_A113 = IN.ase_color.a;
				float2 texCoord12 = IN.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float2 Input_UVs84 = texCoord12;
				float FB_Columns78 = _Flipbook_Columns;
				float temp_output_4_0_g38 = FB_Columns78;
				float FB_Rows79 = _Flipbook_Rows;
				float temp_output_5_0_g38 = FB_Rows79;
				float2 appendResult7_g38 = (float2(temp_output_4_0_g38 , temp_output_5_0_g38));
				float totalFrames39_g38 = ( temp_output_4_0_g38 * temp_output_5_0_g38 );
				float2 appendResult8_g38 = (float2(totalFrames39_g38 , temp_output_5_0_g38));
				float CVS_X87 = IN.ase_texcoord2.z;
				float clampResult42_g38 = clamp( 0.0 , 0.0001 , ( totalFrames39_g38 - 1.0 ) );
				float temp_output_35_0_g38 = frac( ( ( CVS_X87 + clampResult42_g38 ) / totalFrames39_g38 ) );
				float2 appendResult29_g38 = (float2(temp_output_35_0_g38 , ( 1.0 - temp_output_35_0_g38 )));
				float2 temp_output_15_0_g38 = ( ( Input_UVs84 / appendResult7_g38 ) + ( floor( ( appendResult8_g38 * appendResult29_g38 ) ) / appendResult7_g38 ) );
				float2 UVs38_g41 = Input_UVs84;
				float Flipbook_Columns41_g41 = FB_Columns78;
				float temp_output_4_0_g42 = Flipbook_Columns41_g41;
				float Flipbook_Rows44_g41 = FB_Rows79;
				float temp_output_5_0_g42 = Flipbook_Rows44_g41;
				float2 appendResult7_g42 = (float2(temp_output_4_0_g42 , temp_output_5_0_g42));
				float totalFrames39_g42 = ( temp_output_4_0_g42 * temp_output_5_0_g42 );
				float2 appendResult8_g42 = (float2(totalFrames39_g42 , temp_output_5_0_g42));
				float Current_Frame47_g41 = CVS_X87;
				float clampResult42_g42 = clamp( 0.0 , 0.0001 , ( totalFrames39_g42 - 1.0 ) );
				float temp_output_35_0_g42 = frac( ( ( Current_Frame47_g41 + clampResult42_g42 ) / totalFrames39_g42 ) );
				float2 appendResult29_g42 = (float2(temp_output_35_0_g42 , ( 1.0 - temp_output_35_0_g42 )));
				float2 temp_output_15_0_g42 = ( ( UVs38_g41 / appendResult7_g42 ) + ( floor( ( appendResult8_g42 * appendResult29_g42 ) ) / appendResult7_g42 ) );
				float2 temp_output_9_0_g41 = temp_output_15_0_g42;
				float4 tex2DNode16_g41 = tex2D( _MotionVectorTex, temp_output_9_0_g41 );
				float2 appendResult18_g41 = (float2(tex2DNode16_g41.r , tex2DNode16_g41.g));
				float2 temp_cast_0 = (1.0).xx;
				float Current_Frame_Frac57_g41 = frac( Current_Frame47_g41 );
				float temp_output_36_0_g41 = _MotionVectorStrenght;
				float temp_output_4_0_g43 = Flipbook_Columns41_g41;
				float temp_output_5_0_g43 = Flipbook_Rows44_g41;
				float2 appendResult7_g43 = (float2(temp_output_4_0_g43 , temp_output_5_0_g43));
				float totalFrames39_g43 = ( temp_output_4_0_g43 * temp_output_5_0_g43 );
				float2 appendResult8_g43 = (float2(totalFrames39_g43 , temp_output_5_0_g43));
				float clampResult42_g43 = clamp( 0.0 , 0.0001 , ( totalFrames39_g43 - 1.0 ) );
				float temp_output_35_0_g43 = frac( ( ( ( Current_Frame47_g41 + 1.0 ) + clampResult42_g43 ) / totalFrames39_g43 ) );
				float2 appendResult29_g43 = (float2(temp_output_35_0_g43 , ( 1.0 - temp_output_35_0_g43 )));
				float2 temp_output_15_0_g43 = ( ( UVs38_g41 / appendResult7_g43 ) + ( floor( ( appendResult8_g43 * appendResult29_g43 ) ) / appendResult7_g43 ) );
				float2 temp_output_3_0_g41 = temp_output_15_0_g43;
				float4 tex2DNode25_g41 = tex2D( _MotionVectorTex, temp_output_3_0_g41 );
				float2 appendResult28_g41 = (float2(tex2DNode25_g41.r , tex2DNode25_g41.g));
				float2 temp_cast_1 = (1.0).xx;
				float4 lerpResult7_g41 = lerp( tex2D( _MainTex, ( temp_output_9_0_g41 - ( ( ( ( appendResult18_g41 * 2.0 ) - temp_cast_0 ) * Current_Frame_Frac57_g41 ) * temp_output_36_0_g41 ) ) ) , tex2D( _MainTex, ( temp_output_3_0_g41 + ( temp_output_36_0_g41 * ( ( ( 2.0 * appendResult28_g41 ) - temp_cast_1 ) * ( 1.0 - Current_Frame_Frac57_g41 ) ) ) ) ) , Current_Frame_Frac57_g41);
				#ifdef _USEMOTIONVECTOR_ON
				float4 staticSwitch24 = lerpResult7_g41;
				#else
				float4 staticSwitch24 = tex2D( _MainTex, temp_output_15_0_g38 );
				#endif
				float4 break28 = staticSwitch24;
				float TX_G76 = break28.g;
				float TX_R75 = break28.r;
				float4 screenPos = IN.ase_texcoord3;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float screenDepth33 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm.xy ),_ZBufferParams);
				float distanceDepth33 = abs( ( screenDepth33 - LinearEyeDepth( ase_screenPosNorm.z,_ZBufferParams ) ) / ( _DepthFade ) );
				
				float Alpha = ( ( ( temp_output_34_0 * ( ( VertexColor_A113 * TX_G76 ) + TX_R75 ) ) * saturate( distanceDepth33 ) ) * VertexColor_A113 );
				float AlphaClipThreshold = 0.5;

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif
				return 0;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "Universal2D"
			Tags { "LightMode"="Universal2D" }
			
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA
			

			HLSLPROGRAM
			
			#pragma multi_compile_instancing
			#define _RECEIVE_SHADOWS_OFF 1
			#define ASE_ABSOLUTE_VERTEX_POS 1
			#define ASE_SRP_VERSION 999999
			#define REQUIRE_DEPTH_TEXTURE 1

			
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma shader_feature _ _SAMPLE_GI
			#pragma multi_compile _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile _ DEBUG_DISPLAY
			#define SHADERPASS SHADERPASS_UNLIT


			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"


			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_FRAG_COLOR
			#pragma shader_feature_local _USEMOTIONVECTOR_ON
			#define REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				float4 ase_texcoord1 : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 worldPos : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				float4 shadowCoord : TEXCOORD1;
				#endif
				#ifdef ASE_FOG
				float fogFactor : TEXCOORD2;
				#endif
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_color : COLOR;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _InsideColor;
			float2 _WPOPannerSpeed;
			float _WPOPower;
			float _Flipbook_Columns;
			float _Flipbook_Rows;
			float _MotionVectorStrenght;
			float _DepthFade;
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END
			sampler2D _WPOTexture;
			sampler2D _MainTex;
			sampler2D _MotionVectorTex;
			#ifndef UNITY_DECLARE_DEPTH_TEXTURE_INCLUDED // URP 14+ declares it already
			uniform float4 _CameraDepthTexture_TexelSize;
			#endif


						
			VertexOutput VertexFunction ( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float2 texCoord22 = v.ase_texcoord * float2( 1,1 ) + float2( 0,0 );
				float2 panner27 = ( 1.0 * _Time.y * _WPOPannerSpeed + texCoord22);
				
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord5 = screenPos;
				
				o.ase_texcoord3 = v.ase_texcoord;
				o.ase_color = v.ase_color;
				o.ase_texcoord4 = v.ase_texcoord1;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = ( v.vertex.xyz + ( v.ase_normal * ( (tex2Dlod( _WPOTexture, float4( panner27, 0, 0.0) )).rgb * _WPOPower ) ) );
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float4 positionCS = TransformWorldToHClip( positionWS );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				o.worldPos = positionWS;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				VertexPositionInputs vertexInput = (VertexPositionInputs)0;
				vertexInput.positionWS = positionWS;
				vertexInput.positionCS = positionCS;
				o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				#ifdef ASE_FOG
				o.fogFactor = ComputeFogFactor( positionCS.z );
				#endif
				o.clipPos = positionCS;
				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				float4 ase_texcoord1 : TEXCOORD1;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_texcoord = v.ase_texcoord;
				o.ase_color = v.ase_color;
				o.ase_texcoord1 = v.ase_texcoord1;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				o.ase_texcoord1 = patch[0].ase_texcoord1 * bary.x + patch[1].ase_texcoord1 * bary.y + patch[2].ase_texcoord1 * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag ( VertexOutput IN , FRONT_FACE_TYPE ase_vface : FRONT_FACE_SEMANTIC ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 WorldPosition = IN.worldPos;
				#endif
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif
				float CVS_Y88 = IN.ase_texcoord3.w;
				float4 color55 = IsGammaSpace() ? float4(0.5,0,1,0) : float4(0.2140411,0,1,0);
				float2 texCoord12 = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float2 Input_UVs84 = texCoord12;
				float FB_Columns78 = _Flipbook_Columns;
				float temp_output_4_0_g38 = FB_Columns78;
				float FB_Rows79 = _Flipbook_Rows;
				float temp_output_5_0_g38 = FB_Rows79;
				float2 appendResult7_g38 = (float2(temp_output_4_0_g38 , temp_output_5_0_g38));
				float totalFrames39_g38 = ( temp_output_4_0_g38 * temp_output_5_0_g38 );
				float2 appendResult8_g38 = (float2(totalFrames39_g38 , temp_output_5_0_g38));
				float CVS_X87 = IN.ase_texcoord3.z;
				float clampResult42_g38 = clamp( 0.0 , 0.0001 , ( totalFrames39_g38 - 1.0 ) );
				float temp_output_35_0_g38 = frac( ( ( CVS_X87 + clampResult42_g38 ) / totalFrames39_g38 ) );
				float2 appendResult29_g38 = (float2(temp_output_35_0_g38 , ( 1.0 - temp_output_35_0_g38 )));
				float2 temp_output_15_0_g38 = ( ( Input_UVs84 / appendResult7_g38 ) + ( floor( ( appendResult8_g38 * appendResult29_g38 ) ) / appendResult7_g38 ) );
				float2 UVs38_g41 = Input_UVs84;
				float Flipbook_Columns41_g41 = FB_Columns78;
				float temp_output_4_0_g42 = Flipbook_Columns41_g41;
				float Flipbook_Rows44_g41 = FB_Rows79;
				float temp_output_5_0_g42 = Flipbook_Rows44_g41;
				float2 appendResult7_g42 = (float2(temp_output_4_0_g42 , temp_output_5_0_g42));
				float totalFrames39_g42 = ( temp_output_4_0_g42 * temp_output_5_0_g42 );
				float2 appendResult8_g42 = (float2(totalFrames39_g42 , temp_output_5_0_g42));
				float Current_Frame47_g41 = CVS_X87;
				float clampResult42_g42 = clamp( 0.0 , 0.0001 , ( totalFrames39_g42 - 1.0 ) );
				float temp_output_35_0_g42 = frac( ( ( Current_Frame47_g41 + clampResult42_g42 ) / totalFrames39_g42 ) );
				float2 appendResult29_g42 = (float2(temp_output_35_0_g42 , ( 1.0 - temp_output_35_0_g42 )));
				float2 temp_output_15_0_g42 = ( ( UVs38_g41 / appendResult7_g42 ) + ( floor( ( appendResult8_g42 * appendResult29_g42 ) ) / appendResult7_g42 ) );
				float2 temp_output_9_0_g41 = temp_output_15_0_g42;
				float4 tex2DNode16_g41 = tex2D( _MotionVectorTex, temp_output_9_0_g41 );
				float2 appendResult18_g41 = (float2(tex2DNode16_g41.r , tex2DNode16_g41.g));
				float2 temp_cast_0 = (1.0).xx;
				float Current_Frame_Frac57_g41 = frac( Current_Frame47_g41 );
				float temp_output_36_0_g41 = _MotionVectorStrenght;
				float temp_output_4_0_g43 = Flipbook_Columns41_g41;
				float temp_output_5_0_g43 = Flipbook_Rows44_g41;
				float2 appendResult7_g43 = (float2(temp_output_4_0_g43 , temp_output_5_0_g43));
				float totalFrames39_g43 = ( temp_output_4_0_g43 * temp_output_5_0_g43 );
				float2 appendResult8_g43 = (float2(totalFrames39_g43 , temp_output_5_0_g43));
				float clampResult42_g43 = clamp( 0.0 , 0.0001 , ( totalFrames39_g43 - 1.0 ) );
				float temp_output_35_0_g43 = frac( ( ( ( Current_Frame47_g41 + 1.0 ) + clampResult42_g43 ) / totalFrames39_g43 ) );
				float2 appendResult29_g43 = (float2(temp_output_35_0_g43 , ( 1.0 - temp_output_35_0_g43 )));
				float2 temp_output_15_0_g43 = ( ( UVs38_g41 / appendResult7_g43 ) + ( floor( ( appendResult8_g43 * appendResult29_g43 ) ) / appendResult7_g43 ) );
				float2 temp_output_3_0_g41 = temp_output_15_0_g43;
				float4 tex2DNode25_g41 = tex2D( _MotionVectorTex, temp_output_3_0_g41 );
				float2 appendResult28_g41 = (float2(tex2DNode25_g41.r , tex2DNode25_g41.g));
				float2 temp_cast_1 = (1.0).xx;
				float4 lerpResult7_g41 = lerp( tex2D( _MainTex, ( temp_output_9_0_g41 - ( ( ( ( appendResult18_g41 * 2.0 ) - temp_cast_0 ) * Current_Frame_Frac57_g41 ) * temp_output_36_0_g41 ) ) ) , tex2D( _MainTex, ( temp_output_3_0_g41 + ( temp_output_36_0_g41 * ( ( ( 2.0 * appendResult28_g41 ) - temp_cast_1 ) * ( 1.0 - Current_Frame_Frac57_g41 ) ) ) ) ) , Current_Frame_Frac57_g41);
				#ifdef _USEMOTIONVECTOR_ON
				float4 staticSwitch24 = lerpResult7_g41;
				#else
				float4 staticSwitch24 = tex2D( _MainTex, temp_output_15_0_g38 );
				#endif
				float4 break28 = staticSwitch24;
				float TX_B77 = break28.b;
				float3 VertexColor_RGB112 = (IN.ase_color).rgb;
				float3 temp_output_69_0 = ( ( TX_B77 * (_InsideColor).rgb ) + ( VertexColor_RGB112 * ( 1.0 - TX_B77 ) ) );
				float3 lerpResult56 = lerp( ( 0.6 * temp_output_69_0 ) , temp_output_69_0 , ase_vface);
				float2 texCoord30 = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_34_0 = ( 1.0 - texCoord30.y );
				float3 lerpResult53 = lerp( ( 0.5 * lerpResult56 ) , lerpResult56 , saturate( ( temp_output_34_0 * temp_output_34_0 ) ));
				float CVS_Z89 = IN.ase_texcoord4.x;
				float3 lerpResult59 = lerp( (color55).rgb , lerpResult53 , CVS_Z89);
				float TX_G76 = break28.g;
				float3 lerpResult51 = lerp( lerpResult59 , lerpResult53 , TX_G76);
				
				float VertexColor_A113 = IN.ase_color.a;
				float TX_R75 = break28.r;
				float4 screenPos = IN.ase_texcoord5;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float screenDepth33 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm.xy ),_ZBufferParams);
				float distanceDepth33 = abs( ( screenDepth33 - LinearEyeDepth( ase_screenPosNorm.z,_ZBufferParams ) ) / ( _DepthFade ) );
				
				float3 BakedAlbedo = 0;
				float3 BakedEmission = 0;
				float3 Color = ( CVS_Y88 * lerpResult51 );
				float Alpha = ( ( ( temp_output_34_0 * ( ( VertexColor_A113 * TX_G76 ) + TX_R75 ) ) * saturate( distanceDepth33 ) ) * VertexColor_A113 );
				float AlphaClipThreshold = 0.5;
				float AlphaClipThresholdShadow = 0.5;

				#ifdef _ALPHATEST_ON
					clip( Alpha - AlphaClipThreshold );
				#endif

				#if defined(_DBUFFER)
					ApplyDecalToBaseColor(IN.clipPos, Color);
				#endif

				#if defined(_ALPHAPREMULTIPLY_ON)
				Color *= Alpha;
				#endif


				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif

				#ifdef ASE_FOG
					Color = MixFog( Color, IN.fogFactor );
				#endif

				return half4( Color, Alpha );
			}

			ENDHLSL
		}


		
        Pass
        {
			
            Name "SceneSelectionPass"
            Tags { "LightMode"="SceneSelectionPass" }
        
			Cull Off

			HLSLPROGRAM
        
			#pragma multi_compile_instancing
			#define _RECEIVE_SHADOWS_OFF 1
			#define ASE_ABSOLUTE_VERTEX_POS 1
			#define ASE_SRP_VERSION 999999
			#define REQUIRE_DEPTH_TEXTURE 1

        
			#pragma only_renderers d3d11 glcore gles gles3 
			#pragma vertex vert
			#pragma fragment frag

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#pragma shader_feature_local _USEMOTIONVECTOR_ON
			#define REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				float4 ase_texcoord1 : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
        
			CBUFFER_START(UnityPerMaterial)
			float4 _InsideColor;
			float2 _WPOPannerSpeed;
			float _WPOPower;
			float _Flipbook_Columns;
			float _Flipbook_Rows;
			float _MotionVectorStrenght;
			float _DepthFade;
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			sampler2D _WPOTexture;
			sampler2D _MainTex;
			sampler2D _MotionVectorTex;
			#ifndef UNITY_DECLARE_DEPTH_TEXTURE_INCLUDED // URP 14+ declares it already
			uniform float4 _CameraDepthTexture_TexelSize;
			#endif


			
			int _ObjectId;
			int _PassValue;

			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};
        
			VertexOutput VertexFunction(VertexInput v  )
			{
				VertexOutput o;
				ZERO_INITIALIZE(VertexOutput, o);

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);


				float2 texCoord22 = v.ase_texcoord * float2( 1,1 ) + float2( 0,0 );
				float2 panner27 = ( 1.0 * _Time.y * _WPOPannerSpeed + texCoord22);
				
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord1 = screenPos;
				
				o.ase_texcoord = v.ase_texcoord;
				o.ase_color = v.ase_color;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = ( v.vertex.xyz + ( v.ase_normal * ( (tex2Dlod( _WPOTexture, float4( panner27, 0, 0.0) )).rgb * _WPOPower ) ) );
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				o.clipPos = TransformWorldToHClip(positionWS);
				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_texcoord = v.ase_texcoord;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif
			
			half4 frag(VertexOutput IN ) : SV_TARGET
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;
				float2 texCoord30 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_34_0 = ( 1.0 - texCoord30.y );
				float VertexColor_A113 = IN.ase_color.a;
				float2 texCoord12 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float2 Input_UVs84 = texCoord12;
				float FB_Columns78 = _Flipbook_Columns;
				float temp_output_4_0_g38 = FB_Columns78;
				float FB_Rows79 = _Flipbook_Rows;
				float temp_output_5_0_g38 = FB_Rows79;
				float2 appendResult7_g38 = (float2(temp_output_4_0_g38 , temp_output_5_0_g38));
				float totalFrames39_g38 = ( temp_output_4_0_g38 * temp_output_5_0_g38 );
				float2 appendResult8_g38 = (float2(totalFrames39_g38 , temp_output_5_0_g38));
				float CVS_X87 = IN.ase_texcoord.z;
				float clampResult42_g38 = clamp( 0.0 , 0.0001 , ( totalFrames39_g38 - 1.0 ) );
				float temp_output_35_0_g38 = frac( ( ( CVS_X87 + clampResult42_g38 ) / totalFrames39_g38 ) );
				float2 appendResult29_g38 = (float2(temp_output_35_0_g38 , ( 1.0 - temp_output_35_0_g38 )));
				float2 temp_output_15_0_g38 = ( ( Input_UVs84 / appendResult7_g38 ) + ( floor( ( appendResult8_g38 * appendResult29_g38 ) ) / appendResult7_g38 ) );
				float2 UVs38_g41 = Input_UVs84;
				float Flipbook_Columns41_g41 = FB_Columns78;
				float temp_output_4_0_g42 = Flipbook_Columns41_g41;
				float Flipbook_Rows44_g41 = FB_Rows79;
				float temp_output_5_0_g42 = Flipbook_Rows44_g41;
				float2 appendResult7_g42 = (float2(temp_output_4_0_g42 , temp_output_5_0_g42));
				float totalFrames39_g42 = ( temp_output_4_0_g42 * temp_output_5_0_g42 );
				float2 appendResult8_g42 = (float2(totalFrames39_g42 , temp_output_5_0_g42));
				float Current_Frame47_g41 = CVS_X87;
				float clampResult42_g42 = clamp( 0.0 , 0.0001 , ( totalFrames39_g42 - 1.0 ) );
				float temp_output_35_0_g42 = frac( ( ( Current_Frame47_g41 + clampResult42_g42 ) / totalFrames39_g42 ) );
				float2 appendResult29_g42 = (float2(temp_output_35_0_g42 , ( 1.0 - temp_output_35_0_g42 )));
				float2 temp_output_15_0_g42 = ( ( UVs38_g41 / appendResult7_g42 ) + ( floor( ( appendResult8_g42 * appendResult29_g42 ) ) / appendResult7_g42 ) );
				float2 temp_output_9_0_g41 = temp_output_15_0_g42;
				float4 tex2DNode16_g41 = tex2D( _MotionVectorTex, temp_output_9_0_g41 );
				float2 appendResult18_g41 = (float2(tex2DNode16_g41.r , tex2DNode16_g41.g));
				float2 temp_cast_0 = (1.0).xx;
				float Current_Frame_Frac57_g41 = frac( Current_Frame47_g41 );
				float temp_output_36_0_g41 = _MotionVectorStrenght;
				float temp_output_4_0_g43 = Flipbook_Columns41_g41;
				float temp_output_5_0_g43 = Flipbook_Rows44_g41;
				float2 appendResult7_g43 = (float2(temp_output_4_0_g43 , temp_output_5_0_g43));
				float totalFrames39_g43 = ( temp_output_4_0_g43 * temp_output_5_0_g43 );
				float2 appendResult8_g43 = (float2(totalFrames39_g43 , temp_output_5_0_g43));
				float clampResult42_g43 = clamp( 0.0 , 0.0001 , ( totalFrames39_g43 - 1.0 ) );
				float temp_output_35_0_g43 = frac( ( ( ( Current_Frame47_g41 + 1.0 ) + clampResult42_g43 ) / totalFrames39_g43 ) );
				float2 appendResult29_g43 = (float2(temp_output_35_0_g43 , ( 1.0 - temp_output_35_0_g43 )));
				float2 temp_output_15_0_g43 = ( ( UVs38_g41 / appendResult7_g43 ) + ( floor( ( appendResult8_g43 * appendResult29_g43 ) ) / appendResult7_g43 ) );
				float2 temp_output_3_0_g41 = temp_output_15_0_g43;
				float4 tex2DNode25_g41 = tex2D( _MotionVectorTex, temp_output_3_0_g41 );
				float2 appendResult28_g41 = (float2(tex2DNode25_g41.r , tex2DNode25_g41.g));
				float2 temp_cast_1 = (1.0).xx;
				float4 lerpResult7_g41 = lerp( tex2D( _MainTex, ( temp_output_9_0_g41 - ( ( ( ( appendResult18_g41 * 2.0 ) - temp_cast_0 ) * Current_Frame_Frac57_g41 ) * temp_output_36_0_g41 ) ) ) , tex2D( _MainTex, ( temp_output_3_0_g41 + ( temp_output_36_0_g41 * ( ( ( 2.0 * appendResult28_g41 ) - temp_cast_1 ) * ( 1.0 - Current_Frame_Frac57_g41 ) ) ) ) ) , Current_Frame_Frac57_g41);
				#ifdef _USEMOTIONVECTOR_ON
				float4 staticSwitch24 = lerpResult7_g41;
				#else
				float4 staticSwitch24 = tex2D( _MainTex, temp_output_15_0_g38 );
				#endif
				float4 break28 = staticSwitch24;
				float TX_G76 = break28.g;
				float TX_R75 = break28.r;
				float4 screenPos = IN.ase_texcoord1;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float screenDepth33 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm.xy ),_ZBufferParams);
				float distanceDepth33 = abs( ( screenDepth33 - LinearEyeDepth( ase_screenPosNorm.z,_ZBufferParams ) ) / ( _DepthFade ) );
				
				surfaceDescription.Alpha = ( ( ( temp_output_34_0 * ( ( VertexColor_A113 * TX_G76 ) + TX_R75 ) ) * saturate( distanceDepth33 ) ) * VertexColor_A113 );
				surfaceDescription.AlphaClipThreshold = 0.5;


				#if _ALPHATEST_ON
					float alphaClipThreshold = 0.01f;
					#if ALPHA_CLIP_THRESHOLD
						alphaClipThreshold = surfaceDescription.AlphaClipThreshold;
					#endif
					clip(surfaceDescription.Alpha - alphaClipThreshold);
				#endif

				half4 outColor = half4(_ObjectId, _PassValue, 1.0, 1.0);
				return outColor;
			}

			ENDHLSL
        }

		
        Pass
        {
			
            Name "ScenePickingPass"
            Tags { "LightMode"="Picking" }
        
			HLSLPROGRAM

			#pragma multi_compile_instancing
			#define _RECEIVE_SHADOWS_OFF 1
			#define ASE_ABSOLUTE_VERTEX_POS 1
			#define ASE_SRP_VERSION 999999
			#define REQUIRE_DEPTH_TEXTURE 1


			#pragma only_renderers d3d11 glcore gles gles3 
			#pragma vertex vert
			#pragma fragment frag

        
			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define SHADERPASS SHADERPASS_DEPTHONLY
			

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#pragma shader_feature_local _USEMOTIONVECTOR_ON
			#define REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				float4 ase_texcoord1 : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
        
			CBUFFER_START(UnityPerMaterial)
			float4 _InsideColor;
			float2 _WPOPannerSpeed;
			float _WPOPower;
			float _Flipbook_Columns;
			float _Flipbook_Rows;
			float _MotionVectorStrenght;
			float _DepthFade;
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			sampler2D _WPOTexture;
			sampler2D _MainTex;
			sampler2D _MotionVectorTex;
			#ifndef UNITY_DECLARE_DEPTH_TEXTURE_INCLUDED // URP 14+ declares it already
			uniform float4 _CameraDepthTexture_TexelSize;
			#endif


			
        
			float4 _SelectionID;

        
			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};
        
			VertexOutput VertexFunction(VertexInput v  )
			{
				VertexOutput o;
				ZERO_INITIALIZE(VertexOutput, o);

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);


				float2 texCoord22 = v.ase_texcoord * float2( 1,1 ) + float2( 0,0 );
				float2 panner27 = ( 1.0 * _Time.y * _WPOPannerSpeed + texCoord22);
				
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord1 = screenPos;
				
				o.ase_texcoord = v.ase_texcoord;
				o.ase_color = v.ase_color;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = ( v.vertex.xyz + ( v.ase_normal * ( (tex2Dlod( _WPOTexture, float4( panner27, 0, 0.0) )).rgb * _WPOPower ) ) );
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				o.clipPos = TransformWorldToHClip(positionWS);
				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_texcoord = v.ase_texcoord;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag(VertexOutput IN ) : SV_TARGET
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;
				float2 texCoord30 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_34_0 = ( 1.0 - texCoord30.y );
				float VertexColor_A113 = IN.ase_color.a;
				float2 texCoord12 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float2 Input_UVs84 = texCoord12;
				float FB_Columns78 = _Flipbook_Columns;
				float temp_output_4_0_g38 = FB_Columns78;
				float FB_Rows79 = _Flipbook_Rows;
				float temp_output_5_0_g38 = FB_Rows79;
				float2 appendResult7_g38 = (float2(temp_output_4_0_g38 , temp_output_5_0_g38));
				float totalFrames39_g38 = ( temp_output_4_0_g38 * temp_output_5_0_g38 );
				float2 appendResult8_g38 = (float2(totalFrames39_g38 , temp_output_5_0_g38));
				float CVS_X87 = IN.ase_texcoord.z;
				float clampResult42_g38 = clamp( 0.0 , 0.0001 , ( totalFrames39_g38 - 1.0 ) );
				float temp_output_35_0_g38 = frac( ( ( CVS_X87 + clampResult42_g38 ) / totalFrames39_g38 ) );
				float2 appendResult29_g38 = (float2(temp_output_35_0_g38 , ( 1.0 - temp_output_35_0_g38 )));
				float2 temp_output_15_0_g38 = ( ( Input_UVs84 / appendResult7_g38 ) + ( floor( ( appendResult8_g38 * appendResult29_g38 ) ) / appendResult7_g38 ) );
				float2 UVs38_g41 = Input_UVs84;
				float Flipbook_Columns41_g41 = FB_Columns78;
				float temp_output_4_0_g42 = Flipbook_Columns41_g41;
				float Flipbook_Rows44_g41 = FB_Rows79;
				float temp_output_5_0_g42 = Flipbook_Rows44_g41;
				float2 appendResult7_g42 = (float2(temp_output_4_0_g42 , temp_output_5_0_g42));
				float totalFrames39_g42 = ( temp_output_4_0_g42 * temp_output_5_0_g42 );
				float2 appendResult8_g42 = (float2(totalFrames39_g42 , temp_output_5_0_g42));
				float Current_Frame47_g41 = CVS_X87;
				float clampResult42_g42 = clamp( 0.0 , 0.0001 , ( totalFrames39_g42 - 1.0 ) );
				float temp_output_35_0_g42 = frac( ( ( Current_Frame47_g41 + clampResult42_g42 ) / totalFrames39_g42 ) );
				float2 appendResult29_g42 = (float2(temp_output_35_0_g42 , ( 1.0 - temp_output_35_0_g42 )));
				float2 temp_output_15_0_g42 = ( ( UVs38_g41 / appendResult7_g42 ) + ( floor( ( appendResult8_g42 * appendResult29_g42 ) ) / appendResult7_g42 ) );
				float2 temp_output_9_0_g41 = temp_output_15_0_g42;
				float4 tex2DNode16_g41 = tex2D( _MotionVectorTex, temp_output_9_0_g41 );
				float2 appendResult18_g41 = (float2(tex2DNode16_g41.r , tex2DNode16_g41.g));
				float2 temp_cast_0 = (1.0).xx;
				float Current_Frame_Frac57_g41 = frac( Current_Frame47_g41 );
				float temp_output_36_0_g41 = _MotionVectorStrenght;
				float temp_output_4_0_g43 = Flipbook_Columns41_g41;
				float temp_output_5_0_g43 = Flipbook_Rows44_g41;
				float2 appendResult7_g43 = (float2(temp_output_4_0_g43 , temp_output_5_0_g43));
				float totalFrames39_g43 = ( temp_output_4_0_g43 * temp_output_5_0_g43 );
				float2 appendResult8_g43 = (float2(totalFrames39_g43 , temp_output_5_0_g43));
				float clampResult42_g43 = clamp( 0.0 , 0.0001 , ( totalFrames39_g43 - 1.0 ) );
				float temp_output_35_0_g43 = frac( ( ( ( Current_Frame47_g41 + 1.0 ) + clampResult42_g43 ) / totalFrames39_g43 ) );
				float2 appendResult29_g43 = (float2(temp_output_35_0_g43 , ( 1.0 - temp_output_35_0_g43 )));
				float2 temp_output_15_0_g43 = ( ( UVs38_g41 / appendResult7_g43 ) + ( floor( ( appendResult8_g43 * appendResult29_g43 ) ) / appendResult7_g43 ) );
				float2 temp_output_3_0_g41 = temp_output_15_0_g43;
				float4 tex2DNode25_g41 = tex2D( _MotionVectorTex, temp_output_3_0_g41 );
				float2 appendResult28_g41 = (float2(tex2DNode25_g41.r , tex2DNode25_g41.g));
				float2 temp_cast_1 = (1.0).xx;
				float4 lerpResult7_g41 = lerp( tex2D( _MainTex, ( temp_output_9_0_g41 - ( ( ( ( appendResult18_g41 * 2.0 ) - temp_cast_0 ) * Current_Frame_Frac57_g41 ) * temp_output_36_0_g41 ) ) ) , tex2D( _MainTex, ( temp_output_3_0_g41 + ( temp_output_36_0_g41 * ( ( ( 2.0 * appendResult28_g41 ) - temp_cast_1 ) * ( 1.0 - Current_Frame_Frac57_g41 ) ) ) ) ) , Current_Frame_Frac57_g41);
				#ifdef _USEMOTIONVECTOR_ON
				float4 staticSwitch24 = lerpResult7_g41;
				#else
				float4 staticSwitch24 = tex2D( _MainTex, temp_output_15_0_g38 );
				#endif
				float4 break28 = staticSwitch24;
				float TX_G76 = break28.g;
				float TX_R75 = break28.r;
				float4 screenPos = IN.ase_texcoord1;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float screenDepth33 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm.xy ),_ZBufferParams);
				float distanceDepth33 = abs( ( screenDepth33 - LinearEyeDepth( ase_screenPosNorm.z,_ZBufferParams ) ) / ( _DepthFade ) );
				
				surfaceDescription.Alpha = ( ( ( temp_output_34_0 * ( ( VertexColor_A113 * TX_G76 ) + TX_R75 ) ) * saturate( distanceDepth33 ) ) * VertexColor_A113 );
				surfaceDescription.AlphaClipThreshold = 0.5;


				#if _ALPHATEST_ON
					float alphaClipThreshold = 0.01f;
					#if ALPHA_CLIP_THRESHOLD
						alphaClipThreshold = surfaceDescription.AlphaClipThreshold;
					#endif
					clip(surfaceDescription.Alpha - alphaClipThreshold);
				#endif

				half4 outColor = 0;
				outColor = _SelectionID;
				
				return outColor;
			}
        
			ENDHLSL
        }
		
		
        Pass
        {
			
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }

			ZTest LEqual
			ZWrite On

        
			HLSLPROGRAM
			
			#pragma multi_compile_instancing
			#define _RECEIVE_SHADOWS_OFF 1
			#define ASE_ABSOLUTE_VERTEX_POS 1
			#define ASE_SRP_VERSION 999999
			#define REQUIRE_DEPTH_TEXTURE 1

			
			#pragma only_renderers d3d11 glcore gles gles3 
			#pragma multi_compile_fog
			#pragma instancing_options renderinglayer
			#pragma vertex vert
			#pragma fragment frag

        
			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define VARYINGS_NEED_NORMAL_WS

			#define SHADERPASS SHADERPASS_DEPTHNORMALSONLY

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#pragma shader_feature_local _USEMOTIONVECTOR_ON
			#define REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float3 normalWS : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_color : COLOR;
				float4 ase_texcoord2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
        
			CBUFFER_START(UnityPerMaterial)
			float4 _InsideColor;
			float2 _WPOPannerSpeed;
			float _WPOPower;
			float _Flipbook_Columns;
			float _Flipbook_Rows;
			float _MotionVectorStrenght;
			float _DepthFade;
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END
			sampler2D _WPOTexture;
			sampler2D _MainTex;
			sampler2D _MotionVectorTex;
			#ifndef UNITY_DECLARE_DEPTH_TEXTURE_INCLUDED // URP 14+ declares it already
			uniform float4 _CameraDepthTexture_TexelSize;
			#endif


			      
			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};
        
			VertexOutput VertexFunction(VertexInput v  )
			{
				VertexOutput o;
				ZERO_INITIALIZE(VertexOutput, o);

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float2 texCoord22 = v.ase_texcoord * float2( 1,1 ) + float2( 0,0 );
				float2 panner27 = ( 1.0 * _Time.y * _WPOPannerSpeed + texCoord22);
				
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord2 = screenPos;
				
				o.ase_texcoord1 = v.ase_texcoord;
				o.ase_color = v.ase_color;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = ( v.vertex.xyz + ( v.ase_normal * ( (tex2Dlod( _WPOTexture, float4( panner27, 0, 0.0) )).rgb * _WPOPower ) ) );
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float3 normalWS = TransformObjectToWorldNormal(v.ase_normal);

				o.clipPos = TransformWorldToHClip(positionWS);
				o.normalWS.xyz =  normalWS;

				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_texcoord = v.ase_texcoord;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag(VertexOutput IN ) : SV_TARGET
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;
				float2 texCoord30 = IN.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_34_0 = ( 1.0 - texCoord30.y );
				float VertexColor_A113 = IN.ase_color.a;
				float2 texCoord12 = IN.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float2 Input_UVs84 = texCoord12;
				float FB_Columns78 = _Flipbook_Columns;
				float temp_output_4_0_g38 = FB_Columns78;
				float FB_Rows79 = _Flipbook_Rows;
				float temp_output_5_0_g38 = FB_Rows79;
				float2 appendResult7_g38 = (float2(temp_output_4_0_g38 , temp_output_5_0_g38));
				float totalFrames39_g38 = ( temp_output_4_0_g38 * temp_output_5_0_g38 );
				float2 appendResult8_g38 = (float2(totalFrames39_g38 , temp_output_5_0_g38));
				float CVS_X87 = IN.ase_texcoord1.z;
				float clampResult42_g38 = clamp( 0.0 , 0.0001 , ( totalFrames39_g38 - 1.0 ) );
				float temp_output_35_0_g38 = frac( ( ( CVS_X87 + clampResult42_g38 ) / totalFrames39_g38 ) );
				float2 appendResult29_g38 = (float2(temp_output_35_0_g38 , ( 1.0 - temp_output_35_0_g38 )));
				float2 temp_output_15_0_g38 = ( ( Input_UVs84 / appendResult7_g38 ) + ( floor( ( appendResult8_g38 * appendResult29_g38 ) ) / appendResult7_g38 ) );
				float2 UVs38_g41 = Input_UVs84;
				float Flipbook_Columns41_g41 = FB_Columns78;
				float temp_output_4_0_g42 = Flipbook_Columns41_g41;
				float Flipbook_Rows44_g41 = FB_Rows79;
				float temp_output_5_0_g42 = Flipbook_Rows44_g41;
				float2 appendResult7_g42 = (float2(temp_output_4_0_g42 , temp_output_5_0_g42));
				float totalFrames39_g42 = ( temp_output_4_0_g42 * temp_output_5_0_g42 );
				float2 appendResult8_g42 = (float2(totalFrames39_g42 , temp_output_5_0_g42));
				float Current_Frame47_g41 = CVS_X87;
				float clampResult42_g42 = clamp( 0.0 , 0.0001 , ( totalFrames39_g42 - 1.0 ) );
				float temp_output_35_0_g42 = frac( ( ( Current_Frame47_g41 + clampResult42_g42 ) / totalFrames39_g42 ) );
				float2 appendResult29_g42 = (float2(temp_output_35_0_g42 , ( 1.0 - temp_output_35_0_g42 )));
				float2 temp_output_15_0_g42 = ( ( UVs38_g41 / appendResult7_g42 ) + ( floor( ( appendResult8_g42 * appendResult29_g42 ) ) / appendResult7_g42 ) );
				float2 temp_output_9_0_g41 = temp_output_15_0_g42;
				float4 tex2DNode16_g41 = tex2D( _MotionVectorTex, temp_output_9_0_g41 );
				float2 appendResult18_g41 = (float2(tex2DNode16_g41.r , tex2DNode16_g41.g));
				float2 temp_cast_0 = (1.0).xx;
				float Current_Frame_Frac57_g41 = frac( Current_Frame47_g41 );
				float temp_output_36_0_g41 = _MotionVectorStrenght;
				float temp_output_4_0_g43 = Flipbook_Columns41_g41;
				float temp_output_5_0_g43 = Flipbook_Rows44_g41;
				float2 appendResult7_g43 = (float2(temp_output_4_0_g43 , temp_output_5_0_g43));
				float totalFrames39_g43 = ( temp_output_4_0_g43 * temp_output_5_0_g43 );
				float2 appendResult8_g43 = (float2(totalFrames39_g43 , temp_output_5_0_g43));
				float clampResult42_g43 = clamp( 0.0 , 0.0001 , ( totalFrames39_g43 - 1.0 ) );
				float temp_output_35_0_g43 = frac( ( ( ( Current_Frame47_g41 + 1.0 ) + clampResult42_g43 ) / totalFrames39_g43 ) );
				float2 appendResult29_g43 = (float2(temp_output_35_0_g43 , ( 1.0 - temp_output_35_0_g43 )));
				float2 temp_output_15_0_g43 = ( ( UVs38_g41 / appendResult7_g43 ) + ( floor( ( appendResult8_g43 * appendResult29_g43 ) ) / appendResult7_g43 ) );
				float2 temp_output_3_0_g41 = temp_output_15_0_g43;
				float4 tex2DNode25_g41 = tex2D( _MotionVectorTex, temp_output_3_0_g41 );
				float2 appendResult28_g41 = (float2(tex2DNode25_g41.r , tex2DNode25_g41.g));
				float2 temp_cast_1 = (1.0).xx;
				float4 lerpResult7_g41 = lerp( tex2D( _MainTex, ( temp_output_9_0_g41 - ( ( ( ( appendResult18_g41 * 2.0 ) - temp_cast_0 ) * Current_Frame_Frac57_g41 ) * temp_output_36_0_g41 ) ) ) , tex2D( _MainTex, ( temp_output_3_0_g41 + ( temp_output_36_0_g41 * ( ( ( 2.0 * appendResult28_g41 ) - temp_cast_1 ) * ( 1.0 - Current_Frame_Frac57_g41 ) ) ) ) ) , Current_Frame_Frac57_g41);
				#ifdef _USEMOTIONVECTOR_ON
				float4 staticSwitch24 = lerpResult7_g41;
				#else
				float4 staticSwitch24 = tex2D( _MainTex, temp_output_15_0_g38 );
				#endif
				float4 break28 = staticSwitch24;
				float TX_G76 = break28.g;
				float TX_R75 = break28.r;
				float4 screenPos = IN.ase_texcoord2;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float screenDepth33 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm.xy ),_ZBufferParams);
				float distanceDepth33 = abs( ( screenDepth33 - LinearEyeDepth( ase_screenPosNorm.z,_ZBufferParams ) ) / ( _DepthFade ) );
				
				surfaceDescription.Alpha = ( ( ( temp_output_34_0 * ( ( VertexColor_A113 * TX_G76 ) + TX_R75 ) ) * saturate( distanceDepth33 ) ) * VertexColor_A113 );
				surfaceDescription.AlphaClipThreshold = 0.5;

				#if _ALPHATEST_ON
					clip(surfaceDescription.Alpha - surfaceDescription.AlphaClipThreshold);
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif

				float3 normalWS = IN.normalWS;
				return half4(NormalizeNormalPerPixel(normalWS), 0.0);

			}
        
			ENDHLSL
        }

		
        Pass
        {
			
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
        
			ZTest LEqual
			ZWrite On
        
        
			HLSLPROGRAM
        
			#pragma multi_compile_instancing
			#define _RECEIVE_SHADOWS_OFF 1
			#define ASE_ABSOLUTE_VERTEX_POS 1
			#define ASE_SRP_VERSION 999999
			#define REQUIRE_DEPTH_TEXTURE 1

        
			#pragma exclude_renderers glcore gles gles3 
			#pragma vertex vert
			#pragma fragment frag
        
			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define ATTRIBUTES_NEED_TEXCOORD1
			#define VARYINGS_NEED_NORMAL_WS
			#define VARYINGS_NEED_TANGENT_WS
        
			#define SHADERPASS SHADERPASS_DEPTHNORMALSONLY
        
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#pragma shader_feature_local _USEMOTIONVECTOR_ON
			#define REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float3 normalWS : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_color : COLOR;
				float4 ase_texcoord2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
        
			CBUFFER_START(UnityPerMaterial)
			float4 _InsideColor;
			float2 _WPOPannerSpeed;
			float _WPOPower;
			float _Flipbook_Columns;
			float _Flipbook_Rows;
			float _MotionVectorStrenght;
			float _DepthFade;
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END
			sampler2D _WPOTexture;
			sampler2D _MainTex;
			sampler2D _MotionVectorTex;
			#ifndef UNITY_DECLARE_DEPTH_TEXTURE_INCLUDED // URP 14+ declares it already
			uniform float4 _CameraDepthTexture_TexelSize;
			#endif


			
			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};
      
			VertexOutput VertexFunction(VertexInput v  )
			{
				VertexOutput o;
				ZERO_INITIALIZE(VertexOutput, o);

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float2 texCoord22 = v.ase_texcoord * float2( 1,1 ) + float2( 0,0 );
				float2 panner27 = ( 1.0 * _Time.y * _WPOPannerSpeed + texCoord22);
				
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord2 = screenPos;
				
				o.ase_texcoord1 = v.ase_texcoord;
				o.ase_color = v.ase_color;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = ( v.vertex.xyz + ( v.ase_normal * ( (tex2Dlod( _WPOTexture, float4( panner27, 0, 0.0) )).rgb * _WPOPower ) ) );
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float3 normalWS = TransformObjectToWorldNormal(v.ase_normal);

				o.clipPos = TransformWorldToHClip(positionWS);
				o.normalWS.xyz =  normalWS;
				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_texcoord = v.ase_texcoord;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag(VertexOutput IN ) : SV_TARGET
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;
				float2 texCoord30 = IN.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_34_0 = ( 1.0 - texCoord30.y );
				float VertexColor_A113 = IN.ase_color.a;
				float2 texCoord12 = IN.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float2 Input_UVs84 = texCoord12;
				float FB_Columns78 = _Flipbook_Columns;
				float temp_output_4_0_g38 = FB_Columns78;
				float FB_Rows79 = _Flipbook_Rows;
				float temp_output_5_0_g38 = FB_Rows79;
				float2 appendResult7_g38 = (float2(temp_output_4_0_g38 , temp_output_5_0_g38));
				float totalFrames39_g38 = ( temp_output_4_0_g38 * temp_output_5_0_g38 );
				float2 appendResult8_g38 = (float2(totalFrames39_g38 , temp_output_5_0_g38));
				float CVS_X87 = IN.ase_texcoord1.z;
				float clampResult42_g38 = clamp( 0.0 , 0.0001 , ( totalFrames39_g38 - 1.0 ) );
				float temp_output_35_0_g38 = frac( ( ( CVS_X87 + clampResult42_g38 ) / totalFrames39_g38 ) );
				float2 appendResult29_g38 = (float2(temp_output_35_0_g38 , ( 1.0 - temp_output_35_0_g38 )));
				float2 temp_output_15_0_g38 = ( ( Input_UVs84 / appendResult7_g38 ) + ( floor( ( appendResult8_g38 * appendResult29_g38 ) ) / appendResult7_g38 ) );
				float2 UVs38_g41 = Input_UVs84;
				float Flipbook_Columns41_g41 = FB_Columns78;
				float temp_output_4_0_g42 = Flipbook_Columns41_g41;
				float Flipbook_Rows44_g41 = FB_Rows79;
				float temp_output_5_0_g42 = Flipbook_Rows44_g41;
				float2 appendResult7_g42 = (float2(temp_output_4_0_g42 , temp_output_5_0_g42));
				float totalFrames39_g42 = ( temp_output_4_0_g42 * temp_output_5_0_g42 );
				float2 appendResult8_g42 = (float2(totalFrames39_g42 , temp_output_5_0_g42));
				float Current_Frame47_g41 = CVS_X87;
				float clampResult42_g42 = clamp( 0.0 , 0.0001 , ( totalFrames39_g42 - 1.0 ) );
				float temp_output_35_0_g42 = frac( ( ( Current_Frame47_g41 + clampResult42_g42 ) / totalFrames39_g42 ) );
				float2 appendResult29_g42 = (float2(temp_output_35_0_g42 , ( 1.0 - temp_output_35_0_g42 )));
				float2 temp_output_15_0_g42 = ( ( UVs38_g41 / appendResult7_g42 ) + ( floor( ( appendResult8_g42 * appendResult29_g42 ) ) / appendResult7_g42 ) );
				float2 temp_output_9_0_g41 = temp_output_15_0_g42;
				float4 tex2DNode16_g41 = tex2D( _MotionVectorTex, temp_output_9_0_g41 );
				float2 appendResult18_g41 = (float2(tex2DNode16_g41.r , tex2DNode16_g41.g));
				float2 temp_cast_0 = (1.0).xx;
				float Current_Frame_Frac57_g41 = frac( Current_Frame47_g41 );
				float temp_output_36_0_g41 = _MotionVectorStrenght;
				float temp_output_4_0_g43 = Flipbook_Columns41_g41;
				float temp_output_5_0_g43 = Flipbook_Rows44_g41;
				float2 appendResult7_g43 = (float2(temp_output_4_0_g43 , temp_output_5_0_g43));
				float totalFrames39_g43 = ( temp_output_4_0_g43 * temp_output_5_0_g43 );
				float2 appendResult8_g43 = (float2(totalFrames39_g43 , temp_output_5_0_g43));
				float clampResult42_g43 = clamp( 0.0 , 0.0001 , ( totalFrames39_g43 - 1.0 ) );
				float temp_output_35_0_g43 = frac( ( ( ( Current_Frame47_g41 + 1.0 ) + clampResult42_g43 ) / totalFrames39_g43 ) );
				float2 appendResult29_g43 = (float2(temp_output_35_0_g43 , ( 1.0 - temp_output_35_0_g43 )));
				float2 temp_output_15_0_g43 = ( ( UVs38_g41 / appendResult7_g43 ) + ( floor( ( appendResult8_g43 * appendResult29_g43 ) ) / appendResult7_g43 ) );
				float2 temp_output_3_0_g41 = temp_output_15_0_g43;
				float4 tex2DNode25_g41 = tex2D( _MotionVectorTex, temp_output_3_0_g41 );
				float2 appendResult28_g41 = (float2(tex2DNode25_g41.r , tex2DNode25_g41.g));
				float2 temp_cast_1 = (1.0).xx;
				float4 lerpResult7_g41 = lerp( tex2D( _MainTex, ( temp_output_9_0_g41 - ( ( ( ( appendResult18_g41 * 2.0 ) - temp_cast_0 ) * Current_Frame_Frac57_g41 ) * temp_output_36_0_g41 ) ) ) , tex2D( _MainTex, ( temp_output_3_0_g41 + ( temp_output_36_0_g41 * ( ( ( 2.0 * appendResult28_g41 ) - temp_cast_1 ) * ( 1.0 - Current_Frame_Frac57_g41 ) ) ) ) ) , Current_Frame_Frac57_g41);
				#ifdef _USEMOTIONVECTOR_ON
				float4 staticSwitch24 = lerpResult7_g41;
				#else
				float4 staticSwitch24 = tex2D( _MainTex, temp_output_15_0_g38 );
				#endif
				float4 break28 = staticSwitch24;
				float TX_G76 = break28.g;
				float TX_R75 = break28.r;
				float4 screenPos = IN.ase_texcoord2;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float screenDepth33 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm.xy ),_ZBufferParams);
				float distanceDepth33 = abs( ( screenDepth33 - LinearEyeDepth( ase_screenPosNorm.z,_ZBufferParams ) ) / ( _DepthFade ) );
				
				surfaceDescription.Alpha = ( ( ( temp_output_34_0 * ( ( VertexColor_A113 * TX_G76 ) + TX_R75 ) ) * saturate( distanceDepth33 ) ) * VertexColor_A113 );
				surfaceDescription.AlphaClipThreshold = 0.5;
				
				#if _ALPHATEST_ON
					clip(surfaceDescription.Alpha - surfaceDescription.AlphaClipThreshold);
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif

				float3 normalWS = IN.normalWS;
				return half4(NormalizeNormalPerPixel(normalWS), 0.0);

			}

			ENDHLSL
        }
		
	}
	
	CustomEditor "UnityEditor.ShaderGraphUnlitGUI"
	Fallback "Hidden/InternalErrorShader"
	
}
/*ASEBEGIN
Version=18935
226;629;1728;740;2328.249;-267.3196;1;True;False
Node;AmplifyShaderEditor.CommentaryNode;107;-5749.114,-654.1148;Inherit;False;666.9292;1553.192;Local Variables;19;113;110;111;112;90;89;88;87;71;84;12;79;74;78;73;94;93;17;14;;0.2745098,0.2745098,0.2745098,1;0;0
Node;AmplifyShaderEditor.FunctionNode;71;-5612.758,362.351;Inherit;False;SF_CustomVertexStreams;-1;;37;c056d1337072ded4986ee0ad6d782252;0;0;5;FLOAT4;0;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10
Node;AmplifyShaderEditor.TextureCoordinatesNode;12;-5573.004,9.249569;Inherit;True;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;73;-5554.836,-170.8862;Inherit;False;Property;_Flipbook_Columns;Flipbook_Columns;1;0;Create;True;0;0;0;False;0;False;0;6;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;74;-5532.596,-93.11322;Inherit;False;Property;_Flipbook_Rows;Flipbook_Rows;2;0;Create;True;0;0;0;False;0;False;0;6;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;84;-5345.31,3.833797;Inherit;False;Input_UVs;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TexturePropertyNode;14;-5649.428,-604.1144;Inherit;True;Property;_MainTex;MainTex;0;1;[NoScaleOffset];Create;True;0;0;0;False;0;False;None;d19834d4d1a00f24b8e06a816f8acff7;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.RegisterLocalVarNode;87;-5316.205,303.4381;Inherit;False;CVS_X;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexturePropertyNode;17;-5648.284,-412.8957;Inherit;True;Property;_MotionVectorTex;MotionVectorTex;8;1;[NoScaleOffset];Create;True;0;0;0;False;0;False;None;e5b3cc141e60a0f4896dda20c2239bad;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.RegisterLocalVarNode;79;-5345.09,-92.98822;Inherit;False;FB_Rows;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;78;-5345.924,-171.2087;Inherit;False;FB_Columns;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;10;-5052.92,-657.9885;Inherit;False;1813.204;932.5475;Motion Vector For Flipbooks;20;77;76;75;16;92;83;81;97;96;85;20;91;82;80;86;15;95;28;24;19;;0.2745098,0.2745098,0.2745098,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;94;-5335.585,-413.0183;Inherit;False;TX_MotionVector;-1;True;1;0;SAMPLER2D;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;93;-5340.396,-603.8354;Inherit;False;TX_MainFB;-1;True;1;0;SAMPLER2D;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.GetLocalVarNode;80;-4989.115,-513.3579;Inherit;False;78;FB_Columns;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;86;-4979.465,-586.8407;Inherit;False;84;Input_UVs;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;82;-4976.505,-438.4279;Inherit;False;79;FB_Rows;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;91;-4975.333,-364.122;Inherit;False;87;CVS_X;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;81;-4592.104,-74.2157;Inherit;False;78;FB_Columns;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;15;-4756.126,-530.5692;Inherit;False;Flipbook;-1;;38;53c2488c220f6564ca6c90721ee16673;2,71,0,68,0;8;51;SAMPLER2D;0.0;False;13;FLOAT2;0,0;False;4;FLOAT;3;False;5;FLOAT;3;False;24;FLOAT;0;False;2;FLOAT;0;False;55;FLOAT;0;False;70;FLOAT;0;False;5;COLOR;53;FLOAT2;0;FLOAT;47;FLOAT;48;FLOAT;62
Node;AmplifyShaderEditor.GetLocalVarNode;83;-4576.897,0.2422905;Inherit;False;79;FB_Rows;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;92;-4579.399,75.60431;Inherit;False;87;CVS_X;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;16;-4636.489,154.3779;Inherit;False;Property;_MotionVectorStrenght;MotionVectorStrenght;9;0;Create;True;0;0;0;False;0;False;0;0.001;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;96;-4587.419,-223.4236;Inherit;False;93;TX_MainFB;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.GetLocalVarNode;85;-4588.015,-298.4112;Inherit;False;84;Input_UVs;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;97;-4620.733,-148.7785;Inherit;False;94;TX_MotionVector;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.GetLocalVarNode;95;-4439.907,-611.7589;Inherit;False;93;TX_MainFB;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.FunctionNode;20;-4317.089,-196.4733;Inherit;False;SF_MotionVector;-1;;41;d3cf7c8c8954fa64eaabdfef7cbd0aff;0;7;31;FLOAT2;0,0;False;37;SAMPLER2D;0;False;35;SAMPLER2D;0;False;32;FLOAT;0;False;33;FLOAT;0;False;34;FLOAT;0;False;36;FLOAT;0.001;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;19;-4249.361,-538.4012;Inherit;True;Property;_TextureSample0;Texture Sample 0;11;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.StaticSwitch;24;-3917.165,-398.416;Inherit;False;Property;_UseMotionVector;UseMotionVector;7;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.CommentaryNode;18;-2367.893,594.4765;Inherit;False;2021.282;429.1768;WPO;11;32;50;43;44;39;38;35;36;27;22;23;;0.2745098,0.2745098,0.2745098,1;0;0
Node;AmplifyShaderEditor.VertexColorNode;110;-5689.253,668.7375;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.BreakToComponentsNode;28;-3624.813,-392.7492;Inherit;False;COLOR;1;0;COLOR;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.TextureCoordinatesNode;22;-2321.327,722.8298;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;23;-2304.222,844.1683;Inherit;False;Property;_WPOPannerSpeed;WPOPannerSpeed;6;0;Create;True;0;0;0;False;0;False;0.1,0;-0.01,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RegisterLocalVarNode;76;-3469.683,-371.3425;Inherit;False;TX_G;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;21;-2139.495,221.9663;Inherit;False;1792.165;357.7914;Alpha;12;52;116;42;40;41;33;31;105;37;29;103;115;;0.2745098,0.2745098,0.2745098,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;113;-5328.481,761.3618;Inherit;False;VertexColor_A;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;27;-2052.242,826.3704;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;75;-3468.865,-447.9943;Inherit;False;TX_R;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;25;-3198.696,-652.3381;Inherit;False;2856.054;849.0214;Color;28;34;30;66;45;62;109;106;55;54;108;51;59;49;57;53;46;64;65;56;69;67;99;63;58;68;101;48;114;;0.2745098,0.2745098,0.2745098,1;0;0
Node;AmplifyShaderEditor.GetLocalVarNode;103;-2078.75,436.7289;Inherit;False;76;TX_G;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;115;-2103.561,354.7484;Inherit;False;113;VertexColor_A;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;32;-1833.702,797.7256;Inherit;True;Property;_WPOTexture;WPOTexture;4;1;[NoScaleOffset];Create;True;0;0;0;False;0;False;-1;None;410acd222eb2a164eb344505ebb4baaa;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;29;-1882.348,358.9068;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;31;-1429.027,490.6101;Inherit;False;Property;_DepthFade;DepthFade;10;0;Create;True;0;0;0;False;0;False;0;300;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;105;-1752.263,445.4728;Inherit;False;75;TX_R;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;30;-2378.483,-121.1355;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DepthFade;33;-1240.426,471.2102;Inherit;False;True;False;True;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;35;-1526.903,797.1317;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.OneMinusNode;34;-2149.662,-73.98904;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;36;-1489.666,916.2324;Inherit;False;Property;_WPOPower;WPOPower;5;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;37;-1579.933,359.9664;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;40;-1400.363,334.3434;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;41;-994.5486,466.0659;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;38;-1303.327,801.8297;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.NormalVertexDataNode;39;-1151.593,666.6761;Inherit;False;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;116;-704.5614,432.7484;Inherit;False;113;VertexColor_A;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;42;-846.2494,336.5553;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;43;-948.3722,780.7241;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.PosVertexDataNode;44;-788.7078,653.9948;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;67;-2700.647,-271.8341;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;108;-1236.181,-375.4429;Inherit;False;89;CVS_Z;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;77;-3470.796,-291.9962;Inherit;False;TX_B;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;111;-5525.482,663.3618;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;68;-2667.399,-43.90166;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;65;-2229.646,-358.8339;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;69;-2523.647,-271.8341;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;51;-752.6375,-337.1152;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;88;-5316.436,381.5508;Inherit;False;CVS_Y;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;112;-5326.481,663.3618;Inherit;False;VertexColor_RGB;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;57;-1820.646,-403.834;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ColorNode;55;-1510.467,-543.3001;Inherit;False;Constant;_Color0;Color 0;4;0;Create;True;0;0;0;False;0;False;0.5,0,1,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;62;-498.8217,-359.9316;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;66;-1914.726,-85.2899;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;64;-2377.647,-397.8339;Inherit;False;Constant;_Float0;Float 0;5;0;Create;True;0;0;0;False;0;False;0.6;0.6;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;59;-1049.464,-541.9753;Inherit;True;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;53;-1639.646,-317.834;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;50;-571.6699,755.5195;Inherit;True;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ComponentMaskNode;63;-2914.647,-251.834;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;109;-695.2811,-442.1421;Inherit;False;88;CVS_Y;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;56;-2077.646,-294.8341;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;114;-2891.332,-49.27954;Inherit;False;112;VertexColor_RGB;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;106;-962.217,-239.133;Inherit;False;76;TX_G;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;49;-1984.363,-436.8339;Inherit;False;Constant;_Float3;Float 3;5;0;Create;True;0;0;0;False;0;False;0.5;0.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;58;-3131.648,-252.834;Inherit;False;Property;_InsideColor;InsideColor;3;1;[HDR];Create;True;0;0;0;False;0;False;0,0.8624213,1,0;0.5135358,1.720795,0.5315546,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;99;-2886.604,-330.5273;Inherit;False;77;TX_B;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;101;-3001.604,71.47242;Inherit;False;77;TX_B;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;89;-5316.851,459.2535;Inherit;False;CVS_Z;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;52;-506.3298,337.8205;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;45;-1766.479,-173.1376;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FaceVariableNode;46;-2225.2,-211.0079;Inherit;False;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;90;-5316.184,536.8512;Inherit;False;CVS_W;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;48;-2831.939,75.64605;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;54;-1277.966,-543.3002;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;5;0,0;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;3;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;Universal2D;0;5;Universal2D;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;False;True;2;5;False;-1;10;False;-1;0;1;False;-1;10;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;-1;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;True;2;False;-1;True;3;False;-1;True;True;0;False;-1;0;False;-1;True;1;LightMode=Universal2D;False;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;8;0,0;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;3;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;DepthNormals;0;8;DepthNormals;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;False;-1;True;3;False;-1;False;True;1;LightMode=DepthNormalsOnly;False;True;4;d3d11;glcore;gles;gles3;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;6;0,0;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;3;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;SceneSelectionPass;0;6;SceneSelectionPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=SceneSelectionPass;False;True;4;d3d11;glcore;gles;gles3;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;4;0,0;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;3;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;Meta;0;4;Meta;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Meta;False;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;2;0,0;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;3;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;ShadowCaster;0;2;ShadowCaster;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;False;False;True;False;False;False;False;0;False;-1;False;False;False;False;False;False;False;False;False;True;1;False;-1;True;3;False;-1;False;True;1;LightMode=ShadowCaster;False;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;3;0,0;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;3;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;DepthOnly;0;3;DepthOnly;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;False;False;True;False;False;False;False;0;False;-1;False;False;False;False;False;False;False;False;False;True;1;False;-1;False;False;True;1;LightMode=DepthOnly;False;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;9;0,0;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;3;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;DepthNormalsOnly;0;9;DepthNormalsOnly;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;False;-1;True;3;False;-1;False;True;1;LightMode=DepthNormalsOnly;False;True;15;d3d9;d3d11_9x;d3d11;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;0;0,0;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;3;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;ExtraPrePass;0;0;ExtraPrePass;5;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;False;True;1;1;False;-1;0;False;-1;0;1;False;-1;0;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;True;True;True;True;0;False;-1;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;True;1;False;-1;True;3;False;-1;True;True;0;False;-1;0;False;-1;True;0;False;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;1;0,0;Float;False;True;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;13;SH_Healing_Flames;2992e84f91cbeb14eab234972e07ea9d;True;Forward;0;1;Forward;8;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;2;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;True;True;2;5;False;-1;10;False;-1;0;1;False;-1;10;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;-1;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;True;2;False;-1;True;3;False;-1;True;True;0;False;-1;0;False;-1;True;1;LightMode=UniversalForward;False;False;2;Include;;False;;Native;Define;REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR;False;;Custom;Hidden/InternalErrorShader;0;0;Standard;22;Surface;1;637917750230498746;  Blend;0;0;Two Sided;0;637917750265113725;Cast Shadows;0;637917750271295096;  Use Shadow Threshold;0;0;Receive Shadows;0;637917750278527525;GPU Instancing;1;0;LOD CrossFade;0;0;Built-in Fog;0;0;DOTS Instancing;0;0;Meta Pass;0;0;Extra Pre Pass;0;0;Tessellation;0;0;  Phong;0;0;  Strength;0.5,False,-1;0;  Type;0;0;  Tess;16,False,-1;0;  Min;10,False,-1;0;  Max;25,False,-1;0;  Edge Length;16,False,-1;0;  Max Displacement;25,False,-1;0;Vertex Position,InvertActionOnDeselection;0;637934924214141437;0;10;False;True;False;True;False;True;True;True;True;True;False;;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;7;0,0;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;3;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;ScenePickingPass;0;7;ScenePickingPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Picking;False;True;4;d3d11;glcore;gles;gles3;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
WireConnection;84;0;12;0
WireConnection;87;0;71;7
WireConnection;79;0;74;0
WireConnection;78;0;73;0
WireConnection;94;0;17;0
WireConnection;93;0;14;0
WireConnection;15;13;86;0
WireConnection;15;4;80;0
WireConnection;15;5;82;0
WireConnection;15;2;91;0
WireConnection;20;31;85;0
WireConnection;20;37;96;0
WireConnection;20;35;97;0
WireConnection;20;32;81;0
WireConnection;20;33;83;0
WireConnection;20;34;92;0
WireConnection;20;36;16;0
WireConnection;19;0;95;0
WireConnection;19;1;15;0
WireConnection;24;1;19;0
WireConnection;24;0;20;0
WireConnection;28;0;24;0
WireConnection;76;0;28;1
WireConnection;113;0;110;4
WireConnection;27;0;22;0
WireConnection;27;2;23;0
WireConnection;75;0;28;0
WireConnection;32;1;27;0
WireConnection;29;0;115;0
WireConnection;29;1;103;0
WireConnection;33;0;31;0
WireConnection;35;0;32;0
WireConnection;34;0;30;2
WireConnection;37;0;29;0
WireConnection;37;1;105;0
WireConnection;40;0;34;0
WireConnection;40;1;37;0
WireConnection;41;0;33;0
WireConnection;38;0;35;0
WireConnection;38;1;36;0
WireConnection;42;0;40;0
WireConnection;42;1;41;0
WireConnection;43;0;39;0
WireConnection;43;1;38;0
WireConnection;67;0;99;0
WireConnection;67;1;63;0
WireConnection;77;0;28;2
WireConnection;111;0;110;0
WireConnection;68;0;114;0
WireConnection;68;1;48;0
WireConnection;65;0;64;0
WireConnection;65;1;69;0
WireConnection;69;0;67;0
WireConnection;69;1;68;0
WireConnection;51;0;59;0
WireConnection;51;1;53;0
WireConnection;51;2;106;0
WireConnection;88;0;71;8
WireConnection;112;0;111;0
WireConnection;57;0;49;0
WireConnection;57;1;56;0
WireConnection;62;0;109;0
WireConnection;62;1;51;0
WireConnection;66;0;34;0
WireConnection;66;1;34;0
WireConnection;59;0;54;0
WireConnection;59;1;53;0
WireConnection;59;2;108;0
WireConnection;53;0;57;0
WireConnection;53;1;56;0
WireConnection;53;2;45;0
WireConnection;50;0;44;0
WireConnection;50;1;43;0
WireConnection;63;0;58;0
WireConnection;56;0;65;0
WireConnection;56;1;69;0
WireConnection;56;2;46;0
WireConnection;89;0;71;9
WireConnection;52;0;42;0
WireConnection;52;1;116;0
WireConnection;45;0;66;0
WireConnection;90;0;71;10
WireConnection;48;0;101;0
WireConnection;54;0;55;0
WireConnection;1;2;62;0
WireConnection;1;3;52;0
WireConnection;1;5;50;0
ASEEND*/
//CHKSM=04093B5ACE9538740676494612081FE2C38E8D15