Shader "Hidden/XFur Studio/Modules/Decal Mixing"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Decal("Decal",2D) = "black"{}
        _DecalColor("Decal Color", Color ) = (1,1,1,1)
        _DecalOffsetTiling("Decal Offset Tiling", Vector) = (0,0,1,1)
        _MixMode("Mix Mode", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _Decal;
            float4 _DecalOffsetTiling;
            float4 _MainTex_ST;
            float4 _DecalColor;
            float _MixMode;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 decal = tex2D(_Decal, i.uv * _DecalOffsetTiling.zw + _DecalOffsetTiling.xy) * _DecalColor;
                col.a = 1;

                if (_MixMode < 0.9 )
                    col.rgb = col.rgb * (1 - decal.a) + decal.rgb * decal.a;
                else if (_MixMode < 1.9 )
                    col.rgb = saturate(col.rgb + decal.rgb * decal.a);
                else
                    col.rgb = col.rgb * lerp(1,decal.rgb,decal.a);

                return col;
            }
            ENDCG
        }
    }
}
