/*
 * XFur Studio™, the ultimate system for fur simulation and rendering in Unity.
 * Copyright© 2017-2025, Jorge Pinal Negrete. All Rights Reserved. 
 * 
 */

namespace XFurStudio.Editor {

    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;
    using XFurStudio.Utilities;

    [CustomEditor(typeof(XFurWeatherManager))]
    public class XFurStudioWeatherManager_Editor : Editor {

        public GUISkin pidiSkin2;
        public Texture2D xfurStudioLogo;

        XFurWeatherManager weatherManager;

        bool[] folds = new bool[16];

        private void OnEnable() {
            weatherManager = (XFurWeatherManager)target;
        }

        public override void OnInspectorGUI() {

            if ( !pidiSkin2 ) {
                GUILayout.Space( 12 );
                EditorGUILayout.HelpBox( "The needed GUISkin for this asset has not been found or is corrupted. Please re-download the asset to try to fix this issue or contact support if it persists", MessageType.Error );
                GUILayout.Space( 12 );
                return;
            }

            GUI.color = EditorGUIUtility.isProSkin ? new Color( 0.1f, 0.1f, 0.15f, 1 ) : new Color( 0.5f, 0.5f, 0.6f );
            GUILayout.BeginVertical( EditorStyles.helpBox );
            GUI.color = Color.white;
            GUILayout.BeginHorizontal(); GUILayout.Space( 12 );
            GUILayout.BeginVertical();

            AssetLogoAndVersion();

            pidiSkin2.label = new GUIStyle( EditorStyles.label );

            GUILayout.Space( 16 );
            CenteredLabel( "XFur Studio™ 3 - Wind & Weather Manager, " + weatherManager.Version );
            GUILayout.Space( 16 );

            if (BeginCenteredGroup("Wind Simulation", ref folds[0] ) ) {
                GUILayout.Space( 16 );
                weatherManager.WindStrength = EditorGUILayout.Slider( new GUIContent( "Wind Strength" ), weatherManager.WindStrength, 0 , 2 );
                weatherManager.WindFrequency = EditorGUILayout.Slider( new GUIContent( "Wind Frequency" ), weatherManager.WindFrequency, 0 , 32 );
                GUILayout.Space( 16 );
            }
            EndCenteredGroup();

            
            if (BeginCenteredGroup("Snow Simulation", ref folds[1] ) ) {
                GUILayout.Space( 16 );
                weatherManager.SnowIntensity = EditorGUILayout.Slider( new GUIContent( "Snow Intensity" ), weatherManager.SnowIntensity, 0, 1 );
                weatherManager.SnowAbsDirection = EditorGUILayout.Vector3Field( new GUIContent( "Snow Direction" ), weatherManager.SnowAbsDirection );
                weatherManager.SnowWindInfluence = EditorGUILayout.Slider( new GUIContent( "Wind Influence over Snow" ), weatherManager.SnowWindInfluence, 0 , 2 );
                GUILayout.Space( 16 );
            }
            EndCenteredGroup();

            weatherManager.snowGizmos = folds[1];
            weatherManager.rainGizmos = folds[2];

              
            if (BeginCenteredGroup("Rain Simulation", ref folds[2] ) ) {
                GUILayout.Space( 16 );
                weatherManager.RainIntensity = EditorGUILayout.Slider( new GUIContent( "Rain Intensity" ), weatherManager.RainIntensity, 0, 1 );
                weatherManager.RainAbsDirection = EditorGUILayout.Vector3Field( new GUIContent( "Rain Direction" ), weatherManager.RainAbsDirection );
                weatherManager.RainWindInfluence = EditorGUILayout.Slider( new GUIContent( "Wind Influence over Rain" ), weatherManager.RainWindInfluence, 0 , 2 );
                GUILayout.Space( 16 );
            }
            EndCenteredGroup();
            

            GUILayout.Space( 24 );

            Undo.RecordObject( weatherManager, weatherManager.name + weatherManager.GetInstanceID() );


            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();

            var lStyle = new GUIStyle( EditorStyles.label );
            lStyle.fontStyle = FontStyle.Italic;
            lStyle.fontSize = 8;

            GUILayout.Label( $"Copyright© 2017-{System.DateTime.Today.Year}", lStyle );

            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();

            GUILayout.Space( 24 );
            GUILayout.EndVertical();
            GUILayout.Space( 12 ); GUILayout.EndHorizontal();
            GUILayout.EndVertical();

        }




