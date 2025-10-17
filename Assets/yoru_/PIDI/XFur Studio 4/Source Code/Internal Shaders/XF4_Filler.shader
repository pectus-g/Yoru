Shader "Hidden/XFur Studio 4/Designer/Filler"
{
    Properties
    {
        _MainTex("Input Image", 2D) = "white" {}
        _UVLayout("_UVLayout", 2D) = "black" {}
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
            sampler2D _UVLayout;
            float4 _MainTex_TexelSize;


            float4 Sample(float2 uv, float lod)
            {
                return tex2Dlod(_MainTex, float4(uv, 0, lod));
            }

            float SampleUV(float2 uv, float lod)
            {
                return tex2Dlod(_UVLayout, float4(uv, 0, lod)).r;
            }


            float4 SimpleDilate(float2 uv)
            {


                half alpha = tex2D(_UVLayout, uv).g;
                half edgeThick = 2;

                half axx = tex2D(_UVLayout, uv + edgeThick * float2(-_MainTex_TexelSize.x, -_MainTex_TexelSize.y)).g;
                half axy = tex2D(_UVLayout, uv + edgeThick * float2(-_MainTex_TexelSize.x, _MainTex_TexelSize.y)).g;
                half ayx = tex2D(_UVLayout, uv + edgeThick * float2(_MainTex_TexelSize.x, -_MainTex_TexelSize.y)).g;
                half ayy = tex2D(_UVLayout, uv + edgeThick * float2(_MainTex_TexelSize.x, _MainTex_TexelSize.y)).g;
                half2 dir = half2(ayx + ayy - axx - axy, axy + ayy - axx - ayx);

                half4 colx = tex2D(_MainTex, uv);
                half4 col1 = tex2D(_MainTex, uv + normalize(dir) * _MainTex_TexelSize.xy * edgeThick);

                if (alpha < 0.3)
                    colx = col1;


                fixed4 col = tex2D(_MainTex, uv);
                float map = tex2D(_UVLayout, uv).g;

                float4 average = col;

                if (map < 0.15) {									// only take an average if it is not in a uv ilsand
                    int n = 0;
                    average = float4(0., 0., 0., 0.);

                    for (float x = -1.5; x <= 1.5; x++) {
                        for (float y = -1.5; y <= 1.5; y++) {

                            float4 c = tex2D(_MainTex, uv + _MainTex_TexelSize.xy * float2(x, y));
                            float  m = tex2D(_UVLayout, uv + _MainTex_TexelSize.xy * float2(x, y)).g;

                            n += step(0.1, m);
                            average += c * step(0.1, m);

                        }
                    }

                    average /= n;
                }

                col = average;

                return 0.5 * (col + colx);
            }


            float4 frag(v2f i) : SV_Target
            {
                float4 dilate = SimpleDilate(i.uv);
                return dilate;
            }

            ENDCG
        }
    }
}
