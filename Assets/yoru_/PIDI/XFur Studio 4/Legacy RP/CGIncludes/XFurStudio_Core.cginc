/*
XFur Studio™ 3, by Irreverent Software™
Copyright© 2018-2020, Jorge Pinal Negrete. All Rights Reserved


Available features in XFur Studio 3 Shaders :

- FEATURESET_MOBILE
- FURPROFILE_BLENDING

*/

UNITY_INSTANCING_BUFFER_START(XFurProps)
UNITY_DEFINE_INSTANCED_PROP(float, _XFurInstancedCurrentPass)
UNITY_INSTANCING_BUFFER_END(XFurProps)

//SKIN RENDERING
sampler2D _XFurSkinMap;
sampler2D _XFurSkinNormal;
half _XFurSkinSmoothness;
half4 _XFurSkinColor;
half _XFurForceNonInstanced;

//The main XFur color map for the mesh, equivalent to the _MainTex in a standard Unity material
sampler2D _XFurMainColorMap;

//An optional normalmap to add to the xfur rendering shader
sampler2D _XFurNormalmap;

//The color variation map assigned to this particular instance, if multi-profile blending is not in use.
sampler2D _XFurSelfColorVariation;

//The XFur strands pattern to be used for this fur mateiral, if multi-profile blending is not in use.
sampler2D _XFurSelfStrandsPattern;

//The VFX blending mask used by the VFX module when in GPU accelerated mode (default)
sampler2D _XFurVFXMask;

half4 _XFurVFX1Color;

half _XFurVFX1Penetration;

half _XFurVFX2Penetration;

half _XFurVFX3Penetration;

half _XFurVFX1Smoothness;

half _XFurVFX2Smoothness;

half _XFurVFX3Smoothness;

half4 _XFurVFX2Color;

half4 _XFurVFX3Color;

half4 _XFurVFX4Color;

//The output of the GPU Accelerated physics module
sampler2D _XFurPhysics;

//The map that controls the fur mask, fur length, fur thickness and fur shadowing 
sampler2D _XFurParamData;

//Texture that controls grooming (XYZ) and fur stiffness
sampler2D _XFurGroomData;

float4 _XFurGroomStrength;

half _XFurHasGroomData;

half _XFurHasPhysicsData;

half _XFurUVTiling;

half _XFurSelfUnderColorMod, _XFurSelfOverColorMod;


half _XFurUseVertexColor;

//The size of a map (usually normals map) that contains additional vertex information, indexed.
half _XFurVertexMapSize;

//The main tint to be applied to the whole fur
half4 _XFurMainTint;

//The tint to be applied to the fur highlights
half4 _XFurSelfHighlightsTint;

//Tint to be applied to the fur self-shadowing
half4 _XFurSelfShadowsTint;

half4 _XFurSelfColor;

half4 _XFurSelfRimColor;

//Ting to be applied across the Red channel of the Color variation map
half4 _XFurSelfColorA;

//Ting to be applied across the Green channel of the Color variation map
half4 _XFurSelfColorB;

//Ting to be applied across the Blue channel of the Color variation map
half4 _XFurSelfColorC;

//Ting to be applied across the Alpha channel of the Color variation map
half4 _XFurSelfColorD;

//The metallic value to be applied to the fur
half _XFurSelfMetallic;

//Whether the strands assigned to this fur instance are virtual 3D texture strands
half _XFurSelfEnable3DStrands;

//The total amount of passes to use with this fur
half _XFurTotalPasses;

//The current pass that we are rendering
half _XFurCurrentPass;

//The absolute length of the fur
half _XFurSelfLength;

//THe absolute thickness of the fur strands
half _XFurSelfThickness;

//The curve used to control the ratio of the fur strand from the root to the tip
half _XFurSelfThicknessCurve;

//The strength of the self-shadowing / occlusion applied to the fur
half _XFurSelfOcclusion;

//The curve that controls how the shadowing goes from the roots to the tips of the fur
half _XFurSelfOcclusionCurve;

//The absolute smoothness / glossiness of the fur
half _XFurSelfSmoothness;

half4 _XFurSelfSpecularTint;

half _XFurSelfTransmission;


half _XFurLODArea;

half _XFurLODStrength;


float4 _XFurMainStandardLightDir;
half4 _XFurMainStandardLightColor;

sampler2D _XFurEmissionMap;
float4 _XFurEmissionColor;


//The absolute offset applied to the anisotropic specularity highlights on the fur
half _XFurSelfSpecOffset;

half _XFurSelfRimBoost;

half _XFurSelfRimPower;

//WIND

float4 _XFurWindDirectionFreq;

float _XFurWindStrength;

float _XFurSelfWindStrength;

//============ MULTI PROFILE BLENDING PARAMETERS ============


half _XFurTotalProfiles;

//Splat map texture to choose between the available XFur Profile settings, up to 4 maps per material
sampler2D _XFurProfilesSplat;

//The color variation map assigned to this particular instance, if multi-profile blending is not in use.
sampler2D _XFurColorVariation[4];

//The tint to be applied to the fur highlights
half4 _XFurHighlightsTint[4];

//Tint to be applied to the fur self-shadowing
half4 _XFurShadowsTint[4];

//Ting to be applied across the Red channel of the Color variation map
half4 _XFurColorA[4];

//Ting to be applied across the Green channel of the Color variation map
half4 _XFurColorB[4];

//Ting to be applied across the Blue channel of the Color variation map
half4 _XFurColorC[4];

//Ting to be applied across the Alpha channel of the Color variation map
half4 _XFurColorD[4];

//The metallic value to be applied to the fur
half _XFurMetallic[4];

//Whether the strands assigned to this fur instance are virtual 3D texture strands
uint _XFurEnable3DStrands[4];

//The absolute length of the fur
half _XFurLength[4];

//THe absolute thickness of the fur strands
half _XFurThickness[4];

//The curve used to control the ratio of the fur strand from the root to the tip
half _XFurThicknessCurve[4];

//The strength of the self-shadowing / occlusion applied to the fur
half _XFurOcclusion[4];

//The curve that controls how the shadowing goes from the roots to the tips of the fur
half _XFurOcclusionCurve[4];

//The absolute smoothness / glossiness of the fur
half _XFurSmoothness[4];

//The absolute offset applied to the anisotropic specularity highlights on the fur
half _XFurSpecOffset[4];

//The spherical colliders that can interact with this XFur instance
float4 _XFurColliders[8];

float _XFurBendingPower;

float _XFurBendingStrength;

// CURLY FUR SETTINGS

half _XFurSelfCurlAmountX;

half _XFurSelfCurlAmountY;

half _XFurSelfCurlSizeX;

half _XFurSelfCurlSizeY;

half _XFurSelfGroomStrength;

half4 _XFurSelfBaseTiling;

half _XFurUseBakedData;

half _XFurNoiseShadingTiling;

half _XFurLegacyVariation;


#if USE_VERTEXBUFFER
#if defined(SHADER_API_D3D11) || defined( SHADER_API_VULKAN ) || defined (SHADER_API_METAL) || defined (SHADER_API_GLES3) || defined(SHADER_API_GLCORE)
ByteAddressBuffer _XFurVertexBuffer;
float4 _XFurVertexBufferStridePosNormal;
#endif
#endif