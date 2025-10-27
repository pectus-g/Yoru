
/*

XFur Studio™, by Irreverent Software™
Copyright© 2018-2025, Jorge Pinal Negrete. All Rights Reserved.

*/

namespace XFurStudio.Modules{


    using XFurStudio.Core;
    using UnityEngine;
    using System.Collections.Generic;

#if UNITY_EDITOR
    using UnityEditor;
#endif

    /// <summary>
    /// This module handles the drawing of textures over the fur (as if they were spray-painted decals)
    /// using the main UV channel to display them. These decals can be mixed in an overlay, additive or multiplied mode.
    /// </summary>
    [System.Serializable]
    public partial class XFurDecalsModule : XFurStudioModule {

        [SerializeField] protected Shader DecalsShader;

        static readonly int _xfurDecalTex = Shader.PropertyToID( "_Decal" );
        static readonly int _xfurDecalOffsetTiling = Shader.PropertyToID( "_DecalOffsetTiling" );
        static readonly int _xfurDecalMixMode = Shader.PropertyToID( "_MixMode" );
        static readonly int _xfurDecalColor = Shader.PropertyToID( "_DecalColor" );

        static Material _decalsMat;

        protected override Vector3Int TargetVersion { get { return new Vector3Int( 4, 0, 0 ); } }

        public enum MixingMode { Overlay, Add, Multiply }

        [System.Serializable]
        public partial class DecalDefinition {
            /// <summary>
            /// The color of the decal
            /// </summary>
            public Color color = Color.white;
           
            /// <summary>
            /// The actual decal texture
            /// </summary>
            public Texture sourceDecal;

            /// <summary>
            /// The offset applied to the decal texture
            /// </summary>
            public Vector2 offset;

            /// <summary>
            /// The tiling of the decal texture
            /// </summary>
            public Vector2 tiling = Vector2.one;

            /// <summary>
            /// The color-mix mode applied to this decal
            /// </summary>
            public MixingMode mixingMode;

            public DecalDefinition() {

                color = Color.white;
                tiling = Vector2.one;
                mixingMode = MixingMode.Overlay;

            }

        }

        /// <summary>
        /// The different decals to be applied to each fur material.
        /// </summary>
        [System.Serializable]
        public partial class PerProfileDecals {

            public bool enabled;

            public Color furTint;

            [System.NonSerialized] public bool[] folds = new bool[4];

            [System.NonSerialized] public RenderTexture finalOutput;

            public int outputMode;

            public List<DecalDefinition> decals = new List<DecalDefinition>();


            public PerProfileDecals() {

                folds = new bool[4];
                decals = new List<DecalDefinition>();

            }

        }

         

        public List<PerProfileDecals> ProfileDecals = new List<PerProfileDecals>();


        public override void Setup( XFurStudioInstance xfurOwner ) {

            _internalName = "UV Decals";
            Status = ModuleStatus.Stable;
            
            base.Setup( xfurOwner );
        
        }


#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.SubsystemRegistration )]
        public static void DestroyMaterial() {
            if ( _decalsMat )
                Object.DestroyImmediate( _decalsMat );
        }
#endif


        public override void Load() {

            if ( Owner.MainRenderer.renderer ) {
                if ( ProfileDecals.Count != Owner.MainRenderer.materials.Length ) {
                    ProfileDecals = new List<PerProfileDecals>();
                    for ( int i = 0; i < Owner.MainRenderer.materials.Length; i++ ) {
                        ProfileDecals.Add( new PerProfileDecals() );
                        ProfileDecals[i].enabled = Owner.MainRenderer.isFurMaterial[i];
                    }
                }
            }
            if ( !_decalsMat && DecalsShader ) {
                _decalsMat = new Material( DecalsShader );
            }

        }


        public override void MainLoop() {

        }


