// Made with Amplify Shader Editor v1.9.1.5
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Distant Lands/Cozy/BiRP/Fog Culling"
{
	Properties
	{
		_Thickness("Thickness", Float) = 500
		_FogVariationTexture("Fog Variation Texture", 2D) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" "IsEmissive" = "true"  }
		Cull Off
		Stencil
		{
			Ref 221
			ReadMask 221
			WriteMask 221
			CompFront Always
			PassFront Replace
			CompBack Always
			PassBack Replace
		}
		GrabPass{ }
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#include "UnityCG.cginc"
		#pragma target 3.0
		#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex);
		#else
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex)
		#endif
		#pragma surface surf Unlit keepalpha noshadow 
		struct Input
		{
			float4 screenPos;
			float3 worldPos;
			half ASEIsFrontFacing : VFACE;
		};

		ASE_DECLARE_SCREENSPACE_TEXTURE( _GrabTexture )
		uniform float4 CZY_LightColor;
		uniform float4 CZY_FogColor1;
		uniform float4 CZY_FogColor2;
		uniform float CZY_FogDepthMultiplier;
		uniform float _Thickness;
		uniform sampler2D _FogVariationTexture;
		UNITY_DECLARE_DEPTH_TEXTURE( _CameraDepthTexture );
		uniform float4 _CameraDepthTexture_TexelSize;
		uniform float3 CZY_VariationWindDirection;
		uniform float CZY_VariationScale;
		uniform float CZY_VariationAmount;
		uniform float CZY_VariationDistance;
		uniform float CZY_FogColorStart1;
		uniform float4 CZY_FogColor3;
		uniform float CZY_FogColorStart2;
		uniform float4 CZY_FogColor4;
		uniform float CZY_FogColorStart3;
		uniform float4 CZY_FogColor5;
		uniform float CZY_FogColorStart4;
		uniform float CZY_LightFlareSquish;
		uniform float3 CZY_SunDirection;
		uniform half CZY_LightIntensity;
		uniform half CZY_LightFalloff;
		uniform float CZY_FilterSaturation;
		uniform float CZY_FilterValue;
		uniform float4 CZY_FilterColor;
		uniform float4 CZY_SunFilterColor;
		uniform float3 CZY_MoonDirection;
		uniform float4 CZY_FogMoonFlareColor;
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

		float2 UnStereo( float2 UV )
		{
			#if UNITY_SINGLE_PASS_STEREO
			float4 scaleOffset = unity_StereoScaleOffset[ unity_StereoEyeIndex ];
			UV.xy = (UV.xy - scaleOffset.zw) / scaleOffset.xy;
			#endif
			return UV;
		}


		float3 InvertDepthDir72_g29( float3 In )
		{
			float3 result = In;
			#if !defined(ASE_SRP_VERSION) || ASE_SRP_VERSION <= 70301
			result *= float3(1,1,-1);
			#endif
			return result;
		}


		float3 InvertDepthDir72_g26( float3 In )
		{
			float3 result = In;
			#if !defined(ASE_SRP_VERSION) || ASE_SRP_VERSION <= 70301
			result *= float3(1,1,-1);
			#endif
			return result;
		}


		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float3 ase_objectScale = float3( length( unity_ObjectToWorld[ 0 ].xyz ), length( unity_ObjectToWorld[ 1 ].xyz ), length( unity_ObjectToWorld[ 2 ].xyz ) );
			float3 break20_g25 = ( ase_objectScale * float3( 0.5,0.5,0.5 ) );
			float3 objToWorld30_g25 = mul( unity_ObjectToWorld, float4( float3( 0,0,0 ), 1 ) ).xyz;
			float isInsideBoundingBox5_g25 = ( ( break20_g25.x > abs( ( _WorldSpaceCameraPos.x - objToWorld30_g25.x ) ) ? 1.0 : 0.0 ) * ( break20_g25.y > abs( ( _WorldSpaceCameraPos.y - objToWorld30_g25.y ) ) ? 1.0 : 0.0 ) * ( break20_g25.z >= abs( ( _WorldSpaceCameraPos.z - objToWorld30_g25.z ) ) ? 1.0 : 0.0 ) );
			float temp_output_19_0_g25 = ( 1.0 - isInsideBoundingBox5_g25 );
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( ase_screenPos );
			float4 screenColor25_g25 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_GrabTexture,ase_grabScreenPos.xy/ase_grabScreenPos.w);
			float3 ase_worldPos = i.worldPos;
			float temp_output_12_0_g25 = length( ( ase_worldPos - _WorldSpaceCameraPos ) );
			float preDepth120_g28 = ( temp_output_12_0_g25 - _Thickness );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			float2 UV22_g30 = ase_screenPosNorm.xy;
			float2 localUnStereo22_g30 = UnStereo( UV22_g30 );
			float2 break64_g29 = localUnStereo22_g30;
			float clampDepth69_g29 = SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy );
			#ifdef UNITY_REVERSED_Z
				float staticSwitch38_g29 = ( 1.0 - clampDepth69_g29 );
			#else
				float staticSwitch38_g29 = clampDepth69_g29;
			#endif
			float3 appendResult39_g29 = (float3(break64_g29.x , break64_g29.y , staticSwitch38_g29));
			float4 appendResult42_g29 = (float4((appendResult39_g29*2.0 + -1.0) , 1.0));
			float4 temp_output_43_0_g29 = mul( unity_CameraInvProjection, appendResult42_g29 );
			float3 temp_output_46_0_g29 = ( (temp_output_43_0_g29).xyz / (temp_output_43_0_g29).w );
			float3 In72_g29 = temp_output_46_0_g29;
			float3 localInvertDepthDir72_g29 = InvertDepthDir72_g29( In72_g29 );
			float4 appendResult49_g29 = (float4(localInvertDepthDir72_g29 , 1.0));
			float4 temp_output_97_0_g28 = mul( unity_CameraToWorld, appendResult49_g29 );
			float lerpResult114_g28 = lerp( preDepth120_g28 , ( preDepth120_g28 * (( 1.0 - CZY_VariationAmount ) + (tex2D( _FogVariationTexture, (( (temp_output_97_0_g28).xz + ( (CZY_VariationWindDirection).xz * _Time.y ) )*( 0.1 / CZY_VariationScale ) + 0.0) ).r - 0.0) * (1.0 - ( 1.0 - CZY_VariationAmount )) / (1.0 - 0.0)) ) , ( 1.0 - saturate( ( preDepth120_g28 / CZY_VariationDistance ) ) ));
			float newFogDepth103_g28 = lerpResult114_g28;
			float temp_output_15_0_g28 = ( CZY_FogDepthMultiplier * sqrt( newFogDepth103_g28 ) );
			float temp_output_1_0_g33 = temp_output_15_0_g28;
			float4 lerpResult28_g33 = lerp( CZY_FogColor1 , CZY_FogColor2 , saturate( ( temp_output_1_0_g33 / CZY_FogColorStart1 ) ));
			float4 lerpResult41_g33 = lerp( saturate( lerpResult28_g33 ) , CZY_FogColor3 , saturate( ( ( CZY_FogColorStart1 - temp_output_1_0_g33 ) / ( CZY_FogColorStart1 - CZY_FogColorStart2 ) ) ));
			float4 lerpResult35_g33 = lerp( lerpResult41_g33 , CZY_FogColor4 , saturate( ( ( CZY_FogColorStart2 - temp_output_1_0_g33 ) / ( CZY_FogColorStart2 - CZY_FogColorStart3 ) ) ));
			float4 lerpResult113_g33 = lerp( lerpResult35_g33 , CZY_FogColor5 , saturate( ( ( CZY_FogColorStart3 - temp_output_1_0_g33 ) / ( CZY_FogColorStart3 - CZY_FogColorStart4 ) ) ));
			float4 temp_output_142_0_g28 = lerpResult113_g33;
			float3 hsvTorgb32_g28 = RGBToHSV( temp_output_142_0_g28.rgb );
			float3 temp_output_91_0_g28 = ase_worldPos;
			float3 appendResult73_g28 = (float3(1.0 , CZY_LightFlareSquish , 1.0));
			float3 normalizeResult5_g28 = normalize( ( ( temp_output_91_0_g28 * appendResult73_g28 ) - _WorldSpaceCameraPos ) );
			float dotResult6_g28 = dot( normalizeResult5_g28 , CZY_SunDirection );
			half LightMask27_g28 = saturate( pow( abs( ( (dotResult6_g28*0.5 + 0.5) * CZY_LightIntensity ) ) , CZY_LightFalloff ) );
			float temp_output_26_0_g28 = ( (temp_output_142_0_g28).a * saturate( temp_output_15_0_g28 ) );
			float3 hsvTorgb2_g32 = RGBToHSV( ( CZY_LightColor * hsvTorgb32_g28.z * saturate( ( LightMask27_g28 * ( 1.5 * temp_output_26_0_g28 ) ) ) ).rgb );
			float3 hsvTorgb3_g32 = HSVToRGB( float3(hsvTorgb2_g32.x,saturate( ( hsvTorgb2_g32.y + CZY_FilterSaturation ) ),( hsvTorgb2_g32.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g32 = ( float4( hsvTorgb3_g32 , 0.0 ) * CZY_FilterColor );
			float3 direction90_g28 = ( temp_output_91_0_g28 - _WorldSpaceCameraPos );
			float3 normalizeResult93_g28 = normalize( direction90_g28 );
			float3 normalizeResult88_g28 = normalize( CZY_MoonDirection );
			float dotResult49_g28 = dot( normalizeResult93_g28 , normalizeResult88_g28 );
			half MoonMask47_g28 = saturate( pow( abs( ( saturate( (dotResult49_g28*1.0 + 0.0) ) * CZY_LightIntensity ) ) , ( CZY_LightFalloff * 3.0 ) ) );
			float3 hsvTorgb2_g31 = RGBToHSV( ( temp_output_142_0_g28 + ( hsvTorgb32_g28.z * saturate( ( temp_output_26_0_g28 * MoonMask47_g28 ) ) * CZY_FogMoonFlareColor ) ).rgb );
			float3 hsvTorgb3_g31 = HSVToRGB( float3(hsvTorgb2_g31.x,saturate( ( hsvTorgb2_g31.y + CZY_FilterSaturation ) ),( hsvTorgb2_g31.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g31 = ( float4( hsvTorgb3_g31 , 0.0 ) * CZY_FilterColor );
			float finalAlpha141_g28 = temp_output_26_0_g28;
			float temp_output_75_0_g28 = ( finalAlpha141_g28 * saturate( ( ( 1.0 - saturate( ( ( ( direction90_g28.y * 0.1 ) * ( 1.0 / ( ( CZY_FogSmoothness * length( ase_objectScale ) ) * 10.0 ) ) ) + ( 1.0 - CZY_FogOffset ) ) ) ) * CZY_FogIntensity ) ) );
			float4 lerpResult22_g25 = lerp( screenColor25_g25 , ( ( temp_output_10_0_g32 * CZY_SunFilterColor ) + temp_output_10_0_g31 ) , temp_output_75_0_g28);
			o.Emission = ( temp_output_19_0_g25 == 1.0 ? lerpResult22_g25 : screenColor25_g25 ).rgb;
			float2 UV22_g27 = ase_screenPosNorm.xy;
			float2 localUnStereo22_g27 = UnStereo( UV22_g27 );
			float2 break64_g26 = localUnStereo22_g27;
			float clampDepth69_g26 = SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy );
			#ifdef UNITY_REVERSED_Z
				float staticSwitch38_g26 = ( 1.0 - clampDepth69_g26 );
			#else
				float staticSwitch38_g26 = clampDepth69_g26;
			#endif
			float3 appendResult39_g26 = (float3(break64_g26.x , break64_g26.y , staticSwitch38_g26));
			float4 appendResult42_g26 = (float4((appendResult39_g26*2.0 + -1.0) , 1.0));
			float4 temp_output_43_0_g26 = mul( unity_CameraInvProjection, appendResult42_g26 );
			float3 temp_output_46_0_g26 = ( (temp_output_43_0_g26).xyz / (temp_output_43_0_g26).w );
			float3 In72_g26 = temp_output_46_0_g26;
			float3 localInvertDepthDir72_g26 = InvertDepthDir72_g26( In72_g26 );
			float4 appendResult49_g26 = (float4(localInvertDepthDir72_g26 , 1.0));
			float3 appendResult6_g25 = (float3(mul( unity_CameraToWorld, appendResult49_g26 ).xyz));
			float temp_output_11_0_g25 = length( ( appendResult6_g25 - _WorldSpaceCameraPos ) );
			float switchResult23_g25 = (((i.ASEIsFrontFacing>0)?(( ( temp_output_11_0_g25 > temp_output_12_0_g25 ? 1.0 : 0.0 ) * temp_output_19_0_g25 )):(( isInsideBoundingBox5_g25 * ( temp_output_11_0_g25 < temp_output_12_0_g25 ? 1.0 : 0.0 ) ))));
			o.Alpha = switchResult23_g25;
		}

		ENDCG
	}
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=19105
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;87;416,-64;Float;False;True;-1;2;ASEMaterialInspector;0;0;Unlit;Distant Lands/Cozy/BiRP/Fog Culling;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Off;0;False;;0;False;;False;0;False;;0;False;;False;0;Custom;0.5;True;False;0;True;Opaque;;Geometry;All;12;all;True;True;True;True;0;False;;True;221;False;;221;False;;221;False;;7;False;;3;False;;0;False;;0;False;;7;False;;3;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;False;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;0;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;15;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
Node;AmplifyShaderEditor.FunctionNode;86;135,28;Inherit;False;Fog Culling;1;;25;8ec1ce8cf4b40a142b3c3eda6d2e5a96;0;0;2;COLOR;0;FLOAT;38
WireConnection;87;2;86;0
WireConnection;87;9;86;38
ASEEND*/
//CHKSM=4D9A53695618469BA9EB20791C01021DDE54B05E