Shader "Hidden/XFur Studio 4/Utility/World & Object 2 Tangent"
{
    Properties
    {
        _InputMap("Input Map", 2D) = "white" {}
        _InputType("Input Type", Float) = 0
        _WorldDirection("World Direction", Vector) = (0, 0, 0, 0)
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
    
            #include "UnityCG.cginc"

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
                float4 tangent : TANGENT;
                half3x3 worldToTangent : TEXCOORD3;
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
                o.tangent = v.tangent;

                half3 wNormal = UnityObjectToWorldNormal(v.normal);
                half3 wTangent = UnityObjectToWorldDir(v.tangent.xyz);
                // compute bitangent from cross product of normal and tangent
                half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                half3 wBitangent = cross(wNormal, wTangent) * tangentSign;
                // output the world to tangent space matrix
                o.worldToTangent = half3x3(wTangent, wBitangent, wNormal);
                return o;
            }

            sampler2D _InputMap;
            float _InputType;
            float4 _InputMap_ST;
            float4 _WorldDirection;


            float3 tangentToWorld(float3 normal, float4 tangent, float3 worldNormal, float3 worldPos) {

                float3 worldTangent = UnityObjectToWorldDir(tangent.xyz);
                float3 worldBinormal = cross(worldNormal, worldTangent) * tangent.w;

                float4 TtoW0 = float4(worldTangent.x, worldBinormal.x, worldNormal.x, worldPos.x);
                float4 TtoW1 = float4(worldTangent.y, worldBinormal.y, worldNormal.y, worldPos.y);
                float4 TtoW2 = float4(worldTangent.z, worldBinormal.z, worldNormal.z, worldPos.z);
                
                return normalize(half3(dot(TtoW0.xyz, normal), dot(TtoW1.xyz, normal), dot(TtoW2.xyz, normal)));

            }




            float3 tangentNormal(float3 normal, float4 tangent, float3 worldNormal, float3 worldPos) {

                float3 worldTangent = UnityObjectToWorldDir(tangent.xyz);
                float3 worldBinormal = cross(worldNormal, worldTangent) * tangent.w;

                float4 TtoW0 = float4(worldTangent.x, worldBinormal.x, worldNormal.x, worldPos.x);
                float4 TtoW1 = float4(worldTangent.y, worldBinormal.y, worldNormal.y, worldPos.y);
                float4 TtoW2 = float4(worldTangent.z, worldBinormal.z, worldNormal.z, worldPos.z);

                return normalize(half3(dot(TtoW0.xyz, normal), dot(TtoW1.xyz, normal), dot(TtoW2.xyz, normal)));

            }


            fixed4 frag(v2f i) : SV_Target {

                float3 input = tex2D(_InputMap, i.uv);

                float3 wD2T = mul( i.worldToTangent, float3( 0, 0, 0 ) );
                float3 wM2T = mul(i.worldToTangent, input.xyz * 2 - 1 );
                float3 t2W = mul( unity_ObjectToWorld, tangentToWorld( input.xyz * 2 - 1, i.tangent, i.normal, i.worldPos.xyz ));

                float4 col = float4( i.normal.xyz, 1 );
                col.xyz = lerp( col.xyz, wD2T, saturate( _InputType ) );
                col.xyz = lerp(col.xyz, wM2T, saturate( _InputType - 1 ) );
                col.xyz = lerp(col.xyz, t2W, saturate( _InputType - 2 ) );

                col.xyz = (col.xyz + 1) / 2.0;
                return col;
            }
            ENDCG
        }
    }
}