        private void GenerateDecals() {

            for( int i = 0; i < ProfileDecals.Count; i++ ) {

                RenderTexture.ReleaseTemporary( ProfileDecals[i].finalOutput );

                RenderTexture tempRT0;
                RenderTexture tempRT1;

                if ( ProfileDecals[i].outputMode == 0 ) {
                    if ( Owner.FurDataProfiles[i].colorMap ) {
                        tempRT0 = RenderTexture.GetTemporary( Owner.FurDataProfiles[i].colorMap.width, Owner.FurDataProfiles[i].colorMap.height );
                        Graphics.Blit( Owner.FurDataProfiles[i].colorMap, tempRT0 );
                    }
                    else {
                        tempRT0 = RenderTexture.GetTemporary( 512, 512, 4, RenderTextureFormat.Default );
                        var target = RenderTexture.active;
                        RenderTexture.active = tempRT0;
                        GL.Clear( true, true, ProfileDecals[i].furTint );
                        RenderTexture.active = target;
                    }
                }
                else {
                    if ( Owner.FurDataProfiles[i].emissionMap ) {
                        tempRT0 = RenderTexture.GetTemporary( Owner.FurDataProfiles[i].emissionMap.width, Owner.FurDataProfiles[i].emissionMap.height );
                        Graphics.Blit( Owner.FurDataProfiles[i].emissionMap, tempRT0 );
                    }
                    else {
                        tempRT0 = RenderTexture.GetTemporary( 512, 512 );
                        var target = RenderTexture.active;
                        RenderTexture.active = tempRT0;
                        GL.Clear( true, true, Color.clear );
                        RenderTexture.active = target;
                    }
                }
                
                ProfileDecals[i].finalOutput = RenderTexture.GetTemporary( tempRT0.width, tempRT0.height );
                ProfileDecals[i].finalOutput.name = "XFUR DECAL";

                tempRT1 = RenderTexture.GetTemporary( tempRT0.width, tempRT0.height );
                
                for (int d = 0; d < ProfileDecals[i].decals.Count; d++ ) {
                    _decalsMat.SetTexture( _xfurDecalTex, ProfileDecals[i].decals[d].sourceDecal ? ProfileDecals[i].decals[d].sourceDecal : Texture2D.blackTexture );
                    _decalsMat.SetVector( _xfurDecalOffsetTiling, new Vector4( ProfileDecals[i].decals[d].offset.x, ProfileDecals[i].decals[d].offset.y, ProfileDecals[i].decals[d].tiling.x, ProfileDecals[i].decals[d].tiling.y ) );
                    _decalsMat.SetFloat( _xfurDecalMixMode, (int)ProfileDecals[i].decals[d].mixingMode );
                    _decalsMat.SetColor( _xfurDecalColor, ProfileDecals[i].decals[d].color );
                    Graphics.Blit( tempRT0, ProfileDecals[i].finalOutput, _decalsMat );
                    Graphics.Blit( ProfileDecals[i].finalOutput, tempRT0 );
                }
                
                RenderTexture.ReleaseTemporary( tempRT0 );
                RenderTexture.ReleaseTemporary( tempRT1 );

            }

        }


        public override void MainRenderLoop( MaterialPropertyBlock block, int furProfileIndex ) {
            
            if ( Status == ModuleStatus.CriticalError || !_enabled ) {
                return;
            }

            if ( !_decalsMat && DecalsShader ) {
                _decalsMat = new Material( DecalsShader );
            }

            GenerateDecals();

            if ( ProfileDecals[furProfileIndex].enabled && ProfileDecals[furProfileIndex].finalOutput ) {
                if ( ProfileDecals[furProfileIndex].outputMode == 0 ) {
                    block.SetTexture( XFurShaderProperties.xfurMainColorMap, ProfileDecals[furProfileIndex].finalOutput );
                }
                else {
                    block.SetTexture( XFurShaderProperties.xfurEmissionMap, ProfileDecals[furProfileIndex].finalOutput );
                }
            }

        }


#if UNITY_EDITOR

        private bool[] folds = new bool[0];

        public override void UpdateModule() {

            _internalName = "UV Decals";
            Status = ModuleStatus.Stable;
            _version = TargetVersion;

            if ( Owner.MainRenderer.renderer ) {
                if ( ProfileDecals.Count != Owner.MainRenderer.materials.Length ) {
                    ProfileDecals = new List<PerProfileDecals>();
                    for ( int i = 0; i < Owner.MainRenderer.materials.Length; i++ ) {
                        ProfileDecals.Add( new PerProfileDecals() );
                        ProfileDecals[i].enabled = Owner.MainRenderer.isFurMaterial[i];
                    }
                }
            }

            if ( !DecalsShader ) {
                DecalsShader = Shader.Find( "Hidden/XFur Studio/Modules/Decal Mixing" );

                if ( !DecalsShader ) {
                    Status = ModuleStatus.CriticalError;
                    Debug.LogError( "Critical Error on the Decals Module : The Decals Mixing shader has not been found. Please re-import the asset in order to restore the missing files" );
                }
            }

            if ( DecalsShader ) {
                if ( !_decalsMat ) {
                    _decalsMat = new Material( DecalsShader );
                }
            }

        }

