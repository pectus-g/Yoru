#if UNITY_EDITOR

namespace XFurStudio.Editor {


    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;
    using XFurStudio.Core;


    [CustomEditor(typeof(XFurStudioStrandsAsset))]
    public class XFurStudioStrandsAsset_Editor : Editor {

        public GUISkin pidiSkin2;
        public Texture2D xfurStudioLogo;

        XFurStudioStrandsAsset strandsAsset;

        bool[] folds = new bool[4];

        RenderTexture previewShape1, previewShape2, previewStrandsMap;

        public void OnEnable() {

            strandsAsset = (XFurStudioStrandsAsset)target;

        }



        public void OnDisable() {
        }


        public override void OnInspectorGUI() {

            GUILayout.BeginHorizontal(); GUILayout.Space( 20 );

            Undo.RecordObject( strandsAsset, strandsAsset.name + "_" + strandsAsset.GetInstanceID() );

            if ( !pidiSkin2 ) {
                if ( AssetDatabase.FindAssets( "l: XFur3UI" ).Length > 0 ) {
                    pidiSkin2 = (GUISkin)AssetDatabase.LoadAssetAtPath( AssetDatabase.GUIDToAssetPath( AssetDatabase.FindAssets( "l: XFurStudio2UI" )[0] ), typeof( GUISkin ) );
                }
            }

            if ( !pidiSkin2 ) {
                GUILayout.Space( 12 );
                EditorGUILayout.HelpBox( "The needed GUISkin for this asset has not been found or is corrupted. Please re-download the asset to try to fix this issue or contact support if it persists", MessageType.Error );
                GUILayout.Space( 12 );
                return;
            }

            pidiSkin2.label = new GUIStyle( EditorStyles.label );

            var lStyle = new GUIStyle( EditorStyles.label );

            GUI.color = EditorGUIUtility.isProSkin ? new Color( 0.1f, 0.1f, 0.15f, 1 ) : new Color( 0.5f, 0.5f, 0.6f );
            GUILayout.BeginVertical();
            GUI.color = Color.white;

            AssetLogoAndVersion();

            GUILayout.Space( 16 );

            strandsAsset.xfurStrandsMethod = (XFurStudioStrandsAsset.XFurStrandsMethod)StandardEnumField( new GUIContent( "Strands Texture Method", "The method that will be used to provide a fur strands texture to the XFur shader" ), strandsAsset.xfurStrandsMethod );

            GUILayout.Space( 16 );

            if ( strandsAsset.xfurStrandsMethod == XFurStudioStrandsAsset.XFurStrandsMethod.ProcedurallyGenerated ) {


                    GUILayout.Space( 24 );

                    CenteredLabel( "First Pass" );

                    GUILayout.Space( 16 );

                    if ( !strandsAsset.strandShapeA.previewShape ) {
                        strandsAsset.strandShapeA.previewShape = new Texture2D( 32, 32 );
                        strandsAsset.strandShapeA.GenerateStrandShape( strandsAsset.strandShapeA.previewShape );
                    }
                    else {
                        strandsAsset.strandShapeA.GenerateStrandShape( strandsAsset.strandShapeA.previewShape );
                    }

                    GUILayout.BeginVertical();
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginHorizontal();
                    GUI.color = EditorGUIUtility.isProSkin ? new Color( 0.1f, 0.1f, 0.15f, 1 ) : new Color( 0.5f, 0.5f, 0.6f );
                    GUILayout.Box( "", EditorStyles.helpBox, GUILayout.Height( 80 ), GUILayout.Width( 80 ) );
                    GUI.color = Color.white;
                    var rect = GUILayoutUtility.GetLastRect();
                    
                    rect.x += 8;
                    rect.y += 8;
                    rect.width = 64;
                    rect.height = 64;

                    GUI.DrawTexture( rect, strandsAsset.strandShapeA.previewShape );
                    GUILayout.Space( 32 );
                    GUILayout.BeginVertical();
                    GUILayout.Space( 8 );
                    strandsAsset.strandShapeA.horizontalSize = EditorGUILayout.Slider( new GUIContent( "Horizontal Size" ), strandsAsset.strandShapeA.horizontalSize, 0, 1 );
                    strandsAsset.strandShapeA.verticalSize = EditorGUILayout.Slider( new GUIContent( "Vertical Size" ), strandsAsset.strandShapeA.verticalSize, 0, 1 );
                    strandsAsset.strandShapeA.gradient = EditorGUILayout.Slider( new GUIContent( "Gradient" ), strandsAsset.strandShapeA.gradient, 0 , 1 );
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.EndVertical();

                    GUILayout.Space( 16 );


                    strandsAsset.firstPassDensity = EditorGUILayout.Slider( new GUIContent( "Strands Density" ), strandsAsset.firstPassDensity, 0, 1 );
                    strandsAsset.firstPassSize = EditorGUILayout.Slider( new GUIContent( "Average Size" ), strandsAsset.firstPassSize, 0, 1 );
                    strandsAsset.firstPassVariation = 1.0f - EditorGUILayout.Slider( new GUIContent( "Size Variation" ), 1.0f - strandsAsset.firstPassVariation, 0, 1 );

                    GUILayout.Space( 24 );

                    CenteredLabel( "Second Pass" );

                    GUILayout.Space( 16 );

                    if ( !strandsAsset.strandShapeB.previewShape ) {
                        strandsAsset.strandShapeB.previewShape = new Texture2D( 32, 32 );
                        strandsAsset.strandShapeB.GenerateStrandShape( strandsAsset.strandShapeB.previewShape );
                    }
                    else {
                        strandsAsset.strandShapeB.GenerateStrandShape( strandsAsset.strandShapeB.previewShape );
                    }

                    GUILayout.BeginVertical();
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginHorizontal();
                    GUI.color = EditorGUIUtility.isProSkin ? new Color( 0.1f, 0.1f, 0.15f, 1 ) : new Color( 0.5f, 0.5f, 0.6f );
                    GUILayout.Box( "", EditorStyles.helpBox, GUILayout.Height( 80 ), GUILayout.Width( 80 ) );
                    GUI.color = Color.white;
                    rect = GUILayoutUtility.GetLastRect();

                    rect.x += 8;
                    rect.y += 8;
                    rect.width = 64;
                    rect.height = 64;

                    GUI.DrawTexture( rect, strandsAsset.strandShapeB.previewShape );
                    GUILayout.Space( 32 );
                    GUILayout.BeginVertical();
                    GUILayout.Space( 8 );
                    strandsAsset.strandShapeB.horizontalSize = EditorGUILayout.Slider( new GUIContent( "Horizontal Size" ), strandsAsset.strandShapeB.horizontalSize, 0, 1 );
                    strandsAsset.strandShapeB.verticalSize = EditorGUILayout.Slider( new GUIContent( "Vertical Size" ), strandsAsset.strandShapeB.verticalSize, 0 , 1 );
                    strandsAsset.strandShapeB.gradient = EditorGUILayout.Slider( new GUIContent( "Gradient" ), strandsAsset.strandShapeB.gradient, 0, 1 );
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.EndVertical();

                    GUILayout.Space( 16 );

                    strandsAsset.secondPassDensity = EditorGUILayout.Slider( new GUIContent( "Strands Density" ), strandsAsset.secondPassDensity, 0, 1 );
                    strandsAsset.secondPassSize = EditorGUILayout.Slider( new GUIContent( "Average Size" ), strandsAsset.secondPassSize, 0, 1 );
                    strandsAsset.secondPassVariation = 1.0f - EditorGUILayout.Slider( new GUIContent( "Size Variation" ), 1.0f - strandsAsset.secondPassVariation, 0, 1 );

                    GUILayout.Space( 16 );

                    CenteredLabel( "Variation Noise Pass" );

                    GUILayout.Space( 16 );

                    strandsAsset.perlinNoiseScale = EditorGUILayout.Slider( new GUIContent("Noise Scale"), strandsAsset.perlinNoiseScale, 4, 32 );

                    GUILayout.Space( 16 );

                    strandsAsset.generateMips = EnableDisableToggle( new GUIContent( "Generate Mip Maps" ), strandsAsset.generateMips );

                    GUILayout.Space( 24 );

                    if ( CenteredButton( "Generate Strands Map", 256 ) ) {
                        strandsAsset.PoissonStrandsGenerator();
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }

                    GUILayout.Space(16);

                    if ( CenteredButton( "Export Strands Map", 256 ) ) {
                        if ( strandsAsset.FurStrandsTexture ) {
                            var pngData = strandsAsset.FurStrandsTexture.EncodeToPNG();
                            var path = EditorUtility.SaveFilePanel( "Export Texture", "Assets/", "_FurStrandsAssetOutput", "png" );

                            if ( pngData != null ) {
                                System.IO.File.WriteAllBytes( path, pngData );
                                AssetDatabase.Refresh();
                            }
                        }
                    }

                    if ( strandsAsset.FurStrandsTexture ) {
                        GUILayout.Space( 16 );
                        GUILayout.Box( "", pidiSkin2.customStyles[1], GUILayout.Height( 250 ) );
                        rect = GUILayoutUtility.GetLastRect();
                        rect.x += rect.width / 2 - 100;
                        rect.y += rect.height / 2 - 100;
                        rect.width = 200;
                        rect.height = 200;

                        GUI.DrawTexture( rect, strandsAsset.FurStrandsTexture );
                        GUILayout.Space( 16 );
                    }



            }
            else {

                strandsAsset.CustomStrandsTexture = ObjectField<Texture2D>( new GUIContent( "Custom Strands Texture" ), strandsAsset.CustomStrandsTexture );

            }


            GUILayout.Space( 16 );

            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();

            lStyle = new GUIStyle(EditorStyles.label);
            lStyle.fontStyle = FontStyle.Italic;
            lStyle.fontSize = 8;

            GUILayout.Label( $"Copyright© 2017-{System.DateTime.Today.Year}", lStyle );

            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();

            GUILayout.Space( 24 );
            GUILayout.EndVertical();
            GUILayout.Space( 20 ); GUILayout.EndHorizontal();
        }


