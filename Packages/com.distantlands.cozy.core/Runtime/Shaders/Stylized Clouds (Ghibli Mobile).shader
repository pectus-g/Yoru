// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Distant Lands/Cozy/BiRP/Stylized Clouds (Ghibli Mobile)"
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

		uniform float4 CZY_CloudHighlightColor;
		uniform float CZY_FilterSaturation;
		uniform float CZY_FilterValue;
		uniform float4 CZY_FilterColor;
		uniform float4 CZY_CloudFilterColor;
		uniform float4 CZY_CloudColor;
		uniform float4 CZY_CloudTextureColor;
		uniform float CZY_Spherize;
		uniform float CZY_WindSpeed;
		uniform float CZY_CloudCohesion;
		uniform float CZY_CumulusCoverageMultiplier;
		uniform float CZY_MainCloudScale;
		uniform float CZY_ShadowingDistance;
		uniform float CZY_ClippingThreshold;
		uniform float4 CZY_LightColor;
		uniform float4 CZY_FogColor5;
		uniform float CZY_LightFlareSquish;
		uniform float3 CZY_SunDirection;
		uniform half CZY_LightIntensity;
		uniform half CZY_LightFalloff;
		uniform float CZY_CloudsFogLightAmount;
		uniform float4 CZY_SunFilterColor;
		uniform float3 CZY_MoonDirection;
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

		struct Gradient
		{
			int type;
			int colorsLength;
			int alphasLength;
			float4 colors[8];
			float2 alphas[8];
		};


		Gradient NewGradient(int type, int colorsLength, int alphasLength, 
		float4 colors0, float4 colors1, float4 colors2, float4 colors3, float4 colors4, float4 colors5, float4 colors6, float4 colors7,
		float2 alphas0, float2 alphas1, float2 alphas2, float2 alphas3, float2 alphas4, float2 alphas5, float2 alphas6, float2 alphas7)
		{
			Gradient g;
			g.type = type;
			g.colorsLength = colorsLength;
			g.alphasLength = alphasLength;
			g.colors[ 0 ] = colors0;
			g.colors[ 1 ] = colors1;
			g.colors[ 2 ] = colors2;
			g.colors[ 3 ] = colors3;
			g.colors[ 4 ] = colors4;
			g.colors[ 5 ] = colors5;
			g.colors[ 6 ] = colors6;
			g.colors[ 7 ] = colors7;
			g.alphas[ 0 ] = alphas0;
			g.alphas[ 1 ] = alphas1;
			g.alphas[ 2 ] = alphas2;
			g.alphas[ 3 ] = alphas3;
			g.alphas[ 4 ] = alphas4;
			g.alphas[ 5 ] = alphas5;
			g.alphas[ 6 ] = alphas6;
			g.alphas[ 7 ] = alphas7;
			return g;
		}


		float2 voronoihash35_g79( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi35_g79( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash35_g79( n + g );
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


		float2 voronoihash13_g79( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi13_g79( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash13_g79( n + g );
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


		float2 voronoihash11_g79( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi11_g79( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash11_g79( n + g );
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


		float2 voronoihash35_g82( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi35_g82( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash35_g82( n + g );
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


		float2 voronoihash13_g82( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi13_g82( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash13_g82( n + g );
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


		float2 voronoihash11_g82( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi11_g82( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash11_g82( n + g );
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


		float4 SampleGradient( Gradient gradient, float time )
		{
			float3 color = gradient.colors[0].rgb;
			UNITY_UNROLL
			for (int c = 1; c < 8; c++)
			{
			float colorPos = saturate((time - gradient.colors[c-1].w) / ( 0.00001 + (gradient.colors[c].w - gradient.colors[c-1].w)) * step(c, (float)gradient.colorsLength-1));
			color = lerp(color, gradient.colors[c].rgb, lerp(colorPos, step(0.01, colorPos), gradient.type));
			}
			#ifndef UNITY_COLORSPACE_GAMMA
			color = half3(GammaToLinearSpaceExact(color.r), GammaToLinearSpaceExact(color.g), GammaToLinearSpaceExact(color.b));
			#endif
			float alpha = gradient.alphas[0].x;
			UNITY_UNROLL
			for (int a = 1; a < 8; a++)
			{
			float alphaPos = saturate((time - gradient.alphas[a-1].y) / ( 0.00001 + (gradient.alphas[a].y - gradient.alphas[a-1].y)) * step(a, (float)gradient.alphasLength-1));
			alpha = lerp(alpha, gradient.alphas[a].x, lerp(alphaPos, step(0.01, alphaPos), gradient.type));
			}
			return float4(color, alpha);
		}


		float2 voronoihash35_g78( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi35_g78( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash35_g78( n + g );
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


		float2 voronoihash13_g78( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi13_g78( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash13_g78( n + g );
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


		float2 voronoihash11_g78( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi11_g78( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash11_g78( n + g );
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
			float3 hsvTorgb2_g81 = RGBToHSV( CZY_CloudHighlightColor.rgb );
			float3 hsvTorgb3_g81 = HSVToRGB( float3(hsvTorgb2_g81.x,saturate( ( hsvTorgb2_g81.y + CZY_FilterSaturation ) ),( hsvTorgb2_g81.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g81 = ( float4( hsvTorgb3_g81 , 0.0 ) * CZY_FilterColor );
			float4 CloudHighlightColor91_g77 = ( temp_output_10_0_g81 * CZY_CloudFilterColor );
			float3 hsvTorgb2_g80 = RGBToHSV( CZY_CloudColor.rgb );
			float3 hsvTorgb3_g80 = HSVToRGB( float3(hsvTorgb2_g80.x,saturate( ( hsvTorgb2_g80.y + CZY_FilterSaturation ) ),( hsvTorgb2_g80.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g80 = ( float4( hsvTorgb3_g80 , 0.0 ) * CZY_FilterColor );
			float4 CloudColor73_g77 = ( temp_output_10_0_g80 * CZY_CloudFilterColor );
			Gradient gradient68_g77 = NewGradient( 0, 2, 2, float4( 0, 0, 0, 0.8676432 ), float4( 1, 1, 1, 0.9294118 ), 0, 0, 0, 0, 0, 0, float2( 1, 0 ), float2( 1, 1 ), 0, 0, 0, 0, 0, 0 );
			float2 temp_output_54_0_g77 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult23_g77 = dot( temp_output_54_0_g77 , temp_output_54_0_g77 );
			float Dot28_g77 = saturate( (0.85 + (dotResult23_g77 - 0.0) * (3.0 - 0.85) / (1.0 - 0.0)) );
			float time35_g79 = 0.0;
			float2 voronoiSmoothId35_g79 = 0;
			float2 CentralUV17_g77 = ( i.uv_texcoord + float2( -0.5,-0.5 ) );
			float2 temp_output_21_0_g79 = (CentralUV17_g77*1.58 + 0.0);
			float2 break2_g79 = abs( temp_output_21_0_g79 );
			float saferPower4_g79 = abs( break2_g79.x );
			float saferPower3_g79 = abs( break2_g79.y );
			float saferPower6_g79 = abs( ( pow( saferPower4_g79 , 2.0 ) + pow( saferPower3_g79 , 2.0 ) ) );
			float Spherize29_g77 = CZY_Spherize;
			float Flatness30_g77 = ( 20.0 * CZY_Spherize );
			float mulTime14_g77 = _Time.y * ( 0.001 * CZY_WindSpeed );
			float Time8_g77 = mulTime14_g77;
			float2 Wind51_g77 = ( Time8_g77 * float2( 0.1,0.2 ) );
			float2 temp_output_10_0_g79 = (( ( temp_output_21_0_g79 * ( pow( saferPower6_g79 , Spherize29_g77 ) * Flatness30_g77 ) ) + float2( 0.5,0.5 ) )*( 2.0 / 5.0 ) + Wind51_g77);
			float2 coords35_g79 = temp_output_10_0_g79 * 60.0;
			float2 id35_g79 = 0;
			float2 uv35_g79 = 0;
			float fade35_g79 = 0.5;
			float voroi35_g79 = 0;
			float rest35_g79 = 0;
			for( int it35_g79 = 0; it35_g79 <2; it35_g79++ ){
			voroi35_g79 += fade35_g79 * voronoi35_g79( coords35_g79, time35_g79, id35_g79, uv35_g79, 0,voronoiSmoothId35_g79 );
			rest35_g79 += fade35_g79;
			coords35_g79 *= 2;
			fade35_g79 *= 0.5;
			}//Voronoi35_g79
			voroi35_g79 /= rest35_g79;
			float time13_g79 = 0.0;
			float2 voronoiSmoothId13_g79 = 0;
			float2 coords13_g79 = temp_output_10_0_g79 * 25.0;
			float2 id13_g79 = 0;
			float2 uv13_g79 = 0;
			float fade13_g79 = 0.5;
			float voroi13_g79 = 0;
			float rest13_g79 = 0;
			for( int it13_g79 = 0; it13_g79 <2; it13_g79++ ){
			voroi13_g79 += fade13_g79 * voronoi13_g79( coords13_g79, time13_g79, id13_g79, uv13_g79, 0,voronoiSmoothId13_g79 );
			rest13_g79 += fade13_g79;
			coords13_g79 *= 2;
			fade13_g79 *= 0.5;
			}//Voronoi13_g79
			voroi13_g79 /= rest13_g79;
			float time11_g79 = 17.23;
			float2 voronoiSmoothId11_g79 = 0;
			float2 coords11_g79 = temp_output_10_0_g79 * 9.0;
			float2 id11_g79 = 0;
			float2 uv11_g79 = 0;
			float fade11_g79 = 0.5;
			float voroi11_g79 = 0;
			float rest11_g79 = 0;
			for( int it11_g79 = 0; it11_g79 <2; it11_g79++ ){
			voroi11_g79 += fade11_g79 * voronoi11_g79( coords11_g79, time11_g79, id11_g79, uv11_g79, 0,voronoiSmoothId11_g79 );
			rest11_g79 += fade11_g79;
			coords11_g79 *= 2;
			fade11_g79 *= 0.5;
			}//Voronoi11_g79
			voroi11_g79 /= rest11_g79;
			float2 temp_output_15_0_g77 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult7_g77 = dot( temp_output_15_0_g77 , temp_output_15_0_g77 );
			float ModifiedCohesion22_g77 = ( CZY_CloudCohesion * 1.0 * ( 1.0 - dotResult7_g77 ) );
			float lerpResult15_g79 = lerp( saturate( ( voroi35_g79 + voroi13_g79 ) ) , voroi11_g79 , ModifiedCohesion22_g77);
			float CumulusCoverage113_g77 = CZY_CumulusCoverageMultiplier;
			float lerpResult16_g79 = lerp( lerpResult15_g79 , 1.0 , ( ( 1.0 - CumulusCoverage113_g77 ) + -0.7 ));
			float time35_g82 = 0.0;
			float2 voronoiSmoothId35_g82 = 0;
			float2 temp_output_21_0_g82 = CentralUV17_g77;
			float2 break2_g82 = abs( temp_output_21_0_g82 );
			float saferPower4_g82 = abs( break2_g82.x );
			float saferPower3_g82 = abs( break2_g82.y );
			float saferPower6_g82 = abs( ( pow( saferPower4_g82 , 2.0 ) + pow( saferPower3_g82 , 2.0 ) ) );
			float Scale24_g77 = ( CZY_MainCloudScale * 0.1 );
			float2 temp_output_10_0_g82 = (( ( temp_output_21_0_g82 * ( pow( saferPower6_g82 , Spherize29_g77 ) * Flatness30_g77 ) ) + float2( 0.5,0.5 ) )*( 2.0 / ( Scale24_g77 * 1.5 ) ) + ( Wind51_g77 * float2( 0.5,0.5 ) ));
			float2 coords35_g82 = temp_output_10_0_g82 * 60.0;
			float2 id35_g82 = 0;
			float2 uv35_g82 = 0;
			float fade35_g82 = 0.5;
			float voroi35_g82 = 0;
			float rest35_g82 = 0;
			for( int it35_g82 = 0; it35_g82 <2; it35_g82++ ){
			voroi35_g82 += fade35_g82 * voronoi35_g82( coords35_g82, time35_g82, id35_g82, uv35_g82, 0,voronoiSmoothId35_g82 );
			rest35_g82 += fade35_g82;
			coords35_g82 *= 2;
			fade35_g82 *= 0.5;
			}//Voronoi35_g82
			voroi35_g82 /= rest35_g82;
			float time13_g82 = 0.0;
			float2 voronoiSmoothId13_g82 = 0;
			float2 coords13_g82 = temp_output_10_0_g82 * 25.0;
			float2 id13_g82 = 0;
			float2 uv13_g82 = 0;
			float fade13_g82 = 0.5;
			float voroi13_g82 = 0;
			float rest13_g82 = 0;
			for( int it13_g82 = 0; it13_g82 <2; it13_g82++ ){
			voroi13_g82 += fade13_g82 * voronoi13_g82( coords13_g82, time13_g82, id13_g82, uv13_g82, 0,voronoiSmoothId13_g82 );
			rest13_g82 += fade13_g82;
			coords13_g82 *= 2;
			fade13_g82 *= 0.5;
			}//Voronoi13_g82
			voroi13_g82 /= rest13_g82;
			float time11_g82 = 17.23;
			float2 voronoiSmoothId11_g82 = 0;
			float2 coords11_g82 = temp_output_10_0_g82 * 9.0;
			float2 id11_g82 = 0;
			float2 uv11_g82 = 0;
			float fade11_g82 = 0.5;
			float voroi11_g82 = 0;
			float rest11_g82 = 0;
			for( int it11_g82 = 0; it11_g82 <2; it11_g82++ ){
			voroi11_g82 += fade11_g82 * voronoi11_g82( coords11_g82, time11_g82, id11_g82, uv11_g82, 0,voronoiSmoothId11_g82 );
			rest11_g82 += fade11_g82;
			coords11_g82 *= 2;
			fade11_g82 *= 0.5;
			}//Voronoi11_g82
			voroi11_g82 /= rest11_g82;
			float lerpResult15_g82 = lerp( saturate( ( voroi35_g82 + voroi13_g82 ) ) , voroi11_g82 , ( ModifiedCohesion22_g77 * 1.1 ));
			float lerpResult16_g82 = lerp( lerpResult15_g82 , 1.0 , ( ( 1.0 - CumulusCoverage113_g77 ) + -0.7 ));
			float temp_output_25_0_g77 = saturate( (0.0 + (( Dot28_g77 * ( 1.0 - lerpResult16_g82 ) ) - 0.6) * (1.0 - 0.0) / (1.0 - 0.6)) );
			float IT2PreAlpha80_g77 = temp_output_25_0_g77;
			float temp_output_77_0_g77 = (0.0 + (( Dot28_g77 * ( 1.0 - lerpResult16_g79 ) ) - 0.6) * (IT2PreAlpha80_g77 - 0.0) / (1.5 - 0.6));
			float clampResult71_g77 = clamp( temp_output_77_0_g77 , 0.0 , 0.9 );
			float AdditionalLayer66_g77 = SampleGradient( gradient68_g77, clampResult71_g77 ).r;
			float4 lerpResult88_g77 = lerp( CloudColor73_g77 , ( CloudColor73_g77 * CZY_CloudTextureColor ) , AdditionalLayer66_g77);
			float4 ModifiedCloudColor97_g77 = lerpResult88_g77;
			Gradient gradient87_g77 = NewGradient( 0, 2, 2, float4( 0.06119964, 0.06119964, 0.06119964, 0.4411841 ), float4( 1, 1, 1, 0.5794156 ), 0, 0, 0, 0, 0, 0, float2( 1, 0 ), float2( 1, 1 ), 0, 0, 0, 0, 0, 0 );
			float time35_g78 = 0.0;
			float2 voronoiSmoothId35_g78 = 0;
			float2 ShadowUV59_g77 = ( CentralUV17_g77 + ( CentralUV17_g77 * float2( -1,-1 ) * CZY_ShadowingDistance * Dot28_g77 ) );
			float2 temp_output_21_0_g78 = ShadowUV59_g77;
			float2 break2_g78 = abs( temp_output_21_0_g78 );
			float saferPower4_g78 = abs( break2_g78.x );
			float saferPower3_g78 = abs( break2_g78.y );
			float saferPower6_g78 = abs( ( pow( saferPower4_g78 , 2.0 ) + pow( saferPower3_g78 , 2.0 ) ) );
			float2 temp_output_10_0_g78 = (( ( temp_output_21_0_g78 * ( pow( saferPower6_g78 , Spherize29_g77 ) * Flatness30_g77 ) ) + float2( 0.5,0.5 ) )*( 2.0 / ( Scale24_g77 * 1.5 ) ) + ( Wind51_g77 * float2( 0.5,0.5 ) ));
			float2 coords35_g78 = temp_output_10_0_g78 * 60.0;
			float2 id35_g78 = 0;
			float2 uv35_g78 = 0;
			float fade35_g78 = 0.5;
			float voroi35_g78 = 0;
			float rest35_g78 = 0;
			for( int it35_g78 = 0; it35_g78 <2; it35_g78++ ){
			voroi35_g78 += fade35_g78 * voronoi35_g78( coords35_g78, time35_g78, id35_g78, uv35_g78, 0,voronoiSmoothId35_g78 );
			rest35_g78 += fade35_g78;
			coords35_g78 *= 2;
			fade35_g78 *= 0.5;
			}//Voronoi35_g78
			voroi35_g78 /= rest35_g78;
			float time13_g78 = 0.0;
			float2 voronoiSmoothId13_g78 = 0;
			float2 coords13_g78 = temp_output_10_0_g78 * 25.0;
			float2 id13_g78 = 0;
			float2 uv13_g78 = 0;
			float fade13_g78 = 0.5;
			float voroi13_g78 = 0;
			float rest13_g78 = 0;
			for( int it13_g78 = 0; it13_g78 <2; it13_g78++ ){
			voroi13_g78 += fade13_g78 * voronoi13_g78( coords13_g78, time13_g78, id13_g78, uv13_g78, 0,voronoiSmoothId13_g78 );
			rest13_g78 += fade13_g78;
			coords13_g78 *= 2;
			fade13_g78 *= 0.5;
			}//Voronoi13_g78
			voroi13_g78 /= rest13_g78;
			float time11_g78 = 17.23;
			float2 voronoiSmoothId11_g78 = 0;
			float2 coords11_g78 = temp_output_10_0_g78 * 9.0;
			float2 id11_g78 = 0;
			float2 uv11_g78 = 0;
			float fade11_g78 = 0.5;
			float voroi11_g78 = 0;
			float rest11_g78 = 0;
			for( int it11_g78 = 0; it11_g78 <2; it11_g78++ ){
			voroi11_g78 += fade11_g78 * voronoi11_g78( coords11_g78, time11_g78, id11_g78, uv11_g78, 0,voronoiSmoothId11_g78 );
			rest11_g78 += fade11_g78;
			coords11_g78 *= 2;
			fade11_g78 *= 0.5;
			}//Voronoi11_g78
			voroi11_g78 /= rest11_g78;
			float lerpResult15_g78 = lerp( saturate( ( voroi35_g78 + voroi13_g78 ) ) , voroi11_g78 , ( ModifiedCohesion22_g77 * 1.1 ));
			float lerpResult16_g78 = lerp( lerpResult15_g78 , 1.0 , ( ( 1.0 - CumulusCoverage113_g77 ) + -0.7 ));
			float4 lerpResult104_g77 = lerp( CloudHighlightColor91_g77 , ModifiedCloudColor97_g77 , saturate( SampleGradient( gradient87_g77, saturate( (0.0 + (( Dot28_g77 * ( 1.0 - lerpResult16_g78 ) ) - 0.6) * (1.0 - 0.0) / (1.0 - 0.6)) ) ).r ));
			float4 IT2Color100_g77 = lerpResult104_g77;
			Gradient gradient93_g77 = NewGradient( 0, 2, 2, float4( 0.06119964, 0.06119964, 0.06119964, 0.4617685 ), float4( 1, 1, 1, 0.5117723 ), 0, 0, 0, 0, 0, 0, float2( 1, 0 ), float2( 1, 1 ), 0, 0, 0, 0, 0, 0 );
			float IT2Alpha101_g77 = SampleGradient( gradient93_g77, temp_output_25_0_g77 ).r;
			clip( ( IT2Alpha101_g77 * ( 1.0 - 0.0 ) ) - CZY_ClippingThreshold);
			float3 hsvTorgb69_g369 = RGBToHSV( CZY_FogColor5.rgb );
			float3 ase_positionWS = i.worldPos;
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
			float4 lerpResult90_g369 = lerp( IT2Color100_g77 , ( ( temp_output_10_0_g371 * CZY_SunFilterColor ) + temp_output_10_0_g370 ) , temp_output_34_0_g369);
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
Node;AmplifyShaderEditor.FunctionNode;1211;-1488,-640;Inherit;False;Stylized Clouds (Ghibli Mobile);-1;;77;0e3bfd67c40d9414689466ab81c18717;0;0;3;COLOR;0;FLOAT;126;FLOAT;127
Node;AmplifyShaderEditor.ClipNode;1212;-1200,-640;Inherit;False;3;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;1213;-1312,-432;Inherit;False;Global;CZY_CloudsFogAmount;CZY_CloudsFogAmount;8;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1214;-1312,-512;Inherit;False;Global;CZY_CloudsFogLightAmount;CZY_CloudsFogLightAmount;7;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;1215;-976,-640;Inherit;False;AddFogToSkyLayer;-1;;369;36a78fe96c9f6fa4dab85c7793736468;0;3;89;COLOR;0,0,0,0;False;91;FLOAT;0;False;59;FLOAT;0;False;2;COLOR;84;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;-695.2959,-681.1561;Float;False;True;-1;2;DistantLands.Cozy.EditorScripts.EmptyShaderGUI;0;0;Unlit;Distant Lands/Cozy/BiRP/Stylized Clouds (Ghibli Mobile);False;False;False;False;False;False;False;False;False;True;False;False;False;False;True;False;False;False;False;False;False;Front;0;False;;0;False;;False;0;False;;0;False;;False;0;Custom;0.5;True;True;-50;True;TransparentCutout;;Transparent;ForwardOnly;12;all;True;True;True;True;0;False;;True;221;False;;255;False;;255;False;;7;False;;3;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;2;5;False;;10;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;0;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;1212;0;1211;0
WireConnection;1212;1;1211;126
WireConnection;1212;2;1211;127
WireConnection;1215;89;1212;0
WireConnection;1215;91;1214;0
WireConnection;1215;59;1213;0
WireConnection;0;2;1215;84
ASEEND*/
//CHKSM=DFF640BA3632D4E484E984B68D2E62B06F903D27