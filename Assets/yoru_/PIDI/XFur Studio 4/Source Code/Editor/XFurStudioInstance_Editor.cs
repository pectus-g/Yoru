#if UNITY_EDITOR

namespace XFurStudio.Editor {

    using XFurStudio.Core;
    using XFurStudio.Modules;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;
    using XFurStudio.Designer;

    [CanEditMultipleObjects]
    [CustomEditor( typeof( XFurStudioInstance ) )]
    public class XFurStudioInstance_Editor : Editor {


        public GUISkin pidiSkin2;
        public Texture2D xfurStudioLogo;

        private XFurStudioInstance xfur;

        XFurRandomizationModule randomModule;
        XFurLODModule lodModule;
        XFurPhysicsModule physicsModule;
        XFurVFXModule vfxModule;
        XFurDecalsModule decalsModule;
        XFurSimpleBendingModule simpleBendingModule;
        XFurDynamicMaskingModule dynamicMaskingModule;

        //Inspector Data

        [SerializeField] protected int inspectorMode = 0;
        [SerializeField] protected int editFurProfile = 0;
        [SerializeField] protected bool inEditMode;

        [SerializeField] protected XFurStudioDesigner designer;

        public XFurStudioStrandsAsset defaultStrands;


        private void OnEnable() {

            xfur = (XFurStudioInstance)target;

            randomModule = xfur.RandomizationModule;
            lodModule = xfur.LODModule;
            physicsModule = xfur.PhysicsModule;
            vfxModule = xfur.VFXModule;
            decalsModule = xfur.DecalsModule;
            simpleBendingModule = xfur.SimpleBendingModule;
            dynamicMaskingModule = xfur.DynamicMasksModule;

            if ( Application.isPlaying ) {
                return;
            }

            xfur.SetupXFurInstance();




            //if ( !Application.isPlaying )
            //EditorApplication.update += xfur.RenderFur;

        }

        public void OnDisable() {
            //EditorApplication.update -= xfur.RenderFur;
            if ( decalsModule ) {
                decalsModule.Unload();
            }

            if ( simpleBendingModule ) {
                simpleBendingModule.Unload();
            }
        }



