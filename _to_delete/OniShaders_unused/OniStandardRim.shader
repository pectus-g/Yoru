// YORU, round 87. Standard for the Oni, plus a Fresnel rim computed in the material.
//
// Why this exists: a point light bright enough to make a dark character read will also
// light everything around him, and culling that light to his layer alone makes him a
// sticker with no shadow and no light pool. The rim is the thing that has to be his and
// his alone, so it belongs in his material, not in the scene. This costs the scene
// nothing: no extra light, no extra draw call, no effect on Yoru or the cave.
//
// The rim is written into Emission, the one channel that survives the deferred path.
// Every map slot matches Unity's Standard shader exactly, so swapping a material over
// keeps its textures, tiling and values without re-wiring anything.
Shader "Yoru/OniStandardRim"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Albedo", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.0

        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _GlossMapScale("Smoothness Scale", Range(0,1)) = 1.0
        [Gamma] _Metallic("Metallic", Range(0,1)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}

        _BumpScale("Normal Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _OcclusionStrength("Occlusion Strength", Range(0,1)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}

        [Header(Rim)]
        _RimColor("Rim Color", Color) = (0.55,0.68,1,1)
        _RimPower("Rim Width (higher is tighter)", Range(0.5,12)) = 4.0
        _RimStrength("Rim Strength", Range(0,4)) = 0.6
        _RimDirectional("Rim Directional (0 all round, 1 one side only)", Range(0,1)) = 0.35
        _RimDirection("Rim Direction (world XYZ)", Vector) = (0,0.35,-1,0)
        _RimOcclude("Rim Follows Occlusion (keeps it out of creases)", Range(0,1)) = 1.0
        _RimTintByAlbedo("Rim Takes Albedo Color", Range(0,1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "PerformanceChecks"="False" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow alphatest:_Cutoff
        #pragma target 3.0
        #pragma shader_feature _NORMALMAP
        #pragma shader_feature _METALLICGLOSSMAP

        sampler2D _MainTex;
        sampler2D _MetallicGlossMap;
        sampler2D _BumpMap;
        sampler2D _OcclusionMap;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
            float3 worldNormal;
            INTERNAL_DATA
        };

        half   _Glossiness;
        half   _GlossMapScale;
        half   _Metallic;
        half   _BumpScale;
        half   _OcclusionStrength;
        fixed4 _Color;

        fixed4 _RimColor;
        half   _RimPower;
        half   _RimStrength;
        half   _RimDirectional;
        float4 _RimDirection;
        half   _RimOcclude;
        half   _RimTintByAlbedo;

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha  = c.a;

            #ifdef _METALLICGLOSSMAP
                fixed4 mg = tex2D(_MetallicGlossMap, IN.uv_MainTex);
                o.Metallic   = mg.r;
                o.Smoothness = mg.a * _GlossMapScale;
            #else
                o.Metallic   = _Metallic;
                o.Smoothness = _Glossiness;
            #endif

            half occ = tex2D(_OcclusionMap, IN.uv_MainTex).g;
            occ = lerp(1.0h, occ, _OcclusionStrength);
            o.Occlusion = occ;

            #ifdef _NORMALMAP
                o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            #endif

            // Fresnel: 1 at the silhouette, 0 facing the camera. _RimPower tightens it to the edge.
            half fres = 1.0h - saturate(dot(normalize(IN.viewDir), o.Normal));
            half rim  = pow(fres, _RimPower);

            // Optional one sided rim, so it reads as a light with a position rather than a glow.
            // The mask is a soft wrap, so the falloff is gradual instead of a hard terminator.
            float3 wn  = WorldNormalVector(IN, o.Normal);
            half side  = saturate(dot(wn, normalize(_RimDirection.xyz)) * 0.5h + 0.5h);
            rim *= lerp(1.0h, side, _RimDirectional);

            // Keep it off the inside of creases, where a real rim would never land.
            rim *= lerp(1.0h, occ, _RimOcclude);

            // A touch of surface colour so the rim belongs to the material instead of
            // reading as one uniform outline pasted over every different surface.
            half3 tint = lerp(half3(1,1,1), saturate(o.Albedo * 2.0h), _RimTintByAlbedo);

            o.Emission = _RimColor.rgb * tint * rim * _RimStrength;
        }
        ENDCG
    }

    FallBack "Standard"
}
