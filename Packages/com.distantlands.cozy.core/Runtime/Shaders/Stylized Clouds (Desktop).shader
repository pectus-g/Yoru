// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Distant Lands/Cozy/BiRP/Stylized Clouds (Desktop)"
{
	Properties
	{
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "TransparentCutout"  "Queue" = "Transparent-50" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull Front
		Stencil
		{
			Ref 221
			Comp Always
			Pass Replace
		}
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#define ASE_VERSION 19801
		#pragma surface surf Unlit keepalpha addshadow fullforwardshadows exclude_path:deferred nofog 
		struct Input
		{
			float2 uv_texcoord;
			float3 worldPos;
		};

		uniform float4 CZY_CloudColor;
		uniform float CZY_FilterSaturation;
		uniform float CZY_FilterValue;
		uniform float4 CZY_FilterColor;
		uniform float4 CZY_CloudFilterColor;
		uniform float4 CZY_CloudHighlightColor;
		uniform float4 CZY_SunFilterColor;
		uniform float CZY_WindSpeed;
		uniform float CZY_MainCloudScale;
		uniform float CZY_CumulusCoverageMultiplier;
		uniform float3 CZY_SunDirection;
		uniform half CZY_SunFlareFalloff;
		uniform float3 CZY_MoonDirection;
		uniform half CZY_CloudMoonFalloff;
		uniform float4 CZY_CloudMoonColor;
		uniform float CZY_DetailScale;
		uniform float CZY_DetailAmount;
		uniform float CZY_BorderHeight;
		uniform float CZY_BorderVariation;
		uniform float CZY_BorderEffect;
		uniform float3 CZY_StormDirection;
		uniform float CZY_NimbusHeight;
		uniform float CZY_NimbusMultiplier;
		uniform float CZY_NimbusVariation;
		uniform sampler2D CZY_ChemtrailsTexture;
		uniform float CZY_ChemtrailsMoveSpeed;
		uniform float CZY_ChemtrailsMultiplier;
		uniform sampler2D CZY_CirrusTexture;
		uniform float CZY_CirrusMoveSpeed;
		uniform float CZY_CirrusMultiplier;
		uniform float CZY_ClippingThreshold;
		uniform float4 CZY_AltoCloudColor;
		uniform sampler2D CZY_AltocumulusTexture;
		uniform float2 CZY_AltocumulusWindSpeed;
		uniform float CZY_AltocumulusScale;
		uniform float CZY_AltocumulusMultiplier;
		uniform sampler2D CZY_CirrostratusTexture;
		uniform float CZY_CirrostratusMoveSpeed;
		uniform float CZY_CirrostratusMultiplier;
		uniform float4 CZY_LightColor;
		uniform float4 CZY_FogColor5;
		uniform float CZY_LightFlareSquish;
		uniform half CZY_LightIntensity;
		uniform half CZY_LightFalloff;
		uniform float CZY_CloudsFogLightAmount;
		uniform float4 CZY_FogMoonFlareColor;
		uniform float CZY_CloudsFogAmount;
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

		float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }

		float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }

		float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }

		float snoise( float2 v )
		{
			const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
			float2 i = floor( v + dot( v, C.yy ) );
			float2 x0 = v - i + dot( i, C.xx );
			float2 i1;
			i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
			float4 x12 = x0.xyxy + C.xxzz;
			x12.xy -= i1;
			i = mod2D289( i );
			float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
			float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
			m = m * m;
			m = m * m;
			float3 x = 2.0 * frac( p * C.www ) - 1.0;
			float3 h = abs( x ) - 0.5;
			float3 ox = floor( x + 0.5 );
			float3 a0 = x - ox;
			m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
			float3 g;
			g.x = a0.x * x0.x + h.x * x0.y;
			g.yz = a0.yz * x12.xz + h.yz * x12.yw;
			return 130.0 * dot( m, g );
		}


		float2 voronoihash81_g5( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi81_g5( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
		{
			float2 n = floor( v );
			float2 f = frac( v );
			float F1 = 8.0;
			float F2 = 8.0; float2 mg = 0;
			for ( int j = -1; j <= 1; j++ )
			{
				for ( int i = -1; i <= 1; i++ )
			 	{
			 		float2 g = float2( i, j );
			 		float2 o = voronoihash81_g5( n + g );
					o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
					float d = 0.5 * dot( r, r );
			 		if( d<F1 ) {
			 			F2 = F1;
			 			F1 = d; mg = g; mr = r; id = o;
			 		} else if( d<F2 ) {
			 			F2 = d;
			
			 		}
			 	}
			}
			return F1;
		}


		float2 voronoihash88_g5( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi88_g5( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
		{
			float2 n = floor( v );
			float2 f = frac( v );
			float F1 = 8.0;
			float F2 = 8.0; float2 mg = 0;
			for ( int j = -1; j <= 1; j++ )
			{
				for ( int i = -1; i <= 1; i++ )
			 	{
			 		float2 g = float2( i, j );
			 		float2 o = voronoihash88_g5( n + g );
					o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
					float d = 0.5 * dot( r, r );
			 		if( d<F1 ) {
			 			F2 = F1;
			 			F1 = d; mg = g; mr = r; id = o;
			 		} else if( d<F2 ) {
			 			F2 = d;
			
			 		}
			 	}
			}
			return F1;
		}


		float2 voronoihash200_g5( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi200_g5( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
		{
			float2 n = floor( v );
			float2 f = frac( v );
			float F1 = 8.0;
			float F2 = 8.0; float2 mg = 0;
			for ( int j = -1; j <= 1; j++ )
			{
				for ( int i = -1; i <= 1; i++ )
			 	{
			 		float2 g = float2( i, j );
			 		float2 o = voronoihash200_g5( n + g );
					o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
					float d = 0.5 * dot( r, r );
			 		if( d<F1 ) {
			 			F2 = F1;
			 			F1 = d; mg = g; mr = r; id = o;
			 		} else if( d<F2 ) {
			 			F2 = d;
			
			 		}
			 	}
			}
			return (F2 + F1) * 0.5;
		}


		float2 voronoihash232_g5( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi232_g5( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
		{
			float2 n = floor( v );
			float2 f = frac( v );
			float F1 = 8.0;
			float F2 = 8.0; float2 mg = 0;
			for ( int j = -1; j <= 1; j++ )
			{
				for ( int i = -1; i <= 1; i++ )
			 	{
			 		float2 g = float2( i, j );
			 		float2 o = voronoihash232_g5( n + g );
					o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
					float d = 0.5 * dot( r, r );
			 		if( d<F1 ) {
			 			F2 = F1;
			 			F1 = d; mg = g; mr = r; id = o;
			 		} else if( d<F2 ) {
			 			F2 = d;
			
			 		}
			 	}
			}
			return F1;
		}


		float2 voronoihash84_g5( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi84_g5( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
		{
			float2 n = floor( v );
			float2 f = frac( v );
			float F1 = 8.0;
			float F2 = 8.0; float2 mg = 0;
			for ( int j = -1; j <= 1; j++ )
			{
				for ( int i = -1; i <= 1; i++ )
			 	{
			 		float2 g = float2( i, j );
			 		float2 o = voronoihash84_g5( n + g );
					o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
					float d = 0.5 * dot( r, r );
			 		if( d<F1 ) {
			 			F2 = F1;
			 			F1 = d; mg = g; mr = r; id = o;
			 		} else if( d<F2 ) {
			 			F2 = d;
			
			 		}
			 	}
			}
			return F1;
		}


		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float3 hsvTorgb2_g6 = RGBToHSV( CZY_CloudColor.rgb );
			float3 hsvTorgb3_g6 = HSVToRGB( float3(hsvTorgb2_g6.x,saturate( ( hsvTorgb2_g6.y + CZY_FilterSaturation ) ),( hsvTorgb2_g6.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g6 = ( float4( hsvTorgb3_g6 , 0.0 ) * CZY_FilterColor );
			float4 CloudColor41_g5 = ( temp_output_10_0_g6 * CZY_CloudFilterColor );
			float3 hsvTorgb2_g66 = RGBToHSV( CZY_CloudHighlightColor.rgb );
			float3 hsvTorgb3_g66 = HSVToRGB( float3(hsvTorgb2_g66.x,saturate( ( hsvTorgb2_g66.y + CZY_FilterSaturation ) ),( hsvTorgb2_g66.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g66 = ( float4( hsvTorgb3_g66 , 0.0 ) * CZY_FilterColor );
			float4 CloudHighlightColor55_g5 = ( temp_output_10_0_g66 * CZY_SunFilterColor );
			float2 Pos33_g5 = i.uv_texcoord;
			float mulTime29_g5 = _Time.y * ( 0.001 * CZY_WindSpeed );
			float TIme30_g5 = mulTime29_g5;
			float simplePerlin2D409_g5 = snoise( ( Pos33_g5 + ( TIme30_g5 * float2( 0.2,-0.4 ) ) )*( 100.0 / CZY_MainCloudScale ) );
			simplePerlin2D409_g5 = simplePerlin2D409_g5*0.5 + 0.5;
			float SimpleCloudDensity153_g5 = simplePerlin2D409_g5;
			float time81_g5 = 0.0;
			float2 voronoiSmoothId81_g5 = 0;
			float2 temp_output_94_0_g5 = ( Pos33_g5 + ( TIme30_g5 * float2( 0.3,0.2 ) ) );
			float2 coords81_g5 = temp_output_94_0_g5 * ( 140.0 / CZY_MainCloudScale );
			float2 id81_g5 = 0;
			float2 uv81_g5 = 0;
			float voroi81_g5 = voronoi81_g5( coords81_g5, time81_g5, id81_g5, uv81_g5, 0, voronoiSmoothId81_g5 );
			float time88_g5 = 0.0;
			float2 voronoiSmoothId88_g5 = 0;
			float2 coords88_g5 = temp_output_94_0_g5 * ( 500.0 / CZY_MainCloudScale );
			float2 id88_g5 = 0;
			float2 uv88_g5 = 0;
			float voroi88_g5 = voronoi88_g5( coords88_g5, time88_g5, id88_g5, uv88_g5, 0, voronoiSmoothId88_g5 );
			float2 appendResult95_g5 = (float2(voroi81_g5 , voroi88_g5));
			float2 VoroDetails109_g5 = appendResult95_g5;
			float CumulusCoverage34_g5 = CZY_CumulusCoverageMultiplier;
			float ComplexCloudDensity141_g5 = (0.0 + (min( SimpleCloudDensity153_g5 , ( 1.0 - VoroDetails109_g5.x ) ) - ( 1.0 - CumulusCoverage34_g5 )) * (1.0 - 0.0) / (1.0 - ( 1.0 - CumulusCoverage34_g5 )));
			float4 lerpResult315_g5 = lerp( CloudHighlightColor55_g5 , CloudColor41_g5 , saturate( (2.0 + (ComplexCloudDensity141_g5 - 0.0) * (0.7 - 2.0) / (1.0 - 0.0)) ));
			float3 ase_positionWS = i.worldPos;
			float3 normalizeResult40_g5 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float dotResult42_g5 = dot( normalizeResult40_g5 , CZY_SunDirection );
			float temp_output_49_0_g5 = abs( (dotResult42_g5*0.5 + 0.5) );
			half LightMask56_g5 = saturate( pow( temp_output_49_0_g5 , CZY_SunFlareFalloff ) );
			float time200_g5 = 0.0;
			float2 voronoiSmoothId200_g5 = 0;
			float mulTime163_g5 = _Time.y * 0.003;
			float2 coords200_g5 = (Pos33_g5*1.0 + ( float2( 1,-2 ) * mulTime163_g5 )) * 10.0;
			float2 id200_g5 = 0;
			float2 uv200_g5 = 0;
			float voroi200_g5 = voronoi200_g5( coords200_g5, time200_g5, id200_g5, uv200_g5, 0, voronoiSmoothId200_g5 );
			float time232_g5 = ( 10.0 * mulTime163_g5 );
			float2 voronoiSmoothId232_g5 = 0;
			float2 coords232_g5 = i.uv_texcoord * 10.0;
			float2 id232_g5 = 0;
			float2 uv232_g5 = 0;
			float voroi232_g5 = voronoi232_g5( coords232_g5, time232_g5, id232_g5, uv232_g5, 0, voronoiSmoothId232_g5 );
			float temp_output_242_0_g5 = ( ( ( 1.0 - 0.0 ) - (1.0 + (voroi200_g5 - 0.0) * (-0.5 - 1.0) / (1.0 - 0.0)) ) - voroi232_g5 );
			float AltoCumulusPlacement376_g5 = temp_output_242_0_g5;
			float CloudThicknessDetails286_g5 = ( VoroDetails109_g5.y * saturate( ( AltoCumulusPlacement376_g5 - 0.26 ) ) );
			float3 normalizeResult43_g5 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float dotResult46_g5 = dot( normalizeResult43_g5 , CZY_MoonDirection );
			half MoonlightMask57_g5 = saturate( pow( abs( (dotResult46_g5*0.5 + 0.5) ) , CZY_CloudMoonFalloff ) );
			float3 hsvTorgb2_g7 = RGBToHSV( CZY_CloudMoonColor.rgb );
			float3 hsvTorgb3_g7 = HSVToRGB( float3(hsvTorgb2_g7.x,saturate( ( hsvTorgb2_g7.y + CZY_FilterSaturation ) ),( hsvTorgb2_g7.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g7 = ( float4( hsvTorgb3_g7 , 0.0 ) * CZY_FilterColor );
			float4 MoonlightColor60_g5 = ( temp_output_10_0_g7 * CZY_CloudFilterColor );
			float4 lerpResult338_g5 = lerp( ( lerpResult315_g5 + ( LightMask56_g5 * CloudHighlightColor55_g5 * ( 1.0 - CloudThicknessDetails286_g5 ) ) + ( MoonlightMask57_g5 * MoonlightColor60_g5 * ( 1.0 - CloudThicknessDetails286_g5 ) ) ) , ( CloudColor41_g5 * float4( 0.5660378,0.5660378,0.5660378,0 ) ) , CloudThicknessDetails286_g5);
			float time84_g5 = 0.0;
			float2 voronoiSmoothId84_g5 = 0;
			float2 coords84_g5 = ( Pos33_g5 + ( TIme30_g5 * float2( 0.3,0.2 ) ) ) * ( 100.0 / CZY_DetailScale );
			float2 id84_g5 = 0;
			float2 uv84_g5 = 0;
			float fade84_g5 = 0.5;
			float voroi84_g5 = 0;
			float rest84_g5 = 0;
			for( int it84_g5 = 0; it84_g5 <3; it84_g5++ ){
			voroi84_g5 += fade84_g5 * voronoi84_g5( coords84_g5, time84_g5, id84_g5, uv84_g5, 0,voronoiSmoothId84_g5 );
			rest84_g5 += fade84_g5;
			coords84_g5 *= 2;
			fade84_g5 *= 0.5;
			}//Voronoi84_g5
			voroi84_g5 /= rest84_g5;
			float temp_output_173_0_g5 = ( (0.0 + (( 1.0 - voroi84_g5 ) - 0.3) * (0.5 - 0.0) / (1.0 - 0.3)) * 0.1 * CZY_DetailAmount );
			float DetailedClouds252_g5 = saturate( ( ComplexCloudDensity141_g5 + temp_output_173_0_g5 ) );
			float CloudDetail179_g5 = temp_output_173_0_g5;
			float2 temp_output_161_0_g5 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult212_g5 = dot( temp_output_161_0_g5 , temp_output_161_0_g5 );
			float BorderHeight154_g5 = ( 1.0 - CZY_BorderHeight );
			float temp_output_151_0_g5 = ( -2.0 * ( 1.0 - CZY_BorderVariation ) );
			float clampResult247_g5 = clamp( ( ( ( CloudDetail179_g5 + SimpleCloudDensity153_g5 ) * saturate( (( BorderHeight154_g5 * temp_output_151_0_g5 ) + (dotResult212_g5 - 0.0) * (( temp_output_151_0_g5 * -4.0 ) - ( BorderHeight154_g5 * temp_output_151_0_g5 )) / (0.5 - 0.0)) ) ) * 10.0 * CZY_BorderEffect ) , -1.0 , 1.0 );
			float BorderLightTransport278_g5 = clampResult247_g5;
			float3 normalizeResult116_g5 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float3 normalizeResult146_g5 = normalize( CZY_StormDirection );
			float dotResult150_g5 = dot( normalizeResult116_g5 , normalizeResult146_g5 );
			float2 temp_output_124_0_g5 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult125_g5 = dot( temp_output_124_0_g5 , temp_output_124_0_g5 );
			float temp_output_140_0_g5 = ( -2.0 * ( 1.0 - ( CZY_NimbusVariation * 0.9 ) ) );
			float NimbusLightTransport269_g5 = saturate( ( ( ( CloudDetail179_g5 + SimpleCloudDensity153_g5 ) * saturate( (( ( 1.0 - CZY_NimbusMultiplier ) * temp_output_140_0_g5 ) + (( dotResult150_g5 + ( CZY_NimbusHeight * 4.0 * dotResult125_g5 ) ) - 0.5) * (( temp_output_140_0_g5 * -4.0 ) - ( ( 1.0 - CZY_NimbusMultiplier ) * temp_output_140_0_g5 )) / (7.0 - 0.5)) ) ) * 10.0 ) );
			float mulTime104_g5 = _Time.y * 0.01;
			float simplePerlin2D143_g5 = snoise( (Pos33_g5*1.0 + mulTime104_g5)*2.0 );
			float mulTime93_g5 = _Time.y * CZY_ChemtrailsMoveSpeed;
			float cos97_g5 = cos( ( mulTime93_g5 * 0.01 ) );
			float sin97_g5 = sin( ( mulTime93_g5 * 0.01 ) );
			float2 rotator97_g5 = mul( Pos33_g5 - float2( 0.5,0.5 ) , float2x2( cos97_g5 , -sin97_g5 , sin97_g5 , cos97_g5 )) + float2( 0.5,0.5 );
			float cos131_g5 = cos( ( mulTime93_g5 * -0.02 ) );
			float sin131_g5 = sin( ( mulTime93_g5 * -0.02 ) );
			float2 rotator131_g5 = mul( Pos33_g5 - float2( 0.5,0.5 ) , float2x2( cos131_g5 , -sin131_g5 , sin131_g5 , cos131_g5 )) + float2( 0.5,0.5 );
			float mulTime107_g5 = _Time.y * 0.01;
			float simplePerlin2D147_g5 = snoise( (Pos33_g5*1.0 + mulTime107_g5)*4.0 );
			float4 ChemtrailsPattern210_g5 = ( ( saturate( simplePerlin2D143_g5 ) * tex2D( CZY_ChemtrailsTexture, (rotator97_g5*0.5 + 0.0) ) ) + ( tex2D( CZY_ChemtrailsTexture, rotator131_g5 ) * saturate( simplePerlin2D147_g5 ) ) );
			float2 temp_output_162_0_g5 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult207_g5 = dot( temp_output_162_0_g5 , temp_output_162_0_g5 );
			float ChemtrailsFinal248_g5 = ( ( ChemtrailsPattern210_g5 * saturate( (0.4 + (dotResult207_g5 - 0.0) * (2.0 - 0.4) / (0.1 - 0.0)) ) ).r > ( 1.0 - ( CZY_ChemtrailsMultiplier * 0.5 ) ) ? 1.0 : 0.0 );
			float mulTime80_g5 = _Time.y * 0.01;
			float simplePerlin2D126_g5 = snoise( (Pos33_g5*1.0 + mulTime80_g5)*2.0 );
			float mulTime75_g5 = _Time.y * CZY_CirrusMoveSpeed;
			float cos101_g5 = cos( ( mulTime75_g5 * 0.01 ) );
			float sin101_g5 = sin( ( mulTime75_g5 * 0.01 ) );
			float2 rotator101_g5 = mul( Pos33_g5 - float2( 0.5,0.5 ) , float2x2( cos101_g5 , -sin101_g5 , sin101_g5 , cos101_g5 )) + float2( 0.5,0.5 );
			float cos112_g5 = cos( ( mulTime75_g5 * -0.02 ) );
			float sin112_g5 = sin( ( mulTime75_g5 * -0.02 ) );
			float2 rotator112_g5 = mul( Pos33_g5 - float2( 0.5,0.5 ) , float2x2( cos112_g5 , -sin112_g5 , sin112_g5 , cos112_g5 )) + float2( 0.5,0.5 );
			float mulTime135_g5 = _Time.y * 0.01;
			float simplePerlin2D122_g5 = snoise( (Pos33_g5*1.0 + mulTime135_g5) );
			simplePerlin2D122_g5 = simplePerlin2D122_g5*0.5 + 0.5;
			float4 CirrusPattern137_g5 = ( ( saturate( simplePerlin2D126_g5 ) * tex2D( CZY_CirrusTexture, (rotator101_g5*1.5 + 0.75) ) ) + ( tex2D( CZY_CirrusTexture, (rotator112_g5*1.0 + 0.0) ) * saturate( simplePerlin2D122_g5 ) ) );
			float2 temp_output_164_0_g5 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult157_g5 = dot( temp_output_164_0_g5 , temp_output_164_0_g5 );
			float4 temp_output_217_0_g5 = ( CirrusPattern137_g5 * saturate( (0.0 + (dotResult157_g5 - 0.0) * (2.0 - 0.0) / (0.2 - 0.0)) ) );
			float Clipping208_g5 = CZY_ClippingThreshold;
			float CirrusAlpha250_g5 = ( ( temp_output_217_0_g5 * ( CZY_CirrusMultiplier * 10.0 ) ).r > Clipping208_g5 ? 1.0 : 0.0 );
			float SimpleRadiance268_g5 = saturate( ( DetailedClouds252_g5 + BorderLightTransport278_g5 + NimbusLightTransport269_g5 + ChemtrailsFinal248_g5 + CirrusAlpha250_g5 ) );
			float4 lerpResult342_g5 = lerp( CloudColor41_g5 , lerpResult338_g5 , ( 1.0 - SimpleRadiance268_g5 ));
			float CloudbreakLightDir426_g5 = saturate( pow( temp_output_49_0_g5 , ( CZY_SunFlareFalloff * 0.5 ) ) );
			float lerpResult316_g5 = lerp( -0.4 , 1.0 , ( saturate( ( ComplexCloudDensity141_g5 - 0.0 ) ) * CloudDetail179_g5 * CloudbreakLightDir426_g5 ));
			float SunThroughClouds399_g5 = saturate( lerpResult316_g5 );
			float3 hsvTorgb2_g8 = RGBToHSV( CZY_AltoCloudColor.rgb );
			float3 hsvTorgb3_g8 = HSVToRGB( float3(hsvTorgb2_g8.x,saturate( ( hsvTorgb2_g8.y + CZY_FilterSaturation ) ),( hsvTorgb2_g8.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g8 = ( float4( hsvTorgb3_g8 , 0.0 ) * CZY_FilterColor );
			float4 CirrusCustomLightColor350_g5 = ( CloudColor41_g5 * ( temp_output_10_0_g8 * CZY_CloudFilterColor ) );
			float temp_output_391_0_g5 = ( AltoCumulusPlacement376_g5 * (0.0 + (tex2D( CZY_AltocumulusTexture, ((Pos33_g5*1.0 + ( CZY_AltocumulusWindSpeed * TIme30_g5 ))*( 1.0 / CZY_AltocumulusScale ) + 0.0) ).r - 0.0) * (1.0 - 0.0) / (0.2 - 0.0)) * CZY_AltocumulusMultiplier );
			float AltoCumulusLightTransport393_g5 = temp_output_391_0_g5;
			float ACCustomLightsClipping387_g5 = ( AltoCumulusLightTransport393_g5 * ( SimpleRadiance268_g5 > Clipping208_g5 ? 0.0 : 1.0 ) );
			float mulTime193_g5 = _Time.y * 0.01;
			float simplePerlin2D224_g5 = snoise( (Pos33_g5*1.0 + mulTime193_g5)*2.0 );
			float mulTime178_g5 = _Time.y * CZY_CirrostratusMoveSpeed;
			float cos138_g5 = cos( ( mulTime178_g5 * 0.01 ) );
			float sin138_g5 = sin( ( mulTime178_g5 * 0.01 ) );
			float2 rotator138_g5 = mul( Pos33_g5 - float2( 0.5,0.5 ) , float2x2( cos138_g5 , -sin138_g5 , sin138_g5 , cos138_g5 )) + float2( 0.5,0.5 );
			float cos198_g5 = cos( ( mulTime178_g5 * -0.02 ) );
			float sin198_g5 = sin( ( mulTime178_g5 * -0.02 ) );
			float2 rotator198_g5 = mul( Pos33_g5 - float2( 0.5,0.5 ) , float2x2( cos198_g5 , -sin198_g5 , sin198_g5 , cos198_g5 )) + float2( 0.5,0.5 );
			float mulTime184_g5 = _Time.y * 0.01;
			float simplePerlin2D216_g5 = snoise( (Pos33_g5*10.0 + mulTime184_g5)*4.0 );
			float4 CirrostratPattern261_g5 = ( ( saturate( simplePerlin2D224_g5 ) * tex2D( CZY_CirrostratusTexture, (rotator138_g5*1.5 + 0.75) ) ) + ( tex2D( CZY_CirrostratusTexture, (rotator198_g5*1.5 + 0.75) ) * saturate( simplePerlin2D216_g5 ) ) );
			float2 temp_output_243_0_g5 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult238_g5 = dot( temp_output_243_0_g5 , temp_output_243_0_g5 );
			float clampResult264_g5 = clamp( ( CZY_CirrostratusMultiplier * 0.5 ) , 0.0 , 0.98 );
			float CirrostratLightTransport281_g5 = ( ( CirrostratPattern261_g5 * saturate( (0.4 + (dotResult238_g5 - 0.0) * (2.0 - 0.4) / (0.1 - 0.0)) ) ).r > ( 1.0 - clampResult264_g5 ) ? 1.0 : 0.0 );
			float CSCustomLightsClipping309_g5 = ( CirrostratLightTransport281_g5 * ( SimpleRadiance268_g5 > Clipping208_g5 ? 0.0 : 1.0 ) );
			float CustomRadiance340_g5 = saturate( ( ACCustomLightsClipping387_g5 + CSCustomLightsClipping309_g5 ) );
			float4 lerpResult331_g5 = lerp( ( lerpResult342_g5 + SunThroughClouds399_g5 ) , CirrusCustomLightColor350_g5 , CustomRadiance340_g5);
			float FinalAlpha375_g5 = saturate( ( DetailedClouds252_g5 + BorderLightTransport278_g5 + AltoCumulusLightTransport393_g5 + ChemtrailsFinal248_g5 + CirrostratLightTransport281_g5 + CirrusAlpha250_g5 + NimbusLightTransport269_g5 ) );
			float4 appendResult420_g5 = (float4((lerpResult331_g5).rgb , FinalAlpha375_g5));
			float4 FinalCloudColor325_g5 = appendResult420_g5;
			clip( ( ( (FinalCloudColor325_g5).w * ( 1.0 - 0.0 ) ) > Clipping208_g5 ? 1.0 : 0.0 ) - Clipping208_g5);
			float3 hsvTorgb69_g369 = RGBToHSV( CZY_FogColor5.rgb );
			float3 normalizeResult54_g369 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float3 temp_output_56_0_g369 = ( normalizeResult54_g369 * _ProjectionParams.z );
			float3 appendResult25_g369 = (float3(1.0 , CZY_LightFlareSquish , 1.0));
			float3 normalizeResult13_g369 = normalize( ( ( temp_output_56_0_g369 * appendResult25_g369 ) - _WorldSpaceCameraPos ) );
			float dotResult16_g369 = dot( normalizeResult13_g369 , CZY_SunDirection );
			half LightMask35_g369 = saturate( pow( abs( ( (dotResult16_g369*0.5 + 0.5) * CZY_LightIntensity ) ) , CZY_LightFalloff ) );
			float temp_output_91_0_g369 = CZY_CloudsFogLightAmount;
			float3 hsvTorgb2_g371 = RGBToHSV( ( CZY_LightColor * hsvTorgb69_g369.z * saturate( ( LightMask35_g369 * ( 1.5 * CZY_FogColor5.a ) * temp_output_91_0_g369 ) ) ).rgb );
			float3 hsvTorgb3_g371 = HSVToRGB( float3(hsvTorgb2_g371.x,saturate( ( hsvTorgb2_g371.y + CZY_FilterSaturation ) ),( hsvTorgb2_g371.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g371 = ( float4( hsvTorgb3_g371 , 0.0 ) * CZY_FilterColor );
			float3 direction88_g369 = ( temp_output_56_0_g369 - _WorldSpaceCameraPos );
			float3 normalizeResult32_g369 = normalize( direction88_g369 );
			float3 normalizeResult30_g369 = normalize( CZY_MoonDirection );
			float dotResult28_g369 = dot( normalizeResult32_g369 , normalizeResult30_g369 );
			half MoonMask18_g369 = saturate( pow( abs( ( saturate( (dotResult28_g369*1.0 + 0.0) ) * CZY_LightIntensity ) ) , ( CZY_LightFalloff * 3.0 ) ) );
			float3 hsvTorgb2_g370 = RGBToHSV( ( CZY_FogColor5 + ( hsvTorgb69_g369.z * saturate( ( CZY_FogColor5.a * MoonMask18_g369 * temp_output_91_0_g369 ) ) * CZY_FogMoonFlareColor ) ).rgb );
			float3 hsvTorgb3_g370 = HSVToRGB( float3(hsvTorgb2_g370.x,saturate( ( hsvTorgb2_g370.y + CZY_FilterSaturation ) ),( hsvTorgb2_g370.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g370 = ( float4( hsvTorgb3_g370 , 0.0 ) * CZY_FilterColor );
			float3 ase_objectScale = float3( length( unity_ObjectToWorld[ 0 ].xyz ), length( unity_ObjectToWorld[ 1 ].xyz ), length( unity_ObjectToWorld[ 2 ].xyz ) );
			float temp_output_34_0_g369 = ( CZY_CloudsFogAmount * saturate( ( ( 1.0 - saturate( ( ( ( direction88_g369.y * 0.1 ) * ( 1.0 / ( ( CZY_FogSmoothness * length( ase_objectScale ) ) * 10.0 ) ) ) + ( 1.0 - CZY_FogOffset ) ) ) ) * CZY_FogIntensity ) ) );
			float4 lerpResult90_g369 = lerp( FinalCloudColor325_g5 , ( ( temp_output_10_0_g371 * CZY_SunFilterColor ) + temp_output_10_0_g370 ) , temp_output_34_0_g369);
			o.Emission = lerpResult90_g369.rgb;
			o.Alpha = 1;
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "DistantLands.Cozy.EditorScripts.EmptyShaderGUI"
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.FunctionNode;1209;-1424,-624;Inherit;False;Stylized Clouds (Desktop);1;;5;b8040dba3255391449edffa0921d9c37;0;0;3;FLOAT4;0;FLOAT;414;FLOAT;415
Node;AmplifyShaderEditor.ClipNode;1211;-1168,-624;Inherit;False;3;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode;1214;-1328,-416;Inherit;False;Global;CZY_CloudsFogAmount;CZY_CloudsFogAmount;8;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1215;-1328,-496;Inherit;False;Global;CZY_CloudsFogLightAmount;CZY_CloudsFogLightAmount;7;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;1213;-992,-624;Inherit;False;AddFogToSkyLayer;-1;;369;36a78fe96c9f6fa4dab85c7793736468;0;3;89;COLOR;0,0,0,0;False;91;FLOAT;0;False;59;FLOAT;0;False;2;COLOR;84;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;-678.2959,-671.1561;Float;False;True;-1;2;DistantLands.Cozy.EditorScripts.EmptyShaderGUI;0;0;Unlit;Distant Lands/Cozy/BiRP/Stylized Clouds (Desktop);False;False;False;False;False;False;False;False;False;True;False;False;False;False;True;False;False;False;False;False;False;Front;0;False;;0;False;;False;0;False;;0;False;;False;0;Custom;0.5;True;True;-50;True;TransparentCutout;;Transparent;ForwardOnly;12;all;True;True;True;True;0;False;;True;221;False;;255;False;;255;False;;7;False;;3;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;5;False;;10;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;0;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;1211;0;1209;0
WireConnection;1211;1;1209;414
WireConnection;1211;2;1209;415
WireConnection;1213;89;1211;0
WireConnection;1213;91;1215;0
WireConnection;1213;59;1214;0
WireConnection;0;2;1213;84
ASEEND*/
//CHKSM=5B247A2C5CEC6DA26B8EDE819BD27AD31BEF1C43