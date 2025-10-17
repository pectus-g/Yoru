/*

XFur Studio™ 4, by Irreverent Software™
Copyright© 2018-2025, Jorge Pinal Negrete. All Rights Reserved.

*/

//UNCOMMENT the line below to use the experimental multi-threaded, bursted enhanced features (including the use of the new Graphics.RenderMesh API)
//Also, make sure that the packages for Burst and Unity Collections have been properly installed in your project.

namespace XFurStudio.Core {

    using System.Collections;
    using System.Collections.Generic;
    using XFurStudio.Utilities;
    using XFurStudio.Modules;
    using UnityEngine.Rendering;

#if UNITY_EDITOR
    using UnityEditor;
#endif
    using UnityEngine;


#if XFUR_BURSTED
    using Unity.Jobs;
    using Unity.Collections;
#endif



    [ExecuteAlways]
    [DefaultExecutionOrder( 99000 )]
    public partial class XFurStudioInstance : MonoBehaviour {

        /// <summary>
        /// The settings for this XFur Studio Instance
        /// </summary>
        [SerializeField] protected XFurStudioInstanceSettings _settings = new XFurStudioInstanceSettings();

        /// <summary>
        /// The settings for this XFur Studio Instance
        /// </summary>
        public XFurStudioInstanceSettings Settings { get { return _settings; } }



        [SerializeField] protected XFurRenderingResources _renderingResources = new XFurRenderingResources();

        /// <summary>
        /// The internal rendering resources of this instance, like its material, shaders and current rendering pipeline
        /// </summary>
        public XFurRenderingResources RenderingResources { get { return _renderingResources; } }

        /// <summary>
        /// The stride of the graphics buffer that will be used for the fur materials
        /// </summary>
        protected Vector4 _xfurVertexBufferStride;



        protected float _xfurTexcoord4Offset;

        /// <summary>
        /// The actual vertex buffer
        /// </summary>
        protected GraphicsBuffer _xfurVertexBuffer;

        /// <summary>
        /// The actual vertex buffer
        /// </summary>
        protected GraphicsBuffer _xfurOldVertexBuffer;

        /// <summary>
        /// Public accessor to the Vertex Buffer used by the fur materials
        /// </summary>
        public GraphicsBuffer XFurVertexBuffer { get { return _xfurVertexBuffer; } }

        /// <summary>
        /// Public accessor to the Vertex Buffer stride.
        /// </summary>
        public Vector4 XFurVertexBufferStride { get { return _xfurVertexBufferStride; } }

#if XFUR_BURSTED
        /// <summary>
        /// The handle for the matrices processing job
        /// </summary>       
        protected JobHandle _xfurMatricesJobHandle;
#else
        private Vector3 tPos;
        private Vector3 tScale;
        private Quaternion tRot;
#endif


#if UNITY_EDITOR
        [System.NonSerialized] public int editFurProfile;
#endif

        /// <summary> The internal fur profile data sets to be assigned to each material</summary>
        [SerializeField] protected FurProfileData[] _internalFurProfiles = new FurProfileData[0];

        /// <summary> Public accessor to the internal fur profile data sets that are assigned to each material</summary>
        public FurProfileData[] FurDataProfiles { get { return _internalFurProfiles; } }

        /// <summary>The main renderer from which bones, materials, profiles, normals etc. will be referenced </summary>
        [SerializeField] protected XFurMeshRendererData _mainRenderer = new XFurMeshRendererData();

        /// <summary>Randomization module (built in)</summary>
        [SerializeField] protected XFurRandomizationModule _randomizationModule;

        /// <summary>LOD module (built in)</summary>
        [SerializeField] protected XFurLODModule _lodModule;

        /// <summary>Physics module (built in)</summary>
        [SerializeField] protected XFurPhysicsModule _physicsModule;

        /// <summary>VFX module (built in)</summary>
        [SerializeField] protected XFurVFXModule _vfxModule;

        /// <summary>Decals module (built in)</summary>
        [SerializeField] protected XFurDecalsModule _decalsModule;

        /// <summary>
        /// Simple touch module (built-in)
        /// </summary>
        [SerializeField] protected XFurSimpleBendingModule _simpleTouchBendingModule;

        /// <summary>
        /// Simple touch module (built-in)
        /// </summary>
        [SerializeField] protected XFurDynamicMaskingModule _dynamicMaskingModule;

        protected Mesh _currentFurMesh;

        protected Mesh _tempBakedMesh;

        //[SerializeField] protected MaterialPropertyBlock _internalMatPropBlock;

        /// <summary> Gives access to the internal Material blocks that represent each fur material, allowing you to modify their data right before it is passed to the renderer. Useful for modules such as physics, randomization, weather etc. where the fur appearance should be modified on the fly and, in cases, independently from the regular fur updates</summary>
        protected MaterialPropertyBlock[] _furBlocks = new MaterialPropertyBlock[0];

        /// <summary>The randomization module assigned to this instance</summary>
        public XFurRandomizationModule RandomizationModule { get { return _randomizationModule; } }

        /// <summary>The Dynamic LOD module assigned to this instance</summary>
        public XFurLODModule LODModule { get { return _lodModule; } }

        /// <summary>The Physics Module assigned to this instance</summary>
        public XFurPhysicsModule PhysicsModule { get { return _physicsModule; } }

        /// <summary>The VFX Module assigned to this instance</summary>
        public XFurVFXModule VFXModule { get { return _vfxModule; } }

        /// <summary>The Decals Module assigned to this instance</summary>
        public XFurDecalsModule DecalsModule { get { return _decalsModule; } }

        /// <summary>
        /// The Simple touch bending module assigned to this instance
        /// </summary>
        public XFurSimpleBendingModule SimpleBendingModule { get { return _simpleTouchBendingModule; } }

        /// <summary>
        /// The dynamic masks module assigned to this instance
        /// </summary>
        public XFurDynamicMaskingModule DynamicMasksModule { get { return _dynamicMaskingModule; } }

        protected static float[] _internalPassIndexArray = new float[128];

        /// <summary>
        /// List of custom-made xfur studio modules. They can be used to modify the appearance of the fur or its behavior
        /// at runtime by injecting custom code in the update and render cycles.
        /// </summary>
        [SerializeField] protected XFurStudioModuleAsset[] _customModules = new XFurStudioModuleAsset[0];

        /// <summary>
        /// Public accessor to the Custom Modules attached to this XFur Studio Instance
        /// </summary>
        public XFurStudioModuleAsset[] CustomModules { get { return _customModules; } }

        public bool IsSkinnedMesh { get { return CurrentFurRenderer.renderer is SkinnedMeshRenderer; } }

        /// <summary>The current, active mesh being rendered by this XFur Instance</summary>
        public Mesh CurrentMesh { get { return _currentFurMesh ? _currentFurMesh : CurrentFurRenderer.originalMesh; } }

#if XFUR_BURSTED
        protected NativeArray<XFurInstancingData> _tMatricesArray;
#else
        protected Matrix4x4[] _tMatricesArray;
#endif

