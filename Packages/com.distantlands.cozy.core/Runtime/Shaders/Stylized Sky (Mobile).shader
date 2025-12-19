// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Distant Lands/Cozy/BiRP/Stylized Sky (Mobile)"
{
	Properties
	{
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Pass
		{
			ColorMask 0
			ZWrite On
		}

		Tags{ "RenderType" = "Opaque"  "Queue" = "Transparent-99" "IsEmissive" = "true"  }
		Cull Front
		Stencil
		{
			Ref 220
			Comp Always
			Pass Replace
		}
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#define ASE_VERSION 19801
		#pragma surface surf Unlit keepalpha noshadow noambient novertexlights nolightmap  nodynlightmap nodirlightmap nofog vertex:vertexDataFunc 
		struct Input
		{
			float4 ase_positionOS4f;
			float2 uv_texcoord;
			float3 worldPos;
		};

		uniform float4 CZY_StarColor;
		uniform samplerCUBE CZY_StarDomeTexture;
		uniform float CZY_DayPercentage;
		uniform float CZY_YearPercentage;
		uniform float4 CZY_SunDirectionParams;
		uniform float4 CZY_HorizonColor;
		uniform float CZY_FilterSaturation;
		uniform float CZY_FilterValue;
		uniform float4 CZY_FilterColor;
		uniform float4 CZY_ZenithColor;
		uniform float CZY_Power;
		uniform float3 CZY_SunDirection;
		uniform float CZY_SunHaloFalloff;
		uniform float4 CZY_SunHaloColor;
		uniform float4 CZY_SunFilterColor;
		uniform float4 CZY_SunColor;
		uniform float CZY_SunSize;
		uniform float3 CZY_EclipseDirection;
		uniform float3 CZY_MoonDirection;
		uniform float CZY_MoonFlareFalloff;
		uniform float4 CZY_MoonFlareColor;
		uniform float4 CZY_LightColor;
		uniform float4 CZY_FogColor5;
		uniform float CZY_LightFlareSquish;
		uniform half CZY_LightIntensity;
		uniform half CZY_LightFalloff;
		uniform float4 CZY_FogMoonFlareColor;
		uniform float CZY_SkyFogAmount;
		uniform float CZY_FogSmoothness;
		uniform float CZY_FogOffset;
		uniform float CZY_FogIntensity;


		float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
		{
			original -= center;
			float C = cos( angle );
			float S = sin( angle );
			float t = 1 - C;
			float m00 = t * u.x * u.x + C;
			float m01 = t * u.x * u.y - S * u.z;
			float m02 = t * u.x * u.z + S * u.y;
			float m10 = t * u.x * u.y + S * u.z;
			float m11 = t * u.y * u.y + C;
			float m12 = t * u.y * u.z - S * u.x;
			float m20 = t * u.x * u.z - S * u.y;
			float m21 = t * u.y * u.z + S * u.x;
			float m22 = t * u.z * u.z + C;
			float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
			return mul( finalMatrix, original ) + center;
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

		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float4 ase_positionOS4f = v.vertex;
			o.ase_positionOS4f = ase_positionOS4f;
		}

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float celestialPosition187_g5 = ( ( CZY_DayPercentage + CZY_YearPercentage ) * 2.0 * UNITY_PI );
			float sunPitch201_g5 = radians( ( CZY_SunDirectionParams.y + -90.0 ) );
			float sunDirection198_g5 = radians( CZY_SunDirectionParams.x );
			float3 ase_positionOS = i.ase_positionOS4f.xyz;
			float3 normalizeResult293_g5 = normalize( ase_positionOS );
			float3 appendResult291_g5 = (float3(normalizeResult293_g5));
			float3 rotatedValue206_g5 = RotateAroundAxis( float3( 0,0,0 ), appendResult291_g5, float3( 0,-1,0 ), sunDirection198_g5 );
			float3 rotatedValue203_g5 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue206_g5, float3( 0,0,1 ), sunPitch201_g5 );
			float3 rotatedValue208_g5 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue203_g5, float3( 0,1,0 ), celestialPosition187_g5 );
			float3 UV209_g5 = rotatedValue208_g5;
			float4 finalStars223_g5 = ( CZY_StarColor * float4( texCUBE( CZY_StarDomeTexture, UV209_g5 ).rgb , 0.0 ) );
			float3 hsvTorgb2_g140 = RGBToHSV( CZY_HorizonColor.rgb );
			float3 hsvTorgb3_g140 = HSVToRGB( float3(hsvTorgb2_g140.x,saturate( ( hsvTorgb2_g140.y + CZY_FilterSaturation ) ),( hsvTorgb2_g140.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g140 = ( float4( hsvTorgb3_g140 , 0.0 ) * CZY_FilterColor );
			float4 HorizonColor192_g5 = temp_output_10_0_g140;
			float3 hsvTorgb2_g139 = RGBToHSV( CZY_ZenithColor.rgb );
			float3 hsvTorgb3_g139 = HSVToRGB( float3(hsvTorgb2_g139.x,saturate( ( hsvTorgb2_g139.y + CZY_FilterSaturation ) ),( hsvTorgb2_g139.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g139 = ( float4( hsvTorgb3_g139 , 0.0 ) * CZY_FilterColor );
			float4 ZenithColor189_g5 = temp_output_10_0_g139;
			float2 temp_output_153_0_g5 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult155_g5 = dot( temp_output_153_0_g5 , temp_output_153_0_g5 );
			float SimpleGradient154_g5 = dotResult155_g5;
			float GradientPos179_g5 = ( 1.0 - saturate( pow( saturate( (0.0 + (SimpleGradient154_g5 - 0.0) * (2.0 - 0.0) / (1.0 - 0.0)) ) , CZY_Power ) ) );
			float4 lerpResult329_g5 = lerp( HorizonColor192_g5 , ZenithColor189_g5 , GradientPos179_g5);
			float4 SimpleSkyGradient333_g5 = lerpResult329_g5;
			float3 ase_positionWS = i.worldPos;
			float3 normalizeResult232_g5 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float dotResult233_g5 = dot( normalizeResult232_g5 , CZY_SunDirection );
			float SunDot277_g5 = dotResult233_g5;
			float3 hsvTorgb2_g136 = RGBToHSV( CZY_SunHaloColor.rgb );
			float3 hsvTorgb3_g136 = HSVToRGB( float3(hsvTorgb2_g136.x,saturate( ( hsvTorgb2_g136.y + CZY_FilterSaturation ) ),( hsvTorgb2_g136.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g136 = ( float4( hsvTorgb3_g136 , 0.0 ) * CZY_FilterColor );
			half4 SunFlare238_g5 = abs( ( saturate( pow( saturate( (SunDot277_g5*0.5 + 0.4) ) , ( ( CZY_SunHaloFalloff * 40.0 ) + 5.0 ) ) ) * ( temp_output_10_0_g136 * CZY_SunFilterColor ) ) );
			float3 hsvTorgb2_g137 = RGBToHSV( CZY_SunColor.rgb );
			float3 hsvTorgb3_g137 = HSVToRGB( float3(hsvTorgb2_g137.x,saturate( ( hsvTorgb2_g137.y + CZY_FilterSaturation ) ),( hsvTorgb2_g137.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g137 = ( float4( hsvTorgb3_g137 , 0.0 ) * CZY_FilterColor );
			float3 normalizeResult270_g5 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float dotResult271_g5 = dot( normalizeResult270_g5 , CZY_EclipseDirection );
			float EclipseDot274_g5 = dotResult271_g5;
			float eclipse285_g5 = ( ( 1.0 - EclipseDot274_g5 ) > ( pow( CZY_SunSize , 3.0 ) * 0.0006 ) ? 0.0 : 1.0 );
			float4 SunRender259_g5 = ( ( temp_output_10_0_g137 * CZY_SunFilterColor ) * saturate( ( ( ( 1.0 - SunDot277_g5 ) > ( pow( CZY_SunSize , 3.0 ) * 0.0007 ) ? 0.0 : 1.0 ) - eclipse285_g5 ) ) );
			float3 normalizeResult264_g5 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float dotResult265_g5 = dot( normalizeResult264_g5 , CZY_MoonDirection );
			float MoonDot276_g5 = dotResult265_g5;
			float3 hsvTorgb2_g135 = RGBToHSV( CZY_MoonFlareColor.rgb );
			float3 hsvTorgb3_g135 = HSVToRGB( float3(hsvTorgb2_g135.x,saturate( ( hsvTorgb2_g135.y + CZY_FilterSaturation ) ),( hsvTorgb2_g135.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g135 = ( float4( hsvTorgb3_g135 , 0.0 ) * CZY_FilterColor );
			half4 MoonFlare231_g5 = abs( ( saturate( pow( saturate( (MoonDot276_g5*0.5 + 0.4) ) , ( ( CZY_MoonFlareFalloff * 20.0 ) + 5.0 ) ) ) * temp_output_10_0_g135 ) );
			float3 hsvTorgb69_g141 = RGBToHSV( CZY_FogColor5.rgb );
			float3 normalizeResult54_g141 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float3 temp_output_56_0_g141 = ( normalizeResult54_g141 * _ProjectionParams.z );
			float3 appendResult25_g141 = (float3(1.0 , CZY_LightFlareSquish , 1.0));
			float3 normalizeResult13_g141 = normalize( ( ( temp_output_56_0_g141 * appendResult25_g141 ) - _WorldSpaceCameraPos ) );
			float dotResult16_g141 = dot( normalizeResult13_g141 , CZY_SunDirection );
			half LightMask35_g141 = saturate( pow( abs( ( (dotResult16_g141*0.5 + 0.5) * CZY_LightIntensity ) ) , CZY_LightFalloff ) );
			float temp_output_91_0_g141 = 0.0;
			float3 hsvTorgb2_g143 = RGBToHSV( ( CZY_LightColor * hsvTorgb69_g141.z * saturate( ( LightMask35_g141 * ( 1.5 * CZY_FogColor5.a ) * temp_output_91_0_g141 ) ) ).rgb );
			float3 hsvTorgb3_g143 = HSVToRGB( float3(hsvTorgb2_g143.x,saturate( ( hsvTorgb2_g143.y + CZY_FilterSaturation ) ),( hsvTorgb2_g143.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g143 = ( float4( hsvTorgb3_g143 , 0.0 ) * CZY_FilterColor );
			float3 direction88_g141 = ( temp_output_56_0_g141 - _WorldSpaceCameraPos );
			float3 normalizeResult32_g141 = normalize( direction88_g141 );
			float3 normalizeResult30_g141 = normalize( CZY_MoonDirection );
			float dotResult28_g141 = dot( normalizeResult32_g141 , normalizeResult30_g141 );
			half MoonMask18_g141 = saturate( pow( abs( ( saturate( (dotResult28_g141*1.0 + 0.0) ) * CZY_LightIntensity ) ) , ( CZY_LightFalloff * 3.0 ) ) );
			float3 hsvTorgb2_g142 = RGBToHSV( ( CZY_FogColor5 + ( hsvTorgb69_g141.z * saturate( ( CZY_FogColor5.a * MoonMask18_g141 * temp_output_91_0_g141 ) ) * CZY_FogMoonFlareColor ) ).rgb );
			float3 hsvTorgb3_g142 = HSVToRGB( float3(hsvTorgb2_g142.x,saturate( ( hsvTorgb2_g142.y + CZY_FilterSaturation ) ),( hsvTorgb2_g142.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g142 = ( float4( hsvTorgb3_g142 , 0.0 ) * CZY_FilterColor );
			float3 ase_objectScale = float3( length( unity_ObjectToWorld[ 0 ].xyz ), length( unity_ObjectToWorld[ 1 ].xyz ), length( unity_ObjectToWorld[ 2 ].xyz ) );
			float temp_output_34_0_g141 = ( CZY_SkyFogAmount * saturate( ( ( 1.0 - saturate( ( ( ( direction88_g141.y * 0.1 ) * ( 1.0 / ( ( CZY_FogSmoothness * length( ase_objectScale ) ) * 10.0 ) ) ) + ( 1.0 - CZY_FogOffset ) ) ) ) * CZY_FogIntensity ) ) );
			float4 lerpResult90_g141 = lerp( ( finalStars223_g5 + float4( half3(0,0,0) , 0.0 ) + (0) + SimpleSkyGradient333_g5 + SunFlare238_g5 + SunRender259_g5 + MoonFlare231_g5 ) , ( ( temp_output_10_0_g143 * CZY_SunFilterColor ) + temp_output_10_0_g142 ) , temp_output_34_0_g141);
			o.Emission = lerpResult90_g141.rgb;
			o.Alpha = 1;
		}

		ENDCG
	}
	CustomEditor "DistantLands.Cozy.EditorScripts.EmptyShaderGUI"
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.RangedFloatNode;600;-400,240;Inherit;False;Global;CZY_SkyFogAmount;CZY_SkyFogAmount;1;0;Create;True;0;0;0;False;0;False;0;0.695;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;598;-400,128;Inherit;False;Stylized Sky (Mobile);0;;5;688f603026dc18c468fc058bac44ec60;0;0;2;COLOR;390;FLOAT;391
Node;AmplifyShaderEditor.FunctionNode;599;-160,144;Inherit;False;AddFogToSkyLayer;-1;;141;36a78fe96c9f6fa4dab85c7793736468;0;3;89;COLOR;0,0,0,0;False;91;FLOAT;0;False;59;FLOAT;0;False;2;COLOR;84;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;112,96;Float;False;True;-1;2;DistantLands.Cozy.EditorScripts.EmptyShaderGUI;0;0;Unlit;Distant Lands/Cozy/BiRP/Stylized Sky (Mobile);False;False;False;False;True;True;True;True;True;True;False;False;False;False;False;False;False;False;False;False;False;Front;0;False;;7;False;;False;0;False;;0;False;;True;0;Translucent;0.5;True;False;-99;False;Opaque;;Transparent;All;12;all;True;True;True;True;0;False;;True;220;False;;255;False;;255;False;;7;False;;3;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;False;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;599;89;598;390
WireConnection;599;59;600;0
WireConnection;0;2;599;84
ASEEND*/
//CHKSM=EE1CB5CBE6FCF6D00D9D9501A955576029BF6194