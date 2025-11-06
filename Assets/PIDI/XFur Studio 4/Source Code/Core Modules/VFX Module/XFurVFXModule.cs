/*

XFur Studio™, by Irreverent Software™
Copyright© 2018-2025, Jorge Pinal Negrete. All Rights Reserved.

*/

namespace XFurStudio.Modules {

    using XFurStudio.Core;
    using UnityEngine;

#if UNITY_EDITOR
    using UnityEditor;
#endif

    [System.Serializable]
    public partial class XFurVFXModule : XFurStudioModule {


        #region SHADER PROPERTIES

        readonly static int _xfurVFXMask = Shader.PropertyToID( "_XFurVFXMask" );
        readonly static int _xfurVFX1Color = Shader.PropertyToID( "_XFurVFX1Color" );
        readonly static int _xfurVFX2Color = Shader.PropertyToID( "_XFurVFX2Color" );
        readonly static int _xfurVFX1Penetration = Shader.PropertyToID( "_XFurVFX1Penetration" );
        readonly static int _xfurVFX2Penetration = Shader.PropertyToID( "_XFurVFX2Penetration" );
        readonly static int _xfurVFX3Penetration = Shader.PropertyToID( "_XFurVFX3Penetration" );
        readonly static int _xfurVFX1Smoothness = Shader.PropertyToID( "_XFurVFX1Smoothness" );
        readonly static int _xfurVFX2Smoothness = Shader.PropertyToID( "_XFurVFX2Smoothness" );
        readonly static int _xfurVFX3Smoothness = Shader.PropertyToID( "_XFurVFX3Smoothness" );



        readonly static int _xfurVFXBasicMode = Shader.PropertyToID( "_XFurBasicMode" );
        readonly static int _xfurVFXObjectMatrix = Shader.PropertyToID( "_XFurObjectMatrix" );
        readonly static int _xfurVFXInputMap = Shader.PropertyToID( "_InputMap" );
        readonly static int _xfurVFXMask0 = Shader.PropertyToID( "_VFXMask0" );
        readonly static int _xfurVFXTiling0 = Shader.PropertyToID( "_VFXTiling0" );
        readonly static int _xfurVFXAdd0 = Shader.PropertyToID( "_FXAdd0" );
        readonly static int _xfurVFXMelt0 = Shader.PropertyToID( "_FXMelt0" );
        readonly static int _xfurVFXFalloff0 = Shader.PropertyToID( "_FXFalloff0" );
        readonly static int _xfurVFXMask1 = Shader.PropertyToID( "_VFXMask1" );
        readonly static int _xfurVFXTiling1 = Shader.PropertyToID( "_VFXTiling1" );
        readonly static int _xfurVFXAdd1 = Shader.PropertyToID( "_FXAdd1" );
        readonly static int _xfurVFXMelt1 = Shader.PropertyToID( "_FXMelt1" );
        readonly static int _xfurVFXFalloff1 = Shader.PropertyToID( "_FXFalloff1" );
        readonly static int _xfurVFXMask2 = Shader.PropertyToID( "_VFXMask2" );
        readonly static int _xfurVFXTiling2 = Shader.PropertyToID( "_VFXTiling2" );
        readonly static int _xfurVFXAdd2 = Shader.PropertyToID( "_FXAdd2" );
        readonly static int _xfurVFXMelt2 = Shader.PropertyToID( "_FXMelt2" );
        readonly static int _xfurVFXFalloff2 = Shader.PropertyToID( "_FXFalloff2" );
        readonly static int _xfurVFXDirection1 = Shader.PropertyToID( "_XFurVFX1Direction" );
        readonly static int _xfurVFXDirection2 = Shader.PropertyToID( "_XFurVFX2Direction" );
        readonly static int _xfurVFXGlobalAdd1 = Shader.PropertyToID( "_XFurVFX1GlobalAdd" );
        readonly static int _xfurVFXGlobalAdd2 = Shader.PropertyToID( "_XFurVFX2GlobalAdd" );

        readonly static int _xfurVFXGlobalMask = Shader.PropertyToID( "_XFurVFXGlobalMask" );

        #endregion