        void MainSettings() {

            GUILayout.Space( 12 );

            EditorGUILayout.HelpBox( xfur.RenderingResources.CurrentStatusMessage, xfur.RenderingResources.CurrentStatusMessageType );

            if ( !xfur.MainRenderer.renderer ) {
                if ( xfur.TryGetComponent<Renderer>( out Renderer mainRenderer ) ) {
                    xfur.SetMainRenderer( mainRenderer );


                    for ( int i = 0; i < xfur.FurDataProfiles.Length; i++ ) {
                        if ( !xfur.FurDataProfiles[i].furStrandsAsset ) {
                            xfur.FurDataProfiles[i].furStrandsAsset = defaultStrands;
                        }
                    }

                    serializedObject.Update();
                    return;

                }
                else {

                    var rends = xfur.GetComponentsInChildren<Renderer>();

                    if ( rends.Length > 0 ) {
                        xfur.SetMainRenderer( rends[0] );

                        for ( int i = 0; i < xfur.FurDataProfiles.Length; i++ ) {
                            if ( !xfur.FurDataProfiles[i].furStrandsAsset ) {
                                xfur.FurDataProfiles[i].furStrandsAsset = defaultStrands;
                            }
                        }

                        serializedObject.Update();
                        return;
                    }

                }
            }


            var tempRender = xfur.MainRenderer.renderer;

            GUILayout.Space( 8 );
            tempRender = (Renderer)EditorGUILayout.ObjectField( new GUIContent( "Main Renderer", "The main renderer component that displays the mesh for this XFur instance or the highest LOD (LOD0) in mesh with multiple levels of detail" ), tempRender, typeof( Renderer ), true );

            if ( tempRender != xfur.MainRenderer.renderer ) {
                if ( xfur.MainRenderer.renderer == null || EditorUtility.DisplayDialog( "WARNING", "Changing the main renderer of this XFur Studio Instance may destroy some settings and profiles assigned to it if the new renderer does not have the same amount of materials and a similar configuration. Do you want to continue?", "Continue", "Cancel" ) ) {
                    xfur.SetMainRenderer( tempRender );

                    for ( int i = 0; i < xfur.FurDataProfiles.Length; i++ ) {
                        if ( !xfur.FurDataProfiles[i].furStrandsAsset ) {
                            xfur.FurDataProfiles[i].furStrandsAsset = defaultStrands;
                        }
                    }

                    serializedObject.Update();
                    return;
                }
            }

            if ( !xfur.MainRenderer.originalMesh ) {
                GUILayout.Space( 12 );
                EditorGUILayout.HelpBox( "No mesh has been assigned to this Renderer", MessageType.Error );
                return;
            }
            else if ( !xfur.MainRenderer.originalMesh.isReadable ) {
                GUILayout.Space( 12 );
                EditorGUILayout.HelpBox( "The mesh used for this XFur Studio Instance is not marked as readable. Please enable Read/Write in the mesh import settings.", MessageType.Error );
            }

            GUILayout.Space( 12 );

            CenteredLabel( "XFur Studio Features" );

            GUILayout.Space( 12 );

            EnableDisableToggle( new GUIContent( "GPU Acceleration", "Uses Vertex Buffers to instantiate the fur (fully GPU-based) rather than the more costly CPU-based Bake method.\n\nShould always be enabled unless targeting platforms incompatible with Vertex Buffers (lower than OpenGLES 3.0)" ), serializedObject.FindProperty( "_settings.useVertexBuffer" ) );

#if UNITY_6000_0_OR_NEWER
            if ( ( xfur.RenderingResources.currentRenderingPipeline == XFurRenderingPipeline.UniversalRP || xfur.RenderingResources.currentRenderingPipeline == XFurRenderingPipeline.HDRP ) ) {
                EnableDisableToggle( new GUIContent( "Render Motion Vectors", "Uses Enables a secondary vertex buffer in order to accurately render Motion Vectors. Requires GPU acceleration and Unity 6+" ), serializedObject.FindProperty( "_settings.renderMotionVectors" ) );
                GUILayout.Space( 8 );
            }
#else
            if ( ( xfur.RenderingResources.currentRenderingPipeline == XFurRenderingPipeline.HDRP ) ) {
                EnableDisableToggle( new GUIContent( "Render Motion Vectors", "Uses Enables a secondary vertex buffer in order to accurately render Motion Vectors. Requires GPU acceleration and Unity 6+" ), serializedObject.FindProperty( "_settings.renderMotionVectors" ) );
                GUILayout.Space( 8 );
            }
#endif

            EnableDisableToggle( new GUIContent( "Normalmaps Support", "Enables support for normal maps to be used alongside the fur. In most cases, this is not necessary as it could produce odd visual results" ), serializedObject.FindProperty( "_settings.useNormalmap" ) );
            EnableDisableToggle( new GUIContent( "Vertex-Baked Data Mode", "Switches to the baked vertex data mode. Reads data previously baked through XFur Studio Designer." ), serializedObject.FindProperty( "_settings.useBakedVertexData" ) );
            EnableDisableToggle( new GUIContent( "Custom Modules Support", "Whether custom modules will be in use or not for this XFur Studio Instance" ), serializedObject.FindProperty( "_settings.useCustomModules" ) );


            GUILayout.Space( 12 );

            CenteredLabel( "Rendering Settings" );

            GUILayout.Space( 12 );

            EnableDisableToggle( new GUIContent( "Auto-Update Materials", "Automatically update the fur materials every certain time, allowing runtime changes to length, thickness, textures etc. to be instantly applied" ), serializedObject.FindProperty( "_settings.autoUpdateMaterials" ), true );

            GUILayout.Space( 8 );

            if ( xfur.RenderingResources.currentRenderingPipeline == XFurRenderingPipeline.LegacyRP ) {
                EnableDisableToggle( new GUIContent( "Forward Add Compatibility", "Enables the Forward Add setup (additional pixel lights, point lights support ) to the XFShells method when using Forward Rendering. This disables GPU instancing, making the shader considerably slower. Consider using Deferred rendering or URP instead." ), serializedObject.FindProperty( "_settings.builtinFwdCompatibilityMode" ) );
            }
            else if ( xfur.RenderingResources.currentRenderingPipeline == XFurRenderingPipeline.UniversalRP ) {
                EnableDisableToggle( new GUIContent( "URP Advanced Lighting", "Enables anisotropic-like highlights on the fur for Forward & Forward+ rendering modes" ), serializedObject.FindProperty( "_settings.urpAdvancedLighting" ) );
            }

            if ( xfur.Settings.autoUpdateMaterials && xfur.IsSkinnedMesh ) {
                EditorGUILayout.PropertyField( serializedObject.FindProperty( "_settings.timeBetweenUpdates" ), new GUIContent( "Time Between Updates", "The update frequency of the fur material properties (in seconds). You can disable auto-updates entirely if you do not plan to modify the fur properties at runtime" ) );
                EnableDisableToggle( new GUIContent( "Compensate for Scale", "Automatically some fur parameters to be scale-relative. The compensation is an approximation and may be slightly incorrect" ), serializedObject.FindProperty( "_settings.autoCompensateForScale" ), true );
            }
            else {
                serializedObject.FindProperty( "_settings.autoCompensateForScale" ).boolValue = false;
            }

            EnableDisableToggle( new GUIContent( "Use Lossy Scale", "Applies internal adjustments to the scale of the mesh that may solve some issues with scaling present in certain third party meshes (p. e.g. scales of 100,100,100 are the default scale exported by some 3D softwares)" ), serializedObject.FindProperty( "_settings.useLossyScale" ), true );

            GUILayout.Space( 8 );



            GUILayout.Space( 8 );



            GUILayout.Space( 16 );

            GUILayout.BeginHorizontal( EditorStyles.helpBox );
            GUILayout.Space( 16 );
            GUILayout.BeginVertical();

            GUILayout.Space( 16 );

            CenteredLabel( "Per-Material Fur Status" );

            GUILayout.Space( 16 );


            for ( int i = 0; i < xfur.MainRenderer.isFurMaterial.Length; i++ ) {
                xfur.MainRenderer.isFurMaterial[i] = EnableDisableToggle( new GUIContent( $"Material {i} : {( xfur.MainRenderer.materials[i] ? xfur.MainRenderer.materials[i].name : "NULL" )}" ), xfur.MainRenderer.isFurMaterial[i] );
            }


            //xfur.MainRenderer.isFurMaterial[0] = EnableDisableToggle( new GUIContent( "Is enabled" ), xfur.MainRenderer.isFurMaterial[0] );


            GUILayout.Space( 16 );
            GUILayout.EndVertical();
            GUILayout.Space( 16 );
            GUILayout.EndHorizontal();

        }