        public override void ModuleUI( SerializedProperty property ) {
            base.ModuleUI( property );
            GUILayout.Space( 16 );

            if ( folds.Length != ProfileDecals.Count ) {
                folds = new bool[ProfileDecals.Count];
            }

            for ( int i = 0; i < ProfileDecals.Count; i++ ) {
                if ( Owner.MainRenderer.isFurMaterial[i] ) {
                    ProfileDecals[i].enabled = EnableDisableToggle( new GUIContent( "Material " + i + " Decals" ), ProfileDecals[i].enabled );
                    GUILayout.Space( 4 );
                }
                else {
                    ProfileDecals[i].enabled = false;
                }
            }

            GUILayout.Space( 16 );

            for ( int i = 0; i < ProfileDecals.Count; i++ ) {
                if ( ProfileDecals[i].enabled ) {

                    GUILayout.Space( 8 );

                    if ( _xfurInstance.FurDataProfiles[i].emissiveFur ) {
                        ProfileDecals[i].outputMode = PopupField( new GUIContent( "Decals Output" ), ProfileDecals[i].outputMode, new string[] { "Diffuse Channel", "Emission Channel" } );
                    }
                    else {
                        ProfileDecals[i].outputMode = 0;
                    }

                    GUILayout.Space( 8 );

                    if ( BeginCenteredGroup("Material "+i+" Decals", ref folds[i] ) ) {
                        GUILayout.Space( 16 );

                        if ( !Owner.FurDataProfiles[i].colorMap ) {
                            ProfileDecals[i].furTint = EditorGUILayout.ColorField( new GUIContent( "Fur Tint Override", "When no fur color map is present, you will need to specify the fur's color in this field and set the actual fur tint to white." ), ProfileDecals[i].furTint );
                            GUILayout.Space( 16 );
                        }

                        for (int d = 0; d < ProfileDecals[i].decals.Count; d++ ) {
                            if ( BeginCenteredGroup("Decal "+d, ref ProfileDecals[i].folds[d] ) ) {
                                GUILayout.Space( 12 );
                                ProfileDecals[i].decals[d].mixingMode = (MixingMode)StandardEnumField( new GUIContent( "Decal Mix Mode", "The way in which the color of this decal will be mixed with the fur's color map" ), ProfileDecals[i].decals[d].mixingMode );
                                ProfileDecals[i].decals[d].sourceDecal = ObjectField<Texture>( new GUIContent( "Decal Texture" ), ProfileDecals[i].decals[d].sourceDecal );
                                ProfileDecals[i].decals[d].color = EditorGUILayout.ColorField( new GUIContent( "Decal Tint" ), ProfileDecals[i].decals[d].color );
                                ProfileDecals[i].decals[d].offset = EditorGUILayout.Vector2Field( new GUIContent( "Decal Offset" ), ProfileDecals[i].decals[d].offset );
                                ProfileDecals[i].decals[d].tiling = EditorGUILayout.Vector2Field( new GUIContent( "Decal Tiling" ), ProfileDecals[i].decals[d].tiling );
                                GUILayout.Space( 16 );

                                if ( CenteredButton("Remove Decal", 128 ) ) {
                                    ProfileDecals[i].decals.RemoveAt( d );
                                    EndCenteredGroup();
                                    break;
                                }
                            }
                            EndCenteredGroup();
                            GUILayout.Space( 4 );
                        }

                        GUILayout.Space( 16 );

                        if ( ProfileDecals[i].decals.Count < 4 ) {
                            if ( CenteredButton( "Add new Decal", 200 ) ) {
                                ProfileDecals[i].decals.Add( new DecalDefinition() );
                            }
                        }
                        GUILayout.Space( 16 );
                    }
                    EndCenteredGroup();
                    GUILayout.Space( 16 );                   
                }
            }

            GUILayout.Space( 16 );


        }

#endif


        public override void Unload() {   
            UnloadResources();
        }


        public override void UnloadResources() {
             
            for (int i = 0; i < ProfileDecals.Count; i++ ) 
               RenderTexture.ReleaseTemporary( ProfileDecals[i].finalOutput );                
            }

        }


    }