        #region PIDI 2020 EDITOR


        public void HelpBox( string message, MessageType messageType ) {
            GUILayout.Space( 8 );
            GUILayout.BeginHorizontal(); GUILayout.Space( 8 );
            GUILayout.BeginVertical( pidiSkin2.customStyles[5] );

            GUILayout.Space( 4 );
            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();

            var mType = "INFO";

            switch ( messageType ) {
                case MessageType.Error:
                    mType = "ERROR";
                    break;

                case MessageType.Warning:
                    mType = "WARNING";
                    break;
            }

            var tStyle = new GUIStyle();
            tStyle.fontSize = 11;
            tStyle.fontStyle = FontStyle.Bold;
            tStyle.normal.textColor = Color.black;

            GUILayout.Label( mType, tStyle );

            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();
            GUILayout.Space( 4 );

            GUILayout.BeginHorizontal(); GUILayout.Space( 8 ); GUILayout.BeginVertical();
            tStyle.fontSize = 9;
            tStyle.fontStyle = FontStyle.Normal;
            tStyle.wordWrap = true;
            GUILayout.TextArea( message, tStyle );

            GUILayout.Space( 8 );
            GUILayout.EndVertical(); GUILayout.Space( 8 ); GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space( 8 ); GUILayout.EndHorizontal();
            GUILayout.Space( 8 );
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
            GUI.color = Color.gray;
            inputObject = (T)EditorGUILayout.ObjectField( inputObject, typeof( T ), allowSceneObjects );
            GUI.color = Color.white;
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
            var tempBool = GUILayout.Button( label, pidiSkin2.button, GUILayout.MaxWidth( width ) );
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
            var tempBool = GUILayout.Button( label, pidiSkin2.button, GUILayout.MaxWidth( width ) );
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
            GUILayout.Label( weatherManager.Version, pidiSkin2.customStyles[2] );
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
            GUILayout.Label( label, pidiSkin2.label );
            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();

        }

        /// <summary>
        /// Begins a custom centered group similar to a foldout that can be expanded with a button
        /// </summary>
        /// <param name="label"></param>
        /// <param name="groupFoldState"></param>
        /// <returns></returns>
        public bool BeginCenteredGroup( string label, ref bool groupFoldState ) {

            if ( GUILayout.Button( label, EditorGUIUtility.isProSkin?pidiSkin2.button:EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).button ) ) {
                groupFoldState = !groupFoldState;
            }
            GUILayout.BeginHorizontal(); GUILayout.Space( 12 );
            GUILayout.BeginVertical();
            return groupFoldState;
        }


        /// <summary>
        /// Finishes a centered group
        /// </summary>
        public void EndCenteredGroup() {
            GUILayout.EndVertical();
            GUILayout.Space( 12 );
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
            currentValue = EditorGUILayout.IntField( currentValue, pidiSkin2.customStyles[4] );
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
            currentValue = EditorGUILayout.FloatField( currentValue, pidiSkin2.customStyles[4] );
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
            currentValue = EditorGUILayout.TextField( currentValue, pidiSkin2.customStyles[4] );
            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );

