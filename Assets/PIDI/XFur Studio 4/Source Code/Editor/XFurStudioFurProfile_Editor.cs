namespace XFurStudio.Editor {

    using XFurStudio.Core;
    using XFurStudio.Modules;
    using UnityEngine;
    using UnityEditor;
    using System.Collections.Generic;

    [CustomEditor( typeof( FurProfileAsset ) )]
    public class FurProfileAsset_Editor : Editor {


        public GUISkin pidiSkin2;
        public Texture2D xfurStudioLogo;

        FurProfileAsset profile;


        private void OnEnable() {
            profile = (FurProfileAsset)target;
        }

        public override void OnInspectorGUI() {


            if ( !xfurStudioLogo ) {
                if ( AssetDatabase.FindAssets( "l: XFurStudio4Logo" ).Length > 0 ) {
                    xfurStudioLogo = (Texture2D)AssetDatabase.LoadAssetAtPath( AssetDatabase.GUIDToAssetPath( AssetDatabase.FindAssets( "l: XFurStudio4Logo" )[0] ), typeof( Texture2D ) );
                }
            }

            if ( !pidiSkin2 ) {
                if ( AssetDatabase.FindAssets( "l: XFurUI" ).Length > 0 ) {
                    pidiSkin2 = (GUISkin)AssetDatabase.LoadAssetAtPath( AssetDatabase.GUIDToAssetPath( AssetDatabase.FindAssets( "l: XFurUI" )[0] ), typeof( GUISkin ) );
                }
            }


            if ( !pidiSkin2 ) {
                GUILayout.Space( 12 );
                EditorGUILayout.HelpBox( "The needed GUISkin for this asset has not been found or is corrupted. Please re-download the asset to try to fix this issue or contact support if it persists", MessageType.Error );
                GUILayout.Space( 12 );
                return;
            }

            Undo.RecordObject( profile, "FurProfileAsset_" + GetInstanceID() );

            pidiSkin2.label = new GUIStyle( EditorStyles.label );

            var lStyle = new GUIStyle( EditorStyles.label );


            AssetLogoAndVersion();


            GUILayout.BeginHorizontal(); GUILayout.Space( 24 );
            GUILayout.BeginVertical();


            if ( serializedObject.isEditingMultipleObjects ) {

                EditorGUILayout.HelpBox( "XFur Studio does not support multi-object editing", MessageType.Warning );

                GUILayout.Space( 16 );

                GUILayout.Space( 16 );

                GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();


                lStyle.fontStyle = FontStyle.Italic;
                lStyle.alignment = TextAnchor.MiddleCenter;
                lStyle.normal.textColor = Color.white;
                lStyle.fontSize = 8;

                GUILayout.Label( $"Copyright© 2017-{System.DateTime.Today.Year}", lStyle );

                GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();

                GUILayout.Space( 24 );
                GUILayout.EndVertical();
                GUILayout.Space( 12 ); GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                return;
            }

            CenteredLabel( "Common Properties" );

            GUILayout.Space( 16 );


            profile.FurProfileData.colorMap = ObjectField<Texture>( new GUIContent( "Fur Color Map", "The texture that controls the color / albedo applied over the whole fur surface" ), profile.FurProfileData.colorMap );

            profile.FurProfileData.mainTint = EditorGUILayout.ColorField( new GUIContent( "Fur Main Tint", "The main tint to be applied to the fur" ), profile.FurProfileData.mainTint );

            GUILayout.Space( 8 );
            profile.FurProfileData.normalMap = ObjectField<Texture>( new GUIContent( "Normalmap", "The normalmap for the surface" ), profile.FurProfileData.normalMap );

            if ( profile.FurProfileData.baseUVTiling == Vector4.zero ) {
                profile.FurProfileData.baseUVTiling = new Vector4( 1, 1, 0, 0 );
            }

            GUILayout.Space( 8 );

            Vector2 tiling = EditorGUILayout.Vector2Field( new GUIContent( "Color / Normals Tiling" ), new Vector2( profile.FurProfileData.baseUVTiling.x, profile.FurProfileData.baseUVTiling.y ) );
            Vector2 offset = EditorGUILayout.Vector2Field( new GUIContent( "Color / Normals Offset" ), new Vector2( profile.FurProfileData.baseUVTiling.z, profile.FurProfileData.baseUVTiling.w ) );


            profile.FurProfileData.baseUVTiling = new Vector4( tiling.x, tiling.y, offset.x, offset.y );

            GUILayout.Space( 8 );

            profile.FurProfileData.furDataMap = ObjectField( new GUIContent( "Fur Data Map", "The texture that controls the parameters of the fur :\n\n R = fur mask\n G = length\n B = occlusion\n A = thickness" ), profile.FurProfileData.furDataMap );
            profile.FurProfileData.furGroomingMap = ObjectField( new GUIContent( "Fur Grooming Map", "The texture that controls the direction of the fur :\n\n RGB = absolute fur direction half-normalized in tangent space" ), profile.FurProfileData.furGroomingMap );

            if ( profile.FurProfileData.furGroomingMap )
                profile.FurProfileData.groomStrength = EditorGUILayout.Slider( new GUIContent( "Fur Grooming Strength" ), profile.FurProfileData.groomStrength, 0, 1f );

            GUILayout.Space( 12 );

            profile.FurProfileData.furLength = EditorGUILayout.Slider( new GUIContent( "Fur Length", "The maximum overall length of the fur. This will be multiplied by the actual fur profile length and the length painted in XFur Studio™ - Designer" ), profile.FurProfileData.furLength, 0.01f, 1 );

            GUILayout.Space( 8 );
            profile.FurProfileData.furThickness = EditorGUILayout.Slider( new GUIContent( "Fur Thickness", "The maximum overall thickness of the fur. This will be multiplied by the actual fur profile thickness and the thickness painted in XFur Studio™ - Designer" ), profile.FurProfileData.furThickness, 0.01f, 1 );
            profile.FurProfileData.furThicknessCurve = EditorGUILayout.Slider( new GUIContent( "Thickness Curve", "How the fur strands' thickness bias will change from the root to the top of each strand" ), profile.FurProfileData.furThicknessCurve, 0, 1 );

            GUILayout.Space( 12 );

            profile.FurProfileData.selfOcclusionTint = EditorGUILayout.ColorField( new GUIContent( "Occlusion Tint" ), profile.FurProfileData.selfOcclusionTint );

            profile.FurProfileData.selfOcclusionStrength = EditorGUILayout.Slider( new GUIContent( "Fur Occlusion / Shadowing", "The shadowing applied over the surface of the fur strands as a simple occlusion pass. Multiplied by the per-profile occlusion value and the one painted through XFur Studio™ - Designer" ), profile.FurProfileData.selfOcclusionStrength, 0, 1 );
            profile.FurProfileData.selfOcclusionCurve = EditorGUILayout.Slider( new GUIContent( "Fur Occlusion Curve", "How the shadowing / occlusion of the fur will go from the root to the tip of each strand" ), profile.FurProfileData.selfOcclusionCurve, 0, 1 );

            GUILayout.Space( 8 );

            profile.FurProfileData.roughness = EditorGUILayout.Slider( new GUIContent( "Roughness" ), profile.FurProfileData.roughness, 0, 1 );

            profile.FurProfileData.specularTint = EditorGUILayout.ColorField( new GUIContent( "Specular Tint" ), profile.FurProfileData.specularTint, true, false, false );
            

            GUILayout.Space( 12 );

            CenteredLabel( "Color Variation" );

            GUILayout.Space( 12 );

            profile.FurProfileData.useLegacyColorVariation = EnableDisableToggle( new GUIContent( "Legacy Color Variation" ), profile.FurProfileData.useLegacyColorVariation || profile.FurProfileData.legacyColorVariationMap );

            GUILayout.Space( 12 );

            if ( !profile.FurProfileData.useLegacyColorVariation ) {
                profile.FurProfileData.noiseShadingTint2 = EditorGUILayout.ColorField( new GUIContent( "Strands (R) Color", "Tint to be applied to the main fur strands" ), profile.FurProfileData.noiseShadingTint2 );
                profile.FurProfileData.mainFurStrandBoost = EditorGUILayout.Slider( new GUIContent( "Strands (R) Boost", "Boost to be applied to the main fur strands. Values higher than 1 make it lighter, while values lower than 1 make it darker" ), profile.FurProfileData.mainFurStrandBoost, 0, 2 );

                GUILayout.Space( 12 );

                profile.FurProfileData.noiseShadingTint3 = EditorGUILayout.ColorField( new GUIContent( "Strands (G) Color", "Tint to be applied to the secondary fur strands" ), profile.FurProfileData.noiseShadingTint3 );
                profile.FurProfileData.secondaryFurStrandBoost = EditorGUILayout.Slider( new GUIContent( "Strands (G) Boost", "Boost to be applied to the secondary fur strands. Values higher than 1 make it lighter, while values lower than 1 make it darker" ), profile.FurProfileData.secondaryFurStrandBoost, 0.0f, 2 );

            }
            else {
                profile.FurProfileData.mainFurStrandBoost = EditorGUILayout.Slider( new GUIContent( "Strands (R) Boost", "Boost to be applied to the main fur strands. Values higher than 1 make it lighter, while values lower than 1 make it darker" ), profile.FurProfileData.mainFurStrandBoost, 0, 2 );
                profile.FurProfileData.secondaryFurStrandBoost = EditorGUILayout.Slider( new GUIContent( "Strands (G) Boost", "Boost to be applied to the secondary fur strands. Values higher than 1 make it lighter, while values lower than 1 make it darker" ), profile.FurProfileData.secondaryFurStrandBoost, 0.0f, 2 );
            }


            if ( profile.FurProfileData.useLegacyColorVariation ) {

                GUILayout.Space( 12 );

                profile.FurProfileData.legacyColorVariationMap = ObjectField<Texture>( new GUIContent( "Color Variation Mask", "The texture that controls four additional coloring variations to be applied over the fur, either all four to the whole fur or two to the undercoat and two to the overcoat by using the four color channels." ), profile.FurProfileData.legacyColorVariationMap );


                if ( profile.FurProfileData.legacyColorVariationMap ) {

                    GUILayout.Space( 8 );
                    profile.FurProfileData.noiseShadingTint0 = EditorGUILayout.ColorField( new GUIContent( "Fur Color A", "The fur color to be applied on the red channel of the Color Variation map" ), profile.FurProfileData.noiseShadingTint0 );
                    profile.FurProfileData.noiseShadingTint1 = EditorGUILayout.ColorField( new GUIContent( "Fur Color B", "The fur color to be applied on the green channel of the Color Variation map" ), profile.FurProfileData.noiseShadingTint1 );
                    profile.FurProfileData.noiseShadingTint2 = EditorGUILayout.ColorField( new GUIContent( "Fur Color C", "The fur color to be applied on the blue channel of the Color Variation map" ), profile.FurProfileData.noiseShadingTint2 );
                    profile.FurProfileData.noiseShadingTint3 = EditorGUILayout.ColorField( new GUIContent( "Fur Color D", "The fur color to be applied on the alpha channel of the Color Variation map" ), profile.FurProfileData.noiseShadingTint3 );

                }

            }
            else {
                GUILayout.Space( 12 );
                profile.FurProfileData.furNoiseShadingTiling = EditorGUILayout.Slider( "Noise Tiling", profile.FurProfileData.furNoiseShadingTiling, 0.1f, 10f );
                profile.FurProfileData.noiseShadingTint0 = EditorGUILayout.ColorField( new GUIContent( "Noise Tint A", "The main tint to apply for noise variation" ), profile.FurProfileData.noiseShadingTint0 );
                profile.FurProfileData.noiseShadingTint1 = EditorGUILayout.ColorField( new GUIContent( "Noise Tint B", "The secondary tint to apply for noise variation" ), profile.FurProfileData.noiseShadingTint1 );

            }



            GUILayout.Space( 12 );

            CenteredLabel( "Emissive Fur" );

            GUILayout.Space( 16 );

            profile.FurProfileData.emissiveTint = EditorGUILayout.ColorField( new GUIContent( "Emissive Color" ), profile.FurProfileData.emissiveTint, true, false, true );
            profile.FurProfileData.emissionMap = ObjectField<Texture>( new GUIContent( "Emission Map" ), profile.FurProfileData.emissionMap );

            

            GUILayout.Space( 12 );

                CenteredLabel( "Curly Fur" );

                GUILayout.Space( 16 );

                profile.FurProfileData.curlyFurParameters.x = EditorGUILayout.Slider( new GUIContent( "Curl Amount X" ), profile.FurProfileData.curlyFurParameters.x, 0, 1 );
                profile.FurProfileData.curlyFurParameters.y = EditorGUILayout.Slider( new GUIContent( "Curl Amount Y" ), profile.FurProfileData.curlyFurParameters.y, 0, 1 );
                profile.FurProfileData.curlyFurParameters.z = EditorGUILayout.Slider( new GUIContent( "Curl Size X" ), profile.FurProfileData.curlyFurParameters.z, 0, 0.1f );
                profile.FurProfileData.curlyFurParameters.w = EditorGUILayout.Slider( new GUIContent( "Curl Size Y" ), profile.FurProfileData.curlyFurParameters.w, 0, 0.1f );

        

            GUILayout.Space( 16 );

            CenteredLabel( "Rim Lighting" );

            GUILayout.Space( 12 );

            profile.FurProfileData.rimLightingTint = EditorGUILayout.ColorField( new GUIContent( "Rim Lighting Tint", "The main tint to be applied to the fur's rim lighting" ), profile.FurProfileData.rimLightingTint );

            profile.FurProfileData.rimLightingPower = EditorGUILayout.Slider( new GUIContent( "Rim Lighting Power" ), profile.FurProfileData.rimLightingPower, 0.1f, 10 );

            profile.FurProfileData.rimLightingStrength = EditorGUILayout.Slider( new GUIContent( "Rim Lighting Strength", "Applies an additional color boost to the fur's rim lighting effect" ), profile.FurProfileData.rimLightingStrength, 1.0f, 3.0f );

            GUILayout.Space( 12 );

            CenteredLabel( "Per Instance Wind Settings" );

            GUILayout.Space( 12 );

            profile.FurProfileData.windStrengthMultiplier = EditorGUILayout.Slider( new GUIContent( "Wind Strength Multiplier", "The value by which the global wind strength will be multiplied, useful to fine tune the overall wind strength applied over this instance" ), profile.FurProfileData.windStrengthMultiplier, 0.0f, 8.0f );

            GUILayout.Space( 32 );
            GUILayout.Space( 32 );

            lStyle = new GUIStyle( EditorStyles.label );
            lStyle.alignment = TextAnchor.MiddleCenter;
            lStyle.fontStyle = FontStyle.Italic;
            lStyle.fontSize = 8;

            GUILayout.Label( $"Copyright© 2017-{System.DateTime.Today.Year},   Jorge Pinal N.", lStyle );

            GUILayout.Space( 24 );



            GUILayout.EndVertical();
            GUILayout.Space( 24 ); GUILayout.EndHorizontal();
            EditorUtility.SetDirty( profile );

        }


        #region PIDI 2020 EDITOR

        public void XFurModuleStatus( XFurStudioModule module ) {
            GUILayout.BeginHorizontal();
            GUILayout.Label( module.Name + ", v" + module.Version, pidiSkin2.label, GUILayout.Width( 140 ) );
            GUILayout.Space( 64 );
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
            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
            var tempBool = GUILayout.Button( label, EditorGUIUtility.isProSkin ? pidiSkin2.button : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button, GUILayout.MaxWidth( width ) );
            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();
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
            GUILayout.Label( profile.Version, pidiSkin2.customStyles[2] );
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
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            currentValue = EditorGUILayout.FloatField( currentValue, EditorGUIUtility.isProSkin ? pidiSkin2.customStyles[4] : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).textField );
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
        public bool EnableDisableToggle( GUIContent label, bool toggleValue, bool trueFalseToggle = false, params GUILayoutOption[] options ) {

            int option = toggleValue ? 1 : 0;

            GUILayout.Space( 4 );

            if ( label != null ) {

                if ( trueFalseToggle ) {
                    option = EditorGUILayout.Popup( label, option, new GUIContent[] { new GUIContent( "FALSE" ), new GUIContent( "TRUE" ) }, EditorGUIUtility.isProSkin ? pidiSkin2.button : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button );
                }
                else {
                    option = EditorGUILayout.Popup( label, option, new GUIContent[] { new GUIContent( "DISABLED" ), new GUIContent( "ENABLED" ) }, EditorGUIUtility.isProSkin ? pidiSkin2.button : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button );
                }
            }
            else {
                if ( trueFalseToggle ) {
                    option = EditorGUILayout.Popup( option, new string[] { "FALSE", "TRUE" }, EditorGUIUtility.isProSkin ? pidiSkin2.button : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button, options );
                }
                else {
                    option = EditorGUILayout.Popup( option, new string[] { "DISABLED", "ENABLED" }, EditorGUIUtility.isProSkin ? pidiSkin2.button : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button, options );
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

                    for ( ; emptyLayers > 0; emptyLayers-- ) layers.Add( "Layer " + ( i - emptyLayers ) );
                    layers.Add( layerName );
                }
                else {
                    emptyLayers++;
                }
            }

            if ( layerNames.Length != layers.Count ) {
                layerNames = new string[layers.Count];
            }
            for ( int i = 0; i < layerNames.Length; i++ ) layerNames[i] = layers[i];


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