        /// <summary>The current, active renderer used by this XFur Instance</summary>
        public XFurMeshRendererData CurrentFurRenderer;

        /// <summary>
        /// Sets the next frame to be skipped (fur not updated/rendered)
        /// </summary>
        [System.NonSerialized] public bool skipRenderFrame;

        /// <summary>
        /// The global XFur Wind and Weather zone affecting this fur simulation
        /// </summary>
        public static XFurWeatherManager WeatherManager;

        /// <summary>
        /// The frustum planes of the currently rendering camera
        /// </summary>
        protected Plane[] _camFrustumPlanes = new Plane[6];

        /// <summary>
        /// The array of all cameras in the scene
        /// </summary>
        protected Camera[] _globalCamerasArray = new Camera[0];


        /// <summary>
        /// The time remaining before the materials are auto-updated again.
        /// </summary>
        protected float _timeForNextUpdate;


        [SerializeField] protected bool _unreadableMeshError;

        private List<Vector3> _mVectorUVs;


        private void OnValidate() {

            if ( !_renderingResources.emptyNormalmap ) {
                _renderingResources.emptyNormalmap = new Texture2D( 1, 1, TextureFormat.ARGB32, 0, true );
                _renderingResources.emptyNormalmap.SetPixel( 0, 0, new Color( 1, 0.5f, 1, 0.5f ) );
                _renderingResources.emptyNormalmap.Apply();
            }
            else {
                _renderingResources.emptyNormalmap.SetPixel( 0, 0, new Color( 1, 0.5f, 1, 0.5f ) );
                _renderingResources.emptyNormalmap.Apply();
            }

            if ( CurrentFurRenderer.renderer ) {
                if ( CurrentFurRenderer.renderer is SkinnedMeshRenderer ) {
                    _unreadableMeshError = !( CurrentFurRenderer.renderer as SkinnedMeshRenderer ).sharedMesh.isReadable;

                    if ( _unreadableMeshError ) {
                        Debug.LogError( "The mesh assigned to this XFur Studio Instance is not marked as readable. Please enable Read/Write in the import settings for this model." );
                    }
                }
            }

            if ( _renderingResources.internalFurMaterial ) {

                if ( _settings.useNormalmap ) {
                    _renderingResources.internalFurMaterial.EnableKeyword( "USE_NORMALMAP" );
                }
                else {
                    _renderingResources.internalFurMaterial.DisableKeyword( "USE_NORMALMAP" );
                }

            }

            if ( CurrentFurRenderer.renderer ) {
                for ( int i = 0; i < CurrentFurRenderer.materials.Length; i++ ) {
                    if ( !CurrentFurRenderer.materials[i] || CurrentFurRenderer.materials[i] != _mainRenderer.renderer.sharedMaterials[i] ) {
                        _mainRenderer.materials[i] = CurrentFurRenderer.materials[i] = _mainRenderer.renderer.sharedMaterials[i];
                    }
                }
            }

            PatchRendererMeshes();

        }


        void OnEnable() {
            //We clear any leftover buffer data
            ClearBufferData();

            //We fill the globalCamerasArray
            _globalCamerasArray = Camera.allCameras;

            //We setup this XFur Studio Instance
            SetupXFurInstance();


        }



        /// <summary>
        /// Sets up the XFur Studio Instance and all of its internal resources / data
        /// </summary>
        public void SetupXFurInstance() {

            //Skip if we do not have a main renderer
            if ( !_mainRenderer.renderer ) {
                return;
            }
            else {//Otherwise set the current fur renderer to be the same as the main renderer
                CurrentFurRenderer = _mainRenderer;
            }

            //If our material property blocks list is not as large as the amount of materials we have
            if ( _furBlocks.Length != _mainRenderer.materials.Length ) {
                //Create a new fur material property blocks array
                _furBlocks = new MaterialPropertyBlock[_mainRenderer.materials.Length];

                //And assign new Material Property blocks to it.
                for ( int i = 0; i < _furBlocks.Length; ++i ) {
                    _furBlocks[i] = new MaterialPropertyBlock();
                }
            }

#if XFUR_BURSTED
            //If our NativeArray of matrices has not been cleaned, clean it now
            if ( _tMatricesArray.IsCreated ) {
                _xfurMatricesJobHandle.Complete();
                _tMatricesArray.Dispose();
            }

            /*If we are running with Burst (multi-threading) optimizations, we prepare the native array that contains the
             * current and previous frame objectToWorld matrices (used for instancing)
            */
            _tMatricesArray = new NativeArray<XFurInstancingData>( 128, Allocator.Persistent );

            //Since we can have up to 128 rendering layers, 128 elements are required in our array
            for ( int i = 0; i < 128; i++ ) {
                var xrData = new XFurInstancingData();
                xrData.objectToWorld = CurrentFurRenderer.renderer.localToWorldMatrix;
                xrData.prevObjectToWorld = CurrentFurRenderer.renderer.localToWorldMatrix;
                _tMatricesArray[i] = xrData;
            }
#endif

            PatchRendererMeshes();



            //We set up the internal pass index array
            for ( int i = 0; i < _internalPassIndexArray.Length; i++ ) {
                _internalPassIndexArray[i] = i;
            }

            //We load our rendering resources (shaders, materials)
            _renderingResources.Load();
            _settings.CheckForUpdates();

            //We cleanup any leftover RenderTexture and module data
            CleanupXFurData();

            //We setup our XFur Studio Modules
            SetupModules();

        }


        protected void PatchRendererMeshes() {
            //If the currently used fur renderer is a SkinnedMeshRenderer (animated mesh)
            if ( CurrentFurRenderer.renderer is SkinnedMeshRenderer && _settings.useVertexBuffer ) {

                /*
                 * We dynamically patch the shared mesh's z value on its UV1 coordinates to store vertex indexes in it.
                 * This is done to ensure that the data is accessible in a predictable way, since access to the VertexID through 
                 * the shader itself may be inconsistent depending on rendering pipeline, HLSL vs ShaderGraph, etc.
                 */
                var mesh = ( CurrentFurRenderer.renderer as SkinnedMeshRenderer ).sharedMesh;

                var uvs = new List<Vector4>();

                mesh.GetUVs( 1, uvs );

                if ( uvs.Count < mesh.vertexCount ) {
                    mesh.GetUVs( 0, uvs );
                }

                for ( int i = 0; i < uvs.Count; i++ ) {
                    var uv = uvs[i];
                    uv.z = i;
                    uvs[i] = uv;
                }

                mesh.SetUVs( 1, uvs );

            }

            if ( _lodModule && _lodModule.IsEnabled ) {
                for ( int x = 0; x < _lodModule.lodRenderers.Length; x++ ) {
                    /*
                 * We dynamically patch the shared mesh's z value on its UV1 coordinates to store vertex indexes in it.
                 * This is done to ensure that the data is accessible in a predictable way, since access to the VertexID through 
                 * the shader itself may be inconsistent depending on rendering pipeline, HLSL vs ShaderGraph, etc.
                 */
                    var mesh = ( _lodModule.lodRenderers[x].renderer as SkinnedMeshRenderer ).sharedMesh;

                    var uvs = new List<Vector4>();

                    mesh.GetUVs( 1, uvs );

                    for ( int i = 0; i < uvs.Count; i++ ) {
                        var uv = uvs[i];
                        uv.z = i;
                        uvs[i] = uv;
                    }

                    mesh.SetUVs( 1, uvs );
                }
            }

        }