        [System.Serializable]
        public partial class VFXEffect {

            public string name = "New Effect";

            /// <summary>
            /// The color of the effect. Applies only to some effects.
            /// </summary>
            public Color color;
                        
            /// <summary>
            /// The specular tint of the effect. Applies only to some effects.
            /// </summary>
            public Color specular;
            
            /// <summary>
            /// How much the effect will penetrate through the fur strands
            /// </summary>
            public float penetration;

            /// <summary>
            /// Smoothness of the effect. While XFur Studio uses roughness, the VFX module still uses smoothness internally
            /// (for backwards compatibility). Remember, smoothness = 1-roughness and roughness = 1-smoothness.
            /// </summary>
            public float smoothness;

            /// <summary>
            /// The intensity of the effect as it is applied over time on the fur.
            /// </summary>
            public float intensity = 2.0f;
            
            /// <summary>
            /// The time it takes for the effect to fade out when it is no longer being applied
            /// </summary>
            public float fadeoutTime = 5f;
            
            
            /// <summary>
            /// The tolerance that the effect will have when spreading on faces pointing opposite of the direction in which the effect
            /// is being applied
            /// </summary>
            public float normalFalloff = 0.15f;
            
            /// <summary>
            /// The texture used to mask the effect being applied, giving it some variety as it is drawn
            /// </summary>
            public Texture vfxMask;
            
            /// <summary>
            /// The tiling of the VFXMask texture
            /// </summary>
            public float vfxTiling = 2;
            
            
            public bool updated;
            
            /// <summary>
            /// Whether the weather component's influence over this effect should be ignored
            /// </summary>
            public bool ignoreWeatherComponent;


            public void Copy( VFXEffect other ) {
                color = other.color;
                specular = other.specular;
                penetration = other.penetration;
                smoothness = other.smoothness;
                intensity = other.intensity;
                fadeoutTime = other.fadeoutTime;
                normalFalloff = other.normalFalloff;
                vfxMask = other.vfxMask;
                vfxTiling = other.vfxTiling;
                ignoreWeatherComponent = other.ignoreWeatherComponent;
            }


        }


        [SerializeField] protected Shader VFXProgressiveShader;

        [SerializeField] protected Material _vfxMat;

        private RenderTexture[] _vfxTexture;

        private int _internalRes;

        public bool disableOnLOD;

        [SerializeField] protected float _updateTime = 0.05f;

        private float _timer;

        /// <summary>
        /// Internal. Do not use. 
        /// </summary>
        public RenderTexture[] VFXTexture { get { return _vfxTexture; } }


        [SerializeField] protected VFXEffect Snow = new VFXEffect { name = "SnowFX", color = new Color( 0.85f, 0.95f, 1f, 1f ), specular = new Color( 0.45f, 0.5f, 0.6f ), smoothness = 0.5f, intensity = 1.25f, fadeoutTime = 4.0f, vfxTiling = 2, normalFalloff = 0.75f, penetration = 0.65f };

        [SerializeField] protected VFXEffect Rain = new VFXEffect { name = "RainFX", color = Color.white, specular = new Color( 0.45f, 0.5f, 0.6f ), smoothness = 0.75f, intensity = 1.25f, fadeoutTime = 4.0f, vfxTiling = 2, normalFalloff = 0.75f };

        [SerializeField] protected VFXEffect Blood = new VFXEffect { name = "BloodFX", color = new Color( 0.75f, 0.05f, 0.05f ), specular = new Color( 0.15f, 0.15f, 0.15f ), smoothness = 0.2f, intensity = 1.25f, fadeoutTime = 10.0f, vfxTiling = 2, normalFalloff = 0.75f };

        protected override Vector3Int TargetVersion { get { return new Vector3Int( 4, 0, 0 ); } }


        public override void Setup( XFurStudioInstance xfurOwner ) {
            
            _internalName = "VFX & Weather";
            Status = ModuleStatus.Stable;
            
            base.Setup( xfurOwner );

        }


