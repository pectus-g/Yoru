namespace XFurStudio.Core {

    using UnityEngine;

    public enum XFurRenderingPipeline { LegacyRP, UniversalRP, HDRP }

    [System.Serializable]
    public struct XFurRenderingResources {

        public XFurRenderingPipeline currentRenderingPipeline;

        public Shader legacyShellsFurShader; 
        public Shader urpShellsFurShader;
        public Shader hdrpShellsFurShader;

        public Material internalFurMaterial;
        public Material internalFurMaterialDoubleSided;

        public Texture2D emptyNormalmap;

        [SerializeField] private Shader xfurPaintShader;
        [SerializeField] private Shader xfurUnwrapShader;
        [SerializeField] private Shader xfurFillerShader;


#if UNITY_EDITOR

        [System.NonSerialized] public string CurrentStatusMessage;
        [System.NonSerialized] public UnityEditor.MessageType CurrentStatusMessageType;

#endif


        public void Load() {           

            var srpAsset = QualitySettings.renderPipeline;

            if ( !QualitySettings.renderPipeline ) {
                srpAsset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            }
            
            if ( srpAsset ) {
                if ( srpAsset.GetType().ToString().Contains("Universal", System.StringComparison.OrdinalIgnoreCase ) ) {
                    currentRenderingPipeline = XFurRenderingPipeline.UniversalRP;
                }
                else {
                    currentRenderingPipeline = XFurRenderingPipeline.HDRP;
                }
            }
            else {
                currentRenderingPipeline = XFurRenderingPipeline.LegacyRP;
            }


            if ( !xfurPaintShader ) {
                xfurPaintShader = Shader.Find( "Hidden/XFur Studio 4/Designer/Painter" );
            }

            if ( !xfurFillerShader ) {
                xfurFillerShader = Shader.Find( "Hidden/XFur Studio 4/Designer/Filler" );
            }

            if ( !xfurUnwrapShader ) {
                xfurUnwrapShader = Shader.Find( "Hidden/XFur Studio 4/Designer/Auto Unwrap" );
            }


            if ( !legacyShellsFurShader ) {
                legacyShellsFurShader = Shader.Find( "Hidden/XFur Studio 4/Legacy/XFur Shells" );
            }

            if ( !urpShellsFurShader ) {
                urpShellsFurShader = Shader.Find( "Hidden/XFur Studio 4/URP/XFur Shells" );
            }

            if ( !hdrpShellsFurShader ) {
                hdrpShellsFurShader = Shader.Find( "Hidden/XFur Studio 4/HDRP/XFur Shells" );
            }

            Shader currentShader = null;

            switch ( currentRenderingPipeline ) {

                case XFurRenderingPipeline.LegacyRP:
                    currentShader = legacyShellsFurShader;
                    break;

                case XFurRenderingPipeline.UniversalRP:
                    currentShader = urpShellsFurShader;
                    break;

                case XFurRenderingPipeline.HDRP:
                    currentShader = hdrpShellsFurShader;
                    break;

            }

            if ( currentShader ) {

                if ( !internalFurMaterial ) {
                    internalFurMaterial = new Material( currentShader );
                    internalFurMaterial.name = "InternalXFurMat";
                    internalFurMaterial.enableInstancing = true;
                }
                else {
                    internalFurMaterial.name = "InternalXFurMat";
                    internalFurMaterial.shader = currentShader;
                    internalFurMaterial.enableInstancing = true;
                }


                if ( !internalFurMaterialDoubleSided ) {
                    internalFurMaterialDoubleSided = new Material( internalFurMaterial );
                    internalFurMaterialDoubleSided.SetFloat( "_Cull", 0 );
                    internalFurMaterialDoubleSided.shader = currentShader;
                    internalFurMaterialDoubleSided.enableInstancing = true;
                }
                else {
                    internalFurMaterialDoubleSided.SetFloat( "_Cull", 0 );
                    internalFurMaterialDoubleSided.shader = currentShader;
                    internalFurMaterialDoubleSided.enableInstancing = true;
                }

#if UNITY_EDITOR
                CurrentStatusMessage = $"XFur Shaders for {currentRenderingPipeline} have been found. All graphics resources are loaded properly.";
                CurrentStatusMessageType = UnityEditor.MessageType.Info;
#endif

            }
            else {
#if UNITY_EDITOR
                CurrentStatusMessage = $"No XFur Shaders for {currentRenderingPipeline} have been found. Please ensure that the shaders for your rendering pipeline have been properly extracted into your project.";
                CurrentStatusMessageType = UnityEditor.MessageType.Error;
                Debug.LogError( CurrentStatusMessage );
#endif
            }           

        }


    }




}