        protected void CheckInternalFurProfiles() {

            if ( _internalFurProfiles == null ) {
                _internalFurProfiles = new FurProfileData[_mainRenderer.materials.Length];
            }
            else if ( _internalFurProfiles.Length != _mainRenderer.materials.Length ) {
                System.Array.Resize( ref _internalFurProfiles, _mainRenderer.materials.Length );
            }

            for ( int i = 0; i < _internalFurProfiles.Length; ++i ) {
                if ( _internalFurProfiles[i].version != _settings.version ) {
                    if ( _internalFurProfiles[i].mainTint == Color.clear && _internalFurProfiles[i].furLength <= 0 ) {
                        _internalFurProfiles[i] = new FurProfileData( _settings.version );
                    }
                    else {
                        _internalFurProfiles[i].Update( _settings.version );
                    }
                }

            }


        }


        /// <summary>
        /// Sets up the different modules for this XFur Studio Instance (both the integrated ones and any custom ones)
        /// </summary>
        protected void SetupModules() {

            //If there's no physics module, we create one and set it up
            if ( !_physicsModule ) {
                _physicsModule = new XFurPhysicsModule();
            }

            _physicsModule.Setup( this );


            //We do the same for our VFX module
            if ( !_vfxModule ) {
                _vfxModule = new XFurVFXModule();
            }

            _vfxModule.Setup( this );


            //Our LOD module
            if ( !_lodModule ) {
                _lodModule = new XFurLODModule();
            }

            _lodModule.Setup( this );


            //Our randomization module
            if ( !_randomizationModule ) {
                _randomizationModule = new XFurRandomizationModule();
            }

            _randomizationModule.Setup( this );


            //Our dynamic masking module
            if ( !_dynamicMaskingModule ) {
                _dynamicMaskingModule = new XFurDynamicMaskingModule();
            }

            _dynamicMaskingModule.Setup( this );


            //Our decals module
            if ( !_decalsModule ) {
                _decalsModule = new XFurDecalsModule();
            }

            _decalsModule.Setup( this );


            //And our SimpleTouchBending module
            if ( !_simpleTouchBendingModule ) {
                _simpleTouchBendingModule = new XFurSimpleBendingModule();
            }

            _simpleTouchBendingModule.Setup( this );


            if ( _randomizationModule && _randomizationModule.IsEnabled ) {
                _randomizationModule.Load();
            }

            if ( _lodModule && _lodModule.IsEnabled ) {
                _lodModule.Load();
            }

            if ( _vfxModule && _vfxModule.IsEnabled ) {
                _vfxModule.Load();
            }

            if ( _physicsModule && _physicsModule.IsEnabled ) {
                _physicsModule.Load();
            }

            if ( _dynamicMaskingModule && _dynamicMaskingModule.IsEnabled ) {
                _dynamicMaskingModule.Load();
            }

            if ( Application.isPlaying ) {

                for ( int i = 0; i < _customModules.Length; i++ ) {
                    if ( _customModules[i] && !_customModules[i].isRuntimeReady ) {
                        _customModules[i] = _customModules[i].CreateRuntime();
                        _customModules[i].Module.Enable();
                        _customModules[i].Module.Setup( this );
                        _customModules[i].Module.Load();
                    }
                }

            }
        }


        protected IEnumerator Start() {

            //If the application is playing, we load all the modules (preparing their internal resources too)...
            if ( Application.isPlaying ) {
                if ( _randomizationModule && _randomizationModule.IsEnabled ) {
                    _randomizationModule.Load();
                }

                if ( _lodModule && _lodModule.IsEnabled ) {
                    _lodModule.Load();
                }

                if ( _vfxModule && _vfxModule.IsEnabled ) {
                    _vfxModule.Load();
                }

                if ( _physicsModule && _physicsModule.IsEnabled ) {
                    _physicsModule.Load();
                }

                if ( _dynamicMaskingModule && _dynamicMaskingModule.IsEnabled ) {
                    _dynamicMaskingModule.Load();
                }

                //We jump-start the rendering by forcing an update to it...
                if ( !_settings.autoUpdateMaterials ) {
                    _settings.autoUpdateMaterials = true;
                    RenderFur();
                    _settings.autoUpdateMaterials = false;
                }

            }

            //We wait half a second
            yield return new WaitForSeconds( 0.5f );

            //And jumpstart it again
            if ( !_settings.autoUpdateMaterials ) {
                _settings.autoUpdateMaterials = true;
                RenderFur();
                _settings.autoUpdateMaterials = false;
            }


            yield break;

        }



        /// <summary>
        /// Experimental. Allows you to set the Main Renderer of this XFur Studio Instance at runtime
        /// </summary>
        /// <param name="renderer"></param>
        public void SetMainRenderer( Renderer renderer ) {
            _mainRenderer.AssignRenderer( renderer );
            CurrentFurRenderer = _mainRenderer;
            SetupXFurInstance();
            CheckInternalFurProfiles();

            if ( _mainRenderer.isFurMaterial.Length == _internalFurProfiles.Length ) {

                for ( int i = 0; i < _mainRenderer.isFurMaterial.Length; ++i ) {

                    if ( !_mainRenderer.materials[i] ) {
                        var rend = _mainRenderer;
                        rend.materials[i] = rend.renderer.sharedMaterials[i];
                        _mainRenderer = rend;
                    }

                }

            }


        }


        /// <summary>
        /// Retrieves the settings and properties of a fur material in use by this instance as a FurProfileData struct
        /// </summary>
        /// <param name="index">The index of the material from which we will retrieve the properties</param>
        /// <param name="furProfileData">The output FurProfileData with the copy of the properties</param>
        public void GetFurData( int index, out FurProfileData furProfileData ) {

            if ( index < _internalFurProfiles.Length ) {
                furProfileData = _internalFurProfiles[index];
                return;
            }

            furProfileData = new FurProfileData();

        }


        /// <summary>
        /// Replaces the fur profile data for the material at a given index
        /// </summary>
        /// <param name="index">The index where we will replace the data</param>
        /// <param name="data">The new fur profile data</param>
        public void ReplaceFurData( int index, FurProfileData data ) {

            data.Update( _settings.version );

            if ( _internalFurProfiles.Length > index && index > -1 ) {
                _internalFurProfiles[index] = data;
            }

        }