        public override void SetQuality( ModuleQuality targetQuality ) {

            _quality = targetQuality;

            switch ( _quality ) {
                case ModuleQuality.VeryLow:
                    _internalRes = 32;
                    break;
                case ModuleQuality.Low:
                    _internalRes = 64;
                    break;
                case ModuleQuality.Normal:
                    _internalRes = 128;
                    break;
                case ModuleQuality.High:
                    _internalRes = 256;
                    break;
            }

        }





        public override void Load() {


            if ( !_vfxMat ) {
                _vfxMat = new Material( VFXProgressiveShader );
            }

            if ( Owner.Settings.useVertexBuffer && Owner.IsSkinnedMesh ) {
                _vfxMat.EnableKeyword( "USE_VERTEXBUFFER" );
            }
            else {
                _vfxMat.DisableKeyword( "USE_VERTEXBUFFER" );
            }

            if ( Application.isPlaying ) {
                _updateTime = Random.Range( _updateTime * 0.9f, _updateTime * 1.1f );
            }

            SetQuality( _quality );

#if UNITY_2019_3_OR_NEWER
            var rd = new RenderTextureDescriptor( _internalRes, _internalRes, RenderTextureFormat.ARGB32, 0, 2 );
#else
            var rd = new RenderTextureDescriptor( _internalRes, _internalRes, RenderTextureFormat.ARGB32, 0 );
#endif

            _vfxTexture = new RenderTexture[Owner.MainRenderer.furProfiles.Length];

            for ( int i = 0; i < _vfxTexture.Length; i++ ) {

                if ( Owner.MainRenderer.isFurMaterial[i] ) {
                    _vfxTexture[i] = RenderTexture.GetTemporary( rd );
                    _vfxTexture[i].filterMode = FilterMode.Bilinear;
                    _vfxTexture[i].wrapMode = TextureWrapMode.Clamp;
                    XFurStudioAPI.LoadPaintResources();
                    XFurStudioAPI.FillTexture( Owner, Color.black, _vfxTexture[i], Color.black );
                    _vfxTexture[i].name = "XFUR_VFX" + i + "_" + Owner.name;
                }
            }



        }


        public override void MainLoop() {

            if ( Status == ModuleStatus.CriticalError ) {
                return;
            }

            if ( Application.isPlaying ) {
                if ( Time.timeSinceLevelLoad > _timer ) {
                    VFXPass();
                    _timer = Time.timeSinceLevelLoad + _updateTime;
                }
            }

        }


        public override void MainRenderLoop( MaterialPropertyBlock block, int furProfileIndex ) {

            if ( Status == ModuleStatus.CriticalError ) {
                return;
            }


            if ( block!= null && furProfileIndex > -1 && _vfxTexture != null && furProfileIndex < _vfxTexture.Length && _vfxTexture[furProfileIndex] != null ) {
                block.SetTexture( _xfurVFXMask, _vfxTexture[furProfileIndex] );
                block.SetColor( _xfurVFX1Color, Blood.color );
                block.SetColor( _xfurVFX2Color, Snow.color );
                block.SetFloat( _xfurVFX1Penetration, 1 - Blood.penetration );
                block.SetFloat( _xfurVFX2Penetration, 1 - Snow.penetration );
                block.SetFloat( _xfurVFX3Penetration, 1 - Rain.penetration );
                block.SetFloat( _xfurVFX1Smoothness, Blood.smoothness );
                block.SetFloat( _xfurVFX2Smoothness, Snow.smoothness );
                block.SetFloat( _xfurVFX3Smoothness, Rain.smoothness );
            }
            else {
                Load();
            }

        }


        public override void Unload() {
            if ( _vfxTexture != null ) {
                for ( int i = 0; i < _vfxTexture.Length; i++ ) {
                    if ( _vfxTexture[i] )
                        RenderTexture.ReleaseTemporary( _vfxTexture[i] );
                }
            }
        }


        public override void UnloadResources() {

        }


        public override void Clone<T>( T otherModule ) {
            if ( otherModule is XFurVFXModule ) {
                var otherVFX = otherModule as XFurVFXModule;
                Snow.Copy( otherVFX.Snow );
                Rain.Copy( otherVFX.Rain );
                Blood.Copy( otherVFX.Blood );
                _updateTime = otherVFX._updateTime;
                SetQuality( otherVFX._quality );
                disableOnLOD = otherVFX.disableOnLOD;
            }
        }


