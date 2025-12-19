// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Distant Lands/Cozy/BiRP/Stylized Sky (Desktop)"
{
	Properties
	{
		_TextureSample7("Texture Sample 7", 2D) = "white" {}
		_TextureSample8("Texture Sample 8", 2D) = "white" {}
		_LightColumns1("Light Columns", 2D) = "white" {}
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
		uniform samplerCUBE CZY_ConstellationDomeTexture;
		uniform float CZY_ConstellationIntensity;
		uniform samplerCUBE CZY_GalaxyDomeTexture;
		uniform float4 CZY_GalaxyColor1;
		uniform sampler2D CZY_GalaxyVariationMap;
		uniform sampler2D _TextureSample7;
		uniform sampler2D _TextureSample8;
		uniform float4 CZY_GalaxyColor2;
		uniform float4 CZY_GalaxyColor3;
		uniform float CZY_GalaxyMultiplier;
		uniform sampler2D CZY_LightColumnsTexture;
		uniform float CZY_LightColumnsPosition;
		uniform float CZY_LightColumnsHeight;
		uniform sampler2D _LightColumns1;
		uniform float4 CZY_LightColumnColor;
		uniform sampler2D CZY_RainbowTexture;
		uniform float3 CZY_SunDirection;
		uniform float CZY_RainbowSize;
		uniform float CZY_RainbowWidth;
		uniform float CZY_RainbowIntensity;
		uniform float4 CZY_HorizonColor;
		uniform float CZY_FilterSaturation;
		uniform float CZY_FilterValue;
		uniform float4 CZY_FilterColor;
		uniform float4 CZY_ZenithColor;
		uniform float CZY_Power;
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
			float celestialPosition291_g5 = ( ( CZY_DayPercentage + CZY_YearPercentage ) * 2.0 * UNITY_PI );
			float sunPitch305_g5 = radians( ( CZY_SunDirectionParams.y + -90.0 ) );
			float sunDirection302_g5 = radians( CZY_SunDirectionParams.x );
			float3 ase_positionOS = i.ase_positionOS4f.xyz;
			float3 normalizeResult397_g5 = normalize( ase_positionOS );
			float3 appendResult395_g5 = (float3(normalizeResult397_g5));
			float3 rotatedValue310_g5 = RotateAroundAxis( float3( 0,0,0 ), appendResult395_g5, float3( 0,-1,0 ), sunDirection302_g5 );
			float3 rotatedValue307_g5 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue310_g5, float3( 0,0,1 ), sunPitch305_g5 );
			float3 rotatedValue312_g5 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue307_g5, float3( 0,1,0 ), celestialPosition291_g5 );
			float3 UV313_g5 = rotatedValue312_g5;
			float4 finalStars327_g5 = ( CZY_StarColor * float4( ( texCUBE( CZY_StarDomeTexture, UV313_g5 ).rgb + ( texCUBE( CZY_ConstellationDomeTexture, UV313_g5 ).rgb * float3( 0.1,0.1,0.1 ) * CZY_ConstellationIntensity ) ) , 0.0 ) );
			float2 Pos300_g5 = i.uv_texcoord;
			float cos265_g5 = cos( 0.002 * _Time.y );
			float sin265_g5 = sin( 0.002 * _Time.y );
			float2 rotator265_g5 = mul( Pos300_g5 - float2( 0.5,0.5 ) , float2x2( cos265_g5 , -sin265_g5 , sin265_g5 , cos265_g5 )) + float2( 0.5,0.5 );
			float cos264_g5 = cos( 0.004 * _Time.y );
			float sin264_g5 = sin( 0.004 * _Time.y );
			float2 rotator264_g5 = mul( Pos300_g5 - float2( 0.5,0.5 ) , float2x2( cos264_g5 , -sin264_g5 , sin264_g5 , cos264_g5 )) + float2( 0.5,0.5 );
			float cos263_g5 = cos( 0.001 * _Time.y );
			float sin263_g5 = sin( 0.001 * _Time.y );
			float2 rotator263_g5 = mul( Pos300_g5 - float2( 0.5,0.5 ) , float2x2( cos263_g5 , -sin263_g5 , sin263_g5 , cos263_g5 )) + float2( 0.5,0.5 );
			float4 appendResult260_g5 = (float4(tex2D( CZY_GalaxyVariationMap, (rotator265_g5*10.0 + 0.0) ).r , tex2D( _TextureSample7, (rotator264_g5*8.0 + 2.04) ).r , tex2D( _TextureSample8, (rotator263_g5*6.0 + 2.04) ).r , 1.0));
			float4 galaxyPlacement316_g5 = appendResult260_g5;
			float4 break269_g5 = galaxyPlacement316_g5;
			float4 galaxyColoring314_g5 = ( ( CZY_GalaxyColor1 * break269_g5.r ) + ( CZY_GalaxyColor2 * break269_g5.g ) + ( CZY_GalaxyColor3 * break269_g5.b ) );
			float4 finalGalaxyColoring320_g5 = ( texCUBE( CZY_GalaxyDomeTexture, UV313_g5 ) * galaxyColoring314_g5 * CZY_GalaxyMultiplier );
			float3 worldPosition399_g5 = appendResult395_g5;
			float3 break450_g5 = worldPosition399_g5;
			float temp_output_414_0_g5 = ( ( atan2( break450_g5.x , break450_g5.z ) / 6.28318548202515 ) + 0.5 );
			float temp_output_455_0_g5 = ( ( ( 1.0 - CZY_LightColumnsHeight ) * 3.0 ) + 1.0 );
			float temp_output_402_0_g5 = (0.0 + (( ( break450_g5.y + -CZY_LightColumnsPosition ) * pow( temp_output_455_0_g5 , temp_output_455_0_g5 ) ) - -1.0) * (1.0 - 0.0) / (1.0 - -1.0));
			float temp_output_400_0_g5 = (( temp_output_402_0_g5 >= 0.0 && temp_output_402_0_g5 <= 1.0 ) ? temp_output_402_0_g5 :  0.0 );
			float2 appendResult406_g5 = (float2(temp_output_414_0_g5 , temp_output_400_0_g5));
			float mulTime413_g5 = _Time.y * 0.005;
			float2 appendResult407_g5 = (float2(( ( temp_output_414_0_g5 + mulTime413_g5 ) * 1.5 ) , temp_output_400_0_g5));
			float4 finalLightColumns411_g5 = ( float4( min( tex2D( CZY_LightColumnsTexture, appendResult406_g5 ).rgb , tex2D( _LightColumns1, appendResult407_g5 ).rgb ) , 0.0 ) * CZY_LightColumnColor );
			float3 ase_positionWS = i.worldPos;
			float3 normalizeResult336_g5 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float dotResult337_g5 = dot( normalizeResult336_g5 , CZY_SunDirection );
			float SunDot381_g5 = dotResult337_g5;
			float temp_output_417_0_g5 = ( 1.0 - SunDot381_g5 );
			float temp_output_418_0_g5 = ( CZY_RainbowSize * 0.01 );
			float temp_output_420_0_g5 = ( temp_output_418_0_g5 + ( CZY_RainbowWidth * 0.01 ) );
			float temp_output_419_0_g5 = (0.0 + (temp_output_417_0_g5 - temp_output_418_0_g5) * (1.0 - 0.0) / (temp_output_420_0_g5 - temp_output_418_0_g5));
			float2 temp_cast_2 = (temp_output_419_0_g5).xx;
			float4 finalRainbow428_g5 = ( tex2D( CZY_RainbowTexture, temp_cast_2 ) * ( ( temp_output_417_0_g5 < temp_output_418_0_g5 ? 0.0 : 1.0 ) * ( temp_output_417_0_g5 > temp_output_420_0_g5 ? 0.0 : 1.0 ) ) * CZY_RainbowIntensity * saturate( sin( ( temp_output_419_0_g5 * UNITY_PI ) ) ) );
			float3 hsvTorgb2_g131 = RGBToHSV( CZY_HorizonColor.rgb );
			float3 hsvTorgb3_g131 = HSVToRGB( float3(hsvTorgb2_g131.x,saturate( ( hsvTorgb2_g131.y + CZY_FilterSaturation ) ),( hsvTorgb2_g131.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g131 = ( float4( hsvTorgb3_g131 , 0.0 ) * CZY_FilterColor );
			float4 HorizonColor296_g5 = temp_output_10_0_g131;
			float3 hsvTorgb2_g130 = RGBToHSV( CZY_ZenithColor.rgb );
			float3 hsvTorgb3_g130 = HSVToRGB( float3(hsvTorgb2_g130.x,saturate( ( hsvTorgb2_g130.y + CZY_FilterSaturation ) ),( hsvTorgb2_g130.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g130 = ( float4( hsvTorgb3_g130 , 0.0 ) * CZY_FilterColor );
			float4 ZenithColor293_g5 = temp_output_10_0_g130;
			float2 temp_output_257_0_g5 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult259_g5 = dot( temp_output_257_0_g5 , temp_output_257_0_g5 );
			float SimpleGradient258_g5 = dotResult259_g5;
			float GradientPos283_g5 = ( 1.0 - saturate( pow( saturate( (0.0 + (SimpleGradient258_g5 - 0.0) * (2.0 - 0.0) / (1.0 - 0.0)) ) , CZY_Power ) ) );
			float4 lerpResult433_g5 = lerp( HorizonColor296_g5 , ZenithColor293_g5 , GradientPos283_g5);
			float4 SimpleSkyGradient437_g5 = lerpResult433_g5;
			float3 hsvTorgb2_g136 = RGBToHSV( CZY_SunHaloColor.rgb );
			float3 hsvTorgb3_g136 = HSVToRGB( float3(hsvTorgb2_g136.x,saturate( ( hsvTorgb2_g136.y + CZY_FilterSaturation ) ),( hsvTorgb2_g136.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g136 = ( float4( hsvTorgb3_g136 , 0.0 ) * CZY_FilterColor );
			half4 SunFlare342_g5 = abs( ( saturate( pow( saturate( (SunDot381_g5*0.5 + 0.4) ) , ( ( CZY_SunHaloFalloff * 40.0 ) + 5.0 ) ) ) * ( temp_output_10_0_g136 * CZY_SunFilterColor ) ) );
			float3 hsvTorgb2_g137 = RGBToHSV( CZY_SunColor.rgb );
			float3 hsvTorgb3_g137 = HSVToRGB( float3(hsvTorgb2_g137.x,saturate( ( hsvTorgb2_g137.y + CZY_FilterSaturation ) ),( hsvTorgb2_g137.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g137 = ( float4( hsvTorgb3_g137 , 0.0 ) * CZY_FilterColor );
			float3 normalizeResult374_g5 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float dotResult375_g5 = dot( normalizeResult374_g5 , CZY_EclipseDirection );
			float EclipseDot378_g5 = dotResult375_g5;
			float eclipse389_g5 = ( ( 1.0 - EclipseDot378_g5 ) > ( pow( CZY_SunSize , 3.0 ) * 0.0006 ) ? 0.0 : 1.0 );
			float4 SunRender363_g5 = ( ( temp_output_10_0_g137 * CZY_SunFilterColor ) * saturate( ( ( ( 1.0 - SunDot381_g5 ) > ( pow( CZY_SunSize , 3.0 ) * 0.0007 ) ? 0.0 : 1.0 ) - eclipse389_g5 ) ) );
			float3 normalizeResult368_g5 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float dotResult369_g5 = dot( normalizeResult368_g5 , CZY_MoonDirection );
			float MoonDot380_g5 = dotResult369_g5;
			float3 hsvTorgb2_g135 = RGBToHSV( CZY_MoonFlareColor.rgb );
			float3 hsvTorgb3_g135 = HSVToRGB( float3(hsvTorgb2_g135.x,saturate( ( hsvTorgb2_g135.y + CZY_FilterSaturation ) ),( hsvTorgb2_g135.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g135 = ( float4( hsvTorgb3_g135 , 0.0 ) * CZY_FilterColor );
			half4 MoonFlare335_g5 = abs( ( saturate( pow( saturate( (MoonDot380_g5*0.5 + 0.4) ) , ( ( CZY_MoonFlareFalloff * 20.0 ) + 5.0 ) ) ) * temp_output_10_0_g135 ) );
			float3 hsvTorgb69_g138 = RGBToHSV( CZY_FogColor5.rgb );
			float3 normalizeResult54_g138 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float3 temp_output_56_0_g138 = ( normalizeResult54_g138 * _ProjectionParams.z );
			float3 appendResult25_g138 = (float3(1.0 , CZY_LightFlareSquish , 1.0));
			float3 normalizeResult13_g138 = normalize( ( ( temp_output_56_0_g138 * appendResult25_g138 ) - _WorldSpaceCameraPos ) );
			float dotResult16_g138 = dot( normalizeResult13_g138 , CZY_SunDirection );
			half LightMask35_g138 = saturate( pow( abs( ( (dotResult16_g138*0.5 + 0.5) * CZY_LightIntensity ) ) , CZY_LightFalloff ) );
			float temp_output_91_0_g138 = 0.0;
			float3 hsvTorgb2_g140 = RGBToHSV( ( CZY_LightColor * hsvTorgb69_g138.z * saturate( ( LightMask35_g138 * ( 1.5 * CZY_FogColor5.a ) * temp_output_91_0_g138 ) ) ).rgb );
			float3 hsvTorgb3_g140 = HSVToRGB( float3(hsvTorgb2_g140.x,saturate( ( hsvTorgb2_g140.y + CZY_FilterSaturation ) ),( hsvTorgb2_g140.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g140 = ( float4( hsvTorgb3_g140 , 0.0 ) * CZY_FilterColor );
			float3 direction88_g138 = ( temp_output_56_0_g138 - _WorldSpaceCameraPos );
			float3 normalizeResult32_g138 = normalize( direction88_g138 );
			float3 normalizeResult30_g138 = normalize( CZY_MoonDirection );
			float dotResult28_g138 = dot( normalizeResult32_g138 , normalizeResult30_g138 );
			half MoonMask18_g138 = saturate( pow( abs( ( saturate( (dotResult28_g138*1.0 + 0.0) ) * CZY_LightIntensity ) ) , ( CZY_LightFalloff * 3.0 ) ) );
			float3 hsvTorgb2_g139 = RGBToHSV( ( CZY_FogColor5 + ( hsvTorgb69_g138.z * saturate( ( CZY_FogColor5.a * MoonMask18_g138 * temp_output_91_0_g138 ) ) * CZY_FogMoonFlareColor ) ).rgb );
			float3 hsvTorgb3_g139 = HSVToRGB( float3(hsvTorgb2_g139.x,saturate( ( hsvTorgb2_g139.y + CZY_FilterSaturation ) ),( hsvTorgb2_g139.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g139 = ( float4( hsvTorgb3_g139 , 0.0 ) * CZY_FilterColor );
			float3 ase_objectScale = float3( length( unity_ObjectToWorld[ 0 ].xyz ), length( unity_ObjectToWorld[ 1 ].xyz ), length( unity_ObjectToWorld[ 2 ].xyz ) );
			float temp_output_34_0_g138 = ( CZY_SkyFogAmount * saturate( ( ( 1.0 - saturate( ( ( ( direction88_g138.y * 0.1 ) * ( 1.0 / ( ( CZY_FogSmoothness * length( ase_objectScale ) ) * 10.0 ) ) ) + ( 1.0 - CZY_FogOffset ) ) ) ) * CZY_FogIntensity ) ) );
			float4 lerpResult90_g138 = lerp( ( finalStars327_g5 + finalGalaxyColoring320_g5 + finalLightColumns411_g5 + finalRainbow428_g5 + SimpleSkyGradient437_g5 + SunFlare342_g5 + SunRender363_g5 + MoonFlare335_g5 ) , ( ( temp_output_10_0_g140 * CZY_SunFilterColor ) + temp_output_10_0_g139 ) , temp_output_34_0_g138);
			o.Emission = lerpResult90_g138.rgb;
			o.Alpha = 1;
		}

		ENDCG
	}
	CustomEditor "DistantLands.Cozy.EditorScripts.EmptyShaderGUI"
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.FunctionNode;598;-432,128;Inherit;False;Stylized Sky (Desktop);0;;5;6fc9715951ffc7d4dae1a16a0961dc28;0;0;2;COLOR;0;FLOAT;245
Node;AmplifyShaderEditor.RangedFloatNode;600;-432,224;Inherit;False;Global;CZY_SkyFogAmount;CZY_SkyFogAmount;1;0;Create;True;0;0;0;False;0;False;0;0.695;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;599;-192,128;Inherit;False;AddFogToSkyLayer;-1;;138;36a78fe96c9f6fa4dab85c7793736468;0;3;89;COLOR;0,0,0,0;False;91;FLOAT;0;False;59;FLOAT;0;False;2;COLOR;84;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;104.1543,85.88161;Float;False;True;-1;2;DistantLands.Cozy.EditorScripts.EmptyShaderGUI;0;0;Unlit;Distant Lands/Cozy/BiRP/Stylized Sky (Desktop);False;False;False;False;True;True;True;True;True;True;False;False;False;False;False;False;False;False;False;False;False;Front;0;False;;7;False;;False;0;False;;0;False;;True;0;Translucent;0.5;True;False;-99;False;Opaque;;Transparent;All;12;all;True;True;True;True;0;False;;True;220;False;;255;False;;255;False;;7;False;;3;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;False;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;599;89;598;0
WireConnection;599;59;600;0
WireConnection;0;2;599;84
ASEEND*/
//CHKSM=EF81AC3692CA8BC141BC70A568EE1A49883FEE94