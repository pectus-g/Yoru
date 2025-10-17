namespace XFurStudio.Core {

    using UnityEngine;

    public static class XFurShaderProperties {

        public static readonly int unityRenderingLayer = Shader.PropertyToID( "unity_RenderingLayer" );

        public static readonly int useVertexBuffer = Shader.PropertyToID( "USE_VERTEXBUFFER" );
        public static readonly int vertexBuffer = Shader.PropertyToID( "_XFurVertexBuffer" );
        public static readonly int vertexBufferStride = Shader.PropertyToID( "_XFurVertexBufferStridePosNormal" );
        public static readonly int oldVertexBuffer = Shader.PropertyToID( "_XFurOldVertexBuffer" );
        public static readonly int xfurUseBakedData = Shader.PropertyToID( "_XFurUseBakedData" );
        public static readonly int xfurMotionVectors = Shader.PropertyToID( "_XFurMotionVectors" );

        //XFur MODULES properties
        public readonly static int xfurVFXMap = Shader.PropertyToID( "_XFurVFXMask" );
        public readonly static int xfurPhysicsMap = Shader.PropertyToID( "_XFurPhysics" );

        //INTERNAL DATA        
        public readonly static int xfurTotalPasses = Shader.PropertyToID( "_XFurTotalPasses" );
        public readonly static int xfurInstancedCurrentPass = Shader.PropertyToID( "_XFurInstancedCurrentPass" );
        public readonly static int xfurForceNonInstanced = Shader.PropertyToID( "_XFurForceNonInstanced" );
        public readonly static int xfurCurrentPass = Shader.PropertyToID( "_XFurCurrentPass" );

        //URP Helpers
        public readonly static int xfurURPRenderingMode = Shader.PropertyToID( "_XFurURPRenderingMode" );

        //HDRP Helpers
        public readonly static int xfurHDWorld2Local = Shader.PropertyToID( "_XFurHDWorld2LocalMatrix" );
        public readonly static int xfurHDLocal2World = Shader.PropertyToID( "_XFurHDLocal2WorldMatrix" );


        //SKIN RENDERING DATA
        public readonly static int xfurSkinColor = Shader.PropertyToID( "_XFurSkinColor" );
        public readonly static int xfurSkinSmoothness = Shader.PropertyToID( "_XFurSkinSmoothness" );
        public readonly static int xfurSkinColorMap = Shader.PropertyToID( "_XFurSkinMap" );
        public readonly static int xfurSkinNormalMap = Shader.PropertyToID( "_XFurSkinNormal" );
        public readonly static int xfurSkinMetalMap = Shader.PropertyToID( "_XFurSkinMetal" );


        //FUR PROPERTIES
        public readonly static int xfurSelfStrandsPattern = Shader.PropertyToID( "_XFurSelfStrandsPattern" );
        public readonly static int xfurHasGroomData = Shader.PropertyToID( "_XFurHasGroomData" );
        public readonly static int xfurGroomData = Shader.PropertyToID( "_XFurGroomData" );
        public readonly static int xfurParamData = Shader.PropertyToID( "_XFurParamData" );
        public readonly static int xfurSelfGroomStrength = Shader.PropertyToID( "_XFurSelfGroomStrength" );

        public readonly static int xfurMainColorMap = Shader.PropertyToID( "_XFurMainColorMap" );
        public readonly static int xfurNormalmap = Shader.PropertyToID( "_XFurNormalmap" );
        public readonly static int xfurSelfBaseTiling = Shader.PropertyToID( "_XFurSelfBaseTiling" );
        public readonly static int xfurEmissionMap = Shader.PropertyToID( "_XFurEmissionMap" );
        public readonly static int xfurEmissionColor = Shader.PropertyToID( "_XFurEmissionColor" );
        public readonly static int xfurSelfColor = Shader.PropertyToID( "_XFurSelfColor" );
        public readonly static int xfurSelfSpecularTint = Shader.PropertyToID( "_XFurSelfSpecularTint" );
        public readonly static int xfurSelfTransmission = Shader.PropertyToID( "_XFurSelfTransmission" );
        public readonly static int xfurSelfRimPower = Shader.PropertyToID( "_XFurSelfRimPower" );
        public readonly static int xfurSelfRimBoost = Shader.PropertyToID( "_XFurSelfRimBoost" );
        public readonly static int xfurSelfRimColor = Shader.PropertyToID( "_XFurSelfRimColor" );

        public readonly static int xfurSelfColorVariation = Shader.PropertyToID( "_XFurSelfColorVariation" );
        public readonly static int xfurSelfUnderColorMod = Shader.PropertyToID( "_XFurSelfUnderColorMod" );
        public readonly static int xfurSelfOverColorMod = Shader.PropertyToID( "_XFurSelfOverColorMod" );

        public readonly static int xfurSelfLength = Shader.PropertyToID( "_XFurSelfLength" );
        public readonly static int xfurSelfSmoothness = Shader.PropertyToID( "_XFurSelfSmoothness" );
        public readonly static int xfurSelfThickness = Shader.PropertyToID( "_XFurSelfThickness" );
        public readonly static int xfurSelfThicknessCurve = Shader.PropertyToID( "_XFurSelfThicknessCurve" );
        public readonly static int xfurSelfShadowsTint = Shader.PropertyToID( "_XFurSelfShadowsTint" );
        public readonly static int xfurSelfOcclusion = Shader.PropertyToID( "_XFurSelfOcclusion" );
        public readonly static int xfurSelfOcclusionCurve = Shader.PropertyToID( "_XFurSelfOcclusionCurve" );

        //COLOR BLENDING
        public readonly static int xfurLegacyVariation = Shader.PropertyToID( "_XFurLegacyVariation" );
        public readonly static int xfurNoiseTiling = Shader.PropertyToID( "_XFurNoiseShadingTiling" );
        public readonly static int xfurSelfColorA = Shader.PropertyToID( "_XFurSelfColorA" );
        public readonly static int xfurSelfColorB = Shader.PropertyToID( "_XFurSelfColorB" );
        public readonly static int xfurSelfColorC = Shader.PropertyToID( "_XFurSelfColorC" );
        public readonly static int xfurSelfColorD = Shader.PropertyToID( "_XFurSelfColorD" );


        public readonly static int xfurSelfCurlAmountX = Shader.PropertyToID( "_XFurSelfCurlAmountX" );
        public readonly static int xfurSelfCurlAmountY = Shader.PropertyToID( "_XFurSelfCurlAmountY" );
        public readonly static int xfurSelfCurlSizeX = Shader.PropertyToID( "_XFurSelfCurlSizeX" );
        public readonly static int xfurSelfCurlSizeY = Shader.PropertyToID( "_XFurSelfCurlSizeY" );

        public readonly static int xfurSelfWindStrength = Shader.PropertyToID( "_XFurSelfWindStrength" );
        public readonly static int xfurUVTiling = Shader.PropertyToID( "_XFurUVTiling" );



    }



}