            return currentValue;
        }


        public Vector3 Vector3Field( GUIContent label, Vector3 currentValue ) {

            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            currentValue.x = EditorGUILayout.FloatField( currentValue.x, pidiSkin2.customStyles[4] );
            GUILayout.Space( 8 );
            currentValue.y = EditorGUILayout.FloatField( currentValue.y, pidiSkin2.customStyles[4] );
            GUILayout.Space( 8 );
            currentValue.z = EditorGUILayout.FloatField( currentValue.z, pidiSkin2.customStyles[4] );
            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );

            return currentValue;

        }


        /// <summary>
        /// Custom slider using the PIDI 2020 editor skin and adding a custom suffix to the float display
        /// </summary>
        /// <param name="label"></param>
        /// <param name="currentValue"></param>
        /// <param name="minSlider"></param>
        /// <param name="maxSlider"></param>
        /// <param name="suffix"></param>
        /// <returns></returns>
        public float SliderField( GUIContent label, float currentValue, float minSlider = 0.0f, float maxSlider = 1.0f, string suffix = "" ) {

            GUILayout.Space( 4 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            GUI.color = Color.gray;
            currentValue = GUILayout.HorizontalSlider( currentValue, minSlider, maxSlider, GUI.skin.horizontalSlider, GUI.skin.horizontalSliderThumb );
            GUI.color = Color.white;
            GUILayout.Space( 12 );
            currentValue = Mathf.Clamp( EditorGUILayout.FloatField( float.Parse( currentValue.ToString( "n2" ) ), pidiSkin2.customStyles[4], GUILayout.MaxWidth( 40 ) ), minSlider, maxSlider );
            GUILayout.EndHorizontal();
            GUILayout.Space( 4 );

            return currentValue;
        }


        /// <summary>
        /// Custom slider using the PIDI 2020 editor skin and adding a custom suffix to the float display
        /// </summary>
        /// <param name="label"></param>
        /// <param name="currentValue"></param>
        /// <param name="minSlider"></param>
        /// <param name="maxSlider"></param>
        /// <param name="suffix"></param>
        /// <returns></returns>
        public int IntSliderField( GUIContent label, int currentValue, int minSlider = 0, int maxSlider = 1 ) {

            GUILayout.Space( 4 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            GUI.color = Color.gray;
            currentValue = (int)GUILayout.HorizontalSlider( currentValue, minSlider, maxSlider, GUI.skin.horizontalSlider, GUI.skin.horizontalSliderThumb );
            GUI.color = Color.white;
            GUILayout.Space( 12 );
            currentValue = (int)Mathf.Clamp( EditorGUILayout.FloatField( float.Parse( currentValue.ToString( "n2" ) ), pidiSkin2.customStyles[4], GUILayout.MaxWidth( 40 ) ), minSlider, maxSlider );
            GUILayout.EndHorizontal();
            GUILayout.Space( 4 );

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
            selected = EditorGUILayout.Popup( selected, options, pidiSkin2.button );
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

            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            if ( label != null ) {
                GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );

                if ( trueFalseToggle ) {
                    option = EditorGUILayout.Popup( option, new string[] { "TRUE", "FALSE" }, pidiSkin2.button );
                }
                else {
                    option = EditorGUILayout.Popup( option, new string[] { "DISABLED", "ENABLED" }, pidiSkin2.button );
                }
            }
            else {
                if ( trueFalseToggle ) {
                    option = EditorGUILayout.Popup( option, new string[] { "TRUE", "FALSE" }, pidiSkin2.button, options );
                }
                else {
                    option = EditorGUILayout.Popup( option, new string[] { "DISABLED", "ENABLED" }, pidiSkin2.button, options );
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );
            return option == 1;
        }


        /// <summary>
        /// Draw an enum field but changing the labels and names of the enum to Upper Case fields
        /// </summary>
        /// <param name="label"></param>
        /// <param name="userEnum"></param>
        /// <returns></returns>
        public int UpperCaseEnumField( GUIContent label, System.Enum userEnum ) {

            var names = System.Enum.GetNames( userEnum.GetType() );

            for ( int i = 0; i < names.Length; i++ ) {
                names[i] = System.Text.RegularExpressions.Regex.Replace( names[i], "(\\B[A-Z])", " $1" ).ToUpper();
            }

            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            var result = EditorGUILayout.Popup( System.Convert.ToInt32( userEnum ), names, pidiSkin2.button );
            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );
            return result;
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
            var result = EditorGUILayout.Popup( System.Convert.ToInt32( userEnum ), names, pidiSkin2.button );
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

            selected.value = EditorGUILayout.MaskField( selected.value, layerNames, pidiSkin2.button );

            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );
            return selected;
        }



        #endregion




    }

}