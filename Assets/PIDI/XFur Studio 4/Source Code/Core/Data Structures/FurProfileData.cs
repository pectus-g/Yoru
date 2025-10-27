/*

XFur Studio™, by Irreverent Software™
Copyright© 2018-2025, Jorge Pinal Negrete. All Rights Reserved.

*/


namespace XFurStudio.Core {


    using UnityEngine;

    /// <summary>
    /// The data representing a specific fur material's configuration and apperance
    /// </summary> 
    [System.Serializable]
    public partial struct FurProfileData {

        /// <summary>
        /// The version of this profile data (used for self-updating the asset)
        /// </summary>
        public Vector3Int version;

        #region LIGHTING SETTINGS
        /// <summary>
        /// Whether the fur should work with the Unity probes system or not
        /// </summary>
        public bool useProbes;


        /// <summary>
        /// Whether the fur should cast shadows or not
        /// </summary>
        public bool castShadows;

        /// <summary>
        /// Whether the fur should receive shadows or not.
        /// </summary>
        public bool receiveShadows;

        /// <summary>
        /// The tint to be applied to the fur rim lighting
        /// </summary>
        public Color rimLightingTint;

        /// <summary>
        /// Controls the strength of the rim lighting effect applied to the fur.
        /// </summary>
        public float rimLightingStrength;

        /// <summary>
        /// Controls the width of the rim lighting ring applied to the fur
        /// </summary>
        [Range( 0.2f, 2f )] public float rimLightingPower;

        #endregion

        #region FUR APPEARANCE

        /// <summary>
        /// Amount of fur samples to render. More samples produce better looking fur at the cost of performance.
        /// For small characters that take only a small portion of the screen and for lower end devices use lower sample counts.
        /// </summary>
        [Range(4,64)] public int renderingSamples;

        /// <summary>
        /// The main tint to be applied to the fur.
        /// </summary>
        public Color mainTint;

        /// <summary>
        /// The color map to be applied to the fur material. If the fur data is baked in the vertices of the model (useVertexData = true)
        /// then the alpha channel of this color map will be used as mask to determine which areas are covered in fur.
        /// </summary>
        public Texture colorMap;

        /// <summary>
        /// The tiling of the main UVs of the fur (used for the Color and Normal maps)
        /// </summary>
        public Vector4 baseUVTiling;

        /// <summary>
        /// The asset containing a reference to the procedurally generated fur strands map
        /// </summary>
        public XFurStudioStrandsAsset furStrandsAsset;

        /// <summary>
        /// A direct reference to a procedurally generated fur strands map (this value is used mostly for backwards compatibility)
        /// </summary>
        public Texture furStrandsTexture;

        /// <summary>
        /// The tiling of the fur strands map used for this material
        /// </summary>
        public float furStrandsTiling;

        
        /// <summary>
        /// Whether the fur will be rendered as a double sided material or not
        /// </summary>
        public bool doubleSided;

        /// <summary>
        /// The fur's length
        /// </summary>
        public float furLength;

        /// <summary>
        /// The fur's thickness
        /// </summary>
        public float furThickness;

        /// <summary>
        /// Controls the fur thickness' variation from the root to the tip of the projected fur strands
        /// </summary>
        public float furThicknessCurve;

        /// <summary>
        /// Whether to use curly fur or not
        /// </summary>
        public bool useCurlyFur;

        /// <summary>
        /// Controls the curliness of the fur (x & y) and the size of the curls (z & w)
        /// </summary>
        public Vector4 curlyFurParameters;

        /// <summary>
        /// Enables the use of emission features for the fur
        /// </summary>
        public bool emissiveFur;

        /// <summary>
        /// The tint to apply to the emissive channel of the material (multiplied by the source color of the Emission map)
        /// </summary>
        [ColorUsage( true, true )]
        public Color emissiveTint;

        /// <summary>
        /// The emission map controls which sections of the fur will emit light and which ones won't.
        /// </summary>
        public Texture emissionMap;

        /// <summary>
        /// Whether to use the legacy method for color variation (through textures)
        /// <param>This feature is now deprecated in favor of the more efficient noise based shading tints</param>
        /// </summary>
        public bool useLegacyColorVariation;

        /// <summary>
        /// The Color Variation Map (used by legacy XFur Studio Instances)
        /// </summary>
        public Texture legacyColorVariationMap;


        /// <summary>
        /// The tiling of the noise applied to the fur, to add some slight tint variations over its surface
        /// </summary>
        public float furNoiseShadingTiling;


        /// <summary>
        /// Tint to apply to the first noise shading pass
        /// </summary>
        public Color noiseShadingTint0;

        /// <summary>
        /// Tint to apply to the secondary noise shading pass
        /// </summary>
        public Color noiseShadingTint1;

        /// <summary>
        /// Legacy: the color to apply to the Blue channel of the color variation map
        /// </summary>
        public Color noiseShadingTint2;

        /// <summary>
        /// Legacy: The color to apply to the Alpha channel of the color variation map
        /// </summary>
        public Color noiseShadingTint3;

