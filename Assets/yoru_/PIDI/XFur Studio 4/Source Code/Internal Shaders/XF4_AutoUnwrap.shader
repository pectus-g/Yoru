Shader "Hidden/XFur Studio 4/Designer/Auto Unwrap"
{
    Properties
	{
		_FillColor( "Fill Color", Vector ) = (1,1,1,1)
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

			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal: NORMAL;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv2 : TEXCOORD0;
				float3 worldPos : TEXCOORD1;
				float3 normal : TEXCOORD2;
				float4 vertex : SV_POSITION;
			};

			float4 _FillColor;

			v2f vert(appdata v)
			{
			#if UNITY_UV_STARTS_AT_TOP
				v.uv.y = 1.0 - v.uv.y;
			#endif
				float4 pos1 = float4(v.uv*2.0 - 1.0, 0.0, 1.0);

				v2f o;
				o.vertex = pos1;
				o.uv2 = v.uv;
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				o.normal = UnityObjectToWorldNormal(v.normal);
				return o;
			}

			half4 frag(v2f i) : SV_Target
			{
				return _FillColor;
			}
			ENDCG
		}
	}
}
