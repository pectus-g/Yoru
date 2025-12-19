// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Distant Lands/Cozy/BiRP/Stylized Clouds (Ghibli Desktop)"
{
	Properties
	{
		_Cutoff( "Mask Clip Value", Float ) = 0.5
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

		uniform float4 CZY_AltoCloudColor;
		uniform float CZY_FilterSaturation;
		uniform float CZY_FilterValue;
		uniform float4 CZY_FilterColor;
		uniform float4 CZY_CloudFilterColor;
		uniform float4 CZY_CloudHighlightColor;
		uniform float4 CZY_CloudColor;
		uniform float CZY_Spherize;
		uniform float CZY_MainCloudScale;
		uniform float CZY_WindSpeed;
		uniform float CZY_CloudCohesion;
		uniform float CZY_CumulusCoverageMultiplier;
		uniform float CZY_ShadowingDistance;
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
		uniform float CZY_CloudThickness;
		uniform float _Cutoff = 0.5;


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


		float2 voronoihash35_g89( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi35_g89( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash35_g89( n + g );
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


		float2 voronoihash13_g89( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi13_g89( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash13_g89( n + g );
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


		float2 voronoihash11_g89( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi11_g89( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash11_g89( n + g );
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


		float2 voronoihash35_g86( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi35_g86( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash35_g86( n + g );
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


		float2 voronoihash13_g86( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi13_g86( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash13_g86( n + g );
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


		float2 voronoihash11_g86( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi11_g86( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash11_g86( n + g );
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


		float2 voronoihash35_g85( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi35_g85( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash35_g85( n + g );
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


		float2 voronoihash13_g85( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi13_g85( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash13_g85( n + g );
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


		float2 voronoihash11_g85( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi11_g85( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash11_g85( n + g );
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


		float2 voronoihash35_g88( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi35_g88( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash35_g88( n + g );
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


		float2 voronoihash13_g88( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi13_g88( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash13_g88( n + g );
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


		float2 voronoihash11_g88( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi11_g88( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash11_g88( n + g );
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


		float2 voronoihash35_g87( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi35_g87( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash35_g87( n + g );
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


		float2 voronoihash13_g87( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi13_g87( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash13_g87( n + g );
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


		float2 voronoihash11_g87( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi11_g87( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash11_g87( n + g );
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
			float3 hsvTorgb2_g90 = RGBToHSV( CZY_AltoCloudColor.rgb );
			float3 hsvTorgb3_g90 = HSVToRGB( float3(hsvTorgb2_g90.x,saturate( ( hsvTorgb2_g90.y + CZY_FilterSaturation ) ),( hsvTorgb2_g90.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g90 = ( float4( hsvTorgb3_g90 , 0.0 ) * CZY_FilterColor );
			float3 hsvTorgb2_g92 = RGBToHSV( CZY_CloudHighlightColor.rgb );
			float3 hsvTorgb3_g92 = HSVToRGB( float3(hsvTorgb2_g92.x,saturate( ( hsvTorgb2_g92.y + CZY_FilterSaturation ) ),( hsvTorgb2_g92.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g92 = ( float4( hsvTorgb3_g92 , 0.0 ) * CZY_FilterColor );
			float4 CloudHighlightColor142_g84 = ( temp_output_10_0_g92 * CZY_CloudFilterColor );
			float3 hsvTorgb2_g91 = RGBToHSV( CZY_CloudColor.rgb );
			float3 hsvTorgb3_g91 = HSVToRGB( float3(hsvTorgb2_g91.x,saturate( ( hsvTorgb2_g91.y + CZY_FilterSaturation ) ),( hsvTorgb2_g91.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g91 = ( float4( hsvTorgb3_g91 , 0.0 ) * CZY_FilterColor );
			float4 CloudColor129_g84 = ( temp_output_10_0_g91 * CZY_CloudFilterColor );
			float4 color169_g84 = IsGammaSpace() ? float4(0.8396226,0.8396226,0.8396226,0) : float4(0.673178,0.673178,0.673178,0);
			Gradient gradient118_g84 = NewGradient( 0, 2, 2, float4( 0.06119964, 0.06119964, 0.06119964, 0.5411765 ), float4( 1, 1, 1, 0.6441138 ), 0, 0, 0, 0, 0, 0, float2( 1, 0 ), float2( 1, 1 ), 0, 0, 0, 0, 0, 0 );
			float2 temp_output_13_0_g84 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult23_g84 = dot( temp_output_13_0_g84 , temp_output_13_0_g84 );
			float Dot49_g84 = saturate( (0.85 + (dotResult23_g84 - 0.0) * (3.0 - 0.85) / (1.0 - 0.0)) );
			float time35_g89 = 0.0;
			float2 voronoiSmoothId35_g89 = 0;
			float2 CentralUV27_g84 = ( i.uv_texcoord + float2( -0.5,-0.5 ) );
			float2 temp_output_21_0_g89 = (CentralUV27_g84*1.58 + 0.0);
			float2 break2_g89 = abs( temp_output_21_0_g89 );
			float saferPower4_g89 = abs( break2_g89.x );
			float saferPower3_g89 = abs( break2_g89.y );
			float saferPower6_g89 = abs( ( pow( saferPower4_g89 , 2.0 ) + pow( saferPower3_g89 , 2.0 ) ) );
			float Spherize26_g84 = CZY_Spherize;
			float Flatness32_g84 = ( 20.0 * CZY_Spherize );
			float Scale164_g84 = ( CZY_MainCloudScale * 0.1 );
			float mulTime5_g84 = _Time.y * ( 0.001 * CZY_WindSpeed );
			float Time6_g84 = mulTime5_g84;
			float2 Wind18_g84 = ( Time6_g84 * float2( 0.1,0.2 ) );
			float2 temp_output_10_0_g89 = (( ( temp_output_21_0_g89 * ( pow( saferPower6_g89 , Spherize26_g84 ) * Flatness32_g84 ) ) + float2( 0.5,0.5 ) )*( 2.0 / Scale164_g84 ) + Wind18_g84);
			float2 coords35_g89 = temp_output_10_0_g89 * 60.0;
			float2 id35_g89 = 0;
			float2 uv35_g89 = 0;
			float fade35_g89 = 0.5;
			float voroi35_g89 = 0;
			float rest35_g89 = 0;
			for( int it35_g89 = 0; it35_g89 <2; it35_g89++ ){
			voroi35_g89 += fade35_g89 * voronoi35_g89( coords35_g89, time35_g89, id35_g89, uv35_g89, 0,voronoiSmoothId35_g89 );
			rest35_g89 += fade35_g89;
			coords35_g89 *= 2;
			fade35_g89 *= 0.5;
			}//Voronoi35_g89
			voroi35_g89 /= rest35_g89;
			float time13_g89 = 0.0;
			float2 voronoiSmoothId13_g89 = 0;
			float2 coords13_g89 = temp_output_10_0_g89 * 25.0;
			float2 id13_g89 = 0;
			float2 uv13_g89 = 0;
			float fade13_g89 = 0.5;
			float voroi13_g89 = 0;
			float rest13_g89 = 0;
			for( int it13_g89 = 0; it13_g89 <2; it13_g89++ ){
			voroi13_g89 += fade13_g89 * voronoi13_g89( coords13_g89, time13_g89, id13_g89, uv13_g89, 0,voronoiSmoothId13_g89 );
			rest13_g89 += fade13_g89;
			coords13_g89 *= 2;
			fade13_g89 *= 0.5;
			}//Voronoi13_g89
			voroi13_g89 /= rest13_g89;
			float time11_g89 = 17.23;
			float2 voronoiSmoothId11_g89 = 0;
			float2 coords11_g89 = temp_output_10_0_g89 * 9.0;
			float2 id11_g89 = 0;
			float2 uv11_g89 = 0;
			float fade11_g89 = 0.5;
			float voroi11_g89 = 0;
			float rest11_g89 = 0;
			for( int it11_g89 = 0; it11_g89 <2; it11_g89++ ){
			voroi11_g89 += fade11_g89 * voronoi11_g89( coords11_g89, time11_g89, id11_g89, uv11_g89, 0,voronoiSmoothId11_g89 );
			rest11_g89 += fade11_g89;
			coords11_g89 *= 2;
			fade11_g89 *= 0.5;
			}//Voronoi11_g89
			voroi11_g89 /= rest11_g89;
			float2 temp_output_4_0_g84 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult7_g84 = dot( temp_output_4_0_g84 , temp_output_4_0_g84 );
			float ModifiedCohesion17_g84 = ( CZY_CloudCohesion * 1.0 * ( 1.0 - dotResult7_g84 ) );
			float lerpResult15_g89 = lerp( saturate( ( voroi35_g89 + voroi13_g89 ) ) , voroi11_g89 , ModifiedCohesion17_g84);
			float CumulusCoverage29_g84 = CZY_CumulusCoverageMultiplier;
			float lerpResult16_g89 = lerp( lerpResult15_g89 , 1.0 , ( ( 1.0 - CumulusCoverage29_g84 ) + -0.7 ));
			float time35_g86 = 0.0;
			float2 voronoiSmoothId35_g86 = 0;
			float2 temp_output_21_0_g86 = CentralUV27_g84;
			float2 break2_g86 = abs( temp_output_21_0_g86 );
			float saferPower4_g86 = abs( break2_g86.x );
			float saferPower3_g86 = abs( break2_g86.y );
			float saferPower6_g86 = abs( ( pow( saferPower4_g86 , 2.0 ) + pow( saferPower3_g86 , 2.0 ) ) );
			float2 temp_output_10_0_g86 = (( ( temp_output_21_0_g86 * ( pow( saferPower6_g86 , Spherize26_g84 ) * Flatness32_g84 ) ) + float2( 0.5,0.5 ) )*( 2.0 / Scale164_g84 ) + Wind18_g84);
			float2 coords35_g86 = temp_output_10_0_g86 * 60.0;
			float2 id35_g86 = 0;
			float2 uv35_g86 = 0;
			float fade35_g86 = 0.5;
			float voroi35_g86 = 0;
			float rest35_g86 = 0;
			for( int it35_g86 = 0; it35_g86 <2; it35_g86++ ){
			voroi35_g86 += fade35_g86 * voronoi35_g86( coords35_g86, time35_g86, id35_g86, uv35_g86, 0,voronoiSmoothId35_g86 );
			rest35_g86 += fade35_g86;
			coords35_g86 *= 2;
			fade35_g86 *= 0.5;
			}//Voronoi35_g86
			voroi35_g86 /= rest35_g86;
			float time13_g86 = 0.0;
			float2 voronoiSmoothId13_g86 = 0;
			float2 coords13_g86 = temp_output_10_0_g86 * 25.0;
			float2 id13_g86 = 0;
			float2 uv13_g86 = 0;
			float fade13_g86 = 0.5;
			float voroi13_g86 = 0;
			float rest13_g86 = 0;
			for( int it13_g86 = 0; it13_g86 <2; it13_g86++ ){
			voroi13_g86 += fade13_g86 * voronoi13_g86( coords13_g86, time13_g86, id13_g86, uv13_g86, 0,voronoiSmoothId13_g86 );
			rest13_g86 += fade13_g86;
			coords13_g86 *= 2;
			fade13_g86 *= 0.5;
			}//Voronoi13_g86
			voroi13_g86 /= rest13_g86;
			float time11_g86 = 17.23;
			float2 voronoiSmoothId11_g86 = 0;
			float2 coords11_g86 = temp_output_10_0_g86 * 9.0;
			float2 id11_g86 = 0;
			float2 uv11_g86 = 0;
			float fade11_g86 = 0.5;
			float voroi11_g86 = 0;
			float rest11_g86 = 0;
			for( int it11_g86 = 0; it11_g86 <2; it11_g86++ ){
			voroi11_g86 += fade11_g86 * voronoi11_g86( coords11_g86, time11_g86, id11_g86, uv11_g86, 0,voronoiSmoothId11_g86 );
			rest11_g86 += fade11_g86;
			coords11_g86 *= 2;
			fade11_g86 *= 0.5;
			}//Voronoi11_g86
			voroi11_g86 /= rest11_g86;
			float lerpResult15_g86 = lerp( saturate( ( voroi35_g86 + voroi13_g86 ) ) , voroi11_g86 , ModifiedCohesion17_g84);
			float lerpResult16_g86 = lerp( lerpResult15_g86 , 1.0 , ( ( 1.0 - CumulusCoverage29_g84 ) + -0.7 ));
			float temp_output_60_0_g84 = saturate( (0.0 + (( Dot49_g84 * ( 1.0 - lerpResult16_g86 ) ) - 0.6) * (1.0 - 0.0) / (1.0 - 0.6)) );
			float IT1PreAlpha119_g84 = temp_output_60_0_g84;
			float time35_g85 = 0.0;
			float2 voronoiSmoothId35_g85 = 0;
			float2 temp_output_21_0_g85 = CentralUV27_g84;
			float2 break2_g85 = abs( temp_output_21_0_g85 );
			float saferPower4_g85 = abs( break2_g85.x );
			float saferPower3_g85 = abs( break2_g85.y );
			float saferPower6_g85 = abs( ( pow( saferPower4_g85 , 2.0 ) + pow( saferPower3_g85 , 2.0 ) ) );
			float2 temp_output_10_0_g85 = (( ( temp_output_21_0_g85 * ( pow( saferPower6_g85 , Spherize26_g84 ) * Flatness32_g84 ) ) + float2( 0.5,0.5 ) )*( 2.0 / ( Scale164_g84 * 1.5 ) ) + ( Wind18_g84 * float2( 0.5,0.5 ) ));
			float2 coords35_g85 = temp_output_10_0_g85 * 60.0;
			float2 id35_g85 = 0;
			float2 uv35_g85 = 0;
			float fade35_g85 = 0.5;
			float voroi35_g85 = 0;
			float rest35_g85 = 0;
			for( int it35_g85 = 0; it35_g85 <2; it35_g85++ ){
			voroi35_g85 += fade35_g85 * voronoi35_g85( coords35_g85, time35_g85, id35_g85, uv35_g85, 0,voronoiSmoothId35_g85 );
			rest35_g85 += fade35_g85;
			coords35_g85 *= 2;
			fade35_g85 *= 0.5;
			}//Voronoi35_g85
			voroi35_g85 /= rest35_g85;
			float time13_g85 = 0.0;
			float2 voronoiSmoothId13_g85 = 0;
			float2 coords13_g85 = temp_output_10_0_g85 * 25.0;
			float2 id13_g85 = 0;
			float2 uv13_g85 = 0;
			float fade13_g85 = 0.5;
			float voroi13_g85 = 0;
			float rest13_g85 = 0;
			for( int it13_g85 = 0; it13_g85 <2; it13_g85++ ){
			voroi13_g85 += fade13_g85 * voronoi13_g85( coords13_g85, time13_g85, id13_g85, uv13_g85, 0,voronoiSmoothId13_g85 );
			rest13_g85 += fade13_g85;
			coords13_g85 *= 2;
			fade13_g85 *= 0.5;
			}//Voronoi13_g85
			voroi13_g85 /= rest13_g85;
			float time11_g85 = 17.23;
			float2 voronoiSmoothId11_g85 = 0;
			float2 coords11_g85 = temp_output_10_0_g85 * 9.0;
			float2 id11_g85 = 0;
			float2 uv11_g85 = 0;
			float fade11_g85 = 0.5;
			float voroi11_g85 = 0;
			float rest11_g85 = 0;
			for( int it11_g85 = 0; it11_g85 <2; it11_g85++ ){
			voroi11_g85 += fade11_g85 * voronoi11_g85( coords11_g85, time11_g85, id11_g85, uv11_g85, 0,voronoiSmoothId11_g85 );
			rest11_g85 += fade11_g85;
			coords11_g85 *= 2;
			fade11_g85 *= 0.5;
			}//Voronoi11_g85
			voroi11_g85 /= rest11_g85;
			float lerpResult15_g85 = lerp( saturate( ( voroi35_g85 + voroi13_g85 ) ) , voroi11_g85 , ( ModifiedCohesion17_g84 * 1.1 ));
			float lerpResult16_g85 = lerp( lerpResult15_g85 , 1.0 , ( ( 1.0 - CumulusCoverage29_g84 ) + -0.7 ));
			float temp_output_61_0_g84 = saturate( (0.0 + (( Dot49_g84 * ( 1.0 - lerpResult16_g85 ) ) - 0.6) * (1.0 - 0.0) / (1.0 - 0.6)) );
			float IT2PreAlpha114_g84 = temp_output_61_0_g84;
			float temp_output_151_0_g84 = (0.0 + (( Dot49_g84 * ( 1.0 - lerpResult16_g89 ) ) - 0.6) * (max( IT1PreAlpha119_g84 , IT2PreAlpha114_g84 ) - 0.0) / (1.5 - 0.6));
			float clampResult73_g84 = clamp( temp_output_151_0_g84 , 0.0 , 0.9 );
			float AdditionalLayer138_g84 = SampleGradient( gradient118_g84, clampResult73_g84 ).r;
			float4 lerpResult75_g84 = lerp( CloudColor129_g84 , ( CloudColor129_g84 * color169_g84 ) , AdditionalLayer138_g84);
			float4 ModifiedCloudColor100_g84 = lerpResult75_g84;
			Gradient gradient136_g84 = NewGradient( 0, 2, 2, float4( 0.06119964, 0.06119964, 0.06119964, 0.4411841 ), float4( 1, 1, 1, 0.5794156 ), 0, 0, 0, 0, 0, 0, float2( 1, 0 ), float2( 1, 1 ), 0, 0, 0, 0, 0, 0 );
			float time35_g88 = 0.0;
			float2 voronoiSmoothId35_g88 = 0;
			float2 ShadowUV115_g84 = ( CentralUV27_g84 + ( CentralUV27_g84 * float2( -1,-1 ) * CZY_ShadowingDistance * Dot49_g84 ) );
			float2 temp_output_21_0_g88 = ShadowUV115_g84;
			float2 break2_g88 = abs( temp_output_21_0_g88 );
			float saferPower4_g88 = abs( break2_g88.x );
			float saferPower3_g88 = abs( break2_g88.y );
			float saferPower6_g88 = abs( ( pow( saferPower4_g88 , 2.0 ) + pow( saferPower3_g88 , 2.0 ) ) );
			float2 temp_output_10_0_g88 = (( ( temp_output_21_0_g88 * ( pow( saferPower6_g88 , Spherize26_g84 ) * Flatness32_g84 ) ) + float2( 0.5,0.5 ) )*( 2.0 / Scale164_g84 ) + Wind18_g84);
			float2 coords35_g88 = temp_output_10_0_g88 * 60.0;
			float2 id35_g88 = 0;
			float2 uv35_g88 = 0;
			float fade35_g88 = 0.5;
			float voroi35_g88 = 0;
			float rest35_g88 = 0;
			for( int it35_g88 = 0; it35_g88 <2; it35_g88++ ){
			voroi35_g88 += fade35_g88 * voronoi35_g88( coords35_g88, time35_g88, id35_g88, uv35_g88, 0,voronoiSmoothId35_g88 );
			rest35_g88 += fade35_g88;
			coords35_g88 *= 2;
			fade35_g88 *= 0.5;
			}//Voronoi35_g88
			voroi35_g88 /= rest35_g88;
			float time13_g88 = 0.0;
			float2 voronoiSmoothId13_g88 = 0;
			float2 coords13_g88 = temp_output_10_0_g88 * 25.0;
			float2 id13_g88 = 0;
			float2 uv13_g88 = 0;
			float fade13_g88 = 0.5;
			float voroi13_g88 = 0;
			float rest13_g88 = 0;
			for( int it13_g88 = 0; it13_g88 <2; it13_g88++ ){
			voroi13_g88 += fade13_g88 * voronoi13_g88( coords13_g88, time13_g88, id13_g88, uv13_g88, 0,voronoiSmoothId13_g88 );
			rest13_g88 += fade13_g88;
			coords13_g88 *= 2;
			fade13_g88 *= 0.5;
			}//Voronoi13_g88
			voroi13_g88 /= rest13_g88;
			float time11_g88 = 17.23;
			float2 voronoiSmoothId11_g88 = 0;
			float2 coords11_g88 = temp_output_10_0_g88 * 9.0;
			float2 id11_g88 = 0;
			float2 uv11_g88 = 0;
			float fade11_g88 = 0.5;
			float voroi11_g88 = 0;
			float rest11_g88 = 0;
			for( int it11_g88 = 0; it11_g88 <2; it11_g88++ ){
			voroi11_g88 += fade11_g88 * voronoi11_g88( coords11_g88, time11_g88, id11_g88, uv11_g88, 0,voronoiSmoothId11_g88 );
			rest11_g88 += fade11_g88;
			coords11_g88 *= 2;
			fade11_g88 *= 0.5;
			}//Voronoi11_g88
			voroi11_g88 /= rest11_g88;
			float lerpResult15_g88 = lerp( saturate( ( voroi35_g88 + voroi13_g88 ) ) , voroi11_g88 , ModifiedCohesion17_g84);
			float lerpResult16_g88 = lerp( lerpResult15_g88 , 1.0 , ( ( 1.0 - CumulusCoverage29_g84 ) + -0.7 ));
			float4 lerpResult83_g84 = lerp( CloudHighlightColor142_g84 , ModifiedCloudColor100_g84 , saturate( SampleGradient( gradient136_g84, saturate( (0.0 + (( Dot49_g84 * ( 1.0 - lerpResult16_g88 ) ) - 0.6) * (1.0 - 0.0) / (1.0 - 0.6)) ) ).r ));
			float4 IT1Color80_g84 = lerpResult83_g84;
			Gradient gradient109_g84 = NewGradient( 0, 2, 2, float4( 0.06119964, 0.06119964, 0.06119964, 0.4411841 ), float4( 1, 1, 1, 0.5794156 ), 0, 0, 0, 0, 0, 0, float2( 1, 0 ), float2( 1, 1 ), 0, 0, 0, 0, 0, 0 );
			float time35_g87 = 0.0;
			float2 voronoiSmoothId35_g87 = 0;
			float2 temp_output_21_0_g87 = ShadowUV115_g84;
			float2 break2_g87 = abs( temp_output_21_0_g87 );
			float saferPower4_g87 = abs( break2_g87.x );
			float saferPower3_g87 = abs( break2_g87.y );
			float saferPower6_g87 = abs( ( pow( saferPower4_g87 , 2.0 ) + pow( saferPower3_g87 , 2.0 ) ) );
			float2 temp_output_10_0_g87 = (( ( temp_output_21_0_g87 * ( pow( saferPower6_g87 , Spherize26_g84 ) * Flatness32_g84 ) ) + float2( 0.5,0.5 ) )*( 2.0 / ( Scale164_g84 * 1.5 ) ) + ( Wind18_g84 * float2( 0.5,0.5 ) ));
			float2 coords35_g87 = temp_output_10_0_g87 * 60.0;
			float2 id35_g87 = 0;
			float2 uv35_g87 = 0;
			float fade35_g87 = 0.5;
			float voroi35_g87 = 0;
			float rest35_g87 = 0;
			for( int it35_g87 = 0; it35_g87 <2; it35_g87++ ){
			voroi35_g87 += fade35_g87 * voronoi35_g87( coords35_g87, time35_g87, id35_g87, uv35_g87, 0,voronoiSmoothId35_g87 );
			rest35_g87 += fade35_g87;
			coords35_g87 *= 2;
			fade35_g87 *= 0.5;
			}//Voronoi35_g87
			voroi35_g87 /= rest35_g87;
			float time13_g87 = 0.0;
			float2 voronoiSmoothId13_g87 = 0;
			float2 coords13_g87 = temp_output_10_0_g87 * 25.0;
			float2 id13_g87 = 0;
			float2 uv13_g87 = 0;
			float fade13_g87 = 0.5;
			float voroi13_g87 = 0;
			float rest13_g87 = 0;
			for( int it13_g87 = 0; it13_g87 <2; it13_g87++ ){
			voroi13_g87 += fade13_g87 * voronoi13_g87( coords13_g87, time13_g87, id13_g87, uv13_g87, 0,voronoiSmoothId13_g87 );
			rest13_g87 += fade13_g87;
			coords13_g87 *= 2;
			fade13_g87 *= 0.5;
			}//Voronoi13_g87
			voroi13_g87 /= rest13_g87;
			float time11_g87 = 17.23;
			float2 voronoiSmoothId11_g87 = 0;
			float2 coords11_g87 = temp_output_10_0_g87 * 9.0;
			float2 id11_g87 = 0;
			float2 uv11_g87 = 0;
			float fade11_g87 = 0.5;
			float voroi11_g87 = 0;
			float rest11_g87 = 0;
			for( int it11_g87 = 0; it11_g87 <2; it11_g87++ ){
			voroi11_g87 += fade11_g87 * voronoi11_g87( coords11_g87, time11_g87, id11_g87, uv11_g87, 0,voronoiSmoothId11_g87 );
			rest11_g87 += fade11_g87;
			coords11_g87 *= 2;
			fade11_g87 *= 0.5;
			}//Voronoi11_g87
			voroi11_g87 /= rest11_g87;
			float lerpResult15_g87 = lerp( saturate( ( voroi35_g87 + voroi13_g87 ) ) , voroi11_g87 , ( ModifiedCohesion17_g84 * 1.1 ));
			float lerpResult16_g87 = lerp( lerpResult15_g87 , 1.0 , ( ( 1.0 - CumulusCoverage29_g84 ) + -0.7 ));
			float4 lerpResult88_g84 = lerp( CloudHighlightColor142_g84 , ModifiedCloudColor100_g84 , saturate( SampleGradient( gradient109_g84, saturate( (0.0 + (( Dot49_g84 * ( 1.0 - lerpResult16_g87 ) ) - 0.6) * (1.0 - 0.0) / (1.0 - 0.6)) ) ).r ));
			float4 IT2Color106_g84 = lerpResult88_g84;
			Gradient gradient62_g84 = NewGradient( 0, 2, 2, float4( 0.06119964, 0.06119964, 0.06119964, 0.4617685 ), float4( 1, 1, 1, 0.5117723 ), 0, 0, 0, 0, 0, 0, float2( 1, 0 ), float2( 1, 1 ), 0, 0, 0, 0, 0, 0 );
			float IT2Alpha65_g84 = SampleGradient( gradient62_g84, temp_output_61_0_g84 ).r;
			float4 lerpResult79_g84 = lerp( ( ( temp_output_10_0_g90 * CZY_CloudFilterColor ) * IT1Color80_g84 ) , IT2Color106_g84 , IT2Alpha65_g84);
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
			float4 lerpResult90_g369 = lerp( lerpResult79_g84 , ( ( temp_output_10_0_g371 * CZY_SunFilterColor ) + temp_output_10_0_g370 ) , temp_output_34_0_g369);
			o.Emission = lerpResult90_g369.rgb;
			Gradient gradient59_g84 = NewGradient( 0, 2, 2, float4( 0.06119964, 0.06119964, 0.06119964, 0.4617685 ), float4( 1, 1, 1, 0.5117723 ), 0, 0, 0, 0, 0, 0, float2( 1, 0 ), float2( 1, 1 ), 0, 0, 0, 0, 0, 0 );
			float IT1Alpha66_g84 = SampleGradient( gradient59_g84, temp_output_60_0_g84 ).r;
			float temp_output_173_0_g84 = max( IT1Alpha66_g84 , IT2Alpha65_g84 );
			o.Alpha = ( saturate( ( temp_output_173_0_g84 + ( temp_output_173_0_g84 * 2.0 * CZY_CloudThickness ) ) ) * ( 1.0 - 0.0 ) );
			clip( 0.5 - _Cutoff );
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
Node;AmplifyShaderEditor.FunctionNode;1213;-1440,-624;Inherit;False;Stylized Clouds (Ghibli Desktop);-1;;84;6b2329401197307438a30ab78c237583;0;0;3;COLOR;0;FLOAT;183;FLOAT;184
Node;AmplifyShaderEditor.RangedFloatNode;1215;-1424,-400;Inherit;False;Global;CZY_CloudsFogAmount;CZY_CloudsFogAmount;8;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1216;-1424,-480;Inherit;False;Global;CZY_CloudsFogLightAmount;CZY_CloudsFogLightAmount;7;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;1214;-1088,-608;Inherit;False;AddFogToSkyLayer;-1;;369;36a78fe96c9f6fa4dab85c7793736468;0;3;89;COLOR;0,0,0,0;False;91;FLOAT;0;False;59;FLOAT;0;False;2;COLOR;84;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;-695.2959,-681.1561;Float;False;True;-1;2;DistantLands.Cozy.EditorScripts.EmptyShaderGUI;0;0;Unlit;Distant Lands/Cozy/BiRP/Stylized Clouds (Ghibli Desktop);False;False;False;False;False;False;False;False;False;True;False;False;False;False;True;False;False;False;False;False;False;Front;0;False;;0;False;;False;0;False;;0;False;;False;0;Custom;0.5;True;True;-50;True;TransparentCutout;;Transparent;ForwardOnly;12;all;True;True;True;True;0;False;;True;221;False;;255;False;;255;False;;7;False;;3;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;2;5;False;;10;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;0;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;1214;89;1213;0
WireConnection;1214;91;1216;0
WireConnection;1214;59;1215;0
WireConnection;0;2;1214;84
WireConnection;0;9;1213;183
WireConnection;0;10;1213;184
ASEEND*/
//CHKSM=5FCA46A5BFFF118B79AFA7D1627182EC9C752E7E