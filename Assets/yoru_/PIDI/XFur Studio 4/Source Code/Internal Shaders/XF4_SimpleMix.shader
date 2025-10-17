Shader "Hidden/XFur Studio 4/Designer/SimpleMix"
{
    Properties
    {
        [HideInInspector] _MainTex("Input Image", 2D) = "white" {}
        [HideInInspector] _MaskA("Dynamic Mask", 2D) = "white"{}
    }
        SubShader
    {
        // No culling or depth
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };


            v2f vert(appdata v)
            {
                v2f o;
                o.uv = v.uv;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            sampler2D _MainTex;
            sampler2D _MaskA;


            float4 frag(v2f i) : SV_Target
            {
                half4 col = tex2D( _MainTex, i.uv);
                col.r *= tex2D( _MaskA, i.uv).r;
                return col;
            }

            ENDCG
        }
    }
}
