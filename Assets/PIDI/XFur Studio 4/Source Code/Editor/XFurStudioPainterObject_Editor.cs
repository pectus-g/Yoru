

namespace XFurStudio.Editor {

    using XFurStudio.Core;
    using XFurStudio.Utilities;
    using UnityEngine;
    using UnityEditor;
    using System.Collections.Generic;

    [CustomEditor( typeof( XFurStudioPainterObject ) )]
    public class XFurStudioPainterObject_Editor : Editor {


        public GUISkin pidiSkin2;
        public Texture2D xfurStudioLogo;
        public XFurStudioPainterObject painterObject;




        private void OnEnable() {

            painterObject = (XFurStudioPainterObject)target;

        }


        bool[] folds = new bool[32];

        public override void OnInspectorGUI() {


            serializedObject.Update();

            if ( !xfurStudioLogo ) {
                if ( AssetDatabase.FindAssets( "l: XFurStudio2Logo" ).Length > 0 ) {
                    xfurStudioLogo = (Texture2D)AssetDatabase.LoadAssetAtPath( AssetDatabase.GUIDToAssetPath( AssetDatabase.FindAssets( "l: XFurStudio2Logo" )[0] ), typeof( Texture2D ) );
                }
            }

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


            GUILayout.BeginHorizontal(); GUILayout.Space( 32 );

            GUILayout.BeginVertical();
            GUI.color = Color.white;

            AssetLogoAndVersion();

            GUILayout.Space( 16 );


            if ( serializedObject.isEditingMultipleObjects ) {

                EditorGUILayout.HelpBox( "XFur Studio depends on per-instance behavior and data sets. Editing multiple instances is not allowed. If you need to share properties across multiple instances, use XFur Templates or Unity Prefabs instead", MessageType.Warning );

                GUILayout.Space( 32 );

                GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();


                lStyle.fontStyle = FontStyle.Italic;
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


            Undo.RecordObject( painterObject, "XFurInstance_" + GetInstanceID() );


            GUILayout.Space( 16 );

            painterObject.PaintDataMode = (XFurStudioAPI.PaintDataMode)EditorGUILayout.EnumPopup( new GUIContent( "XFur Paint Mode" ), painterObject.PaintDataMode, EditorGUIUtility.isProSkin ? pidiSkin2.button : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button );

            GUILayout.Space( 12 );

            //painterObject.PainterObjectShape = (XFurStudioPainterObject.PainterShape)StandardEnumField( new GUIContent( "Painter Shape", "The shape used by this object to paint XFur Instances" ), painterObject.PainterObjectShape );

            //painterObject.PainterObjectShape = XFurStudioPainterObject.PainterShape.Spherical;

            //if ( painterObject.PainterObjectShape == XFurStudioPainterObject.PainterShape.Spherical ) {
                painterObject.Radius = EditorGUILayout.FloatField( new GUIContent( "Radius" ), painterObject.Radius );
            //}
            //else {
             //   painterObject.BoxSize = EditorGUILayout.Vector3Field( new GUIContent( "Size" ), painterObject.BoxSize );
           // }

            GUILayout.Space( 12 );

            if ( painterObject.PaintDataMode == XFurStudioAPI.PaintDataMode.SnowFX || painterObject.PaintDataMode == XFurStudioAPI.PaintDataMode.BloodFX || painterObject.PaintDataMode == XFurStudioAPI.PaintDataMode.WaterFX ) {

                painterObject.BrushOpacity = EditorGUILayout.Slider( new GUIContent( "Effect Intensity" ), painterObject.BrushOpacity, 0.0f, 10.0f );
                painterObject.InvertBrush = EnableDisableToggle( new GUIContent( "Invert Value", "Inverts the value to paint. For the fur mask, shaves fur instead of growing it. For Fur Length, it makes the fur shorter instead of longer, for Fur Occlusion removes it instead of adding it, etc." ), painterObject.InvertBrush, true );

            }
            else {

                if ( painterObject.PaintDataMode == XFurStudioAPI.PaintDataMode.FurColor ) {
                    painterObject.BrushColor = EditorGUILayout.ColorField( new GUIContent( "Brush Color" ), painterObject.BrushColor );
                }

                if ( painterObject.PaintDataMode != XFurStudioAPI.PaintDataMode.FurMask ) {
                    painterObject.BrushOpacity = EditorGUILayout.Slider( new GUIContent( "Effect Intensity" ), painterObject.BrushOpacity, 0, 1 );
                    painterObject.BrushHardness = EditorGUILayout.Slider( new GUIContent( "Brush Hardness" ), painterObject.BrushHardness, 0, 1 );
                }

                if ( painterObject.PaintDataMode == XFurStudioAPI.PaintDataMode.FurMask || painterObject.PaintDataMode == XFurStudioAPI.PaintDataMode.FurLength || painterObject.PaintDataMode == XFurStudioAPI.PaintDataMode.FurThickness || painterObject.PaintDataMode == XFurStudioAPI.PaintDataMode.FurOcclusion  ) {
                    painterObject.InvertBrush = EnableDisableToggle( new GUIContent( "Invert Value", "Inverts the value to paint. For the fur mask, shaves fur instead of growing it. For Fur Length, it makes the fur shorter instead of longer, for Fur Occlusion removes it instead of adding it, etc." ), painterObject.InvertBrush, true );
                    /*
                    if ( painterObject.PaintDataMode == XFurStudioAPI.PaintDataMode.FurColorBlend ) {
                        GUILayout.Space( 8 );
                        painterObject.ColorBlendMask.x = EnableDisableToggle( new GUIContent( "Paint Channel 1 Mask" ), painterObject.ColorBlendMask.x > 0, true ) ? 1 : 0;
                        painterObject.ColorBlendMask.y = EnableDisableToggle( new GUIContent( "Paint Channel 2 Mask" ), painterObject.ColorBlendMask.y > 0, true ) ? 1 : 0;
                        painterObject.ColorBlendMask.z = EnableDisableToggle( new GUIContent( "Paint Channel 3 Mask" ), painterObject.ColorBlendMask.z > 0, true ) ? 1 : 0;
                        painterObject.ColorBlendMask.w = EnableDisableToggle( new GUIContent( "Paint Channel 4 Mask" ), painterObject.ColorBlendMask.w > 0, true ) ? 1 : 0;
                    }
                    */
                }

            }


            GUILayout.Space( 16 );

            GUILayout.Space( 32 );

            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();


            lStyle.fontStyle = FontStyle.Italic;
            lStyle.fontSize = 8;

            GUILayout.Label( $"Copyright© 2017-{System.DateTime.Today.Year}", lStyle );

            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();

            GUILayout.Space( 24 );
            GUILayout.EndVertical();

            GUILayout.Space( 32 ); GUILayout.EndHorizontal();

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
            tStyle.fontSize = 10;
            tStyle.fontStyle = FontStyle.Bold;
            tStyle.wordWrap = true;
            GUILayout.TextArea( message, tStyle );

            GUILayout.Space( 8 );
            GUILayout.EndVertical(); GUILayout.Space( 8 ); GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space( 8 ); GUILayout.EndHorizontal();
            GUILayout.Space( 8 );
        }



        public Color ColorField( GUIContent label, Color currentValue ) {

            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            currentValue = EditorGUILayout.ColorField( currentValue );
            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );

            return currentValue;

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
            GUILayout.Label( "4.1.0", pidiSkin2.customStyles[2] );
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
            currentValue = EditorGUILayout.IntField( label, currentValue );
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


        public Vector2 Vector2Field( GUIContent label, Vector2 currentValue ) {

            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            currentValue.x = EditorGUILayout.FloatField( currentValue.x, pidiSkin2.customStyles[4] );
            GUILayout.Space( 8 );
            currentValue.y = EditorGUILayout.FloatField( currentValue.y, pidiSkin2.customStyles[4] );
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


        public Vector4 Vector4Field( GUIContent label, Vector4 currentValue ) {

            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            currentValue.x = EditorGUILayout.FloatField( currentValue.x, pidiSkin2.customStyles[4] );
            GUILayout.Space( 8 );
            currentValue.y = EditorGUILayout.FloatField( currentValue.y, pidiSkin2.customStyles[4] );
            GUILayout.Space( 8 );
            currentValue.z = EditorGUILayout.FloatField( currentValue.z, pidiSkin2.customStyles[4] );
            GUILayout.Space( 8 );
            currentValue.w = EditorGUILayout.FloatField( currentValue.w, pidiSkin2.customStyles[4] );
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
            currentValue = (int)Mathf.Clamp( EditorGUILayout.IntField( int.Parse( currentValue.ToString() ), pidiSkin2.customStyles[4], GUILayout.MaxWidth( 40 ) ), minSlider, maxSlider );
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

            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            if ( label != null ) {
                GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );

                if ( trueFalseToggle ) {
                    option = EditorGUILayout.Popup( option, new string[] { "False", "True" }, option == 0 ? pidiSkin2.customStyles[5] : pidiSkin2.customStyles[6], options );
                }
                else {
                    option = EditorGUILayout.Popup( option, new string[] { "Disabled", "Enabled" }, option == 0 ? pidiSkin2.customStyles[5] : pidiSkin2.customStyles[6], options );
                }
            }
            else {
                if ( trueFalseToggle ) {
                    option = EditorGUILayout.Popup( option, new string[] { "False", "True" }, option == 0 ? pidiSkin2.customStyles[5] : pidiSkin2.customStyles[6], options );
                }
                else {
                    option = EditorGUILayout.Popup( option, new string[] { "Disabled", "Enabled" }, option == 0 ? pidiSkin2.customStyles[5] : pidiSkin2.customStyles[6], options );
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
            var result = EditorGUILayout.Popup( System.Convert.ToInt32( userEnum ), names, EditorGUIUtility.isProSkin ? pidiSkin2.button : EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector ).button );
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
                names[i] = names[i];
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

            selected.value = EditorGUILayout.MaskField( selected.value, layerNames, pidiSkin2.button );

            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );
            return selected;
        }



        #endregion





    }


}
