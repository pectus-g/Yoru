Shader "Hidden/XFur Studio/Modules/GPU Physics"
{
    Properties
    {
        _InputMap("Input Map", 2D) = "white" {}
        _GroomingMap("Grooming Map", 2D) = "white" {}
        _VFXMap("VFX Map",2D) = "black"{}
        _PhysicsMap("Physics Map", 2D) = "black"{}
        _HasGroomData("Has Groom Data", Float) = 0
        _InputType("Input Type", Float) = 0
        _XFWorldDirection("World Direction", Vector) = (0, 0, 0, 0)
        _PhysicsThreshold("Physics Threshold", Float) = 0
    }

        SubShader
        {
            Tags { "RenderType" = "Opaque" }
            LOD 100

            //WPOS PASS
            Pass
            {
                CGPROGRAM
                #pragma target 2.0
                #pragma vertex vert
                #pragma fragment frag
                #pragma shader_feature_local USE_VERTEXBUFFER

                #include "UnityCG.cginc"

                struct appdata
                {
                    float4 vertex : POSITION;
                    float3 normal: NORMAL;
                    float2 uv : TEXCOORD;
                    float4 texcoord1 : TEXCOORD1;
                    float4 tangent:TANGENT;
                };

                struct v2f
                {
                    float2 uv : TEXCOORD0;
                    float3 worldPos : TEXCOORD1;
                    float3 normal : TEXCOORD2;
                    float4 vertex : SV_POSITION;
                    float4 tangent : TANGENT;
                };


                float _XFurBasicMode;
                float4x4 _XFurObjectMatrix;
                float4x4 _XFurWorldMatrix;
                
#if USE_VERTEXBUFFER
#if defined(SHADER_API_D3D11) || defined( SHADER_API_VULKAN ) || defined (SHADER_API_METAL) || defined (SHADER_API_GLES3) || defined(SHADER_API_GLCORE)
                ByteAddressBuffer _XFurVertexBuffer;
				float4 _XFurVertexBufferStridePosNormal;


				float3 GetPosition(uint vertexID)
				{
					return asfloat(_XFurVertexBuffer.Load3((vertexID * _XFurVertexBufferStridePosNormal.x + _XFurVertexBufferStridePosNormal.y )));// [vertexID * _XFurVertexBufferStride] ;
				}


				float3 GetNormal( uint vertexID){
	
					return asfloat( _XFurVertexBuffer.Load3(( vertexID * _XFurVertexBufferStridePosNormal.x +  _XFurVertexBufferStridePosNormal.z )));
				}

				float4 GetTangent( uint vertexID){
	
					return asfloat( _XFurVertexBuffer.Load4(( vertexID * _XFurVertexBufferStridePosNormal.x + _XFurVertexBufferStridePosNormal.w )));
				}
                #endif
                #endif


                v2f vert(appdata v)
                {
                    #if UNITY_UV_STARTS_AT_TOP
                    v.uv.y = 1.0 - v.uv.y;
                    #endif

                    v2f o;
                    o.vertex = float4(v.uv * 2.0 - 1.0, 0.0, 1.0);
                    o.uv = v.uv;

#if USE_VERTEXBUFFER
                   #if defined(SHADER_API_D3D11) || defined( SHADER_API_VULKAN ) || defined (SHADER_API_METAL) || defined (SHADER_API_GLES3) || defined(SHADER_API_GLCORE)
                        v.vertex = float4( GetPosition( v.texcoord1.z), 1 );	
					    v.normal = GetNormal( v.texcoord1.z );
                        #endif
#endif

                    o.worldPos = mul( _XFurObjectMatrix, v.vertex).xyz;
                    o.normal = lerp(mul(_XFurObjectMatrix, v.normal), mul(_XFurObjectMatrix, v.normal), _XFurBasicMode);
                    o.tangent = v.tangent;
                    return o;
                }

                sampler2D _InputMap;
                sampler2D _PhysicsMap;
                float _InputType;
                float4 _InputMap_ST;
                float4 _XFWorldDirection;
                float4 _XFWorldPosition;



                fixed4 frag(v2f i) : SV_Target{
                    #if UNITY_UV_STARTS_AT_TOP
                    i.uv.y = 1.0 - i.uv.y;
                    #endif

                    float4 col = float4( i.worldPos -_XFWorldPosition.xyz, 1);
                    return col;
                }
                ENDCG
            }


            //MOTIONV GOAL PASS
            Pass
            {
                CGPROGRAM
                #pragma target 2.0
                #pragma vertex vert
                #pragma fragment frag
                #pragma shader_feature_local USE_VERTEXBUFFER
                #include "UnityCG.cginc"

                struct appdata
                {
                    float4 vertex : POSITION;
                    float3 normal: NORMAL;
                    float2 uv : TEXCOORD;
                    float4 texcoord1 : TEXCOORD1;
                    float4 tangent:TANGENT;
                };

                struct v2f
                {
                    float2 uv : TEXCOORD0;
                    float3 worldPos : TEXCOORD1;
                    float3 normal : TEXCOORD2;
                    float4 vertex : SV_POSITION;
                    float4 tangent : TANGENT;
                };


                float _XFurBasicMode;
                float4x4 _XFurObjectMatrix;
                float4 _XFWorldPosition;

                #if USE_VERTEXBUFFER
                #if defined(SHADER_API_D3D11) || defined( SHADER_API_VULKAN ) || defined (SHADER_API_METAL) || defined (SHADER_API_GLES3) || defined(SHADER_API_GLCORE)
                ByteAddressBuffer _XFurVertexBuffer;
				float4 _XFurVertexBufferStridePosNormal;
                
				float3 GetPosition(uint vertexID)
				{
					return asfloat(_XFurVertexBuffer.Load3((vertexID * _XFurVertexBufferStridePosNormal.x + _XFurVertexBufferStridePosNormal.y )));// [vertexID * _XFurVertexBufferStride] ;
				}


				float3 GetNormal( uint vertexID){
	
					return asfloat( _XFurVertexBuffer.Load3(( vertexID * _XFurVertexBufferStridePosNormal.x +  _XFurVertexBufferStridePosNormal.z )));
				}

				float4 GetTangent( uint vertexID){
	
					return asfloat( _XFurVertexBuffer.Load4(( vertexID * _XFurVertexBufferStridePosNormal.x + _XFurVertexBufferStridePosNormal.w )));
				}
                #endif
                #endif


                v2f vert(appdata v)
                {
                    #if UNITY_UV_STARTS_AT_TOP
                    v.uv.y = 1.0 - v.uv.y;
                    #endif

                    v2f o;
                    o.vertex = float4(v.uv * 2.0 - 1.0, 0.0, 1.0);
                    o.uv = v.uv;

                    #if USE_VERTEXBUFFER
                    #if defined(SHADER_API_D3D11) || defined( SHADER_API_VULKAN ) || defined (SHADER_API_METAL) || defined (SHADER_API_GLES3) || defined(SHADER_API_GLCORE)
                    v.vertex = float4( GetPosition( v.texcoord1.z), 1 );		 
                    v.normal = GetNormal( v.texcoord1.z );
                    #endif
                    #endif


                    o.worldPos = mul(_XFurObjectMatrix, v.vertex).xyz;
                    o.worldPos -= _XFWorldPosition.xyz;
                    o.normal = lerp(mul(_XFurObjectMatrix, v.normal), mul(_XFurObjectMatrix, v.normal), _XFurBasicMode);
                    o.tangent = v.tangent;
                    return o;
                }

                sampler2D _InputMap;
                sampler2D _PhysicsMap;
                float _PhysicsThreshold;
                float _InputType;
                float4 _InputMap_ST;
                float4 _XFWorldDirection;
                float _XFurGravityStrength;
                float _XFurPhysicsSensitivity;
                float _XFurPhysicsStrength;

                fixed4 frag(v2f i) : SV_Target{
                    #if UNITY_UV_STARTS_AT_TOP
                    i.uv.y = 1.0 - i.uv.y;
                    #endif

                    float4 wPos = tex2D(_InputMap,i.uv);
                    float3 norm = ( wPos.xyz - i.worldPos)*_XFurPhysicsSensitivity;
                    norm += float3(0, -_XFurGravityStrength, 0);
                    norm += _XFWorldDirection.xyz*_XFurPhysicsSensitivity*0.15;
                    norm = clamp(norm, float3(-1, -1, -1), float3(1, 1, 1));
                    float4 col = float4( norm * _XFurPhysicsStrength, 1 );
                    return col;
                }
                ENDCG
            }

                //GPU PHYSICS PASS

                Pass
                {
                    CGPROGRAM
                    #pragma target 2.0
                    #pragma vertex vert
                    #pragma fragment frag
                    #pragma shader_feature_local USE_VERTEXBUFFER
                    #include "UnityCG.cginc"

                    struct appdata
                    {
                        float4 vertex : POSITION;
                        float3 normal: NORMAL;
                        float2 uv : TEXCOORD;
                        float4 texcoord1 : TEXCOORD1;
                        float4 tangent:TANGENT;
                    };

                    struct v2f
                    {
                        float2 uv : TEXCOORD0;
                        float3 worldPos : TEXCOORD1;
                        float3 normal : TEXCOORD2;
                        float4 vertex : SV_POSITION;
                        float4 tangent : TANGENT;
                    };


                    float _XFurBasicMode;
                    float4x4 _XFurObjectMatrix;

                    #if USE_VERTEXBUFFER
                    #if defined(SHADER_API_D3D11) || defined( SHADER_API_VULKAN ) || defined (SHADER_API_METAL) || defined (SHADER_API_GLES3) || defined(SHADER_API_GLCORE)
                    ByteAddressBuffer _XFurVertexBuffer;
				    float4 _XFurVertexBufferStridePosNormal;
				    int _VertexCount;
				    float _Scale;

				    float3 GetPosition(uint vertexID)
				    {
					    return asfloat(_XFurVertexBuffer.Load3((vertexID * _XFurVertexBufferStridePosNormal.x + _XFurVertexBufferStridePosNormal.y )));// [vertexID * _XFurVertexBufferStride] ;
				    }


				    float3 GetNormal( uint vertexID){
	
					    return asfloat( _XFurVertexBuffer.Load3(( vertexID * _XFurVertexBufferStridePosNormal.x +  _XFurVertexBufferStridePosNormal.z )));
				    }

				    float4 GetTangent( uint vertexID){
	
					    return asfloat( _XFurVertexBuffer.Load4(( vertexID * _XFurVertexBufferStridePosNormal.x + _XFurVertexBufferStridePosNormal.w )));
				    }
                    #endif
                    #endif


                    v2f vert(appdata v)
                    {
                        #if UNITY_UV_STARTS_AT_TOP
                        v.uv.y = 1.0 - v.uv.y;
                        #endif

                        v2f o;
                        o.vertex = float4(v.uv * 2.0 - 1.0, 0.0, 1.0);
                        o.uv = v.uv;

                        #if USE_VERTEXBUFFER
                        #if defined(SHADER_API_D3D11) || defined( SHADER_API_VULKAN ) || defined (SHADER_API_METAL) || defined (SHADER_API_GLES3) || defined(SHADER_API_GLCORE)
                        v.vertex = float4( GetPosition( v.texcoord1.z), 1 );
					    v.normal = GetNormal( v.texcoord1.z );
                        #endif
                        #endif

                        o.worldPos = mul(_XFurObjectMatrix, v.vertex).xyz;
                        o.normal = lerp( mul(_XFurObjectMatrix, v.normal), mul(_XFurObjectMatrix, v.normal), _XFurBasicMode);
                        o.tangent = v.tangent;
                        return o;
                    }

                    sampler2D _InputMap;
                    sampler2D _PhysicsMap;
                    float _InputType;
                    float4 _InputMap_ST;
                    float4 _XFWorldDirection;


                    fixed4 frag(v2f i) : SV_Target{

                        #if UNITY_UV_STARTS_AT_TOP
                        i.uv.y = 1.0 - i.uv.y;
                        #endif

                        float4 col1 = tex2D( _InputMap, i.uv.xy );
                        float4 col2 = tex2D( _PhysicsMap, i.uv.xy );
                        float4 norm = lerp( col2, col1, 0.15 );
                        //norm.xyz += _XFWorldDirection.xyz*0.25;
                        return norm;
                    }
                    ENDCG
                }

        }
}