        void ModuleSettings() {

            Undo.RecordObject( xfur, "XFurInstance_" + GetInstanceID() );

            GUILayout.Space( 12 );

            GUILayout.BeginHorizontal( EditorStyles.helpBox );
            GUILayout.Space( 16 );
            GUILayout.BeginVertical();
            GUILayout.Space( 16 );
            CenteredLabel( "Built-In Modules" );
            GUILayout.Space( 16 );


            XFurModuleStatus( randomModule );

            GUILayout.Space( 4 );

            XFurModuleStatus( lodModule );

            GUILayout.Space( 4 );

            XFurModuleStatus( physicsModule );

            GUILayout.Space( 4 );

            XFurModuleStatus( vfxModule );

            GUILayout.Space( 4 );

            XFurModuleStatus( decalsModule );

            GUILayout.Space( 4 );


            XFurModuleStatus( simpleBendingModule );


            GUILayout.Space( 4 );


            XFurModuleStatus( dynamicMaskingModule );


            GUILayout.Space( 16 );

            GUILayout.EndVertical();
            GUILayout.Space( 16 );
            GUILayout.EndHorizontal();


            GUILayout.Space( 16 );

            if ( randomModule.IsEnabled ) {
                if ( BeginCenteredGroup( "Randomization", ref xfur.folds[2] ) ) {
                    randomModule.ModuleUI( serializedObject.FindProperty( "_randomizationModule" ) );
                }
                EndCenteredGroup();
            }

            if ( lodModule.IsEnabled ) {
                if ( BeginCenteredGroup( "Dynamic LOD", ref xfur.folds[3] ) ) {
                    lodModule.ModuleUI( serializedObject.FindProperty( "_lodModule" ) );
                }
                EndCenteredGroup();
            }

            if ( physicsModule.IsEnabled ) {
                if ( BeginCenteredGroup( "Physics", ref xfur.folds[4] ) ) {
                    physicsModule.ModuleUI( serializedObject.FindProperty( "_physicsModule" ) );
                }
                EndCenteredGroup();
            }

            if ( vfxModule.IsEnabled ) {
                if ( BeginCenteredGroup( "VFX & Weather", ref xfur.folds[5] ) ) {
                    vfxModule.ModuleUI( serializedObject.FindProperty( "_vfxModule" ) );
                }
                EndCenteredGroup();
            }

            if ( decalsModule.IsEnabled ) {
                if ( BeginCenteredGroup( "UV Based Decals", ref xfur.folds[6] ) ) {
                    decalsModule.ModuleUI( serializedObject.FindProperty( "_decalsModule" ) );
                }
                EndCenteredGroup();
            }

            if ( simpleBendingModule.IsEnabled ) {
                if ( BeginCenteredGroup( "Simple Bending", ref xfur.folds[7] ) ) {
                    simpleBendingModule.ModuleUI( serializedObject.FindProperty( "_simpleTouchBendingModule" ) );
                }
                EndCenteredGroup();
            }

            if ( dynamicMaskingModule.IsEnabled ) {
                if ( BeginCenteredGroup( "Dynamic Masks", ref xfur.folds[8] ) ) {
                    dynamicMaskingModule.ModuleUI( serializedObject.FindProperty( "_dynamicMaskingModule" ) );
                }
                EndCenteredGroup();
            }


        }


