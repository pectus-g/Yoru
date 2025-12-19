// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Distant Lands/Cozy/BiRP/Stylized Clouds (Mobile)"
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
		uniform float CZY_DetailScale;
		uniform float CZY_DetailAmount;
		uniform half CZY_CloudFlareFalloff;
		uniform float CZY_ClippingThreshold;
		uniform float4 CZY_LightColor;
		uniform float4 CZY_FogColor5;
		uniform float CZY_LightFlareSquish;
		uniform half CZY_LightIntensity;
		uniform half CZY_LightFalloff;
		uniform float CZY_CloudsFogLightAmount;
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


		float2 voronoihash22_g84( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi22_g84( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash22_g84( n + g );
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


		float2 voronoihash32_g84( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi32_g84( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
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
			 		float2 o = voronoihash32_g84( n + g );
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
			float3 hsvTorgb2_g85 = RGBToHSV( CZY_CloudColor.rgb );
			float3 hsvTorgb3_g85 = HSVToRGB( float3(hsvTorgb2_g85.x,saturate( ( hsvTorgb2_g85.y + CZY_FilterSaturation ) ),( hsvTorgb2_g85.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g85 = ( float4( hsvTorgb3_g85 , 0.0 ) * CZY_FilterColor );
			float4 temp_output_85_0_g84 = ( temp_output_10_0_g85 * CZY_CloudFilterColor );
			float3 hsvTorgb2_g86 = RGBToHSV( CZY_CloudHighlightColor.rgb );
			float3 hsvTorgb3_g86 = HSVToRGB( float3(hsvTorgb2_g86.x,saturate( ( hsvTorgb2_g86.y + CZY_FilterSaturation ) ),( hsvTorgb2_g86.z + CZY_FilterValue )) );
			float4 temp_output_10_0_g86 = ( float4( hsvTorgb3_g86 , 0.0 ) * CZY_FilterColor );
			float4 temp_output_86_0_g84 = ( temp_output_10_0_g86 * CZY_SunFilterColor );
			float2 Pos13_g84 = i.uv_texcoord;
			float mulTime2_g84 = _Time.y * ( 0.001 * CZY_WindSpeed );
			float TIme5_g84 = mulTime2_g84;
			float simplePerlin2D28_g84 = snoise( ( Pos13_g84 + ( TIme5_g84 * float2( 0.2,-0.4 ) ) )*( 100.0 / CZY_MainCloudScale ) );
			simplePerlin2D28_g84 = simplePerlin2D28_g84*0.5 + 0.5;
			float2 temp_output_24_0_g84 = ( i.uv_texcoord - float2( 0.5,0.5 ) );
			float dotResult19_g84 = dot( temp_output_24_0_g84 , temp_output_24_0_g84 );
			float CurrentCloudCover31_g84 = CZY_CumulusCoverageMultiplier;
			float temp_output_38_0_g84 = (0.0 + (dotResult19_g84 - 0.0) * (CurrentCloudCover31_g84 - 0.0) / (0.27 - 0.0));
			float time22_g84 = 0.0;
			float2 voronoiSmoothId22_g84 = 0;
			float2 coords22_g84 = ( Pos13_g84 + ( TIme5_g84 * float2( 0.3,0.2 ) ) ) * ( 140.0 / CZY_MainCloudScale );
			float2 id22_g84 = 0;
			float2 uv22_g84 = 0;
			float voroi22_g84 = voronoi22_g84( coords22_g84, time22_g84, id22_g84, uv22_g84, 0, voronoiSmoothId22_g84 );
			float temp_output_56_0_g84 = (0.0 + (min( ( simplePerlin2D28_g84 + temp_output_38_0_g84 ) , ( ( 1.0 - voroi22_g84 ) + temp_output_38_0_g84 ) ) - ( 1.0 - CurrentCloudCover31_g84 )) * (1.0 - 0.0) / (1.0 - ( 1.0 - CurrentCloudCover31_g84 )));
			float4 lerpResult69_g84 = lerp( temp_output_86_0_g84 , temp_output_85_0_g84 , saturate( (2.0 + (temp_output_56_0_g84 - 0.0) * (0.7 - 2.0) / (1.0 - 0.0)) ));
			float3 ase_positionWS = i.worldPos;
			float3 normalizeResult9_g84 = normalize( ( ase_positionWS - _WorldSpaceCameraPos ) );
			float dotResult16_g84 = dot( normalizeResult9_g84 , CZY_SunDirection );
			float temp_output_37_0_g84 = abs( (dotResult16_g84*0.5 + 0.5) );
			half LightMask57_g84 = saturate( pow( temp_output_37_0_g84 , CZY_SunFlareFalloff ) );
			float temp_output_63_0_g84 = ( voroi22_g84 * saturate( ( CurrentCloudCover31_g84 - 0.8 ) ) );
			float4 lerpResult82_g84 = lerp( ( lerpResult69_g84 + ( LightMask57_g84 * temp_output_86_0_g84 * ( 1.0 - temp_output_63_0_g84 ) ) ) , ( temp_output_85_0_g84 * float4( 0.5660378,0.5660378,0.5660378,0 ) ) , temp_output_63_0_g84);
			float time32_g84 = 0.0;
			float2 voronoiSmoothId32_g84 = 0;
			float2 coords32_g84 = ( Pos13_g84 + ( TIme5_g84 * float2( 0.3,0.2 ) ) ) * ( 100.0 / CZY_DetailScale );
			float2 id32_g84 = 0;
			float2 uv32_g84 = 0;
			float fade32_g84 = 0.5;
			float voroi32_g84 = 0;
			float rest32_g84 = 0;
			for( int it32_g84 = 0; it32_g84 <3; it32_g84++ ){
			voroi32_g84 += fade32_g84 * voronoi32_g84( coords32_g84, time32_g84, id32_g84, uv32_g84, 0,voronoiSmoothId32_g84 );
			rest32_g84 += fade32_g84;
			coords32_g84 *= 2;
			fade32_g84 *= 0.5;
			}//Voronoi32_g84
			voroi32_g84 /= rest32_g84;
			float temp_output_50_0_g84 = ( (0.0 + (( 1.0 - voroi32_g84 ) - 0.3) * (0.5 - 0.0) / (1.0 - 0.3)) * 0.1 * CZY_DetailAmount );
			float temp_output_80_0_g84 = saturate( ( temp_output_56_0_g84 + temp_output_50_0_g84 ) );
			float4 lerpResult78_g84 = lerp( temp_output_85_0_g84 , lerpResult82_g84 , (1.0 + (temp_output_80_0_g84 - 0.0) * (0.0 - 1.0) / (1.0 - 0.0)));
			float CloudDetail51_g84 = temp_output_50_0_g84;
			float CloudLight54_g84 = saturate( pow( temp_output_37_0_g84 , CZY_CloudFlareFalloff ) );
			float4 lerpResult75_g84 = lerp( float4( 0,0,0,0 ) , temp_output_86_0_g84 , ( saturate( ( CurrentCloudCover31_g84 - 1.0 ) ) * CloudDetail51_g84 * CloudLight54_g84 ));
			float4 SunThroughCLouds81_g84 = ( lerpResult75_g84 * 1.3 );
			clip( ( ( 1.0 - 0.0 ) * temp_output_80_0_g84 ) - CZY_ClippingThreshold);
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
			float4 lerpResult90_g369 = lerp( ( lerpResult78_g84 + SunThroughCLouds81_g84 ) , ( ( temp_output_10_0_g371 * CZY_SunFilterColor ) + temp_output_10_0_g370 ) , temp_output_34_0_g369);
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
Node;AmplifyShaderEditor.FunctionNode;1212;-1616,-640;Inherit;False;Stylized Clouds (Mobile);-1;;84;f983fbf9fb93b5247b6549c0ab1e449a;0;0;3;COLOR;0;FLOAT;108;FLOAT;109
Node;AmplifyShaderEditor.ClipNode;1214;-1376,-640;Inherit;False;3;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;1215;-1360,-432;Inherit;False;Global;CZY_CloudsFogAmount;CZY_CloudsFogAmount;8;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1216;-1360,-512;Inherit;False;Global;CZY_CloudsFogLightAmount;CZY_CloudsFogLightAmount;7;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;1217;-1024,-640;Inherit;False;AddFogToSkyLayer;-1;;369;36a78fe96c9f6fa4dab85c7793736468;0;3;89;COLOR;0,0,0,0;False;91;FLOAT;0;False;59;FLOAT;0;False;2;COLOR;84;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;-695.2959,-681.1561;Float;False;True;-1;2;DistantLands.Cozy.EditorScripts.EmptyShaderGUI;0;0;Unlit;Distant Lands/Cozy/BiRP/Stylized Clouds (Mobile);False;False;False;False;False;False;False;False;False;True;False;False;False;False;True;False;False;False;False;False;False;Front;0;False;;0;False;;False;0;False;;0;False;;False;0;Custom;0.5;True;True;-50;True;TransparentCutout;;Transparent;ForwardOnly;12;all;True;True;True;True;0;False;;True;221;False;;255;False;;255;False;;7;False;;3;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;2;5;False;;10;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;0;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;1214;0;1212;0
WireConnection;1214;1;1212;108
WireConnection;1214;2;1212;109
WireConnection;1217;89;1214;0
WireConnection;1217;91;1216;0
WireConnection;1217;59;1215;0
WireConnection;0;2;1217;84
ASEEND*/
//CHKSM=B128F915A3464B76B9F59FAF5F4DC52B3704B56D