        /// <summary>
        /// Replaces the fur profile data for the material at a given index
        /// </summary>
        /// <param name="index">The index where we will replace the data</param>
        /// <param name="asset">The asset containing the new fur profile data</param>
        public void ReplaceFurData( int index, FurProfileAsset asset ) {

            var data = asset.FurProfileData;

            ReplaceFurData( index, data );

        }

        /// <summary>
        /// Sets the fur profile data for a given index, with fine control over which data gets replaced and which remains untouched.
        /// </summary>
        /// <param name="index">The index where the old fur profile data is stored</param>
        /// <param name="data">The new fur profile data</param>
        /// <param name="replaceColorMap">Whether the color map and tint of the fur will be replaced or not</param>
        /// <param name="replaceDataMaps">Whether the data and grooming maps will be replaced or not</param>
        /// <param name="replaceColorMix">Whether the color variation settings will be replaced or not</param>
        /// <param name="replaceStrandsMap">Whether the strands map/ asset will be replaced or not</param>
        public void SetFurData( int index, FurProfileData data, bool replaceColorMap = false, bool replaceDataMaps = false, bool replaceColorMix = false, bool replaceStrandsMap = false ) {

            data.Update( _settings.version );

            if ( _internalFurProfiles.Length > index && index > -1 ) {

                if ( !replaceColorMap ) {
                    data.colorMap = _internalFurProfiles[index].colorMap;
                }

                if ( !replaceDataMaps ) {
                    data.furDataMap = _internalFurProfiles[index].furDataMap;
                    data.furGroomingMap = _internalFurProfiles[index].furGroomingMap;
                }

                if ( !replaceStrandsMap ) {
                    data.renderingSamples = _internalFurProfiles[index].renderingSamples;
                    data.furStrandsAsset = _internalFurProfiles[index].furStrandsAsset;
                    data.furStrandsTexture = _internalFurProfiles[index].furStrandsTexture;
                }

                if ( !replaceColorMix ) {
                    data.useLegacyColorVariation = _internalFurProfiles[index].useLegacyColorVariation;
                    data.legacyColorVariationMap = _internalFurProfiles[index].legacyColorVariationMap;
                    data.noiseShadingTint0 = _internalFurProfiles[index].noiseShadingTint0;
                    data.noiseShadingTint1 = _internalFurProfiles[index].noiseShadingTint1;
                    data.noiseShadingTint2 = _internalFurProfiles[index].noiseShadingTint2;
                    data.noiseShadingTint3 = _internalFurProfiles[index].noiseShadingTint3;
                }

                _internalFurProfiles[index] = data;


                UpdateFurMaterials( true );

            }

        }


        /// <summary>
        /// Sets the fur profile data for a given index, with fine control over which data gets replaced and which remains untouched.
        /// </summary>
        /// <param name="index">The index where the old fur profile data is stored</param>
        /// <param name="asset">The asset where the new fur profile data is stored</param>
        /// <param name="replaceColorMap">Whether the color map and tint of the fur will be replaced or not</param>
        /// <param name="replaceDataMaps">Whether the data and grooming maps will be replaced or not</param>
        /// <param name="replaceColorMix">Whether the color variation settings will be replaced or not</param>
        /// <param name="replaceStrandsMap">Whether the strands map/ asset will be replaced or not</param>
        public void SetFurData( int index, FurProfileAsset asset, bool replaceColorMap = false, bool replaceDataMaps = false, bool replaceColorMix = false, bool replaceStrandsMap = false ) {

            var data = asset.FurProfileData;

            SetFurData( index, data, replaceColorMap, replaceDataMaps, replaceColorMix, replaceStrandsMap );

        }





        /// <summary>
        /// Applies all changes to the fur properties across all materials and forcefully updates any modules that work on the render loop
        /// </summary>
        /// <param name="forceUpdate">Whether to force the update regardless of the Auto-update settings</param>
        public void UpdateFurMaterials( bool forceUpdate = false ) {


            for ( int i = 0; i < FurDataProfiles.Length; ++i ) {

#if UNITY_EDITOR
                if ( _mainRenderer.isFurMaterial.Length != _internalFurProfiles.Length ) {

                    _mainRenderer.AssignRenderer( _mainRenderer.renderer );

                }
#endif
                if ( _mainRenderer.isFurMaterial[i] ) {

#if UNITY_EDITOR
                    if ( !Application.isPlaying ) {
                        _internalFurProfiles[i].Update( _settings.version );
                    }
#endif


#if UNITY_2020_1_OR_NEWER

                    if ( _renderingResources.currentRenderingPipeline == XFurRenderingPipeline.HDRP ) {

                        int flags = 0xff; // enable all light layers
                        float flagsFloat = System.BitConverter.ToSingle( System.BitConverter.GetBytes( flags ), 0 );
                        float[] renderFlags = new float[128];
                        for ( int x = 0; x < 128; x++ ) {
                            renderFlags[x] = flagsFloat;
                        }


                        _furBlocks[i].SetFloatArray( XFurShaderProperties.unityRenderingLayer, renderFlags );
                    }

#endif

                    if ( _vfxModule.IsEnabled && Application.isPlaying ) {
                        _vfxModule.MainRenderLoop( _furBlocks[i], i );
                    }
                    else {
                        _furBlocks[i].SetTexture( XFurShaderProperties.xfurVFXMap, Texture2D.blackTexture );
                    }

                    if ( _lodModule ) {
                        _lodModule.MainRenderLoop( _furBlocks[i], i );
                    }

                    if ( _physicsModule.IsEnabled && Application.isPlaying ) {
                        _physicsModule.MainRenderLoop( _furBlocks[i], i );
                    }
                    else {
                        _furBlocks[i].SetTexture( XFurShaderProperties.xfurPhysicsMap, Texture2D.blackTexture );
                    }

                    _furBlocks[i].SetFloat( XFurShaderProperties.xfurTotalPasses, _internalFurProfiles[i].renderingSamples );

                    _furBlocks[i].SetFloatArray( XFurShaderProperties.xfurInstancedCurrentPass, _internalPassIndexArray );

                    _furBlocks[i].SetFloat( XFurShaderProperties.xfurURPRenderingMode, _settings.urpAdvancedLighting ? 1 : 0 );


                    if ( _renderingResources.currentRenderingPipeline == XFurRenderingPipeline.HDRP ) {
                        _furBlocks[i].SetMatrix( XFurShaderProperties.xfurHDWorld2Local, MainRenderer.renderer.transform.worldToLocalMatrix );
                        _furBlocks[i].SetMatrix( XFurShaderProperties.xfurHDLocal2World, MainRenderer.renderer.transform.localToWorldMatrix );
                    }

                    if ( !Application.isPlaying || forceUpdate || ( _settings.autoUpdateMaterials && _timeForNextUpdate <= 0 ) ) {
                        _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfGroomStrength, _internalFurProfiles[i].groomStrength );


                        _furBlocks[i].SetFloatArray( XFurShaderProperties.xfurInstancedCurrentPass, _internalPassIndexArray );


                        if ( _internalFurProfiles[i].furStrandsAsset ) {
                            _furBlocks[i].SetTexture( XFurShaderProperties.xfurSelfStrandsPattern, _internalFurProfiles[i].furStrandsAsset.FurStrandsTexture );
                        }
                        else if ( _internalFurProfiles[i].furStrandsTexture ) {
                            _furBlocks[i].SetTexture( XFurShaderProperties.xfurSelfStrandsPattern, _internalFurProfiles[i].furStrandsTexture );
                        }
                        else {
                            _furBlocks[i].SetTexture( XFurShaderProperties.xfurSelfStrandsPattern, Texture2D.whiteTexture );
                        }


                        if ( _internalFurProfiles[i].furGroomingMap ) {
                            _furBlocks[i].SetFloat( XFurShaderProperties.xfurHasGroomData, 1 );
                            _furBlocks[i].SetTexture( XFurShaderProperties.xfurGroomData, _internalFurProfiles[i].furGroomingMap );
                        }
                        else {
                            _furBlocks[i].SetFloat( XFurShaderProperties.xfurHasGroomData, _settings.useBakedVertexData ? 1 : 0 );
                            _furBlocks[i].SetTexture( XFurShaderProperties.xfurGroomData, Texture2D.blackTexture );
                        }

                        _furBlocks[i].SetVector( XFurShaderProperties.xfurSelfColor, _internalFurProfiles[i].mainTint );

                        _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfRimPower, _internalFurProfiles[i].rimLightingPower );
                        _furBlocks[i].SetColor( XFurShaderProperties.xfurSelfRimColor, _internalFurProfiles[i].rimLightingTint );
                        _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfRimBoost, _internalFurProfiles[i].rimLightingStrength );

                        _furBlocks[i].SetFloat( XFurShaderProperties.xfurUseBakedData, _settings.useBakedVertexData ? 1 : 0 );
                        _furBlocks[i].SetFloat( XFurShaderProperties.xfurMotionVectors, _settings.renderMotionVectors ? 1 : 0 );

                        _furBlocks[i].SetTexture( XFurShaderProperties.xfurParamData, _internalFurProfiles[i].furDataMap ? _internalFurProfiles[i].furDataMap : Texture2D.whiteTexture );

                        _furBlocks[i].SetTexture( XFurShaderProperties.xfurMainColorMap, _internalFurProfiles[i].colorMap ? _internalFurProfiles[i].colorMap : Texture2D.whiteTexture );

                        if ( _settings.useNormalmap && _renderingResources.emptyNormalmap ) {
                            _furBlocks[i].SetTexture( XFurShaderProperties.xfurNormalmap, _internalFurProfiles[i].normalMap ? _internalFurProfiles[i].normalMap : _renderingResources.emptyNormalmap );
                        }


                        _furBlocks[i].SetVector( XFurShaderProperties.xfurSelfBaseTiling, _internalFurProfiles[i].baseUVTiling );

                        _furBlocks[i].SetTexture( XFurShaderProperties.xfurEmissionMap, ( _internalFurProfiles[i].emissionMap ? _internalFurProfiles[i].emissionMap : Texture2D.whiteTexture ) );

                        _furBlocks[i].SetVector( XFurShaderProperties.xfurEmissionColor, (Vector4)_internalFurProfiles[i].emissiveTint );

                        _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfSmoothness, _internalFurProfiles[i].roughness );
                        _furBlocks[i].SetColor( XFurShaderProperties.xfurSelfSpecularTint, _internalFurProfiles[i].specularTint );


