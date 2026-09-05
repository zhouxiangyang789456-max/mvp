// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Knife/Portal Alpha URP"
{
	Properties
	{
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		_Noise("Noise", 2D) = "white" {}
		_NoiseSpeed("Noise Speed", Vector) = (0,0,0,0)
		_DisplacementNoise("DisplacementNoise", 2D) = "white" {}
		_MaskRemap("Mask Remap", Vector) = (0,0,0,0)
		_DisplacementNoiseSpeed("DisplacementNoiseSpeed", Vector) = (0,0,0,0)
		_GradientMaskRemap("Gradient Mask Remap", Vector) = (0,0,0,0)
		_DisplacementNoiseRemap("DisplacementNoiseRemap", Vector) = (0,0,0,0)
		_GlowRemap("Glow Remap", Vector) = (0,0,0,0)
		[HDR]_Color("Color", Color) = (1,1,1,1)
		[HDR]_BackGradientColor("Back Gradient Color", Color) = (1,1,1,1)
		_NoiseRemap("Noise Remap", Vector) = (0,0,0,0)
		_GlowIntensity("Glow Intensity", Float) = 0
		_GlowPower("Glow Power", Float) = 0
		_SoftIntersectionDistance("Soft Intersection Distance", Float) = 0
		_HueOffset("HueOffset", Float) = 0
		_Intensity("Intensity", Float) = 0

	}

	SubShader
	{
		LOD 0

		
		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
		
		Cull Back
		HLSLINCLUDE
		#pragma target 2.0
		ENDHLSL

		
		Pass
		{
			
			Name "Forward"
			Tags { "LightMode"="UniversalForward" }
			
			Blend SrcAlpha OneMinusSrcAlpha , One OneMinusSrcAlpha
			ZWrite Off
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA
			

			HLSLPROGRAM
			#pragma multi_compile_instancing
			#define ASE_SRP_VERSION 70108
			#define REQUIRE_DEPTH_TEXTURE 1

			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x

			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"

			#if ASE_SRP_VERSION <= 70108
			#define REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
			#endif

			#define ASE_NEEDS_VERT_NORMAL


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
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
				float4 ase_texcoord4 : TEXCOORD4;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			sampler2D _DisplacementNoise;
			sampler2D _Noise;
			uniform float4 _CameraDepthTexture_TexelSize;
			CBUFFER_START( UnityPerMaterial )
			float2 _DisplacementNoiseSpeed;
			float4 _Noise_ST;
			float4 _DisplacementNoiseRemap;
			float _HueOffset;
			float4 _Color;
			float _Intensity;
			float4 _GlowRemap;
			float _GlowPower;
			float _GlowIntensity;
			float4 _BackGradientColor;
			float4 _GradientMaskRemap;
			float2 _NoiseSpeed;
			float4 _NoiseRemap;
			float4 _MaskRemap;
			float _SoftIntersectionDistance;
			CBUFFER_END


			float3 HSVToRGB( float3 c )
			{
				float4 K = float4( 1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0 );
				float3 p = abs( frac( c.xxx + K.xyz ) * 6.0 - K.www );
				return c.z * lerp( K.xxx, saturate( p - K.xxx ), c.y );
			}
			
			float3 RGBToHSV(float3 c)
			{
				float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
				float4 p = lerp( float4( c.bg, K.wz ), float4( c.gb, K.xy ), step( c.b, c.g ) );
				float4 q = lerp( float4( p.xyw, c.r ), float4( c.r, p.yzx ), step( p.x, c.r ) );
				float d = q.x - min( q.w, q.y );
				float e = 1.0e-10;
				return float3( abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
			}

			VertexOutput vert ( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float2 uv0_Noise = v.ase_texcoord.xy * _Noise_ST.xy + _Noise_ST.zw;
				float2 panner199 = ( 1.0 * _Time.y * _DisplacementNoiseSpeed + uv0_Noise);
				
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord4 = screenPos;
				
				o.ase_texcoord3.xy = v.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord3.zw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = ( (_DisplacementNoiseRemap.z + (tex2Dlod( _DisplacementNoise, float4( panner199, 0, 0.0) ).r - _DisplacementNoiseRemap.x) * (_DisplacementNoiseRemap.w - _DisplacementNoiseRemap.z) / (_DisplacementNoiseRemap.y - _DisplacementNoiseRemap.x)) * v.ase_normal );
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

			half4 frag ( VertexOutput IN  ) : SV_Target
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
				float hue_offset218 = _HueOffset;
				float3 hsvTorgb210 = RGBToHSV( _Color.rgb );
				float3 hsvTorgb209 = HSVToRGB( float3(frac( ( hue_offset218 + hsvTorgb210.x ) ),hsvTorgb210.y,hsvTorgb210.z) );
				float4 appendResult226 = (float4(( hsvTorgb209 * _Intensity ) , _Color.a));
				float4 main_color221 = appendResult226;
				float2 uv0168 = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult177 = clamp( (_GlowRemap.z + (uv0168.y - _GlowRemap.x) * (_GlowRemap.w - _GlowRemap.z) / (_GlowRemap.y - _GlowRemap.x)) , 0.0 , 1.0 );
				float3 hsvTorgb212 = RGBToHSV( _BackGradientColor.rgb );
				float3 hsvTorgb211 = HSVToRGB( float3(frac( ( hue_offset218 + hsvTorgb212.x ) ),hsvTorgb212.y,hsvTorgb212.z) );
				float4 appendResult227 = (float4(( hsvTorgb211 * _Intensity ) , _BackGradientColor.a));
				float4 back_color222 = appendResult227;
				float4 break187 = back_color222;
				float2 uv0179 = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float lerpResult189 = lerp( break187.w , 0.0 , saturate( (_GradientMaskRemap.z + (uv0179.y - _GradientMaskRemap.x) * (_GradientMaskRemap.w - _GradientMaskRemap.z) / (_GradientMaskRemap.y - _GradientMaskRemap.x)) ));
				float4 appendResult192 = (float4(break187.x , break187.y , break187.z , lerpResult189));
				float2 uv0_Noise = IN.ase_texcoord3.xy * _Noise_ST.xy + _Noise_ST.zw;
				float2 panner120 = ( 1.0 * _Time.y * _NoiseSpeed + uv0_Noise);
				float4 lerpResult158 = lerp( ( main_color221 + ( main_color221 * ( pow( clampResult177 , _GlowPower ) * _GlowIntensity ) ) ) , appendResult192 , ( 1.0 - saturate( (_NoiseRemap.z + (tex2D( _Noise, panner120 ).r - _NoiseRemap.x) * (_NoiseRemap.w - _NoiseRemap.z) / (_NoiseRemap.y - _NoiseRemap.x)) ) ));
				
				float2 uv0122 = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float4 screenPos = IN.ase_texcoord4;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float screenDepth206 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm.xy ),_ZBufferParams);
				float distanceDepth206 = saturate( abs( ( screenDepth206 - LinearEyeDepth( ase_screenPosNorm.z,_ZBufferParams ) ) / ( _SoftIntersectionDistance ) ) );
				
				float3 BakedAlbedo = 0;
				float3 BakedEmission = 0;
				float3 Color = lerpResult158.rgb;
				float Alpha = saturate( ( (lerpResult158).a * saturate( (_MaskRemap.z + (uv0122.y - _MaskRemap.x) * (_MaskRemap.w - _MaskRemap.z) / (_MaskRemap.y - _MaskRemap.x)) ) * distanceDepth206 ) );
				float AlphaClipThreshold = 0.5;

				#ifdef _ALPHATEST_ON
					clip( Alpha - AlphaClipThreshold );
				#endif

				#ifdef ASE_FOG
					Color = MixFog( Color, IN.fogFactor );
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif

				return half4( Color, Alpha );
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "ShadowCaster"
			Tags { "LightMode"="ShadowCaster" }

			ZWrite On
			ZTest LEqual

			HLSLPROGRAM
			#pragma multi_compile_instancing
			#define ASE_SRP_VERSION 70108
			#define REQUIRE_DEPTH_TEXTURE 1

			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x

			#pragma vertex ShadowPassVertex
			#pragma fragment ShadowPassFragment

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

			#define ASE_NEEDS_VERT_NORMAL


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
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
				float4 ase_texcoord3 : TEXCOORD3;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			sampler2D _DisplacementNoise;
			sampler2D _Noise;
			uniform float4 _CameraDepthTexture_TexelSize;
			CBUFFER_START( UnityPerMaterial )
			float2 _DisplacementNoiseSpeed;
			float4 _Noise_ST;
			float4 _DisplacementNoiseRemap;
			float _HueOffset;
			float4 _Color;
			float _Intensity;
			float4 _GlowRemap;
			float _GlowPower;
			float _GlowIntensity;
			float4 _BackGradientColor;
			float4 _GradientMaskRemap;
			float2 _NoiseSpeed;
			float4 _NoiseRemap;
			float4 _MaskRemap;
			float _SoftIntersectionDistance;
			CBUFFER_END


			float3 HSVToRGB( float3 c )
			{
				float4 K = float4( 1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0 );
				float3 p = abs( frac( c.xxx + K.xyz ) * 6.0 - K.www );
				return c.z * lerp( K.xxx, saturate( p - K.xxx ), c.y );
			}
			
			float3 RGBToHSV(float3 c)
			{
				float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
				float4 p = lerp( float4( c.bg, K.wz ), float4( c.gb, K.xy ), step( c.b, c.g ) );
				float4 q = lerp( float4( p.xyw, c.r ), float4( c.r, p.yzx ), step( p.x, c.r ) );
				float d = q.x - min( q.w, q.y );
				float e = 1.0e-10;
				return float3( abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
			}

			float3 _LightDirection;

			VertexOutput ShadowPassVertex( VertexInput v )
			{
				VertexOutput o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );

				float2 uv0_Noise = v.ase_texcoord.xy * _Noise_ST.xy + _Noise_ST.zw;
				float2 panner199 = ( 1.0 * _Time.y * _DisplacementNoiseSpeed + uv0_Noise);
				
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord3 = screenPos;
				
				o.ase_texcoord2.xy = v.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord2.zw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = ( (_DisplacementNoiseRemap.z + (tex2Dlod( _DisplacementNoise, float4( panner199, 0, 0.0) ).r - _DisplacementNoiseRemap.x) * (_DisplacementNoiseRemap.w - _DisplacementNoiseRemap.z) / (_DisplacementNoiseRemap.y - _DisplacementNoiseRemap.x)) * v.ase_normal );
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

				float3 normalWS = TransformObjectToWorldDir( v.ase_normal );

				float4 clipPos = TransformWorldToHClip( ApplyShadowBias( positionWS, normalWS, _LightDirection ) );

				#if UNITY_REVERSED_Z
					clipPos.z = min(clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE);
				#else
					clipPos.z = max(clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE);
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					VertexPositionInputs vertexInput = (VertexPositionInputs)0;
					vertexInput.positionWS = positionWS;
					vertexInput.positionCS = clipPos;
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				o.clipPos = clipPos;

				return o;
			}

			half4 ShadowPassFragment(VertexOutput IN  ) : SV_TARGET
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

				float hue_offset218 = _HueOffset;
				float3 hsvTorgb210 = RGBToHSV( _Color.rgb );
				float3 hsvTorgb209 = HSVToRGB( float3(frac( ( hue_offset218 + hsvTorgb210.x ) ),hsvTorgb210.y,hsvTorgb210.z) );
				float4 appendResult226 = (float4(( hsvTorgb209 * _Intensity ) , _Color.a));
				float4 main_color221 = appendResult226;
				float2 uv0168 = IN.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult177 = clamp( (_GlowRemap.z + (uv0168.y - _GlowRemap.x) * (_GlowRemap.w - _GlowRemap.z) / (_GlowRemap.y - _GlowRemap.x)) , 0.0 , 1.0 );
				float3 hsvTorgb212 = RGBToHSV( _BackGradientColor.rgb );
				float3 hsvTorgb211 = HSVToRGB( float3(frac( ( hue_offset218 + hsvTorgb212.x ) ),hsvTorgb212.y,hsvTorgb212.z) );
				float4 appendResult227 = (float4(( hsvTorgb211 * _Intensity ) , _BackGradientColor.a));
				float4 back_color222 = appendResult227;
				float4 break187 = back_color222;
				float2 uv0179 = IN.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float lerpResult189 = lerp( break187.w , 0.0 , saturate( (_GradientMaskRemap.z + (uv0179.y - _GradientMaskRemap.x) * (_GradientMaskRemap.w - _GradientMaskRemap.z) / (_GradientMaskRemap.y - _GradientMaskRemap.x)) ));
				float4 appendResult192 = (float4(break187.x , break187.y , break187.z , lerpResult189));
				float2 uv0_Noise = IN.ase_texcoord2.xy * _Noise_ST.xy + _Noise_ST.zw;
				float2 panner120 = ( 1.0 * _Time.y * _NoiseSpeed + uv0_Noise);
				float4 lerpResult158 = lerp( ( main_color221 + ( main_color221 * ( pow( clampResult177 , _GlowPower ) * _GlowIntensity ) ) ) , appendResult192 , ( 1.0 - saturate( (_NoiseRemap.z + (tex2D( _Noise, panner120 ).r - _NoiseRemap.x) * (_NoiseRemap.w - _NoiseRemap.z) / (_NoiseRemap.y - _NoiseRemap.x)) ) ));
				float2 uv0122 = IN.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float4 screenPos = IN.ase_texcoord3;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float screenDepth206 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm.xy ),_ZBufferParams);
				float distanceDepth206 = saturate( abs( ( screenDepth206 - LinearEyeDepth( ase_screenPosNorm.z,_ZBufferParams ) ) / ( _SoftIntersectionDistance ) ) );
				
				float Alpha = saturate( ( (lerpResult158).a * saturate( (_MaskRemap.z + (uv0122.y - _MaskRemap.x) * (_MaskRemap.w - _MaskRemap.z) / (_MaskRemap.y - _MaskRemap.x)) ) * distanceDepth206 ) );
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
			
			Name "DepthOnly"
			Tags { "LightMode"="DepthOnly" }

			ZWrite On
			ColorMask 0

			HLSLPROGRAM
			#pragma multi_compile_instancing
			#define ASE_SRP_VERSION 70108
			#define REQUIRE_DEPTH_TEXTURE 1

			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x

			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

			#define ASE_NEEDS_VERT_NORMAL


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
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
				float4 ase_texcoord3 : TEXCOORD3;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			sampler2D _DisplacementNoise;
			sampler2D _Noise;
			uniform float4 _CameraDepthTexture_TexelSize;
			CBUFFER_START( UnityPerMaterial )
			float2 _DisplacementNoiseSpeed;
			float4 _Noise_ST;
			float4 _DisplacementNoiseRemap;
			float _HueOffset;
			float4 _Color;
			float _Intensity;
			float4 _GlowRemap;
			float _GlowPower;
			float _GlowIntensity;
			float4 _BackGradientColor;
			float4 _GradientMaskRemap;
			float2 _NoiseSpeed;
			float4 _NoiseRemap;
			float4 _MaskRemap;
			float _SoftIntersectionDistance;
			CBUFFER_END


			float3 HSVToRGB( float3 c )
			{
				float4 K = float4( 1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0 );
				float3 p = abs( frac( c.xxx + K.xyz ) * 6.0 - K.www );
				return c.z * lerp( K.xxx, saturate( p - K.xxx ), c.y );
			}
			
			float3 RGBToHSV(float3 c)
			{
				float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
				float4 p = lerp( float4( c.bg, K.wz ), float4( c.gb, K.xy ), step( c.b, c.g ) );
				float4 q = lerp( float4( p.xyw, c.r ), float4( c.r, p.yzx ), step( p.x, c.r ) );
				float d = q.x - min( q.w, q.y );
				float e = 1.0e-10;
				return float3( abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
			}

			VertexOutput vert( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float2 uv0_Noise = v.ase_texcoord.xy * _Noise_ST.xy + _Noise_ST.zw;
				float2 panner199 = ( 1.0 * _Time.y * _DisplacementNoiseSpeed + uv0_Noise);
				
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord3 = screenPos;
				
				o.ase_texcoord2.xy = v.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord2.zw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = ( (_DisplacementNoiseRemap.z + (tex2Dlod( _DisplacementNoise, float4( panner199, 0, 0.0) ).r - _DisplacementNoiseRemap.x) * (_DisplacementNoiseRemap.w - _DisplacementNoiseRemap.z) / (_DisplacementNoiseRemap.y - _DisplacementNoiseRemap.x)) * v.ase_normal );
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
					vertexInput.positionCS = clipPos;
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				return o;
			}

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

				float hue_offset218 = _HueOffset;
				float3 hsvTorgb210 = RGBToHSV( _Color.rgb );
				float3 hsvTorgb209 = HSVToRGB( float3(frac( ( hue_offset218 + hsvTorgb210.x ) ),hsvTorgb210.y,hsvTorgb210.z) );
				float4 appendResult226 = (float4(( hsvTorgb209 * _Intensity ) , _Color.a));
				float4 main_color221 = appendResult226;
				float2 uv0168 = IN.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult177 = clamp( (_GlowRemap.z + (uv0168.y - _GlowRemap.x) * (_GlowRemap.w - _GlowRemap.z) / (_GlowRemap.y - _GlowRemap.x)) , 0.0 , 1.0 );
				float3 hsvTorgb212 = RGBToHSV( _BackGradientColor.rgb );
				float3 hsvTorgb211 = HSVToRGB( float3(frac( ( hue_offset218 + hsvTorgb212.x ) ),hsvTorgb212.y,hsvTorgb212.z) );
				float4 appendResult227 = (float4(( hsvTorgb211 * _Intensity ) , _BackGradientColor.a));
				float4 back_color222 = appendResult227;
				float4 break187 = back_color222;
				float2 uv0179 = IN.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float lerpResult189 = lerp( break187.w , 0.0 , saturate( (_GradientMaskRemap.z + (uv0179.y - _GradientMaskRemap.x) * (_GradientMaskRemap.w - _GradientMaskRemap.z) / (_GradientMaskRemap.y - _GradientMaskRemap.x)) ));
				float4 appendResult192 = (float4(break187.x , break187.y , break187.z , lerpResult189));
				float2 uv0_Noise = IN.ase_texcoord2.xy * _Noise_ST.xy + _Noise_ST.zw;
				float2 panner120 = ( 1.0 * _Time.y * _NoiseSpeed + uv0_Noise);
				float4 lerpResult158 = lerp( ( main_color221 + ( main_color221 * ( pow( clampResult177 , _GlowPower ) * _GlowIntensity ) ) ) , appendResult192 , ( 1.0 - saturate( (_NoiseRemap.z + (tex2D( _Noise, panner120 ).r - _NoiseRemap.x) * (_NoiseRemap.w - _NoiseRemap.z) / (_NoiseRemap.y - _NoiseRemap.x)) ) ));
				float2 uv0122 = IN.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float4 screenPos = IN.ase_texcoord3;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float screenDepth206 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm.xy ),_ZBufferParams);
				float distanceDepth206 = saturate( abs( ( screenDepth206 - LinearEyeDepth( ase_screenPosNorm.z,_ZBufferParams ) ) / ( _SoftIntersectionDistance ) ) );
				
				float Alpha = saturate( ( (lerpResult158).a * saturate( (_MaskRemap.z + (uv0122.y - _MaskRemap.x) * (_MaskRemap.w - _MaskRemap.z) / (_MaskRemap.y - _MaskRemap.x)) ) * distanceDepth206 ) );
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

	
	}
	CustomEditor "UnityEditor.ShaderGraph.PBRMasterGUI"
	Fallback "Hidden/InternalErrorShader"
	
}
/*ASEBEGIN
Version=18000
221;610;1564;449;-1069.235;-164.2518;1;True;False
Node;AmplifyShaderEditor.RangedFloatNode;213;-364.7161,-1255.744;Inherit;False;Property;_HueOffset;HueOffset;14;0;Create;True;0;0;False;0;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;218;-77.41611,-1255.744;Inherit;False;hue_offset;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;136;-492.1509,-652.9429;Inherit;False;Property;_BackGradientColor;Back Gradient Color;9;1;[HDR];Create;True;0;0;False;0;1,1,1,1;0.6482242,0.03301889,1,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RGBToHSVNode;212;24.87862,-774.5729;Inherit;False;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.GetLocalVarNode;220;145.2478,-837.4665;Inherit;False;218;hue_offset;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;128;-463.8096,-945.645;Inherit;False;Property;_Color;Color;8;1;[HDR];Create;True;0;0;False;0;1,1,1,1;2.008455,6.141238,8.651807,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;216;314.3478,-809.5167;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;219;194.1839,-1160.844;Inherit;False;218;hue_offset;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RGBToHSVNode;210;43.14779,-1064.135;Inherit;False;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.FractNode;217;465.1477,-788.7167;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;214;376.2839,-1130.944;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;228;810.2538,-880.2861;Inherit;False;Property;_Intensity;Intensity;15;0;Create;True;0;0;False;0;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.HSVToRGBNode;211;652.934,-794.2668;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.FractNode;215;493.2839,-1093.244;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;230;949.2539,-784.2861;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.HSVToRGBNode;209;656.9033,-1052.629;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.TextureCoordinatesNode;168;-531.589,1299.087;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector4Node;169;-486.5891,1505.087;Inherit;False;Property;_GlowRemap;Glow Remap;7;0;Create;True;0;0;False;0;0,0,0,0;0,1,0,1;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;119;-1062.522,-1.47821;Inherit;False;0;118;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;227;1145.026,-770.9414;Inherit;False;FLOAT4;4;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;229;918.2538,-1054.286;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TFHCRemapNode;170;-207.5891,1199.087;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;121;-992.5222,193.5218;Inherit;False;Property;_NoiseSpeed;Noise Speed;1;0;Create;True;0;0;False;0;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RegisterLocalVarNode;222;1444.571,-776.7189;Inherit;False;back_color;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.DynamicAppendNode;226;1232.661,-1030.911;Inherit;False;FLOAT4;4;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode;178;0.9009399,1345.805;Inherit;False;Property;_GlowPower;Glow Power;12;0;Create;True;0;0;False;0;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;179;107.6137,603.2732;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector4Node;181;90.61374,751.2732;Inherit;False;Property;_GradientMaskRemap;Gradient Mask Remap;5;0;Create;True;0;0;False;0;0,0,0,0;0,1,0,1;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PannerNode;120;-808.5222,83.52179;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;177;-28.09906,1185.805;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;225;141.3504,-257.0063;Inherit;False;222;back_color;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode;174;89.89633,1453.417;Inherit;False;Property;_GlowIntensity;Glow Intensity;11;0;Create;True;0;0;False;0;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;161;-298.1896,234.2589;Inherit;False;Property;_NoiseRemap;Noise Remap;10;0;Create;True;0;0;False;0;0,0,0,0;0,1,0,1;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;118;-426.3905,-32.07336;Inherit;True;Property;_Noise;Noise;0;0;Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TFHCRemapNode;180;431.6137,503.2732;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;176;157.9009,1089.805;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;221;1503.385,-1049.361;Inherit;False;main_color;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;175;302.8963,951.4172;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;182;625.5552,425.5932;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode;160;-50.18964,-11.74115;Inherit;True;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;224;400.3596,-398.979;Inherit;False;221;main_color;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.BreakToComponentsNode;187;428.8795,-133.7948;Inherit;False;FLOAT4;1;0;FLOAT4;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.SaturateNode;162;228.8104,36.25885;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;172;723.097,-310.4502;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;223;607.2858,-510.4799;Inherit;False;221;main_color;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.LerpOp;189;712.9888,-17.03029;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;173;891.097,-429.4502;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;122;-318.0683,488.7252;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.OneMinusNode;195;457.0071,11.94894;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;192;891.0543,-172.4064;Inherit;False;COLOR;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.Vector4Node;125;-299.5222,649.5218;Inherit;False;Property;_MaskRemap;Mask Remap;3;0;Create;True;0;0;False;0;0,0,0,0;0,1,0,1;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;198;-156.5253,1846.545;Inherit;False;0;118;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;197;-162.7583,2011.363;Float;False;Property;_DisplacementNoiseSpeed;DisplacementNoiseSpeed;4;0;Create;True;0;0;False;0;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.LerpOp;158;1087.905,-181.9657;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.TFHCRemapNode;124;41.47778,401.5218;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;199;133.4747,1884.545;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;207;800.6277,552.4569;Inherit;False;Property;_SoftIntersectionDistance;Soft Intersection Distance;13;0;Create;True;0;0;False;0;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DepthFade;206;1071.628,486.4569;Inherit;False;True;True;True;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;167;290.4193,384.8418;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;200;612.2415,1957.363;Float;False;Property;_DisplacementNoiseRemap;DisplacementNoiseRemap;6;0;Create;True;0;0;False;0;0,0,0,0;0,1,0,1;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ComponentMaskNode;190;1177.88,20.2052;Inherit;False;False;False;False;True;1;0;COLOR;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;201;310.4747,1805.545;Inherit;True;Property;_DisplacementNoise;DisplacementNoise;2;0;Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TFHCRemapNode;202;927.2415,1826.363;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.NormalVertexDataNode;203;990.9023,2127.879;Inherit;False;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;191;1362.88,264.2052;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;205;1532.621,301.2715;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;204;1233.475,1836.545;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;234;1834.453,202.2677;Float;False;False;-1;2;UnityEditor.ShaderGraph.PBRMasterGUI;0;1;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;ShadowCaster;0;2;ShadowCaster;0;False;False;False;True;0;False;-1;False;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;0;0;False;False;False;False;False;False;True;1;False;-1;True;3;False;-1;False;True;1;LightMode=ShadowCaster;False;0;Hidden/InternalErrorShader;0;0;Standard;0;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;235;1834.453,202.2677;Float;False;False;-1;2;UnityEditor.ShaderGraph.PBRMasterGUI;0;1;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;DepthOnly;0;3;DepthOnly;0;False;False;False;True;0;False;-1;False;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;0;0;False;False;False;False;True;False;False;False;False;0;False;-1;False;True;1;False;-1;False;False;True;1;LightMode=DepthOnly;False;0;Hidden/InternalErrorShader;0;0;Standard;0;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;232;1834.453,202.2677;Float;False;False;-1;2;UnityEditor.ShaderGraph.PBRMasterGUI;0;1;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;ExtraPrePass;0;0;ExtraPrePass;5;False;False;False;True;0;False;-1;False;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;0;0;True;1;1;False;-1;0;False;-1;0;1;False;-1;0;False;-1;False;False;True;0;False;-1;True;True;True;True;True;0;False;-1;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;True;1;False;-1;True;3;False;-1;True;True;0;False;-1;0;False;-1;True;0;False;0;Hidden/InternalErrorShader;0;0;Standard;0;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;233;1834.453,202.2677;Float;False;True;-1;2;UnityEditor.ShaderGraph.PBRMasterGUI;0;3;Knife/Portal Alpha URP;2992e84f91cbeb14eab234972e07ea9d;True;Forward;0;1;Forward;7;False;False;False;True;0;False;-1;False;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;True;0;0;True;1;5;False;-1;10;False;-1;1;1;False;-1;10;False;-1;False;False;False;True;True;True;True;True;0;False;-1;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;True;2;False;-1;True;3;False;-1;True;True;0;False;-1;0;False;-1;True;1;LightMode=UniversalForward;False;0;Hidden/InternalErrorShader;0;0;Standard;11;Surface;1;  Blend;0;Two Sided;1;Cast Shadows;1;Receive Shadows;1;GPU Instancing;1;LOD CrossFade;0;Built-in Fog;0;Meta Pass;0;Extra Pre Pass;0;Vertex Position,InvertActionOnDeselection;1;0;5;False;True;True;True;False;False;;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;236;1834.453,202.2677;Float;False;False;-1;2;UnityEditor.ShaderGraph.PBRMasterGUI;0;1;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;Meta;0;4;Meta;0;False;False;False;True;0;False;-1;False;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;0;0;False;False;False;True;2;False;-1;False;False;False;False;False;True;1;LightMode=Meta;False;0;Hidden/InternalErrorShader;0;0;Standard;0;0
WireConnection;218;0;213;0
WireConnection;212;0;136;0
WireConnection;216;0;220;0
WireConnection;216;1;212;1
WireConnection;210;0;128;0
WireConnection;217;0;216;0
WireConnection;214;0;219;0
WireConnection;214;1;210;1
WireConnection;211;0;217;0
WireConnection;211;1;212;2
WireConnection;211;2;212;3
WireConnection;215;0;214;0
WireConnection;230;0;211;0
WireConnection;230;1;228;0
WireConnection;209;0;215;0
WireConnection;209;1;210;2
WireConnection;209;2;210;3
WireConnection;227;0;230;0
WireConnection;227;3;136;4
WireConnection;229;0;209;0
WireConnection;229;1;228;0
WireConnection;170;0;168;2
WireConnection;170;1;169;1
WireConnection;170;2;169;2
WireConnection;170;3;169;3
WireConnection;170;4;169;4
WireConnection;222;0;227;0
WireConnection;226;0;229;0
WireConnection;226;3;128;4
WireConnection;120;0;119;0
WireConnection;120;2;121;0
WireConnection;177;0;170;0
WireConnection;118;1;120;0
WireConnection;180;0;179;2
WireConnection;180;1;181;1
WireConnection;180;2;181;2
WireConnection;180;3;181;3
WireConnection;180;4;181;4
WireConnection;176;0;177;0
WireConnection;176;1;178;0
WireConnection;221;0;226;0
WireConnection;175;0;176;0
WireConnection;175;1;174;0
WireConnection;182;0;180;0
WireConnection;160;0;118;1
WireConnection;160;1;161;1
WireConnection;160;2;161;2
WireConnection;160;3;161;3
WireConnection;160;4;161;4
WireConnection;187;0;225;0
WireConnection;162;0;160;0
WireConnection;172;0;224;0
WireConnection;172;1;175;0
WireConnection;189;0;187;3
WireConnection;189;2;182;0
WireConnection;173;0;223;0
WireConnection;173;1;172;0
WireConnection;195;0;162;0
WireConnection;192;0;187;0
WireConnection;192;1;187;1
WireConnection;192;2;187;2
WireConnection;192;3;189;0
WireConnection;158;0;173;0
WireConnection;158;1;192;0
WireConnection;158;2;195;0
WireConnection;124;0;122;2
WireConnection;124;1;125;1
WireConnection;124;2;125;2
WireConnection;124;3;125;3
WireConnection;124;4;125;4
WireConnection;199;0;198;0
WireConnection;199;2;197;0
WireConnection;206;0;207;0
WireConnection;167;0;124;0
WireConnection;190;0;158;0
WireConnection;201;1;199;0
WireConnection;202;0;201;1
WireConnection;202;1;200;1
WireConnection;202;2;200;2
WireConnection;202;3;200;3
WireConnection;202;4;200;4
WireConnection;191;0;190;0
WireConnection;191;1;167;0
WireConnection;191;2;206;0
WireConnection;205;0;191;0
WireConnection;204;0;202;0
WireConnection;204;1;203;0
WireConnection;233;2;158;0
WireConnection;233;3;205;0
WireConnection;233;5;204;0
ASEEND*/
//CHKSM=C2E3F5899BC916A12FC97B8B45950C3DE50DBA48