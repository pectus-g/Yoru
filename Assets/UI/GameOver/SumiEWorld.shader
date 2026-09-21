// YORU game over screen: turns the frozen game frame into an ink painting on paper.
// Drawn by GameOverController on one full screen RawImage. _Ink 0 = the frame as it was, 1 = the painting.
//
// All the ink maths runs in gamma space (the way the look was designed in the mockup), so the project
// being in Linear colour space is handled here by converting in and out.
Shader "YORU/UI/SumiE World"
{
    Properties
    {
        [PerRendererData] _MainTex ("Frozen frame (sharp)", 2D) = "white" {}
        _SoftTex ("Frozen frame (half size, with mips, for the wash)", 2D) = "white" {}
        _PaperTex ("Paper", 2D) = "white" {}
        _Ink ("Ink amount", Range(0, 1)) = 0
        _InkColor ("Ink colour", Color) = (0.085, 0.08, 0.085, 1)
        _InkStrength ("Ink strength", Range(0, 1)) = 0.62
        _BlurMip ("Wash softness (mip level)", Range(0, 6)) = 2
        _BlurSpread ("Wash spread (texels)", Range(0, 16)) = 6
        _Low ("Black point", Range(0, 1)) = 0.04
        _High ("White point", Range(0, 1)) = 0.6
        _EdgeStart ("Edge fade starts at", Range(0, 1)) = 0.25
        _EdgeFade ("Edge fade strength", Range(0, 1)) = 0.75
        _MistStrength ("Mist strength", Range(0, 1)) = 0.82
        _MistCenterY ("Mist centre height (0 bottom, 1 top)", Range(0, 1)) = 0.444
        _MistSize ("Mist size (x, y as parts of the screen)", Vector) = (0.333, 0.306, 0, 0)
        _FlipY ("Flip the frozen frame (0 or 1)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _SoftTex;
            float4 _SoftTex_TexelSize;
            sampler2D _PaperTex;
            float _Ink;
            fixed4 _InkColor;
            float _InkStrength;
            float _BlurMip;
            float _BlurSpread;
            float _Low;
            float _High;
            float _EdgeStart;
            float _EdgeFade;
            float _MistStrength;
            float _MistCenterY;
            float4 _MistSize;
            float _FlipY;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float3 ToGamma(float3 c)
            {
            #ifdef UNITY_COLORSPACE_GAMMA
                return c;
            #else
                return LinearToGammaSpace(c);
            #endif
            }

            float3 FromGamma(float3 c)
            {
            #ifdef UNITY_COLORSPACE_GAMMA
                return c;
            #else
                return GammaToLinearSpace(c);
            #endif
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;                                            // the screen, never flipped: paper, mist and edge fade use this
                float2 fuv = float2(uv.x, lerp(uv.y, 1.0 - uv.y, _FlipY));    // the frozen frame

                float3 sharp = ToGamma(tex2D(_MainTex, fuv).rgb);

                // The wash: a soft copy of the frame (a low mip, five taps).
                float2 px = _SoftTex_TexelSize.xy * _BlurSpread;
                float3 soft = tex2Dlod(_SoftTex, float4(fuv, 0, _BlurMip)).rgb * 0.36;
                soft += tex2Dlod(_SoftTex, float4(fuv + px * float2( 1,  1), 0, _BlurMip)).rgb * 0.16;
                soft += tex2Dlod(_SoftTex, float4(fuv + px * float2(-1,  1), 0, _BlurMip)).rgb * 0.16;
                soft += tex2Dlod(_SoftTex, float4(fuv + px * float2( 1, -1), 0, _BlurMip)).rgb * 0.16;
                soft += tex2Dlod(_SoftTex, float4(fuv + px * float2(-1, -1), 0, _BlurMip)).rgb * 0.16;
                soft = ToGamma(soft);

                // Light to ink: levels, then two washes (diluted ink, then full ink), like a brush loaded twice.
                float g = dot(soft, float3(0.30, 0.59, 0.11));
                g = saturate((g - _Low) / max(_High - _Low, 0.02));
                float ink = 1.0 - g;
                float bands = 0.55 * saturate((ink - 0.25) / 0.25) + 0.45 * saturate((ink - 0.62) / 0.20);

                // The painting fades out toward the edge of the paper.
                float v = length((uv - 0.5) * 2.0);
                float edge = 1.0 - pow(saturate((v - _EdgeStart) / max(1.0 - _EdgeStart, 0.01)), 1.3) * _EdgeFade;

                float3 paper = ToGamma(tex2D(_PaperTex, uv).rgb);
                float3 inkc = ToGamma(_InkColor.rgb);
                float a = _InkStrength * saturate(bands) * edge;
                float3 painting = paper * (1.0 - a) + inkc * (a * 0.9);

                // A band of paper mist behind the words.
                float2 m = float2((uv.x - 0.5) / max(_MistSize.x, 0.01), (uv.y - _MistCenterY) / max(_MistSize.y, 0.01));
                float mist = exp(-dot(m, m) * 1.6) * _MistStrength;
                painting = lerp(painting, paper, mist);

                float3 result = lerp(sharp, painting, _Ink);
                return fixed4(FromGamma(result), i.color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
