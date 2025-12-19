// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Distant Lands/Cozy/BiRP/Stylized Fog (Physical Height)"
{
	Properties
	{
		_FogVariationTexture("Fog Variation Texture", 2D) = "white" {}
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
		
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#include "UnityCG.cginc"
		#pragma target 3.0
		#define ASE_VERSION 19801
		#pragma surface surf Unlit keepalpha noshadow noambient novertexlights nolightmap  nodynlightmap nodirlightmap nofog nometa noforwardadd 
		struct Input
		{
			float4 screenPos;
			float3 worldPos;
		};

		uniform float4 CZY_LightColor;
		uniform float4 CZY_FogColor1;
		uniform float4 CZY_FogColor2;
		uniform float CZY_FogDepthMultiplier;
		UNITY_DECLARE_DEPTH_TEXTURE( _CameraDepthTexture );
		uniform float4 _CameraDepthTexture_TexelSize;
		uniform sampler2D _FogVariationTexture;
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
		uniform float4 CZY_HeightFogColor;
		uniform float CZY_HeightFogBase;
		uniform float CZY_HeightFogTransition;
		uniform float CZY_HeightFogBaseVariationScale;
		uniform float CZY_HeightFogBaseVariationAmount;
		uniform float CZY_HeightFogIntensity;
		uniform float CZY_FogSmoothness;
		uniform float CZY_FogOffset;
		uniform float CZY_FogIntensity;


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


		float3 InvertDepthDir72_g80( float3 In )
		{
			float3 result = In;
			#if !defined(ASE_SRP_VERSION) || ASE_SRP_VERSION <= 70301
			result *= float3(1,1,-1);
			#endif
			return result;
		}


		float3 InvertDepthDir72_g77( float3 In )
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
			float4 ase_positionSS = float4( i.screenPos.xyz , i.screenPos.w + 1e-7 );
			float4 ase_positionSSNorm = ase_positionSS / ase_positionSS.w;
			ase_positionSSNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_positionSSNorm.z : ase_positionSSNorm.z * 0.5 + 0.5;
			float2 UV22_g81 = ase_positionSSNorm.xy;
			float2 localUnStereo22_g81 = UnStereo( UV22_g81 );
			float2 break64_g80 = localUnStereo22_g81;
			float depth01_69_g80 = SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_positionSSNorm.xy );
			#ifdef UNITY_REVERSED_Z
				float staticSwitch38_g80 = ( 1.0 - depth01_69_g80 );
			#else
				float staticSwitch38_g80 = depth01_69_g80;
			#endif
			float3 appendResult39_g80 = (float3(break64_g80.x , break64_g80.y , staticSwitch38_g80));
			float4 appendResult42_g80 = (float4((appendResult39_g80*2.0 + -1.0) , 1.0));
			float4 temp_output_43_0_g80 = mul( unity_CameraInvProjection, appendResult42_g80 );
			float3 temp_output_46_0_g80 = ( (temp_output_43_0_g80).xyz / (temp_output_43_0_g80).w );
			float3 In72_g80 = temp_output_46_0_g80;
			float3 localInvertDepthDir72_g80 = InvertDepthDir72_g80( In72_g80 );
			float4 appendResult49_g80 = (float4(localInvertDepthDir72_g80 , 1.0));
			float4 temp_output_97_0_g79 = mul( unity_CameraToWorld, appendResult49_g80 );
			float preDepth120_g79 = distance( temp_output_97_0_g79 , float4( _WorldSpaceCameraPos , 0.0 ) );
			float lerpResult114_g79 = lerp( preDepth120_g79 , ( preDepth120_g79 * (( 1.0 - CZY_VariationAmount ) + (tex2D( _FogVariationTexture, (( (temp_output_97_0_g79).xz + ( (CZY_VariationWindDirection).xz * _Time.y ) )*( 0.1 / CZY_VariationScale ) + 0.0) ).r - 0.0) * (1.0 - ( 1.0 - CZY_VariationAmount )) / (1.0 - 0.0)) ) , ( 1.0 - saturate( ( preDepth120_g79 / CZY_VariationDistance ) ) ));
			float newFogDepth103_g79 = lerpResult114_g79;
			float temp_output_15_0_g79 = ( CZY_FogDepthMultiplier * sqrt( newFogDepth103_g79 ) );
			float temp_output_1_0_g84 = temp_output_15_0_g79;
			float4 lerpResult28_g84 = lerp( CZY_FogColor1 , CZY_FogColor2 , saturate( ( temp_output_1_0_g84 / CZY_FogColorStart1 ) ));
			float4 lerpResult41_g84 = lerp( saturate( lerpResult28_g84 ) , CZY_FogColor3 , saturate( ( ( CZY_FogColorStart1 - temp_output_1_0_g84 ) / ( CZY_FogColorStart1 - CZY_FogColorStart2 ) ) ));
			float4 lerpResult35_g84 = lerp( lerpResult41_g84 , CZY_FogColor4 , saturate( ( ( CZY_FogColorStart2 - temp_output_1_0_g84 ) / ( CZY_FogColorStart2 - CZY_FogColorStart3 ) ) ));
			float4 lerpResult113_g84 = lerp( lerpResult35_g84 , CZY_FogColor5 , saturate( ( ( CZY_FogColorStart3 - temp_output_1_0_g84 ) / ( CZY_FogColorStart3 - CZY_FogColorStart4 ) ) ));
			float4 temp_output_157_0_g79 = lerpResult113_g84;
			float3 hsvTorgb32_g79 = RGBToHSV( temp_output_157_0_g79.rgb );
			float3 ase_positionWS = i.worldPos;
			float3 normalizeResult160_g79 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float3 temp_output_91_0_g79 = ( normalizeResult160_g79 * _ProjectionParams.z );
			float3 appendResult73_g79 = (float3(1.0 , CZY_LightFlareSquish , 1.0));
			float3 normalizeResult5_g79 = normalize( ( ( temp_output_91_0_g79 * appendResult73_g79 ) - _WorldSpaceCameraPos ) );
			float dotResult6_g79 = dot( normalizeResult5_g79 , CZY_SunDirection );
			half LightMask27_g79 = saturate( pow( abs( ( (dotResult6_g79*0.5 + 0.5) * CZY_LightIntensity ) ) , CZY_LightFalloff ) );
			float temp_output_26_0_g79 = ( (temp_output_157_0_g79).a * saturate( temp_output_15_0_g79 ) );
			float3 hsvTorgb2_g83 = RGBToHSV( ( CZY_LightColor * hsvTorgb32_g79.z * saturate( ( LightMask27_g79 * ( 1.5 * temp_output_26_0_g79 ) ) ) ).rgb );
			float3 hsvTorgb3_g83 = HSVToRGB( float3(hsvTorgb2_g83.x,saturate( ( hsvTorgb2_g83.y + CZY_FilterSaturation ) ),( hsvTorgb2_g83.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g83 = ( float4( hsvTorgb3_g83 , 0.0 ) * CZY_FilterColor );
			float3 direction90_g79 = ( temp_output_91_0_g79 - _WorldSpaceCameraPos );
			float3 normalizeResult93_g79 = normalize( direction90_g79 );
			float3 normalizeResult88_g79 = normalize( CZY_MoonDirection );
			float dotResult49_g79 = dot( normalizeResult93_g79 , normalizeResult88_g79 );
			half MoonMask47_g79 = saturate( pow( abs( ( saturate( (dotResult49_g79*1.0 + 0.0) ) * CZY_LightIntensity ) ) , ( CZY_LightFalloff * 3.0 ) ) );
			float3 hsvTorgb2_g82 = RGBToHSV( ( temp_output_157_0_g79 + ( hsvTorgb32_g79.z * saturate( ( temp_output_26_0_g79 * MoonMask47_g79 ) ) * CZY_FogMoonFlareColor ) ).rgb );
			float3 hsvTorgb3_g82 = HSVToRGB( float3(hsvTorgb2_g82.x,saturate( ( hsvTorgb2_g82.y + CZY_FilterSaturation ) ),( hsvTorgb2_g82.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g82 = ( float4( hsvTorgb3_g82 , 0.0 ) * CZY_FilterColor );
			float2 UV22_g78 = ase_positionSSNorm.xy;
			float2 localUnStereo22_g78 = UnStereo( UV22_g78 );
			float2 break64_g77 = localUnStereo22_g78;
			float depth01_69_g77 = SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_positionSSNorm.xy );
			#ifdef UNITY_REVERSED_Z
				float staticSwitch38_g77 = ( 1.0 - depth01_69_g77 );
			#else
				float staticSwitch38_g77 = depth01_69_g77;
			#endif
			float3 appendResult39_g77 = (float3(break64_g77.x , break64_g77.y , staticSwitch38_g77));
			float4 appendResult42_g77 = (float4((appendResult39_g77*2.0 + -1.0) , 1.0));
			float4 temp_output_43_0_g77 = mul( unity_CameraInvProjection, appendResult42_g77 );
			float3 temp_output_46_0_g77 = ( (temp_output_43_0_g77).xyz / (temp_output_43_0_g77).w );
			float3 In72_g77 = temp_output_46_0_g77;
			float3 localInvertDepthDir72_g77 = InvertDepthDir72_g77( In72_g77 );
			float4 appendResult49_g77 = (float4(localInvertDepthDir72_g77 , 1.0));
			float4 temp_output_18_0_g76 = mul( unity_CameraToWorld, appendResult49_g77 );
			float mulTime63_g76 = _Time.y * 0.01;
			float depthLinearEye31_g76 = LinearEyeDepth( SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_positionSSNorm.xy ) );
			float temp_output_121_0_g75 = ( ( 1.0 - saturate( ( ( temp_output_18_0_g76.y - CZY_HeightFogBase ) / ( CZY_HeightFogTransition + ( ( 1.0 - tex2D( _FogVariationTexture, ((temp_output_18_0_g76).xz*( 1.0 / CZY_HeightFogBaseVariationScale ) + mulTime63_g76) ).r ) * CZY_HeightFogBaseVariationAmount ) ) ) ) ) * saturate( ( depthLinearEye31_g76 * 0.01 * CZY_HeightFogIntensity ) ) * CZY_HeightFogColor.a );
			float4 lerpResult108_g75 = lerp( ( ( temp_output_10_0_g83 * CZY_SunFilterColor ) + temp_output_10_0_g82 ) , CZY_HeightFogColor , temp_output_121_0_g75);
			o.Emission = lerpResult108_g75.rgb;
			float finalAlpha141_g79 = temp_output_26_0_g79;
			float3 ase_objectScale = float3( length( unity_ObjectToWorld[ 0 ].xyz ), length( unity_ObjectToWorld[ 1 ].xyz ), length( unity_ObjectToWorld[ 2 ].xyz ) );
			float temp_output_124_56_g75 = ( finalAlpha141_g79 * saturate( ( ( 1.0 - saturate( ( ( ( direction90_g79.y * 0.1 ) * ( 1.0 / ( ( CZY_FogSmoothness * length( ase_objectScale ) ) * 10.0 ) ) ) + ( 1.0 - CZY_FogOffset ) ) ) ) * CZY_FogIntensity ) ) );
			o.Alpha = ( ( 1.0 - 0.0 ) * max( temp_output_121_0_g75 , temp_output_124_56_g75 ) );
		}

		ENDCG
	}
	CustomEditor "DistantLands.Cozy.EditorScripts.EmptyShaderGUI"
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.FunctionNode;297;274.0955,-417.3223;Inherit;False;Stylized Fog (Physical Height);0;;75;6863d88adda26194cbbb00d58f08515c;0;0;2;COLOR;0;FLOAT;123
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;799.9159,-507.1588;Float;False;True;-1;2;DistantLands.Cozy.EditorScripts.EmptyShaderGUI;0;0;Unlit;Distant Lands/Cozy/BiRP/Stylized Fog (Physical Height);False;False;False;False;True;True;True;True;True;True;True;True;False;False;True;False;False;False;False;False;False;Front;2;False;;7;False;;False;0;False;;0;False;;True;0;Custom;0.5;True;False;-100;True;Custom;HeightFog;Transparent;All;12;all;True;True;True;True;0;False;;True;222;False;;255;False;;255;False;;6;False;;3;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;False;2;5;False;;10;False;;0;5;False;;10;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;5;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;0;2;297;0
WireConnection;0;9;297;123
ASEEND*/
//CHKSM=1C4B024CCA284FD028DF5AF3E76FBFB945D91445