        protected void VFXPass() {

            if ( _internalRes < 16 ) {
                Unload();
                Load();
            }

            if ( !_vfxMat ) {
                _vfxMat = new Material( VFXProgressiveShader );
            }

            var targetMatrix = Owner.CurrentFurRenderer.renderer.transform.localToWorldMatrix;
            var targetMesh = Owner.CurrentMesh;

            if ( !targetMesh ) {
                return;
            }

#if UNITY_2019_3_OR_NEWER
            var rd = new RenderTextureDescriptor( _internalRes, _internalRes, RenderTextureFormat.ARGB32, 0, 2 );
#else
            var rd = new RenderTextureDescriptor( _internalRes, _internalRes, RenderTextureFormat.ARGB32, 0 );
#endif

            if ( Owner.Settings.useVertexBuffer && Owner.IsSkinnedMesh ) {
                _vfxMat.EnableKeyword( "USE_VERTEXBUFFER" );
                _vfxMat.SetBuffer( XFurShaderProperties.vertexBuffer, Owner.XFurVertexBuffer );
                _vfxMat.SetVector( XFurShaderProperties.vertexBufferStride, Owner.XFurVertexBufferStride );

            }
            else {
                _vfxMat.DisableKeyword( "USE_VERTEXBUFFER" );
            }


            for ( int i = 0; i < _vfxTexture.Length; i++ ) {

                if ( _vfxTexture[i] && Owner.MainRenderer.isFurMaterial[i] ) {
                    var tempRT1 = RenderTexture.GetTemporary( rd );


                    _vfxMat.SetVector( _xfurVFXGlobalMask, new Vector4( Snow.ignoreWeatherComponent ? 0 : 1, Rain.ignoreWeatherComponent ? 0 : 1, 1 ) );

                    _vfxMat.SetFloat( _xfurVFXBasicMode, 0 );

                    if ( Owner.IsSkinnedMesh ) {
                        if ( ( Owner.CurrentFurRenderer.renderer as SkinnedMeshRenderer ).rootBone ) {
                            _vfxMat.SetMatrix( _xfurVFXObjectMatrix, ( Owner.CurrentFurRenderer.renderer as SkinnedMeshRenderer ).rootBone.localToWorldMatrix );
                        }
                        else {
                            _vfxMat.SetMatrix( _xfurVFXObjectMatrix, ( Owner.CurrentFurRenderer.renderer as SkinnedMeshRenderer ).localToWorldMatrix );
                        }
                    }
                    else {
                        _vfxMat.SetMatrix( _xfurVFXObjectMatrix, Owner.CurrentFurRenderer.renderer.localToWorldMatrix );
                    }



                    _vfxMat.SetTexture( _xfurVFXInputMap, _vfxTexture[i] );
                    _vfxMat.SetTexture( _xfurVFXMask0, Blood.vfxMask ? Blood.vfxMask : Texture2D.whiteTexture );
                    _vfxMat.SetFloat( _xfurVFXTiling0, Blood.vfxTiling );
                    _vfxMat.SetFloat( _xfurVFXAdd0, 0 );

                    if ( Blood.fadeoutTime <= 0 ) {
                        _vfxMat.SetFloat( _xfurVFXMelt0, 0 );
                    }
                    else {
                        _vfxMat.SetFloat( _xfurVFXMelt0, ( 1.0f / Blood.fadeoutTime ) * _updateTime );
                    }

                    _vfxMat.SetTexture( _xfurVFXMask1, Snow.vfxMask ? Snow.vfxMask : Texture2D.whiteTexture );
                    _vfxMat.SetFloat( _xfurVFXTiling1, Snow.vfxTiling );
                    _vfxMat.SetFloat( _xfurVFXAdd1, Snow.intensity * _updateTime );

                    if ( Snow.fadeoutTime <= 0 ) {
                        _vfxMat.SetFloat( _xfurVFXMelt1, 0 );
                    }
                    else {
                        _vfxMat.SetFloat( _xfurVFXMelt1, ( 1.0f / Snow.fadeoutTime ) * _updateTime );
                    }

                    _vfxMat.SetFloat( _xfurVFXFalloff1, Snow.normalFalloff );
                    _vfxMat.SetTexture( _xfurVFXMask2, Rain.vfxMask ? Rain.vfxMask : Texture2D.whiteTexture );
                    _vfxMat.SetFloat( _xfurVFXTiling2, Rain.vfxTiling );
                    _vfxMat.SetFloat( _xfurVFXAdd2, Rain.intensity * _updateTime );

                    if ( Rain.fadeoutTime <= 0 ) {
                        _vfxMat.SetFloat( _xfurVFXMelt2, 0 );
                    }
                    else {
                        _vfxMat.SetFloat( _xfurVFXMelt2, ( 1.0f / Rain.fadeoutTime ) * _updateTime );
                    }

                    _vfxMat.SetFloat( _xfurVFXFalloff2, Rain.normalFalloff );

                    if ( XFurStudioInstance.WeatherManager ) {
                        _vfxMat.SetVector( _xfurVFXDirection1, XFurStudioInstance.WeatherManager.SnowDirection );
                        _vfxMat.SetVector( _xfurVFXDirection2, XFurStudioInstance.WeatherManager.RainDirection );
                        _vfxMat.SetFloat( _xfurVFXGlobalAdd1, XFurStudioInstance.WeatherManager.SnowIntensity );
                        _vfxMat.SetFloat( _xfurVFXGlobalAdd2, XFurStudioInstance.WeatherManager.RainIntensity );
                    }
                    else {
                        _vfxMat.SetVector( _xfurVFXDirection1, Vector3.down );
                        _vfxMat.SetVector( _xfurVFXDirection2, Vector3.down );
                    }


                    var currentActive = RenderTexture.active;
                    RenderTexture.active = tempRT1;
                    GL.Clear( true, true, new Color( 0, 0, 0, 0 ) );
                    _vfxMat.SetPass( 0 );
                    Graphics.DrawMeshNow( targetMesh, targetMatrix, i );
                    RenderTexture.active = currentActive;

                    Graphics.Blit( tempRT1, _vfxTexture[i] );

                    RenderTexture.ReleaseTemporary( tempRT1 );

                    XFurStudioAPI.LoadPaintResources();
                    XFurStudioAPI.FillUVCracks( _xfurInstance, _vfxTexture[i], i );

                }

            }

        }




#if UNITY_EDITOR