        bool[] cModulesFolds = new bool[32];

        void CustomModuleSettings() {

            Undo.RecordObject( xfur, "XFurInstance_" + GetInstanceID() );

            GUILayout.Space( 16 );

            EditorGUILayout.HelpBox( "\nCustom Module settings are stored in the Custom Module Asset and are not tied to each XFur Studio Instance. Modifications made on this XFur Studio Instance will be replicated on all instances sharing the same Custom Module Asset.\n\nThe Custom Module Assets become tied to each instance in Play Mode only, in a similar way to Runtime Animator Controllers.\n", MessageType.Warning );

            GUILayout.Space( 16 );

            GUILayout.BeginHorizontal( EditorStyles.helpBox );
            GUILayout.Space( 16 );
            GUILayout.BeginVertical();
            GUILayout.Space( 16 );
            CenteredLabel( "Custom Modules" );
            GUILayout.Space( 16 );

            EditorGUILayout.PropertyField( serializedObject.FindProperty( "_customModules" ) );

            GUILayout.Space( 16 );

            GUILayout.EndVertical();
            GUILayout.Space( 16 );
            GUILayout.EndHorizontal();




            if ( cModulesFolds.Length < xfur.CustomModules.Length ) {
                cModulesFolds = new bool[xfur.CustomModules.Length];
            }

            for ( int i = 0; i < xfur.CustomModules.Length; i++ ) {
                if ( xfur.CustomModules[i] ) {
                    if ( BeginCenteredGroup( xfur.CustomModules[i].Module.Name, ref cModulesFolds[i] ) ) {

                        GUILayout.Space( 12 );
                        xfur.CustomModules[i].Module.Setup( xfur );
                        xfur.CustomModules[i].ModuleUI();

                        GUILayout.Space( 12 );

                    }
                    EndCenteredGroup();
                }
            }

            GUILayout.Space( 16 );


        }



        void XFurDesigner() {

            GUILayout.Space( 12 );

            string[] profNames = new string[xfur.FurDataProfiles.Length];

            for ( int i = 0; i < profNames.Length; i++ ) {
                if ( !xfur.MainRenderer.materials[i] ) {
                    var rend = xfur.MainRenderer;
                    rend.materials[i] = rend.renderer.sharedMaterials[i];
                    xfur.MainRenderer = rend;
                }
                profNames[i] = xfur.MainRenderer.materials[i].name;
            }



            GUILayout.Space( 16 );

            if ( xfur.MainRenderer.furProfiles != null && xfur.MainRenderer.furProfiles.Length > 0 ) {

                xfur.editFurProfile = EditorGUILayout.Popup( "Active Fur Material", xfur.editFurProfile, profNames );

                xfur.editFurProfile = Mathf.Clamp( xfur.editFurProfile, 0, xfur.FurDataProfiles.Length - 1 );

                GUILayout.Space( 8 );

                if ( !designer && xfur.MainRenderer.isFurMaterial[xfur.editFurProfile] ) {
                    if ( CenteredButton( "Enter Edit Mode", 256 ) ) {
                        inEditMode = !inEditMode;

                        if ( inEditMode ) {
                            designer = CreateInstance<XFurStudioDesigner>();
                            designer.xfur = xfur;
                            designer.editFurProfile = xfur.editFurProfile;
                            designer.titleContent = new GUIContent( "XFur Studio Designer" );
                            designer.Show();
                        }

                    }
                }
                else {
                    GUILayout.Space( 8 );

                    EditorGUILayout.HelpBox( "In order to save any changes done to the fur (especially any styling) you MUST click the Export/Load button in XFur Designer and export the profile data. This will generate the actual texture maps that will be imported into Unity to store these changes.", MessageType.Warning );

                    GUILayout.Space( 8 );
                }
            }
            else {
                EditorGUILayout.HelpBox( "There are no fur enabled materials on this XFur Studio Instance", MessageType.Warning );
            }

        }



