// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Distant Lands/Cozy/BiRP/Stylized Clouds (Painted Skies)"
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
		uniform sampler2D CZY_CirrostratusTexture;
		uniform float CZY_CirrostratusMoveSpeed;
		uniform float CZY_CirrostratusMultiplier;
		uniform float CZY_AltocumulusScale;
		uniform float2 CZY_AltocumulusWindSpeed;
		uniform float CZY_AltocumulusMultiplier;
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
		uniform float3 CZY_MoonDirection;
		uniform half CZY_MoonFlareFalloff;
		uniform float4 CZY_CloudMoonColor;
		uniform sampler2D CZY_CloudTexture;
		uniform float3 CZY_SunDirection;
		uniform half CZY_CloudFlareFalloff;
		uniform float4 CZY_AltoCloudColor;
		uniform float4 CZY_CloudTextureColor;
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
		uniform float CZY_TextureAmount;
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


		float2 voronoihash20_g77( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi20_g77( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash20_g77( n + g );
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


		float2 voronoihash23_g77( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi23_g77( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash23_g77( n + g );
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


		float2 voronoihash135_g77( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi135_g77( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash135_g77( n + g );
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


		float2 voronoihash179_g77( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi179_g77( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash179_g77( n + g );
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


		float2 voronoihash205_g77( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi205_g77( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash205_g77( n + g );
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


		float2 voronoihash32_g77( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi32_g77( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash32_g77( n + g );
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
			float4 CloudColor386_g77 = ( temp_output_10_0_g79 * CZY_CloudFilterColor );
			float3 hsvTorgb2_g78 = RGBToHSV( CZY_CloudHighlightColor.rgb );
			float3 hsvTorgb3_g78 = HSVToRGB( float3(hsvTorgb2_g78.x,saturate( ( hsvTorgb2_g78.y + CZY_FilterSaturation ) ),( hsvTorgb2_g78.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g78 = ( float4( hsvTorgb3_g78 , 0.0 ) * CZY_FilterColor );
			float4 CloudHighlightColor385_g77 = ( temp_output_10_0_g78 * CZY_SunFilterColor );
			float2 Pos10_g77 = i.uv_texcoord;
			float mulTime4_g77 = _Time.y * ( 0.001 * CZY_WindSpeed );
			float TIme6_g77 = mulTime4_g77;
			float simplePerlin2D47_g77 = snoise( ( Pos10_g77 + ( TIme6_g77 * float2( 0.2,-0.4 ) ) )*( 100.0 / CZY_MainCloudScale ) );
			simplePerlin2D47_g77 = simplePerlin2D47_g77*0.5 + 0.5;
			float SimpleCloudDensity52_g77 = simplePerlin2D47_g77;
			float time20_g77 = 0.0;
			float2 voronoiSmoothId20_g77 = 0;
			float2 temp_output_18_0_g77 = ( Pos10_g77 + ( TIme6_g77 * float2( 0.3,0.2 ) ) );
			float2 coords20_g77 = temp_output_18_0_g77 * ( 140.0 / CZY_MainCloudScale );
			float2 id20_g77 = 0;
			float2 uv20_g77 = 0;
			float voroi20_g77 = voronoi20_g77( coords20_g77, time20_g77, id20_g77, uv20_g77, 0, voronoiSmoothId20_g77 );
			float time23_g77 = 0.0;
			float2 voronoiSmoothId23_g77 = 0;
			float2 coords23_g77 = temp_output_18_0_g77 * ( 500.0 / CZY_MainCloudScale );
			float2 id23_g77 = 0;
			float2 uv23_g77 = 0;
			float voroi23_g77 = voronoi23_g77( coords23_g77, time23_g77, id23_g77, uv23_g77, 0, voronoiSmoothId23_g77 );
			float2 appendResult25_g77 = (float2(voroi20_g77 , voroi23_g77));
			float2 VoroDetails33_g77 = appendResult25_g77;
			float CumulusCoverage48_g77 = CZY_CumulusCoverageMultiplier;
			float ComplexCloudDensity114_g77 = (0.0 + (min( SimpleCloudDensity52_g77 , ( 1.0 - VoroDetails33_g77.x ) ) - ( 1.0 - CumulusCoverage48_g77 )) * (1.0 - 0.0) / (1.0 - ( 1.0 - CumulusCoverage48_g77 )));
			float4 lerpResult299_g77 = lerp( CloudHighlightColor385_g77 , CloudColor386_g77 , saturate( (2.0 + (ComplexCloudDensity114_g77 - 0.0) * (0.7 - 2.0) / (1.0 - 0.0)) ));
			float mulTime162_g77 = _Time.y * 0.01;
			float simplePerlin2D194_g77 = snoise( (Pos10_g77*1.0 + mulTime162_g77)*2.0 );
			float mulTime128_g77 = _Time.y * CZY_CirrostratusMoveSpeed;
			float cos172_g77 = cos( ( mulTime128_g77 * 0.01 ) );
			float sin172_g77 = sin( ( mulTime128_g77 * 0.01 ) );
			float2 rotator172_g77 = mul( Pos10_g77 - float2( 0.5,0.5 ) , float2x2( cos172_g77 , -sin172_g77 , sin172_g77 , cos172_g77 )) + float2( 0.5,0.5 );
			float cos163_g77 = cos( ( mulTime128_g77 * -0.02 ) );
			float sin163_g77 = sin( ( mulTime128_g77 * -0.02 ) );
			float2 rotator163_g77 = mul( Pos10_g77 - float2( 0.5,0.5 ) , float2x2( cos163_g77 , -sin163_g77 , sin163_g77 , cos163_g77 )) + float2( 0.5,0.5 );
			float mulTime155_g77 = _Time.y * 0.01;
			float simplePerlin2D192_g77 = snoise( (Pos10_g77*10.0 + mulTime155_g77)*4.0 );
			float4 CirrostratPattern250_g77 = ( ( saturate( simplePerlin2D194_g77 ) * tex2D( CZY_CirrostratusTexture, (rotator172_g77*1.5 + 0.75) ) ) + ( tex2D( CZY_CirrostratusTexture, (rotator163_g77*1.5 + 0.75) ) * saturate( simplePerlin2D192_g77 ) ) );
			float2 temp_output_228_0_g77 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult239_g77 = dot( temp_output_228_0_g77 , temp_output_228_0_g77 );
			float4 CirrostratLightTransport267_g77 = ( CirrostratPattern250_g77 * saturate( (0.4 + (dotResult239_g77 - 0.0) * (2.0 - 0.4) / (0.1 - 0.0)) ) * ( CZY_CirrostratusMultiplier * 1.0 ) );
			float time135_g77 = 0.0;
			float2 voronoiSmoothId135_g77 = 0;
			float mulTime82_g77 = _Time.y * 0.003;
			float2 coords135_g77 = (Pos10_g77*1.0 + ( float2( 1,-2 ) * mulTime82_g77 )) * 10.0;
			float2 id135_g77 = 0;
			float2 uv135_g77 = 0;
			float voroi135_g77 = voronoi135_g77( coords135_g77, time135_g77, id135_g77, uv135_g77, 0, voronoiSmoothId135_g77 );
			float time179_g77 = ( 10.0 * mulTime82_g77 );
			float2 voronoiSmoothId179_g77 = 0;
			float2 coords179_g77 = i.uv_texcoord * 10.0;
			float2 id179_g77 = 0;
			float2 uv179_g77 = 0;
			float voroi179_g77 = voronoi179_g77( coords179_g77, time179_g77, id179_g77, uv179_g77, 0, voronoiSmoothId179_g77 );
			float AltoCumulusPlacement223_g77 = saturate( ( ( ( 1.0 - 0.0 ) - (1.0 + (voroi135_g77 - 0.0) * (-0.5 - 1.0) / (1.0 - 0.0)) ) - voroi179_g77 ) );
			float time205_g77 = 51.2;
			float2 voronoiSmoothId205_g77 = 0;
			float2 coords205_g77 = (Pos10_g77*1.0 + ( CZY_AltocumulusWindSpeed * TIme6_g77 )) * ( 100.0 / CZY_AltocumulusScale );
			float2 id205_g77 = 0;
			float2 uv205_g77 = 0;
			float fade205_g77 = 0.5;
			float voroi205_g77 = 0;
			float rest205_g77 = 0;
			for( int it205_g77 = 0; it205_g77 <2; it205_g77++ ){
			voroi205_g77 += fade205_g77 * voronoi205_g77( coords205_g77, time205_g77, id205_g77, uv205_g77, 0,voronoiSmoothId205_g77 );
			rest205_g77 += fade205_g77;
			coords205_g77 *= 2;
			fade205_g77 *= 0.5;
			}//Voronoi205_g77
			voroi205_g77 /= rest205_g77;
			float AltoCumulusLightTransport266_g77 = saturate( (-1.0 + (( AltoCumulusPlacement223_g77 * ( 0.1 > voroi205_g77 ? (0.5 + (voroi205_g77 - 0.0) * (0.0 - 0.5) / (0.15 - 0.0)) : 0.0 ) * CZY_AltocumulusMultiplier ) - 0.0) * (3.0 - -1.0) / (1.0 - 0.0)) );
			float ACCustomLightsClipping346_g77 = AltoCumulusLightTransport266_g77;
			float time32_g77 = 0.0;
			float2 voronoiSmoothId32_g77 = 0;
			float2 coords32_g77 = ( Pos10_g77 + ( TIme6_g77 * float2( 0.3,0.2 ) ) ) * ( 100.0 / CZY_DetailScale );
			float2 id32_g77 = 0;
			float2 uv32_g77 = 0;
			float fade32_g77 = 0.5;
			float voroi32_g77 = 0;
			float rest32_g77 = 0;
			for( int it32_g77 = 0; it32_g77 <3; it32_g77++ ){
			voroi32_g77 += fade32_g77 * voronoi32_g77( coords32_g77, time32_g77, id32_g77, uv32_g77, 0,voronoiSmoothId32_g77 );
			rest32_g77 += fade32_g77;
			coords32_g77 *= 2;
			fade32_g77 *= 0.5;
			}//Voronoi32_g77
			voroi32_g77 /= rest32_g77;
			float temp_output_75_0_g77 = ( (0.0 + (( 1.0 - voroi32_g77 ) - 0.3) * (0.5 - 0.0) / (1.0 - 0.3)) * 0.1 * CZY_DetailAmount );
			float DetailedClouds190_g77 = saturate( ( ComplexCloudDensity114_g77 + temp_output_75_0_g77 ) );
			float CloudDetail81_g77 = temp_output_75_0_g77;
			float2 temp_output_71_0_g77 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult77_g77 = dot( temp_output_71_0_g77 , temp_output_71_0_g77 );
			float BorderHeight63_g77 = ( 1.0 - CZY_BorderHeight );
			float temp_output_64_0_g77 = ( -2.0 * ( 1.0 - CZY_BorderVariation ) );
			float clampResult166_g77 = clamp( ( ( ( CloudDetail81_g77 + SimpleCloudDensity52_g77 ) * saturate( (( BorderHeight63_g77 * temp_output_64_0_g77 ) + (dotResult77_g77 - 0.0) * (( temp_output_64_0_g77 * -4.0 ) - ( BorderHeight63_g77 * temp_output_64_0_g77 )) / (0.5 - 0.0)) ) ) * 10.0 * CZY_BorderEffect ) , -1.0 , 1.0 );
			float BorderLightTransport185_g77 = clampResult166_g77;
			float3 ase_positionWS = i.worldPos;
			float3 normalizeResult58_g77 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float3 normalizeResult53_g77 = normalize( CZY_StormDirection );
			float dotResult67_g77 = dot( normalizeResult58_g77 , normalizeResult53_g77 );
			float2 temp_output_46_0_g77 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult62_g77 = dot( temp_output_46_0_g77 , temp_output_46_0_g77 );
			float temp_output_74_0_g77 = ( -2.0 * ( 1.0 - ( CZY_NimbusVariation * 0.9 ) ) );
			float NimbusLightTransport198_g77 = saturate( ( ( ( CloudDetail81_g77 + SimpleCloudDensity52_g77 ) * saturate( (( ( 1.0 - CZY_NimbusMultiplier ) * temp_output_74_0_g77 ) + (( dotResult67_g77 + ( CZY_NimbusHeight * 4.0 * dotResult62_g77 ) ) - 0.5) * (( temp_output_74_0_g77 * -4.0 ) - ( ( 1.0 - CZY_NimbusMultiplier ) * temp_output_74_0_g77 )) / (7.0 - 0.5)) ) ) * 10.0 ) );
			float mulTime156_g77 = _Time.y * 0.01;
			float simplePerlin2D193_g77 = snoise( (Pos10_g77*1.0 + mulTime156_g77)*2.0 );
			float mulTime133_g77 = _Time.y * CZY_ChemtrailsMoveSpeed;
			float cos171_g77 = cos( ( mulTime133_g77 * 0.01 ) );
			float sin171_g77 = sin( ( mulTime133_g77 * 0.01 ) );
			float2 rotator171_g77 = mul( Pos10_g77 - float2( 0.5,0.5 ) , float2x2( cos171_g77 , -sin171_g77 , sin171_g77 , cos171_g77 )) + float2( 0.5,0.5 );
			float cos188_g77 = cos( ( mulTime133_g77 * -0.02 ) );
			float sin188_g77 = sin( ( mulTime133_g77 * -0.02 ) );
			float2 rotator188_g77 = mul( Pos10_g77 - float2( 0.5,0.5 ) , float2x2( cos188_g77 , -sin188_g77 , sin188_g77 , cos188_g77 )) + float2( 0.5,0.5 );
			float mulTime158_g77 = _Time.y * 0.01;
			float simplePerlin2D196_g77 = snoise( (Pos10_g77*1.0 + mulTime158_g77)*4.0 );
			float4 ChemtrailsPattern247_g77 = ( ( saturate( simplePerlin2D193_g77 ) * tex2D( CZY_ChemtrailsTexture, (rotator171_g77*0.5 + 0.0) ) ) + ( tex2D( CZY_ChemtrailsTexture, rotator188_g77 ) * saturate( simplePerlin2D196_g77 ) ) );
			float2 temp_output_227_0_g77 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult240_g77 = dot( temp_output_227_0_g77 , temp_output_227_0_g77 );
			float4 ChemtrailsFinal268_g77 = ( ChemtrailsPattern247_g77 * saturate( (0.4 + (dotResult240_g77 - 0.0) * (2.0 - 0.4) / (0.1 - 0.0)) ) * ( CZY_ChemtrailsMultiplier * 0.5 ) );
			float mulTime106_g77 = _Time.y * 0.01;
			float simplePerlin2D429_g77 = snoise( (Pos10_g77*1.0 + mulTime106_g77)*2.0 );
			float mulTime79_g77 = _Time.y * CZY_CirrusMoveSpeed;
			float cos118_g77 = cos( ( mulTime79_g77 * 0.01 ) );
			float sin118_g77 = sin( ( mulTime79_g77 * 0.01 ) );
			float2 rotator118_g77 = mul( Pos10_g77 - float2( 0.5,0.5 ) , float2x2( cos118_g77 , -sin118_g77 , sin118_g77 , cos118_g77 )) + float2( 0.5,0.5 );
			float cos116_g77 = cos( ( mulTime79_g77 * -0.02 ) );
			float sin116_g77 = sin( ( mulTime79_g77 * -0.02 ) );
			float2 rotator116_g77 = mul( Pos10_g77 - float2( 0.5,0.5 ) , float2x2( cos116_g77 , -sin116_g77 , sin116_g77 , cos116_g77 )) + float2( 0.5,0.5 );
			float mulTime111_g77 = _Time.y * 0.01;
			float simplePerlin2D132_g77 = snoise( (Pos10_g77*1.0 + mulTime111_g77) );
			simplePerlin2D132_g77 = simplePerlin2D132_g77*0.5 + 0.5;
			float4 CirrusPattern215_g77 = ( ( saturate( simplePerlin2D429_g77 ) * tex2D( CZY_CirrusTexture, (rotator118_g77*1.5 + 0.75) ) ) + ( tex2D( CZY_CirrusTexture, (rotator116_g77*1.0 + 0.0) ) * saturate( simplePerlin2D132_g77 ) ) );
			float2 temp_output_168_0_g77 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult186_g77 = dot( temp_output_168_0_g77 , temp_output_168_0_g77 );
			float CirrusAlpha269_g77 = ( ( ( CirrusPattern215_g77 * saturate( (0.0 + (dotResult186_g77 - 0.0) * (2.0 - 0.0) / (0.2 - 0.0)) ) ) * ( CZY_CirrusMultiplier * 10.0 ) ).r * 0.6 );
			float4 SimpleRadiance309_g77 = saturate( ( DetailedClouds190_g77 + BorderLightTransport185_g77 + NimbusLightTransport198_g77 + ChemtrailsFinal268_g77 + CirrusAlpha269_g77 ) );
			float Clipping311_g77 = CZY_ClippingThreshold;
			float4 CSCustomLightsClipping343_g77 = ( CirrostratLightTransport267_g77 * ( SimpleRadiance309_g77.r > Clipping311_g77 ? 0.0 : 1.0 ) );
			float4 CustomRadiance376_g77 = saturate( ( ACCustomLightsClipping346_g77 + CSCustomLightsClipping343_g77 ) );
			float4 CloudThicknessDetails375_g77 = ( VoroDetails33_g77.x * saturate( ( CustomRadiance376_g77 - float4( 0.8,0.8,0.8,0 ) ) ) );
			float3 normalizeResult319_g77 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float dotResult322_g77 = dot( normalizeResult319_g77 , CZY_MoonDirection );
			half MoonlightMask336_g77 = saturate( pow( abs( (dotResult322_g77*0.5 + 0.5) ) , CZY_MoonFlareFalloff ) );
			float3 hsvTorgb2_g80 = RGBToHSV( CZY_CloudMoonColor.rgb );
			float3 hsvTorgb3_g80 = HSVToRGB( float3(hsvTorgb2_g80.x,saturate( ( hsvTorgb2_g80.y + CZY_FilterSaturation ) ),( hsvTorgb2_g80.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g80 = ( float4( hsvTorgb3_g80 , 0.0 ) * CZY_FilterColor );
			float4 MoonlightColor387_g77 = ( temp_output_10_0_g80 * CZY_CloudFilterColor );
			float4 lerpResult298_g77 = lerp( ( lerpResult299_g77 + ( CirrostratLightTransport267_g77 * CloudHighlightColor385_g77 * ( 1.0 - CloudThicknessDetails375_g77 ) ) + ( MoonlightMask336_g77 * MoonlightColor387_g77 * ( 1.0 - CloudThicknessDetails375_g77 ) ) ) , ( CloudColor386_g77 * float4( 0.5660378,0.5660378,0.5660378,0 ) ) , CloudThicknessDetails375_g77);
			float4 lerpResult306_g77 = lerp( CloudColor386_g77 , lerpResult298_g77 , ( 1.0 - SimpleRadiance309_g77 ));
			float mulTime61_g77 = _Time.y * 0.5;
			float2 panner89_g77 = ( ( mulTime61_g77 * 0.004 ) * float2( 0.2,-0.4 ) + Pos10_g77);
			float cos80_g77 = cos( ( mulTime61_g77 * -0.01 ) );
			float sin80_g77 = sin( ( mulTime61_g77 * -0.01 ) );
			float2 rotator80_g77 = mul( Pos10_g77 - float2( 0.5,0.5 ) , float2x2( cos80_g77 , -sin80_g77 , sin80_g77 , cos80_g77 )) + float2( 0.5,0.5 );
			float4 CloudTexture152_g77 = min( tex2D( CZY_CloudTexture, (panner89_g77*1.0 + 0.75) ) , tex2D( CZY_CloudTexture, (rotator80_g77*3.0 + 0.75) ) );
			float clampResult183_g77 = clamp( ( 2.0 * 0.5 ) , 0.0 , 0.98 );
			float CloudTextureFinal222_g77 = ( CloudTexture152_g77 * clampResult183_g77 ).r;
			float3 normalizeResult317_g77 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float dotResult349_g77 = dot( normalizeResult317_g77 , CZY_SunDirection );
			float temp_output_314_0_g77 = abs( (dotResult349_g77*0.5 + 0.5) );
			float CloudLight340_g77 = saturate( pow( temp_output_314_0_g77 , CZY_CloudFlareFalloff ) );
			float4 lerpResult308_g77 = lerp( float4( 0,0,0,0 ) , CloudHighlightColor385_g77 , ( saturate( CustomRadiance376_g77 ) * CloudTextureFinal222_g77 * CloudLight340_g77 ));
			float4 SunThroughClouds300_g77 = ( lerpResult308_g77 * 2.0 );
			float3 hsvTorgb2_g81 = RGBToHSV( CZY_AltoCloudColor.rgb );
			float3 hsvTorgb3_g81 = HSVToRGB( float3(hsvTorgb2_g81.x,saturate( ( hsvTorgb2_g81.y + CZY_FilterSaturation ) ),( hsvTorgb2_g81.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g81 = ( float4( hsvTorgb3_g81 , 0.0 ) * CZY_FilterColor );
			float4 CirrusCustomLightColor390_g77 = ( CloudColor386_g77 * ( temp_output_10_0_g81 * CZY_CloudFilterColor ) );
			float4 lerpResult334_g77 = lerp( ( lerpResult306_g77 + SunThroughClouds300_g77 ) , CirrusCustomLightColor390_g77 , CustomRadiance376_g77);
			float4 lerpResult305_g77 = lerp( CZY_CloudTextureColor , CZY_LightColor , float4( 0.5,0.5,0.5,0 ));
			float4 lerpResult331_g77 = lerp( lerpResult334_g77 , ( lerpResult305_g77 * lerpResult334_g77 ) , CloudTextureFinal222_g77);
			float4 FinalCloudColor367_g77 = lerpResult331_g77;
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
			float4 lerpResult90_g369 = lerp( FinalCloudColor367_g77 , ( ( temp_output_10_0_g371 * CZY_SunFilterColor ) + temp_output_10_0_g370 ) , temp_output_34_0_g369);
			o.Emission = lerpResult90_g369.rgb;
			float temp_output_236_0_g77 = saturate( ( DetailedClouds190_g77 + BorderLightTransport185_g77 + NimbusLightTransport198_g77 ) );
			float4 FinalAlpha278_g77 = saturate( ( saturate( ( temp_output_236_0_g77 + ( (-1.0 + (CloudTextureFinal222_g77 - 0.0) * (1.0 - -1.0) / (1.0 - 0.0)) * CZY_TextureAmount * sin( ( temp_output_236_0_g77 * UNITY_PI ) ) ) ) ) + AltoCumulusLightTransport266_g77 + ChemtrailsFinal268_g77 + CirrostratLightTransport267_g77 + CirrusAlpha269_g77 ) );
			o.Alpha = ( saturate( ( FinalAlpha278_g77.r + ( FinalAlpha278_g77.r * 2.0 * CZY_CloudThickness ) ) ) * ( 1.0 - 0.0 ) );
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
Node;AmplifyShaderEditor.FunctionNode;1211;-1424,-592;Inherit;False;Stylized Clouds (Painted Skies);0;;77;9ff68446d0ede9643a7c3290efe4a319;0;0;2;COLOR;0;FLOAT;446
Node;AmplifyShaderEditor.RangedFloatNode;1212;-1344,-384;Inherit;False;Global;CZY_CloudsFogAmount;CZY_CloudsFogAmount;8;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1213;-1344,-464;Inherit;False;Global;CZY_CloudsFogLightAmount;CZY_CloudsFogLightAmount;7;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;1214;-1008,-592;Inherit;False;AddFogToSkyLayer;-1;;369;36a78fe96c9f6fa4dab85c7793736468;0;3;89;COLOR;0,0,0,0;False;91;FLOAT;0;False;59;FLOAT;0;False;2;COLOR;84;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;-695.2959,-681.1561;Float;False;True;-1;2;DistantLands.Cozy.EditorScripts.EmptyShaderGUI;0;0;Unlit;Distant Lands/Cozy/BiRP/Stylized Clouds (Painted Skies);False;False;False;False;False;False;False;False;False;True;False;False;False;False;True;False;False;False;False;False;False;Front;0;False;;0;False;;False;0;False;;0;False;;False;0;Custom;0.5;True;True;-50;True;TransparentCutout;;Transparent;ForwardOnly;12;all;True;True;True;True;0;False;;True;221;False;;255;False;;255;False;;7;False;;3;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;2;5;False;;10;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;5;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;1214;89;1211;0
WireConnection;1214;91;1213;0
WireConnection;1214;59;1212;0
WireConnection;0;2;1214;84
WireConnection;0;9;1211;446
ASEEND*/
//CHKSM=E45271ACC5DA7722AB6CF434BC61993F5960533C