                        _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfLength, _internalFurProfiles[i].furLength * ( _settings.autoCompensateForScale ? CurrentFurRenderer.renderer.transform.lossyScale.x : 1f ) );
                        _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfThickness, _internalFurProfiles[i].furThickness );
                        _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfThicknessCurve, _internalFurProfiles[i].furThicknessCurve );
                        _furBlocks[i].SetColor( XFurShaderProperties.xfurSelfShadowsTint, _internalFurProfiles[i].selfOcclusionTint );
                        _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfOcclusion, _internalFurProfiles[i].selfOcclusionStrength );
                        _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfOcclusionCurve, _internalFurProfiles[i].selfOcclusionCurve );


                        _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfUnderColorMod, _internalFurProfiles[i].secondaryFurStrandBoost );
                        _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfOverColorMod, _internalFurProfiles[i].mainFurStrandBoost );



                        if ( _internalFurProfiles[i].useCurlyFur ) {
                            _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfCurlAmountX, _internalFurProfiles[i].curlyFurParameters.x );
                            _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfCurlAmountY, _internalFurProfiles[i].curlyFurParameters.y );
                            _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfCurlSizeX, _internalFurProfiles[i].curlyFurParameters.z );
                            _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfCurlSizeY, _internalFurProfiles[i].curlyFurParameters.w );
                        }
                        else {
                            _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfCurlAmountX, 0f );
                            _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfCurlAmountY, 0f );
                            _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfCurlSizeX, 0f );
                            _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfCurlSizeY, 0f );
                        }




                        if ( _internalFurProfiles[i].useLegacyColorVariation ) {
                            _furBlocks[i].SetFloat( XFurShaderProperties.xfurLegacyVariation, 1 );
                            _furBlocks[i].SetTexture( XFurShaderProperties.xfurSelfColorVariation, _internalFurProfiles[i].legacyColorVariationMap ? _internalFurProfiles[i].legacyColorVariationMap : Texture2D.blackTexture );
                            _furBlocks[i].SetColor( XFurShaderProperties.xfurSelfColorA, _internalFurProfiles[i].noiseShadingTint0 );
                            _furBlocks[i].SetColor( XFurShaderProperties.xfurSelfColorB, _internalFurProfiles[i].noiseShadingTint1 );
                            _furBlocks[i].SetColor( XFurShaderProperties.xfurSelfColorC, _internalFurProfiles[i].noiseShadingTint2 );
                            _furBlocks[i].SetColor( XFurShaderProperties.xfurSelfColorD, _internalFurProfiles[i].noiseShadingTint3 );
                        }
                        else {
                            _furBlocks[i].SetFloat( XFurShaderProperties.xfurLegacyVariation, 0 );
                            _furBlocks[i].SetFloat( XFurShaderProperties.xfurNoiseTiling, _internalFurProfiles[i].furNoiseShadingTiling );
                            _furBlocks[i].SetTexture( XFurShaderProperties.xfurSelfColorVariation, _internalFurProfiles[i].legacyColorVariationMap ? _internalFurProfiles[i].legacyColorVariationMap : Texture2D.blackTexture );
                            _furBlocks[i].SetColor( XFurShaderProperties.xfurSelfColorA, _internalFurProfiles[i].noiseShadingTint0 );
                            _furBlocks[i].SetColor( XFurShaderProperties.xfurSelfColorB, _internalFurProfiles[i].noiseShadingTint1 );
                            _furBlocks[i].SetColor( XFurShaderProperties.xfurSelfColorC, _internalFurProfiles[i].noiseShadingTint2 );
                            _furBlocks[i].SetColor( XFurShaderProperties.xfurSelfColorD, _internalFurProfiles[i].noiseShadingTint3 );
                        }

                        _furBlocks[i].SetFloat( XFurShaderProperties.xfurSelfWindStrength, _internalFurProfiles[i].windStrengthMultiplier );

                        _furBlocks[i].SetFloat( XFurShaderProperties.xfurUVTiling, _internalFurProfiles[i].furStrandsTiling );

                    }

                    if ( _decalsModule && _decalsModule.IsEnabled ) {
                        _decalsModule.MainRenderLoop( _furBlocks[i], i );
                    }


                    if ( _simpleTouchBendingModule ) {
                        _simpleTouchBendingModule.MainRenderLoop( _furBlocks[i], i );
                    }


                    if ( _dynamicMaskingModule && _dynamicMaskingModule.IsEnabled ) {
                        _dynamicMaskingModule.MainRenderLoop( _furBlocks[i], i );
                    }

                    for ( int m = 0; m < _customModules.Length; m++ ) {
                        if ( _customModules[m] && _customModules[m].Module.IsEnabled ) {
                            _customModules[m].Module.MainRenderLoop( _furBlocks[i], i );
                        }
                    }


                }
            }

            if ( !Application.isPlaying || forceUpdate || ( _settings.autoUpdateMaterials && _timeForNextUpdate <= 0 ) )
                _timeForNextUpdate = _settings.timeBetweenUpdates;

        }