        #region PIDI 2020 EDITOR



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
            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
            var tempBool = GUILayout.Button( label, EditorGUIUtility.isProSkin ? pidiSkin2.button : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button, GUILayout.MaxWidth( width ) );
            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();
            GUILayout.Space( 2 );
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
            GUILayout.Label( strandsAsset.Version, pidiSkin2.customStyles[2] );
            GUILayout.Space( 6 );
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        /// <summary>
        /// Draws a label centered in the Editor window
        /// </summary>
        /// <param name="label"></param>
        public void CenteredLabel( string label ) {

            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
            GUILayout.Label( label, EditorStyles.boldLabel );
            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();

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
            GUILayout.BeginHorizontal(); GUILayout.Space( 18 );
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
        /// Draw a custom toggle that instead of using a check box uses an Enable/Disable drop down menu
        /// </summary>
        /// <param name="label"></param>
        /// <param name="toggleValue"></param>
        /// <returns></returns>
        public bool EnableDisableToggle( GUIContent label, bool toggleValue, bool trueFalseToggle = false, params GUILayoutOption[] options ) {

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
        public System.Enum StandardEnumField( GUIContent label, System.Enum userEnum ) {


            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            var result = EditorGUILayout.EnumPopup( userEnum, EditorGUIUtility.isProSkin ? pidiSkin2.button : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button );
            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );
            return result;
        }



        #endregion





    }




}

#endif