        void XFurInspector() {

            GUILayout.BeginHorizontal();
            GUILayout.Space( 20 );
            GUILayout.BeginVertical();

            AssetLogoAndVersion();

            var lStyle = new GUIStyle();

            GUILayout.Space( 8 );

            if ( serializedObject.isEditingMultipleObjects ) {

                HelpBox( "XFur Studio depends on per-instance behavior and data sets. Editing multiple instances is not allowed. If you need to share properties across multiple instances, use XFur Templates or Unity Prefabs instead", MessageType.Warning );

                GUILayout.Space( 24 );

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                lStyle = new GUIStyle( EditorStyles.label );
                lStyle.fontStyle = FontStyle.Italic;
                lStyle.fontSize = 8;

                GUILayout.Label( $"Copyright© 2017-{System.DateTime.Today.Year},   Jorge Pinal N.", lStyle );

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space( 24 );
                GUILayout.EndVertical();
                GUILayout.Space( 20 );
                GUILayout.EndHorizontal();
                return;
            }

            GUILayout.BeginHorizontal();


            if ( GUILayout.Button( "Settings", inspectorMode == 0 ? pidiSkin2.customStyles[6] : pidiSkin2.customStyles[5] ) ) {
                inspectorMode = inspectorMode == 0 ? -1 : 0;
            }

            if ( GUILayout.Button( "Built-in Modules", inspectorMode == 1 ? pidiSkin2.customStyles[6] : pidiSkin2.customStyles[5] ) ) {
                inspectorMode = inspectorMode == 1 ? -1 : 1;
            }

            if ( xfur.Settings.useCustomModules ) {
                if ( GUILayout.Button( "Custom Modules", inspectorMode == 2 ? pidiSkin2.customStyles[6] : pidiSkin2.customStyles[5] ) ) {
                    inspectorMode = inspectorMode == 2 ? -1 : 2;
                }
            }

            if ( GUILayout.Button( "Fur Designer", inspectorMode == 3 ? pidiSkin2.customStyles[6] : pidiSkin2.customStyles[5] ) ) {
                inspectorMode = inspectorMode == 3 ? -1 : 3;
            }

            GUILayout.EndHorizontal();



            switch ( inspectorMode ) {

                case 0:
                    MainSettings();
                    break;

                case 1:
                    ModuleSettings();
                    break;

                case 2:
                    CustomModuleSettings();
                    break;

                case 3:
                    XFurDesigner();
                    break;

            }

            //serializedObject.ApplyModifiedProperties();

            lStyle = new GUIStyle( EditorStyles.label );
            lStyle.fontStyle = FontStyle.Italic;
            lStyle.fontSize = 8;

            GUILayout.Space( 24 );

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUILayout.Label( $"Copyright© 2017-{System.DateTime.Today.Year},   Jorge Pinal N.", lStyle );

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space( 24 );
            GUILayout.EndVertical();
            GUILayout.Space( 20 );
            GUILayout.EndHorizontal();

        }



        void Separator( string label ) {


            GUILayout.BeginHorizontal( pidiSkin2.box );
            GUILayout.FlexibleSpace();

            GUILayout.Label( label );

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();


        }



        public override void OnInspectorGUI() {

            //SceneView.RepaintAll();


            //Repaint();

            serializedObject.Update();

            XFurInspector();

            if ( serializedObject.hasModifiedProperties ) {
                serializedObject.ApplyModifiedProperties();
            }

        }








        #region PIDI 2020 EDITOR

