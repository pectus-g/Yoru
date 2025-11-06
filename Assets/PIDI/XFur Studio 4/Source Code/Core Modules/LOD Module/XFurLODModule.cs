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
    public partial class XFurLODModule : XFurStudioModule {

        protected static readonly int _xfurLODArea = Shader.PropertyToID( "_XFurLODArea" );
        protected static readonly int _xfurLODStrength = Shader.PropertyToID( "_XFurLODStrength" );

        /// <summary>
        /// The different sub-renderers, each one holding a specific LOD renderer
        /// </summary>
        public XFurStudioInstance.XFurMeshRendererData[] lodRenderers = new XFurStudioInstance.XFurMeshRendererData[0];

        /// <summary>
        /// The amount of fur samples used for each sub-render
        /// </summary>
        [SerializeField] protected int[] _furSamples = new int[1];

        /// <summary>
        /// The far distance limit. At this distance, the fur samples will be at their minimum.
        /// </summary>
        public float MaxFurDistance = 150;

        /// <summary>
        /// The near distance limit. At this distance, the fur samples will be at their maximum.
        /// </summary>
        public float MinFurDistance = 2;

        /// <summary>
        /// The range of fur samples between the min. and max. distance.
        /// </summary>
        public Vector2Int FurSamplesRange = new Vector2Int( 4, 32 );

        /// <summary>
        /// Use a basic algorithm to reduce GPU overdraw.
        /// </summary>
        public bool useOverdrawReduction;

        /// <summary>
        /// The area (from the center of the camera) in which overdraw will be reduced
        /// </summary>
        [SerializeField] float _overdrawLODArea = 1;

        /// <summary>
        /// The strength of the reduction effect
        /// </summary>
        [SerializeField] int _overdrawLODStrength = 0;
        private int[] _overdrawLODStrengthValues = new int[] { 128, 32, 16, 8 };


        protected override Vector3Int TargetVersion { get{ return new Vector3Int( 4, 0, 0 ); } }

        private Camera mainCam;

        public bool IsFarLOD { get; private set; }

        public override void Setup( XFurStudioInstance xfurOwner ) {
            
            _internalName = "Dynamic LOD";
            Status = ModuleStatus.Stable;
            

            if ( lodRenderers.Length > 0 ) {
                SyncLodGroup();
            }

            base.Setup( xfurOwner );

        }



        public void SyncLodGroup() {
            var lodGroup = Owner.GetComponent<LODGroup>();

            if (lodRenderers.Length != lodGroup.lodCount ) {
                lodRenderers = new XFurStudioInstance.XFurMeshRendererData[lodGroup.lodCount];
            }

            for ( int i = 0; i < lodRenderers.Length; i++ ) {
                if ( !lodRenderers[i].renderer || lodRenderers[i].renderer != lodGroup.GetLODs()[i].renderers[0] ) {
                    lodRenderers[i].AssignRenderer( lodGroup.GetLODs()[i].renderers[0] );
                }
            }
        }


        public override void Load() {
            if ( _furSamples.Length != _xfurInstance.FurDataProfiles.Length ) {
                _furSamples = new int[_xfurInstance.FurDataProfiles.Length];
            }

            for ( int i = 0; i < _furSamples.Length; i++ ) {
                _furSamples[i] = _xfurInstance.FurDataProfiles[i].renderingSamples;
            }
        }


        public override void MainLoop() {

            var distance = 0.0f;

            if ( _furSamples.Length != _xfurInstance.FurDataProfiles.Length ) {
                _furSamples = new int[_xfurInstance.FurDataProfiles.Length];
            }

            for ( int i = 0; i < _furSamples.Length; i++ ) {
                _furSamples[i] = _xfurInstance.FurDataProfiles[i].renderingSamples;
            }


            if ( Application.isPlaying && mainCam ) {
                distance = Vector3.Distance( _xfurInstance.transform.position, mainCam.transform.position );

                for ( int i = 0; i < _xfurInstance.FurDataProfiles.Length; i++ ) {
                    _xfurInstance.FurDataProfiles[i].renderingSamples = (int)Mathf.Lerp( FurSamplesRange.x, FurSamplesRange.y, 1 - Mathf.Clamp01( (distance - MinFurDistance) / MaxFurDistance ) );
                }
            }
            else {

                mainCam = Camera.current;

                for ( int i = 0; i < _xfurInstance.FurDataProfiles.Length; i++ ) {
                    if ( Application.isPlaying ) {
                        _xfurInstance.FurDataProfiles[i].renderingSamples = _furSamples[i];
                    }
                    else {
                        _furSamples[i] = _xfurInstance.FurDataProfiles[i].renderingSamples;
                    }
                }

            }

            IsFarLOD = distance - MinFurDistance > ( MaxFurDistance - MinFurDistance ) * 0.65f;

            for ( int i = 0; i < lodRenderers.Length; i++ ) {
                if ( lodRenderers[i].renderer && lodRenderers[i].renderer.isVisible ) {
                    _xfurInstance.CurrentFurRenderer = lodRenderers[i];
                    break;
                }
            }


            Owner.skipRenderFrame = distance > MaxFurDistance * 2 && lodRenderers.Length < 1;


        }


        public override void MainRenderLoop( MaterialPropertyBlock block, int furProfileIndex ) {
            
            if ( _enabled && useOverdrawReduction ) {
                block.SetFloat( _xfurLODArea, _overdrawLODArea );
                block.SetFloat( _xfurLODStrength, _overdrawLODStrengthValues[_overdrawLODStrength] );
            }
            else {
                block.SetFloat( _xfurLODArea, 4 );
                block.SetFloat( _xfurLODStrength, 128 );
            }

        }


        public override void Unload() {

        }


        public override void UnloadResources() {

        }


#if UNITY_EDITOR

        public override void UpdateModule() {

            _internalName = "Dynamic LOD";
            Status = ModuleStatus.Stable;
            _version = TargetVersion;

            if ( lodRenderers.Length > 0 && !lodRenderers[0].renderer ) {
                SyncLodGroup();
            }
        }

        public override void ModuleUI( SerializedProperty property ) {

            //UnityEditor.Undo.RecordObject( this, _xfurInstance.name + _xfurInstance.GetInstanceID() + this.name );

            base.ModuleUI( property );

            GUILayout.Space( 16 );

                if ( Owner.GetComponent<LODGroup>() ) {
                    if ( CenteredButton( "Sync with LOD Group", 200 ) ) {
                        SyncLodGroup();
                    }
                    GUILayout.Space( 16 );
                }


            useOverdrawReduction = EnableDisableToggle( new GUIContent( "Overdraw Reduction*", "This feature attempts to reduce overdrawing in the fur by limiting the amount of passes directly in front of the camera, where this reduction is less noticeable. Use carefully as its effectiveness is not fully guaranteed. Avoid using it alongside curly fur" ), useOverdrawReduction );

            if ( useOverdrawReduction ) {
                GUILayout.Space( 16 );

                CenteredLabel( "Overdraw Reduction" );

                _overdrawLODArea = EditorGUILayout.Slider( new GUIContent( "Overdraw Reduction Area" ), _overdrawLODArea, 0.15f, 12 );

                _overdrawLODStrength = PopupField( new GUIContent( "Overdraw Reduction Strength" ), _overdrawLODStrength, new string[] { "None", "Low", "Normal", "High" } );

            }

            GUILayout.Space(16);


            CenteredLabel( "FUR RENDERING DISTANCE" );

            GUILayout.Space( 16 );

            MinFurDistance = FloatField( new GUIContent( "Min. Distance", "At any distance closer than this to the camera, fur and effects will be rendered at the full quality defined by the user" ), MinFurDistance );
            MaxFurDistance = FloatField( new GUIContent( "Max. Distance", "The maximum distance at which fur will still be rendered. After this distance, all fur and its effects will be disabled, while the minimum amount of fur samples will be used as the object approaches this distance from the camera" ), MaxFurDistance );

            GUILayout.Space( 16 );

            CenteredLabel( "FUR SAMPLES SETTINGS" );

            GUILayout.Space( 16 );

            FurSamplesRange.x = EditorGUILayout.IntSlider( new GUIContent( "Min. Fur Samples", "The minimum amount of fur samples to be used with this instance when it is furthest away from the camera" ), FurSamplesRange.x, 4, FurSamplesRange.y );
            FurSamplesRange.y = EditorGUILayout.IntSlider( new GUIContent( "Max. Fur Samples", "The maximum amount of fur samples to be used with this instance when it is closest to the camera" ), FurSamplesRange.y, FurSamplesRange.x + 1, 128 );

            GUILayout.Space( 16 );
           
            
                GUILayout.Space( 16 );
                
                CenteredLabel( "Manually Defined LOD Renderers" );

                for (int i = 0; i < lodRenderers.Length; i++ ) {
                    GUILayout.BeginHorizontal();
                    var tempRenderDataCopy = lodRenderers[i];
                    var tempRenderer = tempRenderDataCopy.renderer;
                    tempRenderer = ObjectField<Renderer>( new GUIContent( "LOD" + i + " Renderer" ), tempRenderer );
                    if ( tempRenderer != tempRenderDataCopy.renderer ) {
                        tempRenderDataCopy.AssignRenderer( tempRenderer );
                    }
                    lodRenderers[i] = tempRenderDataCopy;

                    if ( StandardButton( "X", 24 ) ) {
                        UnityEditor.ArrayUtility.RemoveAt<XFurStudioInstance.XFurMeshRendererData>( ref lodRenderers, i );
                        GUILayout.EndHorizontal();
                        break;
                    }
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space( 16 );

                if ( CenteredButton("Add new LOD Renderer", 200) ) {
                    UnityEditor.ArrayUtility.Add<XFurStudioInstance.XFurMeshRendererData>( ref lodRenderers, new XFurStudioInstance.XFurMeshRendererData() );
                }

                GUILayout.Space( 16 );
            

            GUILayout.Space( 24 );

        }

#endif

    }

}