#if UNITY_EDITOR
        protected void OnDrawGizmos() {
            if ( !Application.isPlaying ) {
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
        }
#endif



        /// <summary>
        /// Sets up the Vertex Buffer (if enabled) or the direct mesh that will be used to instantiate and draw the fur.
        /// </summary>
        protected void SetupVertexBufferAndMesh() {

            if ( !CurrentFurRenderer.renderer || _unreadableMeshError ) {
                return;
            }


            if ( IsSkinnedMesh ) {

                if ( _settings.useVertexBuffer ) {

                    _currentFurMesh = null;

                    if ( SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 ) {
                        ( CurrentFurRenderer.renderer as SkinnedMeshRenderer ).vertexBufferTarget = GraphicsBuffer.Target.Vertex;
                    }
                    else {
                        ( CurrentFurRenderer.renderer as SkinnedMeshRenderer ).vertexBufferTarget = GraphicsBuffer.Target.Structured;
                    }

                    var tempRoot = ( CurrentFurRenderer.renderer as SkinnedMeshRenderer ).rootBone;

                    ( CurrentFurRenderer.renderer as SkinnedMeshRenderer ).rootBone = null;


                    _xfurOldVertexBuffer = ( CurrentFurRenderer.renderer as SkinnedMeshRenderer ).GetPreviousVertexBuffer();

                    if ( _settings.renderMotionVectors ) {
                        _xfurVertexBuffer = ( CurrentFurRenderer.renderer as SkinnedMeshRenderer ).GetVertexBuffer();
                    }
                    else {
                        _xfurVertexBuffer = _xfurOldVertexBuffer;
                    }



                    ( CurrentFurRenderer.renderer as SkinnedMeshRenderer ).rootBone = tempRoot;

                    _currentFurMesh = CurrentFurRenderer.originalMesh;



                }
                else {

                    if ( !_tempBakedMesh ) {
                        _tempBakedMesh = new Mesh();
                        _tempBakedMesh.MarkDynamic();
                    }

                    _currentFurMesh = _tempBakedMesh;


                    if ( _tempBakedMesh.vertexCount > 0 && Application.isPlaying && _settings.renderMotionVectors ) {
                        _tempBakedMesh.GetVertices( _mVectorUVs );
                    }

                    var tempRoot = ( CurrentFurRenderer.renderer as SkinnedMeshRenderer ).rootBone;
                    ( CurrentFurRenderer.renderer as SkinnedMeshRenderer ).rootBone = null;
                    ( CurrentFurRenderer.renderer as SkinnedMeshRenderer ).BakeMesh( _tempBakedMesh );
                    ( CurrentFurRenderer.renderer as SkinnedMeshRenderer ).rootBone = tempRoot;

                    if ( _mVectorUVs == null || _mVectorUVs.Capacity != _tempBakedMesh.vertexCount ) {
                        _mVectorUVs = new List<Vector3>( _tempBakedMesh.vertexCount );
                    }

                    if ( Application.isPlaying && _settings.renderMotionVectors ) {
                        _tempBakedMesh.SetUVs( 2, _mVectorUVs );
                    }

                }



            }
            else {
                _currentFurMesh = CurrentFurRenderer.originalMesh;
            }


            if ( _xfurOldVertexBuffer != null ) {
                _xfurVertexBufferStride.x = _xfurOldVertexBuffer.stride;
                _xfurVertexBufferStride.y = CurrentFurRenderer.originalMesh.GetVertexAttributeOffset( VertexAttribute.Position );
                _xfurVertexBufferStride.z = CurrentFurRenderer.originalMesh.GetVertexAttributeOffset( VertexAttribute.Normal );
                _xfurVertexBufferStride.w = CurrentFurRenderer.originalMesh.GetVertexAttributeOffset( VertexAttribute.Tangent );
            }

            if ( IsSkinnedMesh && _settings.useVertexBuffer ) {

                if ( _renderingResources.internalFurMaterial ) {
                    _renderingResources.internalFurMaterial.EnableKeyword( "USE_VERTEXBUFFER" );
                }

                if ( _renderingResources.internalFurMaterialDoubleSided ) {
                    _renderingResources.internalFurMaterialDoubleSided.EnableKeyword( "USE_VERTEXBUFFER" );
                }

                for ( int i = 0; i < _internalFurProfiles.Length; i++ ) {

                    if ( _mainRenderer.isFurMaterial[i] && _furBlocks.Length > i ) {

                        if ( _xfurOldVertexBuffer != null ) {
                            _furBlocks[i].SetBuffer( XFurShaderProperties.vertexBuffer, _xfurVertexBuffer );
                            _furBlocks[i].SetVector( XFurShaderProperties.vertexBufferStride, _xfurVertexBufferStride );
                            _furBlocks[i].SetBuffer( XFurShaderProperties.oldVertexBuffer, _xfurOldVertexBuffer );
                        }

                        CurrentFurRenderer.renderer.SetPropertyBlock( _furBlocks[i], i );
                    }

                }
            }
            else {

                if ( _renderingResources.internalFurMaterial ) {
                    _renderingResources.internalFurMaterial.DisableKeyword( "USE_VERTEXBUFFER" );
                }

                if ( _renderingResources.internalFurMaterialDoubleSided ) {
                    _renderingResources.internalFurMaterialDoubleSided.DisableKeyword( "USE_VERTEXBUFFER" );
                }



            }

        }

        /// <summary>
        /// Renders the actual fur on the surface of the mesh and applies the rendering loop of all the modules.
        /// </summary>
        protected void RenderFur() {


            if ( CurrentFurRenderer.materials == null || CurrentFurRenderer.materials.Length < 1 ) {
                return;
            }

            if ( _lodModule && _lodModule.IsEnabled ) {
                _lodModule.MainLoop();
            }
            else {
                CurrentFurRenderer = _mainRenderer;
            }


            if ( !CurrentFurRenderer.renderer || !CurrentFurRenderer.renderer.isVisible ) {
                return;
            }

            if ( _lodModule.IsFarLOD ) {
                if ( _physicsModule.disableOnLOD ) {
                    _physicsModule.Disable();
                }

                if ( _vfxModule.disableOnLOD ) {
                    _vfxModule.Disable();
                }

            }
            else {
                if ( _physicsModule.disableOnLOD ) {
                    _physicsModule.Enable();
                }

                if ( _vfxModule.disableOnLOD ) {
                    _vfxModule.Enable();
                }
            }

            if ( _physicsModule && _physicsModule.IsEnabled )
                _physicsModule.MainLoop();

            if ( _vfxModule.IsEnabled ) {
                _vfxModule.MainLoop();
            }


            for ( int m = 0; m < _customModules.Length; m++ ) {
                if ( _customModules[m] && _customModules[m].Module.IsEnabled ) {
                    _customModules[m].Module.MainLoop();
                }
            }


            UpdateFurMaterials();


            ReleaseVertexBuffer();
            SetupVertexBufferAndMesh();

            var forceVisible = false;

            if ( _globalCamerasArray.Length < Camera.allCamerasCount ) {
                _globalCamerasArray = new Camera[Camera.allCamerasCount];
            }

            Camera.GetAllCameras( _globalCamerasArray );


            foreach ( Camera cam in _globalCamerasArray ) {
                if ( cam ) {
                    GeometryUtility.CalculateFrustumPlanes( cam, _camFrustumPlanes );
                    if ( GeometryUtility.TestPlanesAABB( _camFrustumPlanes, CurrentFurRenderer.renderer.bounds ) ) {
                        forceVisible = true;
                    }
                }
            }

#if UNITY_EDITOR
            if ( !Application.isPlaying ) {
                if ( SceneView.lastActiveSceneView && SceneView.lastActiveSceneView.camera ) {
                    GeometryUtility.CalculateFrustumPlanes( SceneView.lastActiveSceneView.camera, _camFrustumPlanes );
                    if ( GeometryUtility.TestPlanesAABB( _camFrustumPlanes, CurrentFurRenderer.renderer.bounds ) ) {
                        forceVisible = true;
                    }
                }
            }
#endif

            if ( skipRenderFrame || ( !forceVisible ) || !CurrentFurRenderer.renderer.enabled ) {
                skipRenderFrame = false;
                return;
            }



#if !XFUR_BURSTED

            if ( _tMatricesArray == null || _tMatricesArray.Length != 128 ) {
                _tMatricesArray = new Matrix4x4[128];
            }

            if ( IsSkinnedMesh ) {
                var tempScale = transform.localScale;
                transform.localScale = Vector3.one;


                tPos = CurrentFurRenderer.renderer.transform.position;
                tRot = ( CurrentFurRenderer.renderer as SkinnedMeshRenderer ).rootBone ? ( CurrentFurRenderer.renderer as SkinnedMeshRenderer ).rootBone.rotation : CurrentFurRenderer.renderer.transform.rotation;
                tScale = CurrentFurRenderer.renderer.transform.localScale;

                transform.localScale = tempScale;

                for ( int m = 0; m < 128; ++m ) {
                    _tMatricesArray[m] = Matrix4x4.TRS( tPos, tRot, tScale );
                }

            }
            else {
                for ( int m = 0; m < 128; ++m ) {
                    _tMatricesArray[m] = CurrentFurRenderer.renderer.transform.localToWorldMatrix;
                }
            }



#endif

            CheckInternalFurProfiles();

            if ( !_currentFurMesh ) {
                return;
            }




            for ( int i = 0; i < CurrentFurRenderer.materials.Length; ++i ) {

#if XFUR_BURSTED

                if ( _mainRenderer.isFurMaterial[i] && ( _internalFurProfiles[i].furStrandsAsset || _internalFurProfiles[i].furStrandsTexture ) ) {
                    _furBlocks[i].SetFloat( XFurShaderProperties.xfurForceNonInstanced, _settings.builtinFwdCompatibilityMode ? 1 : 0 );

                    RenderParams rp = new RenderParams( _internalFurProfiles[i].doubleSided ? _renderingResources.internalFurMaterialDoubleSided : _renderingResources.internalFurMaterial );
                    rp.layer = gameObject.layer;
                    rp.lightProbeUsage = _internalFurProfiles[i].useProbes ? UnityEngine.Rendering.LightProbeUsage.BlendProbes : UnityEngine.Rendering.LightProbeUsage.Off;
                    rp.motionVectorMode = _settings.renderMotionVectors ? MotionVectorGenerationMode.Object : MotionVectorGenerationMode.Camera;
                    rp.receiveShadows = FurDataProfiles[i].receiveShadows;
                    rp.shadowCastingMode = _internalFurProfiles[i].castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;


                    _furBlocks[i].SetBuffer( XFurShaderProperties.oldVertexBuffer, _xfurOldVertexBuffer );

                    rp.matProps = _furBlocks[i];

                    if ( !_settings.builtinFwdCompatibilityMode ) {
                        _xfurMatricesJobHandle.Complete();
                        Graphics.RenderMeshInstanced( rp, _currentFurMesh, i, _tMatricesArray, FurDataProfiles[i].renderingSamples );
                    }
                    else {
                        for ( int shell = 0; shell < _internalFurProfiles[i].renderingSamples; ++shell ) {
                            _furBlocks[i].SetFloat( XFurShaderProperties.xfurCurrentPass, shell );
                            Graphics.RenderMesh( rp, _currentFurMesh, i, _tMatricesArray[i].objectToWorld, _tMatricesArray[i].prevObjectToWorld );
                        }
                    }
                }

#else
                    if (_mainRenderer.isFurMaterial[i] && ( _internalFurProfiles[i].furStrandsAsset || _internalFurProfiles[i].furStrandsTexture ) ) {
                        _furBlocks[i].SetFloat( XFurShaderProperties.xfurForceNonInstanced, _settings.builtinFwdCompatibilityMode ? 1 : 0);

                            if (!_settings.builtinFwdCompatibilityMode) {
                                Graphics.DrawMeshInstanced(_currentFurMesh, i, _internalFurProfiles[i].doubleSided ? _renderingResources.internalFurMaterialDoubleSided : _renderingResources.internalFurMaterial, _tMatricesArray, _internalFurProfiles[i].renderingSamples, _furBlocks[i], _internalFurProfiles[i].castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off, _internalFurProfiles[i].receiveShadows, gameObject.layer, null, _internalFurProfiles[i].useProbes ? UnityEngine.Rendering.LightProbeUsage.BlendProbes : UnityEngine.Rendering.LightProbeUsage.Off);
                            }
                            else {
                                for (int shell = 0; shell < _internalFurProfiles[i].renderingSamples; ++shell) {
                                    _furBlocks[i].SetFloat(XFurShaderProperties.xfurCurrentPass, shell);
                                    Graphics.DrawMesh(_currentFurMesh, _tMatricesArray[i], _internalFurProfiles[i].doubleSided ? _renderingResources.internalFurMaterialDoubleSided : _renderingResources.internalFurMaterial, gameObject.layer, null, i, _furBlocks[i], _internalFurProfiles[i].castShadows, _internalFurProfiles[i].receiveShadows, _internalFurProfiles[i].useProbes);
                                }
                            }
                        
                    }
#endif

            }


        }


        /// <summary>
        /// Clears the VertexBuffer data 
        /// </summary>
        void ClearBufferData() {


            if ( _furBlocks != null ) {
                for ( int i = 0; i < _furBlocks.Length; i++ ) {
                    _furBlocks[i].SetBuffer( XFurShaderProperties.vertexBuffer, default( GraphicsBuffer ) );
                    if ( CurrentFurRenderer.renderer )
                        CurrentFurRenderer.renderer.SetPropertyBlock( _furBlocks[i], i );

                }
            }

            ReleaseVertexBuffer();

        }



        void ReleaseVertexBuffer() {

            _xfurVertexBuffer?.Release();
            _xfurOldVertexBuffer?.Release();
            _xfurOldVertexBuffer = null;
            _xfurVertexBuffer = null;

        }


        protected void Update() {

            if ( !CurrentFurRenderer.renderer || _unreadableMeshError ) {
                return;
            }

            if ( _settings.autoUpdateMaterials ) {
                _timeForNextUpdate -= Time.deltaTime;
            }

#if XFUR_BURSTED
            _xfurMatricesJobHandle.Complete();

#endif


        }


        protected void LateUpdate() {

            if ( !CurrentFurRenderer.renderer || _unreadableMeshError ) {
                return;
            }

#if XFUR_BURSTED

            if ( !_tMatricesArray.IsCreated ) {
                return;
            }

            var matricesJob = new XFurMatrixProcessingJob();


            if ( IsSkinnedMesh ) {
                var tempScale = transform.localScale;
                transform.localScale = Vector3.one;

                if ( _settings.useVertexBuffer ) {
                    matricesJob.originalMatrix = Matrix4x4.TRS( CurrentFurRenderer.renderer.transform.position, ( CurrentFurRenderer.renderer as SkinnedMeshRenderer ).rootBone ? ( CurrentFurRenderer.renderer as SkinnedMeshRenderer ).rootBone.rotation : CurrentFurRenderer.renderer.transform.rotation, CurrentFurRenderer.renderer.transform.localScale );
                }
                else {
                    matricesJob.originalMatrix = Matrix4x4.TRS( CurrentFurRenderer.renderer.transform.position, CurrentFurRenderer.renderer.transform.rotation, CurrentFurRenderer.renderer.transform.localScale );
                }

                transform.localScale = tempScale;
            }
            else {
                matricesJob.originalMatrix = CurrentFurRenderer.renderer.transform.localToWorldMatrix;
            }

            matricesJob.matrices = _tMatricesArray;
            _xfurMatricesJobHandle = matricesJob.Schedule( 128, 64 );


#endif


            RenderFur();


        }


        protected void OnDisable() {

            ClearBufferData();

            if ( _renderingResources.emptyNormalmap )
                DestroyImmediate( _renderingResources.emptyNormalmap );

#if XFUR_BURSTED
            _xfurMatricesJobHandle.Complete();
            _tMatricesArray.Dispose();
#endif

            if ( !Application.isPlaying ) {
                CleanupXFurData();
            }

            if ( _randomizationModule ) {
                _randomizationModule.Unload();
            }

            if ( _lodModule ) {
                _lodModule.Unload();
            }

            if ( _vfxModule ) {
                _vfxModule.Unload();
            }

            if ( _physicsModule ) {
                _physicsModule.Unload();
            }

            if ( _decalsModule ) {
                _decalsModule.Unload();
            }

            if ( _simpleTouchBendingModule ) {
                _simpleTouchBendingModule.Unload();
            }

            if ( _dynamicMaskingModule ) {
                _dynamicMaskingModule.Unload();
            }



            if ( Application.isPlaying ) {
                for ( int i = 0; i < _customModules.Length; i++ ) {
                    if ( _customModules[i] && _customModules[i].isRuntimeReady ) {
                        _customModules[i].Module.Unload();
                    }
                }
            }


        }


        protected void OnDestroy() {

            CleanupXFurData();

            if ( _randomizationModule ) {
                _randomizationModule.Unload();
            }

            if ( _lodModule ) {
                _lodModule.Unload();
            }

            if ( _vfxModule ) {
                _vfxModule.Unload();
            }

            if ( _physicsModule ) {
                _physicsModule.Unload();
            }

            if ( _decalsModule ) {
                _decalsModule.Unload();
            }

            if ( _simpleTouchBendingModule ) {
                _simpleTouchBendingModule.Unload();
            }


            if ( _dynamicMaskingModule ) {
                _dynamicMaskingModule.Unload();
            }



            if ( Application.isPlaying ) {
                for ( int i = 0; i < _customModules.Length; i++ ) {
                    if ( _customModules[i] && _customModules[i].isRuntimeReady ) {
                        _customModules[i].Module.Unload();
                        _customModules[i].Module.UnloadResources();
                        DestroyImmediate( _customModules[i] );
                    }
                }
            }

        }


        protected void CleanupXFurData() {

            for ( int i = 0; i < FurDataProfiles.Length; i++ ) {

                if ( FurDataProfiles[i].colorMap is RenderTexture ) {
                    RenderTexture.ReleaseTemporary( (RenderTexture)FurDataProfiles[i].colorMap );
                }

                if ( FurDataProfiles[i].legacyColorVariationMap is RenderTexture ) {
                    RenderTexture.ReleaseTemporary( (RenderTexture)FurDataProfiles[i].legacyColorVariationMap );
                }

                if ( FurDataProfiles[i].furDataMap is RenderTexture ) {
                    RenderTexture.ReleaseTemporary( (RenderTexture)FurDataProfiles[i].furDataMap );
                }

                if ( FurDataProfiles[i].furGroomingMap is RenderTexture ) {
                    RenderTexture.ReleaseTemporary( (RenderTexture)FurDataProfiles[i].furGroomingMap );
                }

            }

        }



        #region EDITOR VARIABLES


#if UNITY_EDITOR

        public bool[] folds = new bool[16];

        public bool[] profileFolds = new bool[32];

        /// <summary>Gets a copy of the Main Renderer data struct for this XFur Studio Instance</summary>
        public XFurMeshRendererData MainRenderer { get { return _mainRenderer; } set { _mainRenderer = value; } }
#else
        public XFurMeshRendererData MainRenderer { get { return _mainRenderer; } }
#endif

        #endregion


    }

}