        public void XFurModuleStatus( XFurStudioModule module ) {
            GUILayout.BeginHorizontal();
            GUILayout.Label( module.Name + ", v" + module.Version, pidiSkin2.label, GUILayout.Width( 140 ) );
            GUILayout.Space( 32 );
            GUILayout.Label( " Status : " + module.Status.ToString(), pidiSkin2.label );
            GUILayout.FlexibleSpace();
            var t = EnableDisableToggle( null, module.IsEnabled, false, GUILayout.MaxWidth( EditorGUIUtility.currentViewWidth - EditorGUIUtility.labelWidth - 200 ) ) && module.Status != XFurStudioModule.ModuleStatus.CriticalError;
            if ( t ) {
                module.Enable();
            }
            else {
                module.Disable();
            }
            GUILayout.EndHorizontal();
        }


        public void HelpBox( string message, MessageType messageType ) {
            EditorGUILayout.HelpBox( message, messageType );
        }




        /// <summary>
        /// Draws a standard object field in the PIDI 2020 style
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="label"></param>
        /// <param name="inputObject"></param>
        /// <param name="allowSceneObjects"></param>
        /// <returns></returns>
        public T ObjectField<T>( GUIContent label, T inputObject, bool allowSceneObjects = true ) where T : UnityEngine.Object {

            GUILayout.Space( 4 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            inputObject = (T)EditorGUILayout.ObjectField( inputObject, typeof( T ), allowSceneObjects );
            GUILayout.EndHorizontal();
            GUILayout.Space( 4 );
            return inputObject;
        }


        /// <summary>
        /// Draws a centered button in the standard PIDI 2020 editor style
        /// </summary>
        /// <param name="label"></param>
        /// <param name="width"></param>
        /// <returns></returns>
        public bool CenteredButton( string label, float width ) {
            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var tempBool = GUILayout.Button( label, EditorGUIUtility.isProSkin ? pidiSkin2.button : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button, GUILayout.MaxWidth( width ) );
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );
            return tempBool;
        }

        /// <summary>
        /// Draws a button in the standard PIDI 2020 editor style
        /// </summary>
        /// <param name="label"></param>
        /// <param name="width"></param>
        /// <returns></returns>
        public bool StandardButton( string label, float width ) {
            var tempBool = GUILayout.Button( label, EditorGUIUtility.isProSkin ? pidiSkin2.button : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button, GUILayout.MaxWidth( width ) );
            return tempBool;
        }


        /// <summary>
        /// Draws the asset's logo and its current version
        /// </summary>
        public void AssetLogoAndVersion() {

            GUILayout.BeginVertical( xfurStudioLogo, pidiSkin2 ? pidiSkin2.customStyles[1] : null );
            GUILayout.Space( 45 );
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label( $"v{xfur.Settings.version.x}.{xfur.Settings.version.y}.{xfur.Settings.version.z}", pidiSkin2.customStyles[2] );
            GUILayout.Space( 6 );
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        /// <summary>
        /// Draws a label centered in the Editor window
        /// </summary>
        /// <param name="label"></param>
        public void CenteredLabel( string label ) {

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label( label, EditorStyles.boldLabel );
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

        }

        /// <summary>
        /// Begins a custom centered group similar to a foldout that can be expanded with a button
        /// </summary>
        /// <param name="label"></param>
        /// <param name="groupFoldState"></param>
        /// <returns></returns>
        public bool BeginCenteredGroup( string label, ref bool groupFoldState ) {

            if ( GUILayout.Button( label, EditorGUIUtility.isProSkin ? pidiSkin2.button : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button ) ) {
                groupFoldState = !groupFoldState;
            }
            GUILayout.BeginHorizontal();
            GUILayout.Space( 18 );
            GUILayout.BeginVertical();
            return groupFoldState;
        }


        /// <summary>
        /// Finishes a centered group
        /// </summary>
        public void EndCenteredGroup() {
            GUILayout.EndVertical();
            GUILayout.Space( 18 );
            GUILayout.EndHorizontal();
        }



        /// <summary>
        /// Custom integer field following the PIDI 2020 editor skin
        /// </summary>
        /// <param name="label"></param>
        /// <param name="currentValue"></param>
        /// <returns></returns>
        public int IntField( GUIContent label, int currentValue ) {

            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            currentValue = EditorGUILayout.IntField( currentValue, EditorGUIUtility.isProSkin ? pidiSkin2.customStyles[4] : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).textField );
            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );

            return currentValue;
        }

        /// <summary>
        /// Custom float field following the PIDI 2020 editor skin
        /// </summary>
        /// <param name="label"></param>
        /// <param name="currentValue"></param>
        /// <returns></returns>
        public float FloatField( GUIContent label, float currentValue ) {

            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            currentValue = EditorGUILayout.FloatField( currentValue );
            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );

