namespace XFurStudio.Core {

    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    [System.Serializable]
    public class XFurStudioInstanceSettings {

        
        /// <summary>
        /// Enables the use of vertex buffers to synchronize the fur with the animated mesh. Enabled by default.
        /// Vertex Buffers provides much better performance since the data is handled directly in the GPU. 
        /// <param>When disabled (for example in platforms with no Vertex Buffer support such as WebGL), CPU Baking is used instead, which is
        /// far more performance heavy.</param>
        /// </summary>
        public bool useVertexBuffer = true;

        /// <summary>
        /// Whether this XFur Studio Instance will render dual vertex buffers, necessary for Motion Vectors support
        /// </summary>
        public bool renderMotionVectors = false;

        /// <summary>
        /// Whether the material should enable the use of normalmaps
        /// </summary>
        public bool useNormalmap = false;


        /// <summary>
        /// Whether custom modules will be in use or not for this XFur Studio Instance
        /// </summary>
        public bool useCustomModules;


        /// <summary>
        /// Switches to the baked vertex data mode. The underlying mesh has to be marked with Read/Write enabled. The fur data
        /// (length, thickness, shadowing, grooming, etc) will be baked into vertex colors. This reduces the
        /// amount of texture maps required.
        /// </summary>
        public bool useBakedVertexData;

        /// <summary>
        /// Enables the use of advanced URP lighting
        /// </summary>
        public bool urpAdvancedLighting = true;


        /// <summary>
        /// If enabled, the fur material will be updated regularly to apply any changes done to the fur. Disabled by default.
        /// </summary>
        public bool autoUpdateMaterials;

        /// <summary>
        /// The time (in seconds) between each automatic update to the fur materials
        /// </summary>
        public float timeBetweenUpdates = 0.1f;

        /// <summary>
        /// When enabled, fur length and other relevant properties are slightly adjusted to account for a model's scale. However,
        /// it is recommended that all models have a uniform (1,1,1) scale.
        /// </summary>
        public bool autoCompensateForScale;

        /// <summary>
        /// When enabled, the actual world-unit scale of the model will be used.
        /// </summary>
        public bool useLossyScale;

        /// <summary>
        /// Whether experimental features will be available or not.
        /// </summary>
        public bool experimentalFeatures;

        /// <summary>
        /// Legacy feature. Enables Forward-mode compatibility (better shadows & lighting) for older built-in projects.
        /// </summary>
        public bool builtinFwdCompatibilityMode;

        /// <summary>
        /// The current version of XFur Studio used in this instance. Useful when updating the asset.
        /// </summary>
        public Vector3Int version = new Vector3Int( 4, 0, 0 );

        private readonly Vector3Int latestVersion = new Vector3Int( 4, 1, 2 );


        [SerializeField] protected XFurStudioInstance xfurInstance;


        /// <summary>
        /// Checks whether this XFurStudioInstance requires any updates
        /// </summary>
        public void CheckForUpdates() {

            if ( version != latestVersion ) {
                ApplyUpdates();
            }

        }


        /// <summary>
        /// Applies any necessary updates to this XFurStudioInstance
        /// </summary>
        protected void ApplyUpdates() {

            switch ( version.x ) {
                //Updates for XFur Studio 3.x.x instances
                case 3:

                    break;

                    //Updates for XFur Studio 2.x.x instances
                case 2:

                    break;

            }

            version = latestVersion;

        }


    }

}