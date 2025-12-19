// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Distant Lands/Cozy/BiRP/Stylized Fog (Stepped)"
{
	Properties
	{
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Pass
		{
			ColorMask 0
			ZWrite On
		}

		Tags{ "RenderType" = "HeightFog"  "Queue" = "Transparent-100" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull Front
		ZWrite Off
		ZTest Always
		Stencil
		{
			Ref 222
			Comp NotEqual
			Pass Replace
		}
		Blend SrcAlpha OneMinusSrcAlpha
		
		GrabPass{ }
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#include "UnityCG.cginc"
		#pragma target 3.0
		#define ASE_VERSION 19801
		#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex);
		#else
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex)
		#endif
		#pragma surface surf Unlit keepalpha noshadow noambient novertexlights nolightmap  nodynlightmap nodirlightmap nofog nometa noforwardadd 
		struct Input
		{
			float4 screenPos;
			float3 worldPos;
		};

		ASE_DECLARE_SCREENSPACE_TEXTURE( _GrabTexture )
		uniform float CZY_FogDepthMultiplier;
		UNITY_DECLARE_DEPTH_TEXTURE( _CameraDepthTexture );
		uniform float4 _CameraDepthTexture_TexelSize;
		uniform float CZY_FogColorStart4;
		uniform float4 CZY_FogColor5;
		uniform float CZY_FogColorStart3;
		uniform float4 CZY_FogColor4;
		uniform float CZY_FogColorStart2;
		uniform float4 CZY_FogColor3;
		uniform float CZY_FogColorStart1;
		uniform float4 CZY_FogColor2;
		uniform float4 CZY_FogColor1;
		uniform float4 CZY_LightColor;
		uniform float CZY_FlareSquish;
		uniform float3 CZy_SunDirection;
		uniform half CZY_LightIntensity;
		uniform half CZY_LightFalloff;
		uniform float CZY_FogSmoothness;
		uniform float CZY_FogOffset;
		uniform float CZY_FogIntensity;


		inline float4 ASE_ComputeGrabScreenPos( float4 pos )
		{
			#if UNITY_UV_STARTS_AT_TOP
			float scale = -1.0;
			#else
			float scale = 1.0;
			#endif
			float4 o = pos;
			o.y = pos.w * 0.5f;
			o.y = ( pos.y - o.y ) * _ProjectionParams.x * scale + o.y;
			return o;
		}


		float2 UnStereo( float2 UV )
		{
			#if UNITY_SINGLE_PASS_STEREO
			float4 scaleOffset = unity_StereoScaleOffset[ unity_StereoEyeIndex ];
			UV.xy = (UV.xy - scaleOffset.zw) / scaleOffset.xy;
			#endif
			return UV;
		}


		float3 InvertDepthDir72_g76( float3 In )
		{
			float3 result = In;
			#if !defined(ASE_SRP_VERSION) || ASE_SRP_VERSION <= 70301
			result *= float3(1,1,-1);
			#endif
			return result;
		}


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

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float4 ase_positionSS = float4( i.screenPos.xyz , i.screenPos.w + 1e-7 );
			float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( ase_positionSS );
			float4 screenColor42_g75 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_GrabTexture,ase_grabScreenPos.xy/ase_grabScreenPos.w);
			float2 appendResult5_g75 = (float2(_WorldSpaceCameraPos.x , _WorldSpaceCameraPos.z));
			float4 ase_positionSSNorm = ase_positionSS / ase_positionSS.w;
			ase_positionSSNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_positionSSNorm.z : ase_positionSSNorm.z * 0.5 + 0.5;
			float2 UV22_g77 = ase_positionSSNorm.xy;
			float2 localUnStereo22_g77 = UnStereo( UV22_g77 );
			float2 break64_g76 = localUnStereo22_g77;
			float depth01_69_g76 = SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_positionSSNorm.xy );
			#ifdef UNITY_REVERSED_Z
				float staticSwitch38_g76 = ( 1.0 - depth01_69_g76 );
			#else
				float staticSwitch38_g76 = depth01_69_g76;
			#endif
			float3 appendResult39_g76 = (float3(break64_g76.x , break64_g76.y , staticSwitch38_g76));
			float4 appendResult42_g76 = (float4((appendResult39_g76*2.0 + -1.0) , 1.0));
			float4 temp_output_43_0_g76 = mul( unity_CameraInvProjection, appendResult42_g76 );
			float3 temp_output_46_0_g76 = ( (temp_output_43_0_g76).xyz / (temp_output_43_0_g76).w );
			float3 In72_g76 = temp_output_46_0_g76;
			float3 localInvertDepthDir72_g76 = InvertDepthDir72_g76( In72_g76 );
			float4 appendResult49_g76 = (float4(localInvertDepthDir72_g76 , 1.0));
			float4 break3_g75 = mul( unity_CameraToWorld, appendResult49_g76 );
			float2 appendResult4_g75 = (float2(break3_g75.x , break3_g75.z));
			float Distance9_g75 = ( CZY_FogDepthMultiplier * sqrt( distance( appendResult5_g75 , appendResult4_g75 ) ) );
			float4 break26_g75 = ( Distance9_g75 > CZY_FogColorStart4 ? CZY_FogColor5 : ( Distance9_g75 > CZY_FogColorStart3 ? CZY_FogColor4 : ( Distance9_g75 > CZY_FogColorStart2 ? CZY_FogColor3 : ( Distance9_g75 > CZY_FogColorStart1 ? CZY_FogColor2 : CZY_FogColor1 ) ) ) );
			float temp_output_1_0_g78 = Distance9_g75;
			float4 appendResult22_g75 = (float4(CZY_FogColorStart1 , CZY_FogColorStart2 , CZY_FogColorStart3 , CZY_FogColorStart4));
			float4 break116_g78 = appendResult22_g75;
			float lerpResult28_g78 = lerp( CZY_FogColor1.a , CZY_FogColor2.a , saturate( ( temp_output_1_0_g78 / break116_g78.x ) ));
			float lerpResult41_g78 = lerp( saturate( lerpResult28_g78 ) , CZY_FogColor3.a , saturate( ( ( break116_g78.x - temp_output_1_0_g78 ) / ( 0.0 - break116_g78.y ) ) ));
			float lerpResult35_g78 = lerp( lerpResult41_g78 , CZY_FogColor4.a , saturate( ( ( break116_g78.y - temp_output_1_0_g78 ) / ( break116_g78.y - break116_g78.z ) ) ));
			float lerpResult113_g78 = lerp( lerpResult35_g78 , CZY_FogColor5.a , saturate( ( ( break116_g78.z - temp_output_1_0_g78 ) / ( break116_g78.z - break116_g78.w ) ) ));
			float4 appendResult27_g75 = (float4(break26_g75.r , break26_g75.g , break26_g75.b , lerpResult113_g78));
			float4 FogColors28_g75 = appendResult27_g75;
			float3 hsvTorgb35_g75 = RGBToHSV( CZY_LightColor.rgb );
			float3 hsvTorgb34_g75 = RGBToHSV( FogColors28_g75.xyz );
			float3 hsvTorgb40_g75 = HSVToRGB( float3(hsvTorgb35_g75.x,hsvTorgb35_g75.y,( hsvTorgb35_g75.z * hsvTorgb34_g75.z )) );
			float3 ase_positionWS = i.worldPos;
			float3 appendResult55_g75 = (float3(1.0 , CZY_FlareSquish , 1.0));
			float3 normalizeResult60_g75 = normalize( ( ( ase_positionWS * appendResult55_g75 ) - _WorldSpaceCameraPos ) );
			float dotResult61_g75 = dot( normalizeResult60_g75 , CZy_SunDirection );
			half LightMask67_g75 = saturate( pow( abs( ( (dotResult61_g75*0.5 + 0.5) * CZY_LightIntensity ) ) , CZY_LightFalloff ) );
			float temp_output_32_0_g75 = ( FogColors28_g75.w * saturate( Distance9_g75 ) );
			float4 lerpResult41_g75 = lerp( FogColors28_g75 , float4( hsvTorgb40_g75 , 0.0 ) , saturate( ( LightMask67_g75 * ( 1.5 * temp_output_32_0_g75 ) ) ));
			float4 lerpResult43_g75 = lerp( screenColor42_g75 , lerpResult41_g75 , temp_output_32_0_g75);
			o.Emission = lerpResult43_g75.xyz;
			float3 direction87_g75 = ( ase_positionWS - _WorldSpaceCameraPos );
			float3 ase_objectScale = float3( length( unity_ObjectToWorld[ 0 ].xyz ), length( unity_ObjectToWorld[ 1 ].xyz ), length( unity_ObjectToWorld[ 2 ].xyz ) );
			o.Alpha = ( ( 1.0 - 0.0 ) * ( FogColors28_g75.w * saturate( ( ( 1.0 - saturate( ( ( ( direction87_g75.y * 0.1 ) * ( 1.0 / ( ( CZY_FogSmoothness * length( ase_objectScale ) ) * 10.0 ) ) ) + ( 1.0 - CZY_FogOffset ) ) ) ) * CZY_FogIntensity ) ) ) );
		}

		ENDCG
	}
	CustomEditor "DistantLands.Cozy.EditorScripts.EmptyShaderGUI"
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.FunctionNode;297;262.5955,-437.3223;Inherit;False;Stylized Fog (Stepped);-1;;75;f6a63b6a876e87b46a29d85f0581f5f2;0;0;2;FLOAT4;0;FLOAT;107
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;576,-528;Float;False;True;-1;2;DistantLands.Cozy.EditorScripts.EmptyShaderGUI;0;0;Unlit;Distant Lands/Cozy/BiRP/Stylized Fog (Stepped);False;False;False;False;True;True;True;True;True;True;True;True;False;False;True;False;False;False;False;False;False;Front;2;False;;7;False;;False;0;False;;0;False;;True;0;Custom;0.5;True;False;-100;True;Custom;HeightFog;Transparent;All;12;all;True;True;True;True;0;False;;True;222;False;;255;False;;255;False;;6;False;;3;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;False;2;5;False;;10;False;;0;5;False;;10;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;0;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;0;2;297;0
WireConnection;0;9;297;107
ASEEND*/
//CHKSM=7956BCCAE957261D749F97EDD0DBB20696331551