            return currentValue;
        }


        /// <summary>
        /// Custom text field following the PIDI 2020 editor skin
        /// </summary>
        /// <param name="label"></param>
        /// <param name="currentValue"></param>
        /// <returns></returns>
        public string TextField( GUIContent label, string currentValue ) {

            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            currentValue = EditorGUILayout.TextField( currentValue, EditorGUIUtility.isProSkin ? pidiSkin2.customStyles[4] : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).textField );
            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );

            return currentValue;
        }


        public Vector2 Vector2Field( GUIContent label, Vector2 currentValue ) {

            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            currentValue.x = EditorGUILayout.FloatField( currentValue.x, EditorGUIUtility.isProSkin ? pidiSkin2.customStyles[4] : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).textField );
            GUILayout.Space( 8 );
            currentValue.y = EditorGUILayout.FloatField( currentValue.y, EditorGUIUtility.isProSkin ? pidiSkin2.customStyles[4] : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).textField );
            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );

            return currentValue;

        }

        public Vector3 Vector3Field( GUIContent label, Vector3 currentValue ) {

            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            currentValue.x = EditorGUILayout.FloatField( currentValue.x, EditorGUIUtility.isProSkin ? pidiSkin2.customStyles[4] : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).textField );
            GUILayout.Space( 8 );
            currentValue.y = EditorGUILayout.FloatField( currentValue.y, EditorGUIUtility.isProSkin ? pidiSkin2.customStyles[4] : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).textField );
            GUILayout.Space( 8 );
            currentValue.z = EditorGUILayout.FloatField( currentValue.z, EditorGUIUtility.isProSkin ? pidiSkin2.customStyles[4] : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).textField );
            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );

            return currentValue;

        }


        public Vector4 Vector4Field( GUIContent label, Vector4 currentValue ) {

            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            currentValue.x = EditorGUILayout.FloatField( currentValue.x, EditorGUIUtility.isProSkin ? pidiSkin2.customStyles[4] : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).textField );
            GUILayout.Space( 8 );
            currentValue.y = EditorGUILayout.FloatField( currentValue.y, EditorGUIUtility.isProSkin ? pidiSkin2.customStyles[4] : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).textField );
            GUILayout.Space( 8 );
            currentValue.z = EditorGUILayout.FloatField( currentValue.z, EditorGUIUtility.isProSkin ? pidiSkin2.customStyles[4] : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).textField );
            GUILayout.Space( 8 );
            currentValue.w = EditorGUILayout.FloatField( currentValue.w, EditorGUIUtility.isProSkin ? pidiSkin2.customStyles[4] : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).textField );
            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );

            return currentValue;

        }


        /// <summary>
        /// Draw a custom popup field in the PIDI 2020 style
        /// </summary>
        /// <param name="label"></param>
        /// <param name="toggleValue"></param>
        /// <returns></returns>
        public int PopupField( GUIContent label, int selected, string[] options ) {


            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            selected = EditorGUILayout.Popup( selected, options, EditorGUIUtility.isProSkin ? pidiSkin2.button : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button );
            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );
            return selected;
        }





        /// <summary>
        /// Draw a custom toggle that instead of using a check box uses an Enable/Disable drop down menu
        /// </summary>
        /// <param name="label"></param>
        /// <param name="toggleValue"></param>
        /// <returns></returns>
        protected void EnableDisableToggle( GUIContent label, SerializedProperty property, bool trueFalseToggle = false, params GUILayoutOption[] options ) {

            int option = property.boolValue ? 1 : 0;

            GUILayout.Space( 4 );

            if ( label != null ) {

                if ( trueFalseToggle ) {
                    option = EditorGUILayout.Popup( label, option, new GUIContent[] { new GUIContent( "False" ), new GUIContent( "True" ) }, EditorGUIUtility.isProSkin ? ( option == 0 ? pidiSkin2.customStyles[5] : pidiSkin2.customStyles[6] ) : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button );
                }
                else {
                    option = EditorGUILayout.Popup( label, option, new GUIContent[] { new GUIContent( "Disabled" ), new GUIContent( "Enabled" ) }, EditorGUIUtility.isProSkin ? ( option == 0 ? pidiSkin2.customStyles[5] : pidiSkin2.customStyles[6] ) : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button );
                }
            }
            else {
                if ( trueFalseToggle ) {
                    option = EditorGUILayout.Popup( option, new string[] { "False", "True" }, EditorGUIUtility.isProSkin ? ( option == 0 ? pidiSkin2.customStyles[5] : pidiSkin2.customStyles[6] ) : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button, options );
                }
                else {
                    option = EditorGUILayout.Popup( option, new string[] { "Disabled", "Enabled" }, EditorGUIUtility.isProSkin ? ( option == 0 ? pidiSkin2.customStyles[5] : pidiSkin2.customStyles[6] ) : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button, options );
                }
            }

            property.boolValue = option == 1;

        }



        /// <summary>
        /// Draw a custom toggle that instead of using a check box uses an Enable/Disable drop down menu
        /// </summary>
        /// <param name="label"></param>
        /// <param name="toggleValue"></param>
        /// <returns></returns>
        protected bool EnableDisableToggle( GUIContent label, bool toggleValue, bool trueFalseToggle = false, params GUILayoutOption[] options ) {

            int option = toggleValue ? 1 : 0;

            GUILayout.Space( 4 );

            if ( label != null ) {

                if ( trueFalseToggle ) {
                    option = EditorGUILayout.Popup( label, option, new GUIContent[] { new GUIContent( "False" ), new GUIContent( "True" ) }, EditorGUIUtility.isProSkin ? ( option == 0 ? pidiSkin2.customStyles[5] : pidiSkin2.customStyles[6] ) : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button );
                }
                else {
                    option = EditorGUILayout.Popup( label, option, new GUIContent[] { new GUIContent( "Disabled" ), new GUIContent( "Enabled" ) }, EditorGUIUtility.isProSkin ? ( option == 0 ? pidiSkin2.customStyles[5] : pidiSkin2.customStyles[6] ) : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button );
                }
            }
            else {
                if ( trueFalseToggle ) {
                    option = EditorGUILayout.Popup( option, new string[] { "False", "True" }, EditorGUIUtility.isProSkin ? ( option == 0 ? pidiSkin2.customStyles[5] : pidiSkin2.customStyles[6] ) : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button, options );
                }
                else {
                    option = EditorGUILayout.Popup( option, new string[] { "Disabled", "Enabled" }, EditorGUIUtility.isProSkin ? ( option == 0 ? pidiSkin2.customStyles[5] : pidiSkin2.customStyles[6] ) : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button, options );
                }
            }

            return option == 1;

        }




        /// <summary>
        /// Draw an enum field but changing the labels and names of the enum to Upper Case fields
        /// </summary>
        /// <param name="label"></param>
        /// <param name="userEnum"></param>
        /// <returns></returns>
        public int StandardEnumField( GUIContent label, System.Enum userEnum ) {

            var names = System.Enum.GetNames( userEnum.GetType() );

            for ( int i = 0; i < names.Length; i++ ) {
                names[i] = names[i].ToUpper();
            }

            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            var result = EditorGUILayout.Popup( System.Convert.ToInt32( userEnum ), names, EditorGUIUtility.isProSkin ? pidiSkin2.button : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button );
            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );
            return result;
        }


        /// <summary>
        /// Draw a layer mask field in the PIDI 2020 style
        /// </summary>
        /// <param name="label"></param>
        /// <param name="selected"></param>
        public LayerMask LayerMaskField( GUIContent label, LayerMask selected ) {

            List<string> layers = null;
            string[] layerNames = null;

            if ( layers == null ) {
                layers = new List<string>();
                layerNames = new string[4];
            }
            else {
                layers.Clear();
            }

            int emptyLayers = 0;
            for ( int i = 0; i < 32; i++ ) {
                string layerName = LayerMask.LayerToName( i );

                if ( layerName != "" ) {

                    for ( ; emptyLayers > 0; emptyLayers-- )
                        layers.Add( "Layer " + ( i - emptyLayers ) );
                    layers.Add( layerName );
                }
                else {
                    emptyLayers++;
                }
            }

            if ( layerNames.Length != layers.Count ) {
                layerNames = new string[layers.Count];
            }
            for ( int i = 0; i < layerNames.Length; i++ )
                layerNames[i] = layers[i];


            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );

            selected.value = EditorGUILayout.MaskField( selected.value, layerNames, EditorGUIUtility.isProSkin ? pidiSkin2.button : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button );

            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );
            return selected;
        }



        #endregion




    }

}

#endif