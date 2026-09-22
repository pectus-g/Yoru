// YORU pause screen: the frozen game seen THROUGH washi paper.
// Drawn by PauseMenuController on one full screen RawImage. _Show 0 = the frame as it was,
// 1 = the finished paper picture. The world is not painted into ink here (that is the game
// over screen): it is blurred wide and used as the light behind the paper, so the paper keeps
// its warmth and fibre and the world only darkens and lightens it.
//
// All the maths runs in gamma space (the way the look was designed in the mockup), so the
// project being in Linear colour space is handled here by converting in and out.
Shader "YORU/UI/Pause Paper"
{
    Properties
    {
        [PerRendererData] _MainTex ("Frozen screen (sharp)", 2D) = "white" {}
        _SoftTex ("Frozen screen (half size, with mips, for the blur)", 2D) = "white" {}
        _PaperTex ("Paper", 2D) = "white" {}
        _Show ("Show the paper picture", Range(0, 1)) = 0
        _World ("How much world comes through", Range(0, 1)) = 0.58
        _Colour ("How much of its colour survives", Range(0, 1)) = 0.3
        _BlurMip ("Blur (mip level)", Range(0, 6)) = 3
        _BlurSpread ("Blur spread (texels)", Range(0, 24)) = 12
        _Low ("Black point", Range(0, 1)) = 0.04
        _High ("White point", Range(0, 1)) = 0.6
        _EdgeStart ("Edge fade starts at", Range(0, 1)) = 0.32
        _EdgeFade ("Edge fade strength", Range(0, 1)) = 0.55
        _MistStrength ("Mist strength", Range(0, 1)) = 0.42
        _MistCenterY ("Mist centre height (0 bottom, 1 top)", Range(0, 1)) = 0.398
        _MistSize ("Mist size (x, y as parts of the screen)", Vector) = (0.396, 0.343, 0, 0)
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
            float _Show;
            float _World;
            float _Colour;
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
                float2 uv = i.uv;                                             // the screen: paper, mist and edge fade use this
                float2 fuv = float2(uv.x, lerp(uv.y, 1.0 - uv.y, _FlipY));    // the frozen frame

                float3 sharp = ToGamma(tex2D(_MainTex, fuv).rgb);

                // The blur: a low mip of the half size copy, five taps.
                float2 px = _SoftTex_TexelSize.xy * _BlurSpread;
                float3 soft = tex2Dlod(_SoftTex, float4(fuv, 0, _BlurMip)).rgb * 0.36;
                soft += tex2Dlod(_SoftTex, float4(fuv + px * float2( 1,  1), 0, _BlurMip)).rgb * 0.16;
                soft += tex2Dlod(_SoftTex, float4(fuv + px * float2(-1,  1), 0, _BlurMip)).rgb * 0.16;
                soft += tex2Dlod(_SoftTex, float4(fuv + px * float2( 1, -1), 0, _BlurMip)).rgb * 0.16;
                soft += tex2Dlod(_SoftTex, float4(fuv + px * float2(-1, -1), 0, _BlurMip)).rgb * 0.16;
                soft = ToGamma(soft);

                float3 paper = ToGamma(tex2D(_PaperTex, uv).rgb);

                // Light through paper: the world's brightness, stretched between the two points,
                // shades the washi. 0.52 = the darkest the paper goes, 1.30 = the brightest.
                float lum = dot(soft, float3(0.30, 0.59, 0.11));
                float l = saturate((lum - _Low) / max(_High - _Low, 0.02));
                float shade = clamp(0.52 + 0.78 * l, 0.0, 1.18);
                float3 lit = paper * shade;

                // A little of the world's colour, its brightness taken out first.
                float3 tint = clamp(soft / max(lum, 0.001), 0.55, 1.6);
                lit *= (1.0 - _Colour) + _Colour * tint;

                // How much of the world comes through at all.
                lit = lerp(paper, lit, _World);

                // The paper wins toward the edges.
                float v = length((uv - 0.5) * 2.0);
                float edge = pow(saturate((v - _EdgeStart) / max(1.0 - _EdgeStart, 0.01)), 1.4) * _EdgeFade;
                lit = lerp(lit, paper, edge);

                // A band of paper mist behind the words.
                float2 m = float2((uv.x - 0.5) / max(_MistSize.x, 0.01), (uv.y - _MistCenterY) / max(_MistSize.y, 0.01));
                float mist = exp(-dot(m, m) * 1.6) * _MistStrength;
                lit = lerp(lit, paper, mist);

                float3 result = lerp(sharp, lit, _Show);
                return fixed4(FromGamma(result), i.color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
