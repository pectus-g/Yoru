Shader "Hidden/XFur Studio 4/Designer/Painter"
{
	Properties
	{
		_InputMap("Input Map", 2D) = "white" {}

		_BrushPosition("Brush Position", Vector) = (0,0,0,0)
		_BrushColor("Brush Color", Vector) = (1,1,1,1)
		_BrushOpacity("Brush Opacity", Float) = 1
		_BrushShape("Brush Shape", 2D) = "white"{}
		_BrushNormal("Brush Normal", Vector) = (0,0,1,1)
		_BrushHardness("Brush Hardness", Float) = 0.9
		_BrushMixMode("Color Mix Mode", Float) = 0
	}
		SubShader
		{

			Cull Off ZWrite Off ZTest Always

			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag

				#include "UnityCG.cginc"


				uniform sampler2D _CameraDepthTexture;


				struct appdata
				{
					float4 vertex : POSITION;
					float3 normal: NORMAL;
					float2 uv : TEXCOORD;
					float4 screenPos : TEXCOORD3;
				};

				struct v2f
				{
					float2 uv : TEXCOORD0;
					float3 worldPos : TEXCOORD1;
					float3 normal : TEXCOORD2;
					float4 vertex : SV_POSITION;
					float4 screenPos : TEXCOORD3;
				};

				v2f vert(appdata v)
				{
					#if UNITY_UV_STARTS_AT_TOP
					v.uv.y = 1.0 - v.uv.y;
					#endif

					v2f o;
					o.vertex = float4(v.uv * 2.0 - 1.0, 0.0, 1.0);
					o.uv = v.uv;
					o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
					o.normal = UnityObjectToWorldNormal(v.normal);
					o.screenPos = ComputeScreenPos(UnityObjectToClipPos(v.vertex));
					return o;
				}

				sampler2D _InputMap;

				uniform float4x4 _ProjMatrix, _WorldToDrawerMatrix;

				sampler2D _BrushShape;
				half4 _BrushPosition, _BrushColor;
				half _BrushOpacity, _BrushMixMode;
				float4 _BrushNormal;
				half _BrushSize, _BrushHardness;
				half _PaintR, _PaintG, _PaintB, _PaintA;


				float SphereMask(float3 position, float3 center, float radius, float hardness) {
					return saturate((radius - distance(position,center)) / (1 - max(saturate(hardness * 2),0.5)));
				}

				half4 frag(v2f i) : SV_Target
				{

					float cookie = SphereMask(i.worldPos, _BrushPosition.xyz, _BrushSize, _BrushHardness);

					cookie *= saturate(sign(dot(i.normal, _BrushNormal) - 0.25));

					cookie *= tex2D(_BrushShape, (float2(sign(i.normal.x) * i.worldPos.y, i.worldPos.z) - _BrushPosition.yz) / (_BrushSize * 2) - 0.5).r;


	#if UNITY_UV_STARTS_AT_TOP
					i.uv.y = 1.0 - i.uv.y;
	#endif
					half4 col = tex2D(_InputMap, i.uv);

					col.r = lerp(lerp(col.r, _BrushColor.r, saturate(_BrushOpacity * cookie) * _PaintR), col.r + _BrushColor.r * saturate(_BrushOpacity * cookie) * _PaintR, _BrushMixMode);
					col.g = lerp(lerp(col.g, _BrushColor.g, saturate(_BrushOpacity * cookie) * _PaintG), col.g + _BrushColor.g * saturate(_BrushOpacity * cookie) * _PaintG, _BrushMixMode);
					col.b = lerp(lerp(col.b, _BrushColor.b, saturate(_BrushOpacity * cookie) * _PaintB), col.b + _BrushColor.b * saturate(_BrushOpacity * cookie) * _PaintB, _BrushMixMode);
					col.a = lerp(lerp(col.a, _BrushColor.a, saturate(_BrushOpacity * cookie) * _PaintA), col.a + _BrushColor.a * saturate(_BrushOpacity * cookie) * _PaintA, _BrushMixMode);
					//col.a = 1;
					return col;
				}
				ENDCG
			}


			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag

				#include "UnityCG.cginc"


				uniform sampler2D _CameraDepthTexture;


				struct appdata
				{
					float4 vertex : POSITION;
					float3 normal: NORMAL;
					float2 uv : TEXCOORD;
					float4 tangent:TANGENT;
				};

				struct v2f
				{
					float2 uv : TEXCOORD0;
					float3 worldPos : TEXCOORD1;
					float3 normal : TEXCOORD2;
					float4 vertex : SV_POSITION;
					float3x3 worldToTangent : TEXCOORD3;
				};

				v2f vert(appdata v)
				{
					#if UNITY_UV_STARTS_AT_TOP
					v.uv.y = 1.0 - v.uv.y;
					#endif

					v2f o;
					o.vertex = float4(v.uv * 2.0 - 1.0, 0.0, 1.0);
					o.uv = v.uv;
					o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
					o.normal = UnityObjectToWorldNormal(v.normal);

					float3 wNormal = UnityObjectToWorldNormal(v.normal);
					float3 wTangent = UnityObjectToWorldDir(v.tangent.xyz);
					// compute bitangent from cross product of normal and tangent
					float tangentSign = v.tangent.w * unity_WorldTransformParams.w;
					float3 wBitangent = cross(wNormal, wTangent);// *tangentSign;
					// output the world to tangent space matrix
					o.worldToTangent = float3x3(wTangent, wBitangent, wNormal);
					return o;
				}

				sampler2D _InputMap;

				uniform float4x4 _ProjMatrix, _WorldToDrawerMatrix;

				sampler2D _BrushShape;
				float4 _BrushPosition, _BrushColor;
				half _BrushOpacity, _BrushMixMode;
				float4 _BrushNormal;
				half _BrushSize, _BrushHardness;


				float SphereMask(float3 position, float3 center, float radius, float hardness) {
					return saturate((radius - distance(position,center)) / (1 - max(saturate(hardness * 2),0.5)));
				}

				half4 frag(v2f i) : SV_Target
				{

					#if UNITY_UV_STARTS_AT_TOP
					i.uv.y = 1.0 - i.uv.y;
					#endif

					float cookie = SphereMask(i.worldPos, _BrushPosition.xyz, _BrushSize, _BrushHardness);

					cookie *= saturate(sign(dot(i.normal, _BrushNormal) - 0.25));

					float4 col = tex2D(_InputMap, i.uv);

					_BrushColor = normalize(_BrushColor);

					float3 tempDir = mul(i.worldToTangent, _BrushColor);

					float4 finalDir = float4(normalize((col.xyz * 2.0 - 1) + tempDir), 1);
					finalDir.xyz = (finalDir.xyz + 1) / 2.0;

					col = lerp(lerp(col, finalDir, saturate(_BrushOpacity * cookie)), lerp(col,float4(0.5f,0.5f,0.5f,1), saturate(_BrushOpacity * cookie)), _BrushMixMode);

					return col;
				}
				ENDCG
			}
				Pass

				{
					Name "DECAL GENERATOR"

					CGPROGRAM
					#pragma vertex vert
					#pragma fragment frag

					#include "UnityCG.cginc"


					struct appdata
					{
						float4 vertex : POSITION;
						float3 normal: NORMAL;
						float2 uv : TEXCOORD;
						float4 screenPos : TEXCOORD3;
					};

					struct v2f
					{
						float2 uv : TEXCOORD0;
						float3 worldPos : TEXCOORD1;
						float3 normal : TEXCOORD2;
						float4 vertex : SV_POSITION;
						float4 screenPos : TEXCOORD3;
					};

					v2f vert(appdata v)
					{
						#if UNITY_UV_STARTS_AT_TOP
						v.uv.y = 1.0 - v.uv.y;
						#endif

						v2f o;
						o.vertex = float4(v.uv * 2.0 - 1.0, 0.0, 1.0);
						o.uv = v.uv;
						o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
						o.normal = UnityObjectToWorldNormal(v.normal);
						o.screenPos = ComputeScreenPos(UnityObjectToClipPos(v.vertex));
						return o;
					}

					sampler2D _InputMap;

					uniform float4x4 _ProjMatrix, _WorldToDrawerMatrix;

					sampler2D _Decal;
					half4 _BrushPosition;
					half _BrushOpacity, _BrushMixMode;
					float4 _BrushNormal;
					half _BrushSize, _BrushHardness;
					half _PaintR, _PaintG, _PaintB, _PaintA;


					float SphereMask(float3 position, float3 center, float radius, float hardness) {
						return saturate((radius - distance(position,center)) / (1 - max(saturate(hardness * 2),0.5)));
					}

					half4 frag(v2f i) : SV_Target
					{

						float cookie = SphereMask(i.worldPos, _BrushPosition.xyz, _BrushSize, _BrushHardness);

						cookie *= saturate(sign(dot(i.normal, _BrushNormal) - 0.25));



						half4 decalX = tex2D(_Decal, (float2(sign(i.normal.x) * i.worldPos.y, i.worldPos.z) - _BrushPosition.yz) / (_BrushSize * 2) - 0.5);
						half4 decalY = tex2D(_Decal, (float2(sign(i.normal.y) * i.worldPos.x, i.worldPos.z) - _BrushPosition.xz) / (_BrushSize * 2) - 0.5);
						half4 decalZ = tex2D(_Decal, (float2(sign(i.normal.z) * i.worldPos.x, i.worldPos.y) - _BrushPosition.yz) / (_BrushSize * 2) - 0.5);

						half3 blend = i.normal / dot(i.normal, 1.0);

						half4 decal = decalX * blend.x + decalY * blend.y + decalZ * blend.z;

						cookie *= decal.a;


		#if UNITY_UV_STARTS_AT_TOP
						i.uv.y = 1.0 - i.uv.y;
		#endif
						half4 col = tex2D(_InputMap, i.uv);

						col = saturate(lerp(lerp(col, decal, saturate(_BrushOpacity * cookie)), col + decal * saturate(_BrushOpacity * cookie), _BrushMixMode));
						return col;
					}
					ENDCG
				}

		}
}