        private bool[] fxFolds = new bool[8];

        public override void UpdateModule() {

            if ( Snow.penetration < 0.1f ) {
                Snow.penetration = 0.65f;
            }

            if ( Blood.penetration < 0.1f ) {
                Blood.penetration = 0.75f;
            }

            if ( Rain.penetration < 0.1f ) {
                Rain.penetration = 0.75f;
            }

            _internalName = "VFX & Weather";
            _version = TargetVersion;
            Status = ModuleStatus.Stable;


            if ( !VFXProgressiveShader ) {
                VFXProgressiveShader = Shader.Find( "Hidden/XFur Studio/Modules/VFX Pass" );

                if ( !VFXProgressiveShader ) {
                    Status = ModuleStatus.CriticalError;
                    Debug.LogError( "Critical Error on the VFX Module : The GPU accelerated VFX shader has not been found. Please re-import the asset in order to restore the missing files" );
                }
            }


        }


        public override void ModuleUI( SerializedProperty property ) {

            base.ModuleUI( property );

            GUILayout.Space( 16 );

            _quality = (ModuleQuality)StandardEnumField( new GUIContent( "FX Mask Quality", "The overall _quality of the mask used to paint different FX over the fur" ), _quality );

            if ( !Application.isPlaying )
                _updateTime = EditorGUILayout.FloatField( new GUIContent( "Update Frequency (s)", "The update frequency (in seconds) of this module" ), Mathf.Clamp( _updateTime, 0.01f, 1 ) );

            GUILayout.Space( 16 );

            if ( Owner.LODModule.IsEnabled ) {
                disableOnLOD = EnableDisableToggle( new GUIContent( "Disable with LOD", "Disables this module when the character is far from the camera" ), disableOnLOD );
            }
            else {
                disableOnLOD = false;
            }

            GUILayout.Space( 16 );

            if ( BeginCenteredGroup( "Snow FX", ref fxFolds[0] ) ) {
                GUILayout.Space( 8 );
                Snow.ignoreWeatherComponent = EnableDisableToggle( new GUIContent( "Ignore Weather Component" ), Snow.ignoreWeatherComponent, true );
                Snow.color = EditorGUILayout.ColorField( new GUIContent( "Snow Color" ), Snow.color );
                Snow.intensity = EditorGUILayout.Slider( new GUIContent( "Intensity" ), Snow.intensity, 0, 24 );
                Snow.penetration = EditorGUILayout.Slider( new GUIContent( "Penetration" ), Snow.penetration, 0.25f, 1.0f );
                Snow.fadeoutTime = EditorGUILayout.FloatField( new GUIContent( "Fade Time" ), Snow.fadeoutTime );
                Snow.normalFalloff = EditorGUILayout.Slider( new GUIContent( "Falloff" ), Snow.normalFalloff, 0.05f, 2.0f );

                GUILayout.Space( 8 );

                Snow.vfxMask = ObjectField<Texture>( new GUIContent( "VFX Mask Texture" ), Snow.vfxMask );
                Snow.vfxTiling = EditorGUILayout.Slider( new GUIContent( "VFX Tiling" ), Snow.vfxTiling, 0, 32 );

                GUILayout.Space( 8 );
            }
            EndCenteredGroup();

            if ( BeginCenteredGroup( "Rain FX", ref fxFolds[1] ) ) {
                GUILayout.Space( 8 );
                Rain.ignoreWeatherComponent = EnableDisableToggle( new GUIContent( "Ignore Weather Component" ), Rain.ignoreWeatherComponent, true );
                Rain.intensity = EditorGUILayout.Slider( new GUIContent( "Intensity" ), Rain.intensity, 0, 24 );
                Rain.penetration = EditorGUILayout.Slider( new GUIContent( "Penetration" ), Rain.penetration, 0.25f, 1.0f );
                Rain.smoothness = 1 - EditorGUILayout.Slider( new GUIContent( "Roughness" ), 1 - Rain.smoothness, 0, 0.75f );
                Rain.fadeoutTime = EditorGUILayout.FloatField( new GUIContent( "Fade Time" ), Rain.fadeoutTime );
                Rain.normalFalloff = EditorGUILayout.Slider( new GUIContent( "Falloff" ), Rain.normalFalloff, 0.05f, 2.0f );

                GUILayout.Space( 8 );

                Rain.vfxMask = ObjectField<Texture>( new GUIContent( "VFX Mask Texture" ), Rain.vfxMask );
                Rain.vfxTiling = EditorGUILayout.Slider( new GUIContent( "VFX Tiling" ), Rain.vfxTiling, 0, 32 );

                GUILayout.Space( 8 );
            }
            EndCenteredGroup();

            if ( BeginCenteredGroup( "Blood FX", ref fxFolds[2] ) ) {
                GUILayout.Space( 8 );

                Blood.color = EditorGUILayout.ColorField( new GUIContent( "Blood Color" ), Blood.color );
                Blood.fadeoutTime = EditorGUILayout.FloatField( new GUIContent( "Fade Time" ), Blood.fadeoutTime );
                Blood.penetration = EditorGUILayout.Slider( new GUIContent( "Penetration" ), Blood.penetration, 0.25f, 1.0f );
                Blood.smoothness = 1 - EditorGUILayout.Slider( new GUIContent( "Roughness" ), 1 - Blood.smoothness, 0, .75f );
                GUILayout.Space( 8 );

                Blood.vfxMask = ObjectField<Texture>( new GUIContent( "VFX Mask Texture" ), Blood.vfxMask );
                Blood.vfxTiling = EditorGUILayout.Slider( new GUIContent( "VFX Tiling" ), Blood.vfxTiling, 0, 32 );

                GUILayout.Space( 8 );
            }
            EndCenteredGroup();

            GUILayout.Space( 16 );


        }

#endif

    }
}