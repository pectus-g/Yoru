// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Distant Lands/Cozy/BiRP/Stylized Clouds (Soft)"
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
		Blend SrcAlpha OneMinusSrcAlpha
		
		CGINCLUDE
		#include "UnityShaderVariables.cginc"
		#include "UnityPBSLighting.cginc"
		#include "Lighting.cginc"
		#pragma target 3.0
		#define ASE_VERSION 19801
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
		uniform half CZY_MoonFlareFalloff;
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
		uniform half CZY_CloudFlareFalloff;
		uniform float4 CZY_AltoCloudColor;
		uniform float CZY_AltocumulusScale;
		uniform float2 CZY_AltocumulusWindSpeed;
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
		uniform float CZY_CloudThickness;


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


		float2 voronoihash84_g77( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi84_g77( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash84_g77( n + g );
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


		float2 voronoihash91_g77( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi91_g77( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash91_g77( n + g );
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


		float2 voronoihash87_g77( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi87_g77( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash87_g77( n + g );
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


		float2 voronoihash201_g77( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi201_g77( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash201_g77( n + g );
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


		float2 voronoihash234_g77( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi234_g77( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash234_g77( n + g );
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


		float2 voronoihash287_g77( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi287_g77( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash287_g77( n + g );
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
			float3 hsvTorgb2_g79 = RGBToHSV( CZY_CloudColor.rgb );
			float3 hsvTorgb3_g79 = HSVToRGB( float3(hsvTorgb2_g79.x,saturate( ( hsvTorgb2_g79.y + CZY_FilterSaturation ) ),( hsvTorgb2_g79.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g79 = ( float4( hsvTorgb3_g79 , 0.0 ) * CZY_FilterColor );
			float4 CloudColor41_g77 = ( temp_output_10_0_g79 * CZY_CloudFilterColor );
			float3 hsvTorgb2_g78 = RGBToHSV( CZY_CloudHighlightColor.rgb );
			float3 hsvTorgb3_g78 = HSVToRGB( float3(hsvTorgb2_g78.x,saturate( ( hsvTorgb2_g78.y + CZY_FilterSaturation ) ),( hsvTorgb2_g78.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g78 = ( float4( hsvTorgb3_g78 , 0.0 ) * CZY_FilterColor );
			float4 CloudHighlightColor56_g77 = ( temp_output_10_0_g78 * CZY_SunFilterColor );
			float2 Pos33_g77 = i.uv_texcoord;
			float mulTime29_g77 = _Time.y * ( 0.001 * CZY_WindSpeed );
			float TIme30_g77 = mulTime29_g77;
			float simplePerlin2D123_g77 = snoise( ( Pos33_g77 + ( TIme30_g77 * float2( 0.2,-0.4 ) ) )*( 100.0 / CZY_MainCloudScale ) );
			simplePerlin2D123_g77 = simplePerlin2D123_g77*0.5 + 0.5;
			float SimpleCloudDensity155_g77 = simplePerlin2D123_g77;
			float time84_g77 = 0.0;
			float2 voronoiSmoothId84_g77 = 0;
			float2 temp_output_97_0_g77 = ( Pos33_g77 + ( TIme30_g77 * float2( 0.3,0.2 ) ) );
			float2 coords84_g77 = temp_output_97_0_g77 * ( 140.0 / CZY_MainCloudScale );
			float2 id84_g77 = 0;
			float2 uv84_g77 = 0;
			float voroi84_g77 = voronoi84_g77( coords84_g77, time84_g77, id84_g77, uv84_g77, 0, voronoiSmoothId84_g77 );
			float time91_g77 = 0.0;
			float2 voronoiSmoothId91_g77 = 0;
			float2 coords91_g77 = temp_output_97_0_g77 * ( 500.0 / CZY_MainCloudScale );
			float2 id91_g77 = 0;
			float2 uv91_g77 = 0;
			float voroi91_g77 = voronoi91_g77( coords91_g77, time91_g77, id91_g77, uv91_g77, 0, voronoiSmoothId91_g77 );
			float2 appendResult98_g77 = (float2(voroi84_g77 , voroi91_g77));
			float2 VoroDetails112_g77 = appendResult98_g77;
			float CumulusCoverage34_g77 = CZY_CumulusCoverageMultiplier;
			float ComplexCloudDensity144_g77 = (0.0 + (min( SimpleCloudDensity155_g77 , ( 1.0 - VoroDetails112_g77.x ) ) - ( 1.0 - CumulusCoverage34_g77 )) * (1.0 - 0.0) / (1.0 - ( 1.0 - CumulusCoverage34_g77 )));
			float4 lerpResult334_g77 = lerp( CloudHighlightColor56_g77 , CloudColor41_g77 , saturate( (2.0 + (ComplexCloudDensity144_g77 - 0.0) * (0.7 - 2.0) / (1.0 - 0.0)) ));
			float3 ase_positionWS = i.worldPos;
			float3 normalizeResult40_g77 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float dotResult42_g77 = dot( normalizeResult40_g77 , CZY_SunDirection );
			float temp_output_50_0_g77 = abs( (dotResult42_g77*0.5 + 0.5) );
			half LightMask57_g77 = saturate( pow( temp_output_50_0_g77 , CZY_SunFlareFalloff ) );
			float CloudThicknessDetails301_g77 = ( VoroDetails112_g77.y * saturate( ( CumulusCoverage34_g77 - 0.8 ) ) );
			float3 normalizeResult43_g77 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float dotResult47_g77 = dot( normalizeResult43_g77 , CZY_MoonDirection );
			half MoonlightMask58_g77 = saturate( pow( abs( (dotResult47_g77*0.5 + 0.5) ) , CZY_MoonFlareFalloff ) );
			float3 hsvTorgb2_g80 = RGBToHSV( CZY_CloudMoonColor.rgb );
			float3 hsvTorgb3_g80 = HSVToRGB( float3(hsvTorgb2_g80.x,saturate( ( hsvTorgb2_g80.y + CZY_FilterSaturation ) ),( hsvTorgb2_g80.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g80 = ( float4( hsvTorgb3_g80 , 0.0 ) * CZY_FilterColor );
			float4 MoonlightColor61_g77 = ( temp_output_10_0_g80 * CZY_CloudFilterColor );
			float4 lerpResult357_g77 = lerp( ( lerpResult334_g77 + ( LightMask57_g77 * CloudHighlightColor56_g77 * ( 1.0 - CloudThicknessDetails301_g77 ) ) + ( MoonlightMask58_g77 * MoonlightColor61_g77 * ( 1.0 - CloudThicknessDetails301_g77 ) ) ) , ( CloudColor41_g77 * float4( 0.5660378,0.5660378,0.5660378,0 ) ) , CloudThicknessDetails301_g77);
			float time87_g77 = 0.0;
			float2 voronoiSmoothId87_g77 = 0;
			float2 coords87_g77 = ( Pos33_g77 + ( TIme30_g77 * float2( 0.3,0.2 ) ) ) * ( 100.0 / CZY_DetailScale );
			float2 id87_g77 = 0;
			float2 uv87_g77 = 0;
			float fade87_g77 = 0.5;
			float voroi87_g77 = 0;
			float rest87_g77 = 0;
			for( int it87_g77 = 0; it87_g77 <3; it87_g77++ ){
			voroi87_g77 += fade87_g77 * voronoi87_g77( coords87_g77, time87_g77, id87_g77, uv87_g77, 0,voronoiSmoothId87_g77 );
			rest87_g77 += fade87_g77;
			coords87_g77 *= 2;
			fade87_g77 *= 0.5;
			}//Voronoi87_g77
			voroi87_g77 /= rest87_g77;
			float temp_output_174_0_g77 = ( (0.0 + (( 1.0 - voroi87_g77 ) - 0.3) * (0.5 - 0.0) / (1.0 - 0.3)) * 0.1 * CZY_DetailAmount );
			float DetailedClouds258_g77 = saturate( ( ComplexCloudDensity144_g77 + temp_output_174_0_g77 ) );
			float CloudDetail180_g77 = temp_output_174_0_g77;
			float2 temp_output_163_0_g77 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult214_g77 = dot( temp_output_163_0_g77 , temp_output_163_0_g77 );
			float BorderHeight156_g77 = ( 1.0 - CZY_BorderHeight );
			float temp_output_153_0_g77 = ( -2.0 * ( 1.0 - CZY_BorderVariation ) );
			float clampResult253_g77 = clamp( ( ( ( CloudDetail180_g77 + SimpleCloudDensity155_g77 ) * saturate( (( BorderHeight156_g77 * temp_output_153_0_g77 ) + (dotResult214_g77 - 0.0) * (( temp_output_153_0_g77 * -4.0 ) - ( BorderHeight156_g77 * temp_output_153_0_g77 )) / (1.0 - 0.0)) ) ) * 10.0 * CZY_BorderEffect ) , -1.0 , 1.0 );
			float BorderLightTransport403_g77 = clampResult253_g77;
			float3 normalizeResult119_g77 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float3 normalizeResult149_g77 = normalize( CZY_StormDirection );
			float dotResult152_g77 = dot( normalizeResult119_g77 , normalizeResult149_g77 );
			float2 temp_output_127_0_g77 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult128_g77 = dot( temp_output_127_0_g77 , temp_output_127_0_g77 );
			float temp_output_143_0_g77 = ( -2.0 * ( 1.0 - ( CZY_NimbusVariation * 0.9 ) ) );
			float NimbusLightTransport280_g77 = saturate( ( ( ( CloudDetail180_g77 + SimpleCloudDensity155_g77 ) * saturate( (( ( 1.0 - CZY_NimbusMultiplier ) * temp_output_143_0_g77 ) + (( dotResult152_g77 + ( CZY_NimbusHeight * 4.0 * dotResult128_g77 ) ) - 0.5) * (( temp_output_143_0_g77 * -4.0 ) - ( ( 1.0 - CZY_NimbusMultiplier ) * temp_output_143_0_g77 )) / (7.0 - 0.5)) ) ) * 10.0 ) );
			float mulTime107_g77 = _Time.y * 0.01;
			float simplePerlin2D146_g77 = snoise( (Pos33_g77*1.0 + mulTime107_g77)*2.0 );
			float mulTime96_g77 = _Time.y * CZY_ChemtrailsMoveSpeed;
			float cos100_g77 = cos( ( mulTime96_g77 * 0.01 ) );
			float sin100_g77 = sin( ( mulTime96_g77 * 0.01 ) );
			float2 rotator100_g77 = mul( Pos33_g77 - float2( 0.5,0.5 ) , float2x2( cos100_g77 , -sin100_g77 , sin100_g77 , cos100_g77 )) + float2( 0.5,0.5 );
			float cos134_g77 = cos( ( mulTime96_g77 * -0.02 ) );
			float sin134_g77 = sin( ( mulTime96_g77 * -0.02 ) );
			float2 rotator134_g77 = mul( Pos33_g77 - float2( 0.5,0.5 ) , float2x2( cos134_g77 , -sin134_g77 , sin134_g77 , cos134_g77 )) + float2( 0.5,0.5 );
			float mulTime110_g77 = _Time.y * 0.01;
			float simplePerlin2D150_g77 = snoise( (Pos33_g77*1.0 + mulTime110_g77)*4.0 );
			float4 ChemtrailsPattern212_g77 = ( ( saturate( simplePerlin2D146_g77 ) * tex2D( CZY_ChemtrailsTexture, (rotator100_g77*0.5 + 0.0) ) ) + ( tex2D( CZY_ChemtrailsTexture, rotator134_g77 ) * saturate( simplePerlin2D150_g77 ) ) );
			float2 temp_output_164_0_g77 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult209_g77 = dot( temp_output_164_0_g77 , temp_output_164_0_g77 );
			float ChemtrailsFinal254_g77 = ( ( ChemtrailsPattern212_g77 * saturate( (0.4 + (dotResult209_g77 - 0.0) * (2.0 - 0.4) / (0.1 - 0.0)) ) ).r > ( 1.0 - ( CZY_ChemtrailsMultiplier * 0.5 ) ) ? 1.0 : 0.0 );
			float mulTime83_g77 = _Time.y * 0.01;
			float simplePerlin2D129_g77 = snoise( (Pos33_g77*1.0 + mulTime83_g77)*2.0 );
			float mulTime78_g77 = _Time.y * CZY_CirrusMoveSpeed;
			float cos104_g77 = cos( ( mulTime78_g77 * 0.01 ) );
			float sin104_g77 = sin( ( mulTime78_g77 * 0.01 ) );
			float2 rotator104_g77 = mul( Pos33_g77 - float2( 0.5,0.5 ) , float2x2( cos104_g77 , -sin104_g77 , sin104_g77 , cos104_g77 )) + float2( 0.5,0.5 );
			float cos115_g77 = cos( ( mulTime78_g77 * -0.02 ) );
			float sin115_g77 = sin( ( mulTime78_g77 * -0.02 ) );
			float2 rotator115_g77 = mul( Pos33_g77 - float2( 0.5,0.5 ) , float2x2( cos115_g77 , -sin115_g77 , sin115_g77 , cos115_g77 )) + float2( 0.5,0.5 );
			float mulTime138_g77 = _Time.y * 0.01;
			float simplePerlin2D125_g77 = snoise( (Pos33_g77*1.0 + mulTime138_g77) );
			simplePerlin2D125_g77 = simplePerlin2D125_g77*0.5 + 0.5;
			float4 CirrusPattern140_g77 = ( ( saturate( simplePerlin2D129_g77 ) * tex2D( CZY_CirrusTexture, (rotator104_g77*1.5 + 0.75) ) ) + ( tex2D( CZY_CirrusTexture, (rotator115_g77*1.0 + 0.0) ) * saturate( simplePerlin2D125_g77 ) ) );
			float2 temp_output_166_0_g77 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult159_g77 = dot( temp_output_166_0_g77 , temp_output_166_0_g77 );
			float4 temp_output_219_0_g77 = ( CirrusPattern140_g77 * saturate( (0.0 + (dotResult159_g77 - 0.0) * (2.0 - 0.0) / (0.2 - 0.0)) ) );
			float Clipping210_g77 = CZY_ClippingThreshold;
			float CirrusAlpha256_g77 = ( ( temp_output_219_0_g77 * ( CZY_CirrusMultiplier * 10.0 ) ).r > Clipping210_g77 ? 1.0 : 0.0 );
			float SimpleRadiance279_g77 = saturate( ( DetailedClouds258_g77 + BorderLightTransport403_g77 + NimbusLightTransport280_g77 + ChemtrailsFinal254_g77 + CirrusAlpha256_g77 ) );
			float4 lerpResult361_g77 = lerp( CloudColor41_g77 , lerpResult357_g77 , ( 1.0 - SimpleRadiance279_g77 ));
			float CloudLight53_g77 = saturate( pow( temp_output_50_0_g77 , CZY_CloudFlareFalloff ) );
			float4 lerpResult335_g77 = lerp( float4( 0,0,0,0 ) , CloudHighlightColor56_g77 , ( saturate( ( CumulusCoverage34_g77 - 1.0 ) ) * CloudDetail180_g77 * CloudLight53_g77 ));
			float4 SunThroughClouds326_g77 = ( lerpResult335_g77 * 1.3 );
			float3 hsvTorgb2_g81 = RGBToHSV( CZY_AltoCloudColor.rgb );
			float3 hsvTorgb3_g81 = HSVToRGB( float3(hsvTorgb2_g81.x,saturate( ( hsvTorgb2_g81.y + CZY_FilterSaturation ) ),( hsvTorgb2_g81.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g81 = ( float4( hsvTorgb3_g81 , 0.0 ) * CZY_FilterColor );
			float4 CirrusCustomLightColor369_g77 = ( CloudColor41_g77 * ( temp_output_10_0_g81 * CZY_CloudFilterColor ) );
			float time201_g77 = 0.0;
			float2 voronoiSmoothId201_g77 = 0;
			float mulTime165_g77 = _Time.y * 0.003;
			float2 coords201_g77 = (Pos33_g77*1.0 + ( float2( 1,-2 ) * mulTime165_g77 )) * 10.0;
			float2 id201_g77 = 0;
			float2 uv201_g77 = 0;
			float voroi201_g77 = voronoi201_g77( coords201_g77, time201_g77, id201_g77, uv201_g77, 0, voronoiSmoothId201_g77 );
			float time234_g77 = ( 10.0 * mulTime165_g77 );
			float2 voronoiSmoothId234_g77 = 0;
			float2 coords234_g77 = i.uv_texcoord * 10.0;
			float2 id234_g77 = 0;
			float2 uv234_g77 = 0;
			float voroi234_g77 = voronoi234_g77( coords234_g77, time234_g77, id234_g77, uv234_g77, 0, voronoiSmoothId234_g77 );
			float AltoCumulusPlacement271_g77 = saturate( ( ( ( 1.0 - 0.0 ) - (1.0 + (voroi201_g77 - 0.0) * (-0.5 - 1.0) / (1.0 - 0.0)) ) - voroi234_g77 ) );
			float time287_g77 = 51.2;
			float2 voronoiSmoothId287_g77 = 0;
			float2 coords287_g77 = (Pos33_g77*1.0 + ( CZY_AltocumulusWindSpeed * TIme30_g77 )) * ( 100.0 / CZY_AltocumulusScale );
			float2 id287_g77 = 0;
			float2 uv287_g77 = 0;
			float fade287_g77 = 0.5;
			float voroi287_g77 = 0;
			float rest287_g77 = 0;
			for( int it287_g77 = 0; it287_g77 <2; it287_g77++ ){
			voroi287_g77 += fade287_g77 * voronoi287_g77( coords287_g77, time287_g77, id287_g77, uv287_g77, 0,voronoiSmoothId287_g77 );
			rest287_g77 += fade287_g77;
			coords287_g77 *= 2;
			fade287_g77 *= 0.5;
			}//Voronoi287_g77
			voroi287_g77 /= rest287_g77;
			float AltoCumulusLightTransport300_g77 = ( ( AltoCumulusPlacement271_g77 * ( 0.1 > voroi287_g77 ? (0.5 + (voroi287_g77 - 0.0) * (0.0 - 0.5) / (0.15 - 0.0)) : 0.0 ) * CZY_AltocumulusMultiplier ) > 0.2 ? 1.0 : 0.0 );
			float ACCustomLightsClipping343_g77 = ( AltoCumulusLightTransport300_g77 * ( SimpleRadiance279_g77 > Clipping210_g77 ? 0.0 : 1.0 ) );
			float mulTime194_g77 = _Time.y * 0.01;
			float simplePerlin2D226_g77 = snoise( (Pos33_g77*1.0 + mulTime194_g77)*2.0 );
			float mulTime179_g77 = _Time.y * CZY_CirrostratusMoveSpeed;
			float cos141_g77 = cos( ( mulTime179_g77 * 0.01 ) );
			float sin141_g77 = sin( ( mulTime179_g77 * 0.01 ) );
			float2 rotator141_g77 = mul( Pos33_g77 - float2( 0.5,0.5 ) , float2x2( cos141_g77 , -sin141_g77 , sin141_g77 , cos141_g77 )) + float2( 0.5,0.5 );
			float cos199_g77 = cos( ( mulTime179_g77 * -0.02 ) );
			float sin199_g77 = sin( ( mulTime179_g77 * -0.02 ) );
			float2 rotator199_g77 = mul( Pos33_g77 - float2( 0.5,0.5 ) , float2x2( cos199_g77 , -sin199_g77 , sin199_g77 , cos199_g77 )) + float2( 0.5,0.5 );
			float mulTime185_g77 = _Time.y * 0.01;
			float simplePerlin2D218_g77 = snoise( (Pos33_g77*10.0 + mulTime185_g77)*4.0 );
			float4 CirrostratPattern270_g77 = ( ( saturate( simplePerlin2D226_g77 ) * tex2D( CZY_CirrostratusTexture, (rotator141_g77*1.5 + 0.75) ) ) + ( tex2D( CZY_CirrostratusTexture, (rotator199_g77*1.5 + 0.75) ) * saturate( simplePerlin2D218_g77 ) ) );
			float2 temp_output_249_0_g77 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult243_g77 = dot( temp_output_249_0_g77 , temp_output_249_0_g77 );
			float clampResult274_g77 = clamp( ( CZY_CirrostratusMultiplier * 0.5 ) , 0.0 , 0.98 );
			float CirrostratLightTransport295_g77 = ( ( CirrostratPattern270_g77 * saturate( (0.4 + (dotResult243_g77 - 0.0) * (2.0 - 0.4) / (0.1 - 0.0)) ) ).r > ( 1.0 - clampResult274_g77 ) ? 1.0 : 0.0 );
			float CSCustomLightsClipping328_g77 = ( CirrostratLightTransport295_g77 * ( SimpleRadiance279_g77 > Clipping210_g77 ? 0.0 : 1.0 ) );
			float CustomRadiance359_g77 = saturate( ( ACCustomLightsClipping343_g77 + CSCustomLightsClipping328_g77 ) );
			float4 lerpResult350_g77 = lerp( ( lerpResult361_g77 + SunThroughClouds326_g77 ) , CirrusCustomLightColor369_g77 , CustomRadiance359_g77);
			float4 FinalCloudColor402_g77 = lerpResult350_g77;
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
			float4 lerpResult90_g369 = lerp( FinalCloudColor402_g77 , ( ( temp_output_10_0_g371 * CZY_SunFilterColor ) + temp_output_10_0_g370 ) , temp_output_34_0_g369);
			o.Emission = lerpResult90_g369.rgb;
			float FinalAlpha399_g77 = saturate( ( DetailedClouds258_g77 + BorderLightTransport403_g77 + AltoCumulusLightTransport300_g77 + ChemtrailsFinal254_g77 + CirrostratLightTransport295_g77 + CirrusAlpha256_g77 + NimbusLightTransport280_g77 ) );
			o.Alpha = ( saturate( ( ( CZY_CloudThickness * 2.0 * FinalAlpha399_g77 ) + FinalAlpha399_g77 ) ) * ( 1.0 - 0.0 ) );
		}

		ENDCG
		CGPROGRAM
		#pragma surface surf Unlit keepalpha fullforwardshadows exclude_path:deferred nofog 

		ENDCG
		Pass
		{
			Name "ShadowCaster"
			Tags{ "LightMode" = "ShadowCaster" }
			ZWrite On
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma multi_compile_shadowcaster
			#pragma multi_compile UNITY_PASS_SHADOWCASTER
			#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
			#include "HLSLSupport.cginc"
			#if ( SHADER_API_D3D11 || SHADER_API_GLCORE || SHADER_API_GLES || SHADER_API_GLES3 || SHADER_API_METAL || SHADER_API_VULKAN )
				#define CAN_SKIP_VPOS
			#endif
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityPBSLighting.cginc"
			sampler3D _DitherMaskLOD;
			struct v2f
			{
				V2F_SHADOW_CASTER;
				float2 customPack1 : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
			v2f vert( appdata_full v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID( v );
				UNITY_INITIALIZE_OUTPUT( v2f, o );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );
				UNITY_TRANSFER_INSTANCE_ID( v, o );
				Input customInputData;
				float3 worldPos = mul( unity_ObjectToWorld, v.vertex ).xyz;
				half3 worldNormal = UnityObjectToWorldNormal( v.normal );
				o.customPack1.xy = customInputData.uv_texcoord;
				o.customPack1.xy = v.texcoord;
				o.worldPos = worldPos;
				TRANSFER_SHADOW_CASTER_NORMALOFFSET( o )
				return o;
			}
			half4 frag( v2f IN
			#if !defined( CAN_SKIP_VPOS )
			, UNITY_VPOS_TYPE vpos : VPOS
			#endif
			) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				Input surfIN;
				UNITY_INITIALIZE_OUTPUT( Input, surfIN );
				surfIN.uv_texcoord = IN.customPack1.xy;
				float3 worldPos = IN.worldPos;
				half3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );
				surfIN.worldPos = worldPos;
				SurfaceOutput o;
				UNITY_INITIALIZE_OUTPUT( SurfaceOutput, o )
				surf( surfIN, o );
				#if defined( CAN_SKIP_VPOS )
				float2 vpos = IN.pos;
				#endif
				half alphaRef = tex3D( _DitherMaskLOD, float3( vpos.xy * 0.25, o.Alpha * 0.9375 ) ).a;
				clip( alphaRef - 0.01 );
				SHADOW_CASTER_FRAGMENT( IN )
			}
			ENDCG
		}
	}
	Fallback "Diffuse"
	CustomEditor "DistantLands.Cozy.EditorScripts.EmptyShaderGUI"
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.RangedFloatNode;1212;-1312,-400;Inherit;False;Global;CZY_CloudsFogAmount;CZY_CloudsFogAmount;8;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1213;-1312,-480;Inherit;False;Global;CZY_CloudsFogLightAmount;CZY_CloudsFogLightAmount;7;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;1211;-1344,-640;Inherit;False;Stylized Clouds (Soft);0;;77;ade1d57100c84e341a80e8ca0ed59008;0;0;2;COLOR;0;FLOAT;420
Node;AmplifyShaderEditor.FunctionNode;1214;-976,-608;Inherit;False;AddFogToSkyLayer;-1;;369;36a78fe96c9f6fa4dab85c7793736468;0;3;89;COLOR;0,0,0,0;False;91;FLOAT;0;False;59;FLOAT;0;False;2;COLOR;84;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;-695.2959,-681.1561;Float;False;True;-1;2;DistantLands.Cozy.EditorScripts.EmptyShaderGUI;0;0;Unlit;Distant Lands/Cozy/BiRP/Stylized Clouds (Soft);False;False;False;False;False;False;False;False;False;True;False;False;False;False;True;False;False;False;False;False;False;Front;0;False;;0;False;;False;0;False;;0;False;;False;0;Custom;0.5;True;True;-50;True;TransparentCutout;;Transparent;ForwardOnly;12;all;True;True;True;True;0;False;;True;221;False;;255;False;;255;False;;7;False;;3;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;2;5;False;;10;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;4;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;1214;89;1211;0
WireConnection;1214;91;1213;0
WireConnection;1214;59;1212;0
WireConnection;0;2;1214;84
WireConnection;0;9;1211;420
ASEEND*/
//CHKSM=0ECB26DFD5AEB6E0403C37FA925C663FA2167672