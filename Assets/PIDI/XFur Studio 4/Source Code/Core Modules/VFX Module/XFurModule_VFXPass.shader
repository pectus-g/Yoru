Shader "Hidden/XFur Studio/Modules/VFX Pass"
{
	Properties
	{
		_InputMap("Input Map", 2D) = "black" {}
		_VFXMask0("VFXMask 0", 2D) = "white"{}
		_VFXMask1("VFXMask 1", 2D) = "white"{}
		_VFXMask2("VFXMask 2", 2D) = "white"{}
		_VFXMask3("VFXMask 3", 2D) = "white"{}
		_XFurVFX0Direction("Direction", Vector) = (0,-1, 0, 1)
		_XFurVFX1Direction("Snow Direction", Vector) = (0,-1, 0, 1)
		_XFurVFX2Direction("Rain Direction", Vector) = (0,-1, 0, 1)
		_XFurVFX3Direction("CustomFX Direction", Vector) = (0,-1, 0, 1)
		_VFXTiling0("VFX Tiling 0", Float) = 2
		_VFXTiling1("VFX Tiling 1", Float) = 2
		_VFXTiling2("VFX Tiling 2", Float) = 2
		_VFXTiling3("VFX Tiling 3", Float) = 2


		_XFurVFX1GlobalAdd("VFX 1 Global Add", Range(0,1)) = 1
		_XFurVFX2GlobalAdd("VFX 2 Global Add", Range(0,1)) = 1
		_XFurVFX3GlobalAdd("VFX 3 Global Add", Range(0,1)) = 1

		_XFurVFXGlobalMask("VFX Global Mask", Vector) = (1,1,1,1)
	}
		SubShader
		{

			Cull Off ZWrite Off ZTest Always

			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma shader_feature_local USE_VERTEXBUFFER
				#include "UnityCG.cginc"


				uniform sampler2D _CameraDepthTexture;

				#if USE_VERTEXBUFFER
				ByteAddressBuffer _XFurVertexBuffer;
				float4 _XFurVertexBufferStridePosNormal;

				float3 GetPosition(uint vertexID)
				{
					return asfloat(_XFurVertexBuffer.Load3((vertexID * _XFurVertexBufferStridePosNormal.x + _XFurVertexBufferStridePosNormal.y )));// [vertexID * _VertexBufferStride] ;
				}


				float3 GetNormal( uint vertexID){
	
					return asfloat( _XFurVertexBuffer.Load3(( vertexID * _XFurVertexBufferStridePosNormal.x +  _XFurVertexBufferStridePosNormal.z )));
				}

				float4 GetTangent( uint vertexID){
	
					return asfloat( _XFurVertexBuffer.Load4(( vertexID * _XFurVertexBufferStridePosNormal.x + _XFurVertexBufferStridePosNormal.w )));
				}

				#endif


				struct appdata
				{
					float4 vertex : POSITION;
					float3 normal: NORMAL;
					float4 uv : TEXCOORD;
					float4 texcoord1 : TEXCOORD1;
					float4 screenPos : TEXCOORD3;
				};

				struct v2f
				{
					float4 uv : TEXCOORD0;
					float3 worldPos : TEXCOORD1;
					float3 normal : TEXCOORD2;
					float4 vertex : SV_POSITION;
					float4 screenPos : TEXCOORD3;
				};

				float _XFurBasicMode;
				float4x4 _XFurObjectMatrix;

				v2f vert(appdata v)
				{
					#if UNITY_UV_STARTS_AT_TOP
					v.uv.y = 1.0 - v.uv.y;
					#endif

					v2f o;

					
                    #if USE_VERTEXBUFFER
                        v.vertex = float4( GetPosition( v.texcoord1.z), 1 );	
					    v.normal = GetNormal( v.texcoord1.z );
                    #endif

					o.vertex = float4(v.uv.xy * 2.0 - 1.0, 0.0, 1.0);
					o.uv = float4(v.uv.xy, v.texcoord1.xy);
					o.worldPos = lerp( mul(_XFurObjectMatrix, v.vertex).xyz, mul(_XFurObjectMatrix, v.vertex).xyz, _XFurBasicMode);
					o.normal = mul( _XFurObjectMatrix, v.normal );
					o.screenPos = ComputeScreenPos(UnityObjectToClipPos(v.vertex));
					return o;
				}

				sampler2D _InputMap;

				sampler2D _VFXMask0, _VFXMask1, _VFXMask2, _VFXMask3;

				uniform float4x4 _ProjMatrix, _WorldToDrawerMatrix;

				float4 _XFurVFX0Direction, _XFurVFX1Direction, _XFurVFX2Direction,_XFurVFX3Direction;
				float _XFurVFX1GlobalAdd, _XFurVFX2GlobalAdd, _XFurVFX3GlobalAdd;
				float _VFXTiling0, _VFXTiling1, _VFXTiling2, _VFXTiling3;
				float _FXAdd0, _FXAdd1, _FXAdd2, _FXAdd3;
				float _FXMelt0, _FXMelt1, _FXMelt2, _FXMelt3;
				float _FXFalloff0, _FXFalloff1, _FXFalloff2, _FXFalloff3;

				float4 _XFurVFXGlobalMask;

				half4 frag(v2f i) : SV_Target
				{


					float4 vfx0 = tex2D(_VFXMask0, i.uv.zw * _VFXTiling0);
					float4 vfx1 = tex2D(_VFXMask1, i.uv.zw * _VFXTiling1);
					float4 vfx2 = tex2D(_VFXMask2, i.uv.zw * _VFXTiling2);





					#if UNITY_UV_STARTS_AT_TOP
					i.uv.y = 1.0 - i.uv.y;
					#endif

					half4 col = tex2D(_InputMap, i.uv);
					col.r += saturate(dot(i.normal, -_XFurVFX0Direction.xyz) - _FXFalloff0) * _FXAdd0 * Luminance(vfx0.rgb);
					col.r = clamp(col.r, 0, min(length(vfx0.rgb * 1.25), 1));
					col.g += pow(saturate(dot(i.normal, -_XFurVFX1Direction.xyz)), _FXFalloff1) * _FXAdd1 * _XFurVFX1GlobalAdd * Luminance(vfx1.rgb) * _XFurVFXGlobalMask.r;
					col.g = clamp(col.g, 0, min(length(vfx1.rgb * 1.25), 1));
					col.b += saturate(dot(i.normal, -_XFurVFX2Direction.xyz) - _FXFalloff2) * _FXAdd2 * _XFurVFX2GlobalAdd * _XFurVFXGlobalMask.g;
					col.b = clamp(col.b, 0, min(length(vfx2.rgb * 1.25), 1));
					col.r -= _FXMelt0;
					col.g -= _FXMelt1;
					col.b -= _FXMelt2;
					return col;
				}
				ENDCG
			}
		}
}