        /// <summary>
        /// Boost to be applied to the color of the main fur strands. 
        /// <param>Values higher than one makes them lighter, lower than one makes them darker.</param>
        /// </summary>
        public float mainFurStrandBoost;

        /// <summary>
        /// Boost to be applied to the color of the secondary fur strands. 
        /// <param>Values higher than one makes them lighter, lower than one makes them darker.</param>
        /// </summary>
        public float secondaryFurStrandBoost;


        #endregion

        #region SIM DATA

        /// <summary>
        /// Whether to use vertex data instead of textures to define which areas of the model are covered by fur, its length variation,
        /// grooming directions etc. Model must be set to Read/Write and the corresponding data must be baked through the XFurStudioDesigner app.
        /// Legacy versions of XFur Studio use data maps (textures) only, so models upgraded from older releases should leave this setting disabled.
        /// </summary>
        public bool useVertexData;

        /// <summary>
        /// The fur data map contains the fur mask (R), fur length (G), thickness (B) and self-shadowing areas (A)
        /// </summary>
        public Texture furDataMap;
        
        /// <summary>
        /// The fur grooming map contains tangent-based directions that specify how the fur bends over the surface of the model
        /// </summary>
        public Texture furGroomingMap;

        /// <summary>
        /// The strength of the grooming effect applied to the fur
        /// </summary>
        [Range( 0, 2 )]public float groomStrength;

        /// <summary>
        /// Adjusts the strength of the wind applied over this fur material
        /// </summary>
        [Range( 0, 2 )] public float windStrengthMultiplier;

        /// <summary>
        /// Adjusts the strength of the physics effects applied over this fur material
        /// </summary>
        [Range( 0, 2 )] public float physicsStrengthMultiplier;


        #endregion


        #region PBR Parameters

        /// <summary>
        /// The normal map applied to the fur material
        /// </summary>
        public Texture normalMap;

        /// <summary>
        /// The self occlusion effect applied to the fur strands
        /// </summary>
        [Range( 0, 1 )] public float selfOcclusionStrength;

        /// <summary>
        /// Controls the variation on the self-occlusion effect from the root to the tip of the fur strands
        /// </summary>
        [Range( 0, 1 )] public float selfOcclusionCurve;

        /// <summary>
        /// The tint to apply to the self-occlusion effect
        /// </summary>
        public Color selfOcclusionTint;

        /// <summary>
        /// The base roughness of the fur
        /// </summary>
        [Range( 0, 1 )] public float roughness;

        /// <summary>
        /// The base metallness of the fur
        /// </summary>
        [Range( 0, 1 )] public float metallic;

        /// <summary>
        /// The tint for the anisotropic specularity in URP
        /// </summary>
        public Color specularTint;

        #endregion


        public FurProfileData( Vector3Int currentVersion, Texture defaultStrandsTexture = null ) {

            useProbes = false;
            castShadows = true;
            receiveShadows = true;

            rimLightingTint = Color.white;
            rimLightingStrength = 0.65f;
            rimLightingPower = 1f;

            renderingSamples = 16;

            furStrandsTiling = 8;
            furNoiseShadingTiling = 3;

            furStrandsAsset = null;
            furStrandsTexture = defaultStrandsTexture;
            
            doubleSided = true;

            mainTint = Color.white;
            colorMap = Texture2D.whiteTexture;
            normalMap = null;
            baseUVTiling = new Vector4( 1, 1, 0, 0 );

            useCurlyFur = false;
            curlyFurParameters = new Vector4( 0.5f, 0.5f, 0.5f, 0.5f );

            furLength = 0.4f;
            furThickness = 1.0f;
            furThicknessCurve = 0.5f;

            emissiveFur = false;
            emissionMap = Texture2D.whiteTexture;
            emissiveTint = Color.black;

            useVertexData = true;
            furDataMap = null;
            furGroomingMap = null;

            groomStrength = 1.0f;
            windStrengthMultiplier = 1.0f;
            physicsStrengthMultiplier = 1.0f;

            selfOcclusionTint = Color.black;
            selfOcclusionCurve = 0.65f;
            selfOcclusionStrength = 0.65f;

            roughness = 0.9f;
            metallic = 0.05f;

            useLegacyColorVariation = false;
            legacyColorVariationMap = null;

            noiseShadingTint0 = Color.white;
            noiseShadingTint1 = new Color( 0.75f, 0.8f, 0.83f, 1.0f );
            noiseShadingTint2 = noiseShadingTint3 = Color.white;

            mainFurStrandBoost = secondaryFurStrandBoost = 1f;

            specularTint = new Color( 0.15f, 0.15f, 0.15f, 1 );

            version = currentVersion;
        
        }


        #region UPDATING AND UPGRADING


        public void Update( Vector3Int updateToVersion ) {

            switch ( version.x ) {

                case 4:
                    break;
            
            }            

            version = updateToVersion;

        }



#endregion


    }


}
