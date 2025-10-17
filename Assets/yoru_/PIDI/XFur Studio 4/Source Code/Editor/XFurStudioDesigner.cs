/*

XFur Studio™, by Irreverent Software™
Copyright© 2018-2025, Jorge Pinal Negrete. All Rights Reserved.

*/

namespace XFurStudio.Designer {

    using XFurStudio.Core;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;
    using System.IO;


    public class XFurStudioDesigner : EditorWindow {

        public XFurStudioInstance xfur;

        public int editFurProfile;

        public GUISkin pidiSkin2;

        public Texture2D xfurStudioLogo;

        public Texture2D genSettings, genProps, brushShave, brushLen, brushThick, brushOcc, brushGroom, exportData;

        public Vector2 scrollView;

        public int exportResolution = 1;

        private bool movingCamera;

        MeshCollider xfurCollider;

        UndoManager xfurUndo = new UndoManager();

        Mesh colliderMesh;


        [System.Serializable]
        class XFurBrushData {


            public int activeTool = 0;
            public bool invert;
            public bool mirror;

            public bool fineTuneBrush;
            public bool hasContact;

            public Vector3 brushCenter;
            public Vector3 brushNormal;

            public float opacity = 0.1f;
            public float falloff = 0.5f;
            public float size = 0.05f;

            public Vector2 minMaxSize = new Vector2( 0.0001f, 1.0f );

        }


        XFurBrushData brushData = new XFurBrushData();

        bool initialFocus;
        bool isGPUAccelerated;

        static void Init() {
            // Get existing open window or if none, make a new one:
            XFurStudioDesigner window = (XFurStudioDesigner)EditorWindow.GetWindow( typeof( XFurStudioDesigner ) );
            window.Show();

        }

        void OnEnable() {

            ActiveEditorTracker.sharedTracker.isLocked = true;

            SceneView.duringSceneGui -= OnSceneGUI;
            // Add (or re-add) the delegate.
            SceneView.duringSceneGui += OnSceneGUI;



        }

        void OnDestroy() {
            SceneView.duringSceneGui -= OnSceneGUI;
            xfur.Settings.useVertexBuffer = isGPUAccelerated;
        }

        void OnSceneGUI( SceneView sceneView ) {



            if ( xfur ) {

                if ( xfurUndo.furData0 != xfur.FurDataProfiles[editFurProfile].furDataMap && xfur.FurDataProfiles[editFurProfile].furDataMap is not RenderTexture ) {
                    xfurUndo.furData0 = xfur.FurDataProfiles[editFurProfile].furDataMap;
                }

                if ( xfurUndo.furData1 != xfur.FurDataProfiles[editFurProfile].furGroomingMap && xfur.FurDataProfiles[editFurProfile].furGroomingMap is not RenderTexture ) {
                    xfurUndo.furData1 = xfur.FurDataProfiles[editFurProfile].furGroomingMap;
                }

            }

            Event currentEvent = Event.current;


            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1 ) {
                movingCamera = true;
            }
            
            if (currentEvent.type == EventType.MouseUp && currentEvent.button == 1 ) {
                movingCamera = false;
            }




            if ( ( currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.F ) || !initialFocus ) {
                sceneView.LookAt( xfur.MainRenderer.renderer.bounds.center );
                
                if ( !initialFocus ) {
                    if ( xfur ) {
                        isGPUAccelerated = xfur.Settings.useVertexBuffer;
                        xfur.Settings.useVertexBuffer = false;
                    }
                }
                
                initialFocus = true;


                return;
            }

            Selection.SetActiveObjectWithContext( null, null );

            if ( !xfurCollider ) {
                xfurCollider = new GameObject( "XFCollider", typeof( MeshCollider ) ).GetComponent<MeshCollider>();
                xfurCollider.gameObject.hideFlags = HideFlags.HideAndDontSave;
                return;
            }

            if ( xfur.CurrentFurRenderer.renderer is SkinnedMeshRenderer ) {

                if ( !colliderMesh || colliderMesh == xfur.CurrentMesh ) {
                    colliderMesh = new Mesh();
                }

                ( xfur.CurrentFurRenderer.renderer as SkinnedMeshRenderer ).BakeMesh( colliderMesh );
            }
            else {
                colliderMesh = xfur.CurrentMesh;
            }

            xfurCollider.sharedMesh = colliderMesh;


            xfurCollider.transform.position = xfur.CurrentFurRenderer.renderer.transform.position;
            xfurCollider.transform.rotation = xfur.CurrentFurRenderer.renderer.transform.rotation;

            if ( brushData.activeTool > 1 && brushData.activeTool < 7 ) {

                Ray ray = HandleUtility.GUIPointToWorldRay( currentEvent.mousePosition );
                var hits = Physics.RaycastAll( ray, 100f );


                if ( !currentEvent.alt ) {
                    HandleUtility.AddDefaultControl( GUIUtility.GetControlID( FocusType.Passive ) );
                }
                else {
                    return;
                }


                if ( currentEvent.shift ) {

                    if ( currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Z ) {
                        xfurUndo.Undo( xfur, editFurProfile, brushData.activeTool );
                        currentEvent.Use();
                        return;
                    }

                }


                if ( currentEvent.shift ) {


                    if ( currentEvent.type == EventType.MouseDrag ) {

                        if ( currentEvent.button == 0 ) {

                            if ( Mathf.Abs( currentEvent.delta.x ) > Mathf.Abs( currentEvent.delta.y ) + 0.1 ) {
                                if ( currentEvent.shift ) {
                                    brushData.size += 0.005f * currentEvent.delta.x;
                                    brushData.size = Mathf.Clamp( brushData.size, brushData.minMaxSize.x, brushData.minMaxSize.y );
                                    currentEvent.Use();
                                }
                            }
                            else if ( Mathf.Abs( currentEvent.delta.y ) > Mathf.Abs( currentEvent.delta.x ) + 0.1 ) {
                                brushData.falloff -= 0.005f * currentEvent.delta.y;
                                brushData.falloff = Mathf.Clamp( brushData.falloff, 0.05f, 1 );
                                currentEvent.Use();
                            }


                        }
                        if ( currentEvent.button == 1 ) {
                            if ( currentEvent.shift ) {
                                brushData.opacity += 0.005f * currentEvent.delta.x;
                                brushData.opacity = Mathf.Clamp( brushData.opacity, 0.05f, 1 );
                                currentEvent.Use();
                            }
                        }

                    }


                }
                else {

                    if ( currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.X ) {
                        brushData.invert = !brushData.invert;
                        currentEvent.Use();
                    }


                    if ( !movingCamera ) {

                        if ( currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.S ) {
                            brushData.mirror = !brushData.mirror;
                            currentEvent.Use();
                        }

                    }

                    
                    for ( int i = 0; i < hits.Length; i++ ) {
                        if ( hits[i].collider == xfurCollider ) {
                            brushData.brushCenter = hits[i].point;
                            brushData.brushNormal = hits[i].normal;
                            brushData.hasContact = true;

                            break;
                        }
                    }


                    if ( currentEvent.type == EventType.MouseDrag && currentEvent.button == 0 && brushData.hasContact ) {
                        var r1 = HandleUtility.GUIPointToWorldRay( currentEvent.mousePosition );
                        var r2 = HandleUtility.GUIPointToWorldRay( currentEvent.mousePosition - currentEvent.delta );
                        var bDirection = Vector3.Normalize( r1.GetPoint( 10 ) - r2.GetPoint( 10 ) );

                        XFurDesignerPaint( bDirection );

                        currentEvent.Use();

                    }

                    if ( currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && brushData.hasContact ) {
                        xfurUndo.RecordUndo( xfur, editFurProfile, brushData.activeTool );

                    }

                    if ( currentEvent.type == EventType.MouseUp && currentEvent.button == 0 ) {
                        brushData.hasContact = false;
                    }

                }

                var hColor = Handles.color;

                Handles.color = new Color( 1, 1, 1, Mathf.Max( 0.1f, brushData.opacity ) );

                Handles.DrawWireDisc( brushData.brushCenter, brushData.brushNormal, brushData.size, 3 );
                Handles.DrawWireDisc( brushData.brushCenter, brushData.brushNormal, brushData.size * brushData.falloff, 3 );

                if ( brushData.mirror ) {
                    Handles.DrawWireDisc( xfur.transform.TransformPoint( Vector3.Reflect( xfur.transform.InverseTransformPoint( brushData.brushCenter ), Vector3.right ) ), xfur.transform.TransformDirection( Vector3.Reflect( xfur.transform.InverseTransformDirection( brushData.brushNormal ), Vector3.right ) ), brushData.size, 3 );
                    Handles.DrawWireDisc( xfur.transform.TransformPoint( Vector3.Reflect( xfur.transform.InverseTransformPoint( brushData.brushCenter ), Vector3.right ) ), xfur.transform.TransformDirection( Vector3.Reflect( xfur.transform.InverseTransformDirection( brushData.brushNormal ), Vector3.right ) ), brushData.size * brushData.falloff, 3 );
                }



                Handles.color = hColor;

            }




        }


        #region XFur Studio Designer 


        public class UndoManager {

            public int maxUndoSteps = 16;

            public Texture furData0, furData1;

            public List<RenderTexture> furDataMapUndoSteps = new List<RenderTexture>();
            public List<RenderTexture> furGroomingMapUndoSteps = new List<RenderTexture>();



            public void RecordUndo( XFurStudioInstance xfur, int onProfile, int dataToStore ) {

                if ( dataToStore > 1 ) {

                    if ( dataToStore == 6 ) {

                        if ( furGroomingMapUndoSteps.Count == maxUndoSteps ) {
                            for ( int i = 0; i < furGroomingMapUndoSteps.Count - 2; i++ ) {
                                Graphics.Blit( furGroomingMapUndoSteps[i + 1], furGroomingMapUndoSteps[i] );
                            }

                            RenderTexture.ReleaseTemporary( furGroomingMapUndoSteps[furGroomingMapUndoSteps.Count - 1] );
                            furGroomingMapUndoSteps.RemoveAt( furGroomingMapUndoSteps.Count - 1 );

                        }

                        if ( xfur.FurDataProfiles[onProfile].furGroomingMap == null ) {
                            XFurStudioAPI.Groom( xfur, onProfile, xfur.transform.position + Vector3.up * 1000, Vector3.forward, 0, 0, 0, Vector3.zero );
                        }

                        furGroomingMapUndoSteps.Add( RenderTexture.GetTemporary( xfur.FurDataProfiles[onProfile].furGroomingMap.width, xfur.FurDataProfiles[onProfile].furGroomingMap.height ) );
                        Graphics.Blit( xfur.FurDataProfiles[onProfile].furGroomingMap, furGroomingMapUndoSteps[furGroomingMapUndoSteps.Count - 1] );
                    }
                    else {


                        if ( furDataMapUndoSteps.Count == maxUndoSteps ) {
                            for ( int i = 0; i < furDataMapUndoSteps.Count - 2; i++ ) {
                                Graphics.Blit( furDataMapUndoSteps[i + 1], furDataMapUndoSteps[i] );
                            }

                            RenderTexture.ReleaseTemporary( furDataMapUndoSteps[furDataMapUndoSteps.Count - 1] );
                            furDataMapUndoSteps.RemoveAt( furDataMapUndoSteps.Count - 1 );

                        }


                        if ( xfur.FurDataProfiles[onProfile].furDataMap == null ) {
                            XFurStudioAPI.Paint( xfur, XFurStudioAPI.PaintDataMode.FurMask, onProfile, xfur.transform.position + Vector3.up * 1000, Vector3.forward, 0, 0, 0, Color.white );
                        }


                        furDataMapUndoSteps.Add( RenderTexture.GetTemporary( xfur.FurDataProfiles[onProfile].furDataMap.width, xfur.FurDataProfiles[onProfile].furDataMap.height ) );
                        Graphics.Blit( xfur.FurDataProfiles[onProfile].furDataMap, furDataMapUndoSteps[furDataMapUndoSteps.Count - 1] );
                    }

                }

            }


            public void Undo( XFurStudioInstance xfur, int onProfile, int dataToUndo ) {

                if ( dataToUndo > 1 ) {

                    if ( dataToUndo == 6 ) {
                        if ( furGroomingMapUndoSteps.Count > 0 ) {
                            Graphics.Blit( furGroomingMapUndoSteps[furGroomingMapUndoSteps.Count - 1], (RenderTexture)xfur.FurDataProfiles[onProfile].furGroomingMap );
                            RenderTexture.ReleaseTemporary( furGroomingMapUndoSteps[furGroomingMapUndoSteps.Count - 1] );
                            furGroomingMapUndoSteps.RemoveAt( furGroomingMapUndoSteps.Count - 1 );
                        }


                    }
                    else {
                        if ( furDataMapUndoSteps.Count > 0 ) {
                            Graphics.Blit( furDataMapUndoSteps[furDataMapUndoSteps.Count - 1], (RenderTexture)xfur.FurDataProfiles[onProfile].furDataMap );

                            RenderTexture.ReleaseTemporary( furDataMapUndoSteps[furDataMapUndoSteps.Count - 1] );
                            furDataMapUndoSteps.RemoveAt( furDataMapUndoSteps.Count - 1 );
                        }
                    }

                }

            }



            public void Clear() {

                foreach ( RenderTexture rt in furDataMapUndoSteps ) {
                    RenderTexture.ReleaseTemporary( rt );
                }

                foreach ( RenderTexture rt in furGroomingMapUndoSteps ) {
                    RenderTexture.ReleaseTemporary( rt );
                }

                furDataMapUndoSteps.Clear();
                furGroomingMapUndoSteps.Clear();

            }



        }




        void XFurDesignerPaint( Vector3 brushDirection ) {

            XFurStudioAPI.PaintDataMode paintMode = XFurStudioAPI.PaintDataMode.FurMask;

            var paintColor = brushData.invert ? Color.black : Color.white;

            switch ( brushData.activeTool ) {

                case 2:
                    paintMode = XFurStudioAPI.PaintDataMode.FurMask;
                    paintColor = brushData.invert ? Color.white : Color.black;
                    break;

                case 3:
                    paintMode = XFurStudioAPI.PaintDataMode.FurLength;
                    paintColor = brushData.invert ? Color.white : Color.black;
                    break;

                case 4:
                    paintMode = XFurStudioAPI.PaintDataMode.FurThickness;
                    paintColor = brushData.invert ? Color.white : Color.black;
                    break;

                case 5:
                    paintMode = XFurStudioAPI.PaintDataMode.FurOcclusion;
                    break;

                case 6:
                    XFurStudioAPI.Groom( xfur, editFurProfile, brushData.brushCenter, brushData.brushNormal, brushData.size, brushData.opacity, brushData.falloff, brushDirection, brushData.invert );

                    if ( brushData.mirror ) {
                        XFurStudioAPI.Groom( xfur, editFurProfile, xfur.transform.TransformPoint( Vector3.Reflect( xfur.transform.InverseTransformPoint( brushData.brushCenter ), Vector3.right ) ), xfur.transform.TransformDirection( Vector3.Reflect( xfur.transform.InverseTransformDirection( brushData.brushNormal ), Vector3.right ) ), brushData.size, brushData.opacity, brushData.falloff, Vector3.Reflect( brushDirection, Vector3.right ), brushData.invert );
                    }
                    return;

            }


            XFurStudioAPI.Paint( xfur, paintMode, editFurProfile, brushData.brushCenter, brushData.brushNormal, brushData.size, brushData.opacity, brushData.falloff, paintColor );

            if ( brushData.mirror ) {
                XFurStudioAPI.Paint( xfur, paintMode, editFurProfile, xfur.transform.TransformPoint( Vector3.Reflect( xfur.transform.InverseTransformPoint( brushData.brushCenter ), Vector3.right ) ), xfur.transform.TransformDirection( Vector3.Reflect( xfur.transform.InverseTransformDirection( brushData.brushNormal ), Vector3.right ) ), brushData.size, brushData.opacity, brushData.falloff, paintColor );
            }


        }


        public void ExportProfiles() {


            var path = EditorUtility.SaveFolderPanel( "Export Textures", "Assets/", "XFur Data Maps" );
            var temporaryOutput = RenderTexture.GetTemporary( 256 * Mathf.RoundToInt( Mathf.Pow( 2, exportResolution ) ), 256 * Mathf.RoundToInt( Mathf.Pow( 2, exportResolution ) ), 24, RenderTextureFormat.ARGB32 );
            var active = RenderTexture.active;
            RenderTexture.active = temporaryOutput;
            var outputTex = new Texture2D( RenderTexture.active.width, RenderTexture.active.height, TextureFormat.ARGB32, true );
            outputTex.wrapMode = TextureWrapMode.Clamp;


            xfur.GetFurData( editFurProfile, out FurProfileData tempProfile );

            var xfurInstanceName = xfur.name.Replace( " ", "_" );

            if ( !Directory.Exists( path + "/" + xfurInstanceName ) ) {
                Directory.CreateDirectory( path + "/" + xfurInstanceName );
            }


            var relativePath = path.Replace( Application.dataPath, "Assets" );

            if ( relativePath != "Assets/" ) {
                relativePath += "/";
            }

            relativePath += xfurInstanceName + "/";

            if ( xfur.FurDataProfiles[editFurProfile].furDataMap && xfur.FurDataProfiles[editFurProfile].furDataMap is RenderTexture ) {
                Graphics.Blit( xfur.FurDataProfiles[editFurProfile].furDataMap, temporaryOutput );
                outputTex.ReadPixels( new Rect( 0, 0, outputTex.width, outputTex.height ), 0, 0 );
                outputTex.Apply();

                var pngData = outputTex.EncodeToPNG();

                if ( pngData != null ) {
                    File.WriteAllBytes( path + "/" + xfurInstanceName + "/" + xfurInstanceName + "_" + xfur.MainRenderer.materials[editFurProfile].name.Replace( " ", "_" ) + "_furDataMap.png", pngData );
                    AssetDatabase.Refresh();
                    tempProfile.furDataMap = AssetDatabase.LoadAssetAtPath<Texture2D>( relativePath + xfurInstanceName + "_" + xfur.MainRenderer.materials[editFurProfile].name.Replace( " ", "_" ) + "_furDataMap.png" );
                }
            }

            if ( xfur.FurDataProfiles[editFurProfile].furGroomingMap && xfur.FurDataProfiles[editFurProfile].furGroomingMap is RenderTexture ) {
                Graphics.Blit( xfur.FurDataProfiles[editFurProfile].furGroomingMap, temporaryOutput );
                outputTex.ReadPixels( new Rect( 0, 0, outputTex.width, outputTex.height ), 0, 0 );
                outputTex.Apply();

                var pngData = outputTex.EncodeToPNG();

                if ( pngData != null ) {
                    File.WriteAllBytes( path + "/" + xfurInstanceName + "/" + xfurInstanceName + "_" + xfur.MainRenderer.materials[editFurProfile].name.Replace( " ", "_" ) + "_furGroomingMap.png", pngData );
                    AssetDatabase.Refresh();
                    tempProfile.furGroomingMap = AssetDatabase.LoadAssetAtPath<Texture2D>( relativePath + xfurInstanceName + "_" + xfur.MainRenderer.materials[editFurProfile].name.Replace( " ", "_" ) + "_furGroomingMap.png" );
                }
            }

            if ( xfur.FurDataProfiles[editFurProfile].colorMap && xfur.FurDataProfiles[editFurProfile].colorMap is RenderTexture ) {
                Graphics.Blit( xfur.FurDataProfiles[editFurProfile].colorMap, temporaryOutput );
                outputTex.ReadPixels( new Rect( 0, 0, outputTex.width, outputTex.height ), 0, 0 );
                outputTex.Apply();

                var pngData = outputTex.EncodeToPNG();

                if ( pngData != null ) {
                    File.WriteAllBytes( path + "/" + xfurInstanceName + "/" + xfurInstanceName + "_" + xfur.MainRenderer.materials[editFurProfile].name.Replace( " ", "_" ) + "_FurColorMap.png", pngData );
                    AssetDatabase.Refresh();
                    tempProfile.colorMap = AssetDatabase.LoadAssetAtPath<Texture2D>( relativePath + xfurInstanceName + "_" + xfur.MainRenderer.materials[editFurProfile].name.Replace( " ", "_" ) + "_FurColorMap.png" );
                }
            }

            if ( xfur.FurDataProfiles[editFurProfile].legacyColorVariationMap && xfur.FurDataProfiles[editFurProfile].legacyColorVariationMap is RenderTexture ) {
                Graphics.Blit( xfur.FurDataProfiles[editFurProfile].legacyColorVariationMap, temporaryOutput );
                outputTex.ReadPixels( new Rect( 0, 0, outputTex.width, outputTex.height ), 0, 0 );
                outputTex.Apply();

                var pngData = outputTex.EncodeToPNG();

                if ( pngData != null ) {
                    File.WriteAllBytes( path + "/" + xfurInstanceName + "/" + xfurInstanceName + "_" + xfur.MainRenderer.materials[editFurProfile].name.Replace( " ", "_" ) + "_FurColorVariationMap.png", pngData );
                    AssetDatabase.Refresh();
                    tempProfile.legacyColorVariationMap = AssetDatabase.LoadAssetAtPath<Texture2D>( relativePath + xfurInstanceName + "_" + xfur.MainRenderer.materials[editFurProfile].name.Replace( " ", "_" ) + "_FurColorVariationMap.png" );
                }
            }

            var asset = CreateInstance<FurProfileAsset>();
            tempProfile.version = new Vector3Int( 4, 0, 0 );
            asset.FurProfileData = tempProfile;
            AssetDatabase.CreateAsset( asset, relativePath + xfurInstanceName + "_" + xfur.MainRenderer.materials[editFurProfile].name.Replace( " ", "_" ) + "_Profile.asset" );
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();


            var loadedProfile = (FurProfileAsset)AssetDatabase.LoadAssetAtPath( relativePath + xfurInstanceName + "_" + xfur.MainRenderer.materials[editFurProfile].name.Replace( " ", "_" ) + "_Profile.asset", typeof( FurProfileAsset ) );

            if ( loadedProfile != null ) {
                Debug.Log( "XFur Profile asset created successfully at " + relativePath );
                xfur.ReplaceFurData( editFurProfile, loadedProfile );
            }


            RenderTexture.active = active;

        }


        #endregion

        private void OnDisable() {

            if ( xfurCollider ) {
                DestroyImmediate( xfurCollider.gameObject, true );

                if ( colliderMesh && colliderMesh != xfur.CurrentMesh )
                    DestroyImmediate( colliderMesh );
            }

            ActiveEditorTracker.sharedTracker.isLocked = false;
            Selection.SetActiveObjectWithContext( xfur, xfur );

            if ( xfur.FurDataProfiles[editFurProfile].furDataMap is RenderTexture || xfur.FurDataProfiles[editFurProfile].furGroomingMap is RenderTexture ) {

                if ( EditorUtility.DisplayDialog( "Warning", "Warning: It seems you have unsaved work. Do you want to export it before closing the window?", "Export Now", "Close Anyway" ) ) {
                    ExportProfiles();
                }

            }

            if ( xfur.FurDataProfiles[editFurProfile].furDataMap is RenderTexture ) {                
                var rt = xfur.FurDataProfiles[editFurProfile].furDataMap;
                xfur.FurDataProfiles[editFurProfile].furDataMap = xfurUndo.furData0;
                RenderTexture.ReleaseTemporary( (RenderTexture)rt );
            }

            if ( xfur.FurDataProfiles[editFurProfile].furGroomingMap is RenderTexture ) {
                var rt = xfur.FurDataProfiles[editFurProfile].furGroomingMap;
                xfur.FurDataProfiles[editFurProfile].furGroomingMap = xfurUndo.furData1;
                RenderTexture.ReleaseTemporary( (RenderTexture)rt );
            }

            xfurUndo.Clear();

        }




        void FurProfileSettings() {

            Undo.RecordObject( xfur, "Modified Fur Profile Properties" );
                       
            CenteredLabel( "Common Properties" );

            GUILayout.Space( 16 );


            xfur.FurDataProfiles[editFurProfile].colorMap = ObjectField<Texture>( new GUIContent( "Fur Color Map", "The texture that controls the color / albedo applied over the whole fur surface" ), xfur.FurDataProfiles[editFurProfile].colorMap );

            xfur.FurDataProfiles[editFurProfile].mainTint = EditorGUILayout.ColorField( new GUIContent( "Fur Main Tint", "The main tint to be applied to the fur" ), xfur.FurDataProfiles[editFurProfile].mainTint );

            if ( xfur.Settings.useNormalmap ) {
                GUILayout.Space( 8 );
                xfur.FurDataProfiles[editFurProfile].normalMap = ObjectField<Texture>( new GUIContent( "Normalmap", "The normalmap for the surface" ), xfur.FurDataProfiles[editFurProfile].normalMap );
            }


            if ( xfur.FurDataProfiles[editFurProfile].baseUVTiling == Vector4.zero ) {
                xfur.FurDataProfiles[editFurProfile].baseUVTiling = new Vector4(1,1,0,0);
            }

            GUILayout.Space( 8 );

            Vector2 tiling = EditorGUILayout.Vector2Field( new GUIContent( "Color / Normals Tiling" ), new Vector2( xfur.FurDataProfiles[editFurProfile].baseUVTiling.x, xfur.FurDataProfiles[editFurProfile].baseUVTiling.y ) );
            Vector2 offset = EditorGUILayout.Vector2Field( new GUIContent( "Color / Normals Offset" ), new Vector2( xfur.FurDataProfiles[editFurProfile].baseUVTiling.z, xfur.FurDataProfiles[editFurProfile].baseUVTiling.w ) );


            xfur.FurDataProfiles[editFurProfile].baseUVTiling = new Vector4( tiling.x, tiling.y, offset.x, offset.y );
        
            GUILayout.Space( 8 );
           
            xfur.FurDataProfiles[editFurProfile].furDataMap = ObjectField( new GUIContent( "Fur Data Map", "The texture that controls the parameters of the fur :\n\n R = fur mask\n G = length\n B = occlusion\n A = thickness" ), xfur.FurDataProfiles[editFurProfile].furDataMap );
            xfur.FurDataProfiles[editFurProfile].furGroomingMap = ObjectField( new GUIContent( "Fur Grooming Map", "The texture that controls the direction of the fur :\n\n RGB = absolute fur direction half-normalized in tangent space" ), xfur.FurDataProfiles[editFurProfile].furGroomingMap );

            if ( xfur.FurDataProfiles[editFurProfile].furGroomingMap )
                xfur.FurDataProfiles[editFurProfile].groomStrength = EditorGUILayout.Slider( new GUIContent( "Fur Grooming Strength" ), xfur.FurDataProfiles[editFurProfile].groomStrength, 0, 1f );

            GUILayout.Space( 12 );

            xfur.FurDataProfiles[editFurProfile].furLength = EditorGUILayout.Slider( new GUIContent( "Fur Length", "The maximum overall length of the fur. This will be multiplied by the actual fur profile length and the length painted in XFur Studio™ - Designer" ), xfur.FurDataProfiles[editFurProfile].furLength, 0.01f, 1 );

            GUILayout.Space( 8 );
            xfur.FurDataProfiles[editFurProfile].furThickness = EditorGUILayout.Slider( new GUIContent( "Fur Thickness", "The maximum overall thickness of the fur. This will be multiplied by the actual fur profile thickness and the thickness painted in XFur Studio™ - Designer" ), xfur.FurDataProfiles[editFurProfile].furThickness, 0.01f, 1 );
            xfur.FurDataProfiles[editFurProfile].furThicknessCurve = EditorGUILayout.Slider( new GUIContent( "Thickness Curve", "How the fur strands' thickness bias will change from the root to the top of each strand" ), xfur.FurDataProfiles[editFurProfile].furThicknessCurve, 0, 1 );
            
            GUILayout.Space( 12 );

            xfur.FurDataProfiles[editFurProfile].selfOcclusionTint = EditorGUILayout.ColorField( new GUIContent( "Occlusion Tint" ), xfur.FurDataProfiles[editFurProfile].selfOcclusionTint );

            xfur.FurDataProfiles[editFurProfile].selfOcclusionStrength = EditorGUILayout.Slider( new GUIContent( "Fur Occlusion / Shadowing", "The shadowing applied over the surface of the fur strands as a simple occlusion pass. Multiplied by the per-profile occlusion value and the one painted through XFur Studio™ - Designer" ), xfur.FurDataProfiles[editFurProfile].selfOcclusionStrength, 0, 1 );
            xfur.FurDataProfiles[editFurProfile].selfOcclusionCurve = EditorGUILayout.Slider( new GUIContent( "Fur Occlusion Curve", "How the shadowing / occlusion of the fur will go from the root to the tip of each strand" ), xfur.FurDataProfiles[editFurProfile].selfOcclusionCurve, 0, 1 );

            GUILayout.Space( 8 );
            
            xfur.FurDataProfiles[editFurProfile].roughness = EditorGUILayout.Slider( new GUIContent( "Roughness" ), xfur.FurDataProfiles[editFurProfile].roughness, 0, 1 );
           
            if ( xfur.RenderingResources.currentRenderingPipeline == XFurRenderingPipeline.HDRP || ( xfur.RenderingResources.currentRenderingPipeline == XFurRenderingPipeline.UniversalRP && xfur.Settings.urpAdvancedLighting ) ) {
                xfur.FurDataProfiles[editFurProfile].specularTint = EditorGUILayout.ColorField( new GUIContent( "Specular Tint" ), xfur.FurDataProfiles[editFurProfile].specularTint, true, false, false );
            }

            GUILayout.Space( 12 );

            CenteredLabel( "Color Variation" );

            GUILayout.Space( 12 );

            xfur.FurDataProfiles[editFurProfile].useLegacyColorVariation = EnableDisableToggle( new GUIContent( "Legacy Color Variation" ), xfur.FurDataProfiles[editFurProfile].useLegacyColorVariation || xfur.FurDataProfiles[editFurProfile].legacyColorVariationMap );

            GUILayout.Space( 12 );

            if ( !xfur.FurDataProfiles[editFurProfile].useLegacyColorVariation ) {
                xfur.FurDataProfiles[editFurProfile].noiseShadingTint2 = EditorGUILayout.ColorField( new GUIContent( "Strands (R) Color", "Tint to be applied to the main fur strands" ), xfur.FurDataProfiles[editFurProfile].noiseShadingTint2 );
                xfur.FurDataProfiles[editFurProfile].mainFurStrandBoost = EditorGUILayout.Slider( new GUIContent( "Strands (R) Boost", "Boost to be applied to the main fur strands. Values higher than 1 make it lighter, while values lower than 1 make it darker" ), xfur.FurDataProfiles[editFurProfile].mainFurStrandBoost, 0, 2 );

                GUILayout.Space( 12 );

                xfur.FurDataProfiles[editFurProfile].noiseShadingTint3 = EditorGUILayout.ColorField( new GUIContent( "Strands (G) Color", "Tint to be applied to the secondary fur strands" ), xfur.FurDataProfiles[editFurProfile].noiseShadingTint3 );
                xfur.FurDataProfiles[editFurProfile].secondaryFurStrandBoost = EditorGUILayout.Slider( new GUIContent( "Strands (G) Boost", "Boost to be applied to the secondary fur strands. Values higher than 1 make it lighter, while values lower than 1 make it darker" ), xfur.FurDataProfiles[editFurProfile].secondaryFurStrandBoost, 0.0f, 2 );

            }
            else {
                xfur.FurDataProfiles[editFurProfile].mainFurStrandBoost = EditorGUILayout.Slider( new GUIContent( "Strands (R) Boost", "Boost to be applied to the main fur strands. Values higher than 1 make it lighter, while values lower than 1 make it darker" ), xfur.FurDataProfiles[editFurProfile].mainFurStrandBoost, 0, 2 );
                xfur.FurDataProfiles[editFurProfile].secondaryFurStrandBoost = EditorGUILayout.Slider( new GUIContent( "Strands (G) Boost", "Boost to be applied to the secondary fur strands. Values higher than 1 make it lighter, while values lower than 1 make it darker" ), xfur.FurDataProfiles[editFurProfile].secondaryFurStrandBoost, 0.0f, 2 );
            }


            if ( xfur.FurDataProfiles[editFurProfile].useLegacyColorVariation ) {

                GUILayout.Space( 12 );

                xfur.FurDataProfiles[editFurProfile].legacyColorVariationMap = ObjectField<Texture>( new GUIContent( "Color Variation Mask", "The texture that controls four additional coloring variations to be applied over the fur, either all four to the whole fur or two to the undercoat and two to the overcoat by using the four color channels." ), xfur.FurDataProfiles[editFurProfile].legacyColorVariationMap );


                if ( xfur.FurDataProfiles[editFurProfile].legacyColorVariationMap ) {

                    GUILayout.Space( 8 );
                    xfur.FurDataProfiles[editFurProfile].noiseShadingTint0 = EditorGUILayout.ColorField( new GUIContent( "Fur Color A", "The fur color to be applied on the red channel of the Color Variation map" ), xfur.FurDataProfiles[editFurProfile].noiseShadingTint0 );
                    xfur.FurDataProfiles[editFurProfile].noiseShadingTint1 = EditorGUILayout.ColorField( new GUIContent( "Fur Color B", "The fur color to be applied on the green channel of the Color Variation map" ), xfur.FurDataProfiles[editFurProfile].noiseShadingTint1 );
                    xfur.FurDataProfiles[editFurProfile].noiseShadingTint2 = EditorGUILayout.ColorField( new GUIContent( "Fur Color C", "The fur color to be applied on the blue channel of the Color Variation map" ), xfur.FurDataProfiles[editFurProfile].noiseShadingTint2 );
                    xfur.FurDataProfiles[editFurProfile].noiseShadingTint3 = EditorGUILayout.ColorField( new GUIContent( "Fur Color D", "The fur color to be applied on the alpha channel of the Color Variation map" ), xfur.FurDataProfiles[editFurProfile].noiseShadingTint3 );
                    
                }

            }
            else {
                GUILayout.Space( 12 );
                xfur.FurDataProfiles[editFurProfile].furNoiseShadingTiling = EditorGUILayout.Slider( "Noise Tiling", xfur.FurDataProfiles[editFurProfile].furNoiseShadingTiling, 0.1f, 10f );
                xfur.FurDataProfiles[editFurProfile].noiseShadingTint0 = EditorGUILayout.ColorField( new GUIContent( "Noise Tint A", "The main tint to apply for noise variation" ), xfur.FurDataProfiles[editFurProfile].noiseShadingTint0 );
                xfur.FurDataProfiles[editFurProfile].noiseShadingTint1 = EditorGUILayout.ColorField( new GUIContent( "Noise Tint B", "The secondary tint to apply for noise variation" ), xfur.FurDataProfiles[editFurProfile].noiseShadingTint1 );

            }


            if ( xfur.FurDataProfiles[editFurProfile].emissiveFur ) {

                GUILayout.Space( 12 );

                CenteredLabel( "Emissive Fur" );

                GUILayout.Space( 16 );

                xfur.FurDataProfiles[editFurProfile].emissiveTint = EditorGUILayout.ColorField( new GUIContent( "Emissive Color" ), xfur.FurDataProfiles[editFurProfile].emissiveTint, true, false, true );
                xfur.FurDataProfiles[editFurProfile].emissionMap = ObjectField<Texture>( new GUIContent( "Emission Map" ), xfur.FurDataProfiles[editFurProfile].emissionMap );

            }


            if ( xfur.FurDataProfiles[editFurProfile].useCurlyFur ) {
                GUILayout.Space( 12 );

                CenteredLabel( "Curly Fur" );

                GUILayout.Space( 16 );

                xfur.FurDataProfiles[editFurProfile].curlyFurParameters.x = EditorGUILayout.Slider( new GUIContent( "Curl Amount X" ), xfur.FurDataProfiles[editFurProfile].curlyFurParameters.x, 0, 1 );
                xfur.FurDataProfiles[editFurProfile].curlyFurParameters.y = EditorGUILayout.Slider( new GUIContent( "Curl Amount Y" ), xfur.FurDataProfiles[editFurProfile].curlyFurParameters.y, 0, 1 );
                xfur.FurDataProfiles[editFurProfile].curlyFurParameters.z = EditorGUILayout.Slider( new GUIContent( "Curl Size X" ), xfur.FurDataProfiles[editFurProfile].curlyFurParameters.z, 0, 0.1f );
                xfur.FurDataProfiles[editFurProfile].curlyFurParameters.w = EditorGUILayout.Slider( new GUIContent( "Curl Size Y" ), xfur.FurDataProfiles[editFurProfile].curlyFurParameters.w, 0, 0.1f );

            }

            GUILayout.Space( 16 );

            CenteredLabel( "Rim Lighting" );

            GUILayout.Space( 12 );

            xfur.FurDataProfiles[editFurProfile].rimLightingTint = EditorGUILayout.ColorField( new GUIContent( "Rim Lighting Tint", "The main tint to be applied to the fur's rim lighting" ), xfur.FurDataProfiles[editFurProfile].rimLightingTint );

            xfur.FurDataProfiles[editFurProfile].rimLightingPower = EditorGUILayout.Slider( new GUIContent( "Rim Lighting Power" ), xfur.FurDataProfiles[editFurProfile].rimLightingPower, 0.1f, 10 );

            xfur.FurDataProfiles[editFurProfile].rimLightingStrength = EditorGUILayout.Slider( new GUIContent( "Rim Lighting Strength", "Applies an additional color boost to the fur's rim lighting effect" ), xfur.FurDataProfiles[editFurProfile].rimLightingStrength, 1.0f, 3.0f );

            GUILayout.Space( 12 );

            CenteredLabel( "Per Instance Wind Settings" );

            GUILayout.Space( 12 );

            xfur.FurDataProfiles[editFurProfile].windStrengthMultiplier = EditorGUILayout.Slider( new GUIContent( "Wind Strength Multiplier", "The value by which the global wind strength will be multiplied, useful to fine tune the overall wind strength applied over this instance" ), xfur.FurDataProfiles[editFurProfile].windStrengthMultiplier, 0.0f, 8.0f );

            GUILayout.Space( 32 );



        }



        void SaveLoadChanges() {

            CenteredLabel( "Export & Load Data" );

            GUILayout.Space( 16 );

            exportResolution = EditorGUILayout.Popup( "Export Resolution", exportResolution, new string[] { "256x256 px", "512x512 px", "1024x1024 px", "2048x2048 px" }, pidiSkin2.button );

            GUILayout.Space( 16 );


            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();


            if ( StandardButton( "Export Profile", 200 ) ) {

                ExportProfiles();

            }


            

            GUILayout.Space( 32 );

            if ( StandardButton( "Load Profile", 200 ) ) {
                var path = EditorUtility.OpenFilePanel( "Load Fur Profile Asset", "Assets/", "asset" );

                path = path.Replace( Application.dataPath, "Assets" );

                var asset = (FurProfileAsset)AssetDatabase.LoadAssetAtPath( path, typeof( FurProfileAsset ) );

                if ( asset && asset.GetType() == typeof( FurProfileAsset ) ) {

                    if ( xfur.FurDataProfiles[editFurProfile].furDataMap is RenderTexture || xfur.FurDataProfiles[editFurProfile].furGroomingMap is RenderTexture ) {
                        if ( !EditorUtility.DisplayDialog( "Unsaved Data detected", "You have unsaved work done for this XFur Studio Instance. Loading a Fur profile will overwritte this data in an irreversible way. Do you wish to continue?", "Yes", "No" ) ) {
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();
                            return;
                        }
                        else {
                            if ( xfur.FurDataProfiles[editFurProfile].furDataMap is RenderTexture ) {
                                RenderTexture.ReleaseTemporary( (RenderTexture)xfur.FurDataProfiles[editFurProfile].furDataMap );
                                xfur.FurDataProfiles[editFurProfile].furDataMap = null;
                            }

                            if ( xfur.FurDataProfiles[editFurProfile].furGroomingMap is RenderTexture ) {
                                RenderTexture.ReleaseTemporary( (RenderTexture)xfur.FurDataProfiles[editFurProfile].furGroomingMap );
                                xfur.FurDataProfiles[editFurProfile].furGroomingMap = null;
                            }
                        }
                    }

                    xfur.ReplaceFurData( editFurProfile, asset );


                    if ( xfurUndo.furData0 != xfur.FurDataProfiles[editFurProfile].furDataMap ) {
                        xfurUndo.furData0 = xfur.FurDataProfiles[editFurProfile].furDataMap;
                    }

                    if ( xfurUndo.furData1 != xfur.FurDataProfiles[editFurProfile].furGroomingMap ) {
                        xfurUndo.furData1 = xfur.FurDataProfiles[editFurProfile].furGroomingMap;
                    }


                    Debug.Log( "Successfully loaded XFur Profile" );
                }
                else {
#if XFURDESKTOP_LEGACY

                                         var legacyAsset = (XFurStudio.XFur_CoatingProfile)AssetDatabase.LoadAssetAtPath( path, typeof( XFurStudio.XFur_CoatingProfile ) );

                                         if ( legacyAsset && legacyAsset.GetType() == typeof( XFurStudio.XFur_CoatingProfile ) ) {
                                             xfur.LoadLegacyXFurProfile( i, legacyAsset );
                                             Debug.Log( "Successfully loaded Legacy XFur Profile" );
                                         }
#endif

#if XFurStudioMobile_LEGACY

                                         var legacyAsset = (XFurStudioMobile.XFur_CoatingProfile)AssetDatabase.LoadAssetAtPath( path, typeof( XFurStudioMobile.XFur_CoatingProfile ) );

                                         if ( legacyAsset && legacyAsset.GetType() == typeof( XFurStudioMobile.XFur_CoatingProfile ) ) {
                                             xfur.LoadLegacyXFurProfile( i, legacyAsset );
                                             Debug.Log( "Successfully loaded Legacy XFur Profile" );
                                         }
#endif

                }



            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();


            GUILayout.Space( 32 );

            CenteredLabel( "Bake to mesh" );

            GUILayout.Space( 16 );

            int furMaterials = 0;

            for ( int i = 0; i < xfur.CurrentFurRenderer.isFurMaterial.Length; i++ ) {
                if ( xfur.CurrentFurRenderer.isFurMaterial[i] ) {
                    furMaterials++;
                }
            }

            if ( furMaterials < 2 ) {

                EditorGUILayout.HelpBox( "Warning: Baking the groom and fur data to your mesh is not a reversible operation. Vertex color and the UV3 channel will be overwritten.\n\nFur masking will lose some precision since it will be handled on a per-vertex rather than a per-pixel basis.\n\nOnce the data is baked, you can safely remove all references to the Data and Grooming maps to ensure they are not included in your build.", MessageType.Warning );

                if ( !xfur.CurrentFurRenderer.originalMesh.isReadable || ( xfur.FurDataProfiles[editFurProfile].furDataMap && !xfur.FurDataProfiles[editFurProfile].furDataMap.isReadable) || ( xfur.FurDataProfiles[editFurProfile].furGroomingMap && !xfur.FurDataProfiles[editFurProfile].furGroomingMap.isReadable ) ) {
                    GUILayout.Space( 16 );
                    EditorGUILayout.HelpBox( "This mesh and / or fur data textures are not marked as readable. Please set the Read/Write toggle to true in the Mesh / Texture Import Settings", MessageType.Error );
                }
                else {
                    GUILayout.Space( 16 );

                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();

                    if ( StandardButton( "Bake all Data", 200 ) ) {
                        BakeXFurDataToMesh();
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                }
            }
            else {
                EditorGUILayout.HelpBox( "Baking is only possible for meshes that have a single fur-enabled material. If your model has multiple fur materials, separate data and grooming maps for each material are necessary.", MessageType.Error );
            }

            GUILayout.Space( 32 );

        }




        void BakeXFurDataToMesh() {


            List<Vector4> uvs = new List<Vector4>();

            xfur.CurrentFurRenderer.originalMesh.GetUVs( 0, uvs );

            Color[] colors = new Color[uvs.Count];
            Vector4[] groomUVs = new Vector4[uvs.Count];

            var tex = new Texture2D( 1, 1 );

            for ( int i = 0; i < uvs.Count; i++ ) {
                if ( xfur.FurDataProfiles[editFurProfile].furDataMap ) {
                    if ( xfur.FurDataProfiles[editFurProfile].furDataMap is RenderTexture ) {
                        RenderTexture.active = ( xfur.FurDataProfiles[editFurProfile].furDataMap as RenderTexture );
                        tex.ReadPixels( new Rect( uvs[i].x * xfur.FurDataProfiles[editFurProfile].furDataMap.width, uvs[i].y * xfur.FurDataProfiles[editFurProfile].furDataMap.height, 1, 1 ), 0, 0 );
                        colors[i] = tex.GetPixel( 0, 0 );
                        colors[i] = new Color( Mathf.Pow( colors[i].r, 2.2f ), Mathf.Pow( colors[i].g, 2.2f ), Mathf.Pow( colors[i].b, 2.2f ), Mathf.Pow( colors[i].a, 2.2f ) );
                    }
                    else {
                        colors[i] = ( xfur.FurDataProfiles[editFurProfile].furDataMap as Texture2D ).GetPixel( Mathf.RoundToInt( uvs[i].x * xfur.FurDataProfiles[editFurProfile].furDataMap.width ), Mathf.RoundToInt( uvs[i].y * xfur.FurDataProfiles[editFurProfile].furDataMap.height ) );
                        colors[i] = new Color( Mathf.Pow( colors[i].r, 2.2f ), Mathf.Pow( colors[i].g, 2.2f ), Mathf.Pow( colors[i].b, 2.2f ), Mathf.Pow( colors[i].a, 2.2f ) );
                    }
                }
                else {
                    colors[i] = new Vector4( 1, 1, 1, 1 );
                }

                
                if ( xfur.FurDataProfiles[editFurProfile].furGroomingMap ) {

                    if ( xfur.FurDataProfiles[editFurProfile].furGroomingMap is RenderTexture ) {
                        RenderTexture.active = ( xfur.FurDataProfiles[editFurProfile].furGroomingMap as RenderTexture );
                        tex.ReadPixels( new Rect( uvs[i].x * xfur.FurDataProfiles[editFurProfile].furGroomingMap.width, uvs[i].y * xfur.FurDataProfiles[editFurProfile].furGroomingMap.height, 1, 1 ), 0, 0 );
                        groomUVs[i] = tex.GetPixel( 0, 0 );
                        groomUVs[i] = new Vector4( Mathf.Pow( groomUVs[i].x, 2.2f ), Mathf.Pow( groomUVs[i].y, 2.2f ), Mathf.Pow( groomUVs[i].z, 2.2f ), Mathf.Pow( groomUVs[i].w, 2.2f ) );

                    }
                    else {
                        groomUVs[i] = ( xfur.FurDataProfiles[editFurProfile].furGroomingMap as Texture2D ).GetPixel( Mathf.RoundToInt( uvs[i].x * xfur.FurDataProfiles[editFurProfile].furGroomingMap.width ), Mathf.RoundToInt( uvs[i].y * xfur.FurDataProfiles[editFurProfile].furGroomingMap.height ) );
                        groomUVs[i] = new Vector4( Mathf.Pow( groomUVs[i].x, 2.2f ), Mathf.Pow( groomUVs[i].y, 2.2f ), Mathf.Pow( groomUVs[i].z, 2.2f ), Mathf.Pow( groomUVs[i].w, 2.2f ) );
                    }

                }
                else {
                    groomUVs[i] = new Vector4( 0.5f, 0.5f, 0.5f, 1.0f );
                }

                
            }

            xfur.CurrentFurRenderer.originalMesh.SetColors( colors );
            xfur.CurrentFurRenderer.originalMesh.SetUVs( 2, groomUVs );

            if ( !xfur.CurrentFurRenderer.originalMesh.name.Contains( "_xfurbaked.asset" ) ) {
                var path = EditorUtility.SaveFilePanel( "Export Baked Mesh", "Assets/", xfur.CurrentFurRenderer.originalMesh.name + "_xfurbaked.asset", "asset" );
                
                if ( string.IsNullOrEmpty( path ) ) {
                    return;
                }

                var relativePath = path.Replace( Application.dataPath, "Assets" );

                var newMesh = Instantiate( xfur.CurrentFurRenderer.originalMesh);
                newMesh.name = xfur.CurrentFurRenderer.originalMesh.name + "_xfurbaked.asset";

                AssetDatabase.CreateAsset( newMesh, relativePath );

                xfur.CurrentFurRenderer.originalMesh = newMesh;

                var tRenderer = xfur.MainRenderer;
                tRenderer.originalMesh = newMesh;
                xfur.MainRenderer = tRenderer;
                

                if ( xfur.CurrentFurRenderer.renderer is MeshRenderer ) {
                    ( xfur.CurrentFurRenderer.renderer as MeshRenderer ).GetComponent<MeshFilter>().sharedMesh = newMesh;
                }
                else {
                    ( xfur.CurrentFurRenderer.renderer as SkinnedMeshRenderer ).sharedMesh = newMesh;
                }


            }



            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();



        }




        void MainFurSettings() {

            Undo.RecordObject( xfur, "Modified Main Fur Settings" );

            CenteredLabel( "Main Fur Settings" );

            GUILayout.Space( 16 );

            xfur.Settings.experimentalFeatures = EnableDisableToggle( new GUIContent( "Experimental Features", "Enables or disables the support for experimental features. WARNING : Experimental features are not intended for use in finished projects as they are not yet production ready. They are available for testing purposes and are subject to change between versions" ), xfur.Settings.experimentalFeatures );
            
            GUILayout.Space( 16 );



            CenteredLabel( "Basic Fur Appearance" );

            GUILayout.Space( 16 );

            xfur.FurDataProfiles[editFurProfile].furStrandsAsset = (XFurStudioStrandsAsset)EditorGUILayout.ObjectField( new GUIContent( "Fur Strands Asset", "The texture map used to generate the fur strands for this fur profile" ), xfur.FurDataProfiles[editFurProfile].furStrandsAsset, typeof( XFurStudioStrandsAsset ), false );

            if ( xfur.FurDataProfiles[editFurProfile].furStrandsTexture ) {
                xfur.FurDataProfiles[editFurProfile].furStrandsTexture = (Texture2D)EditorGUILayout.ObjectField( new GUIContent( "Fur Strands Texture", "A direct reference to a procedurally generated strands map (usually for legacy XFur Studio Instances that have been just upgraded" ), xfur.FurDataProfiles[editFurProfile].furStrandsTexture, typeof(Texture2D), false, GUILayout.Height(EditorGUIUtility.singleLineHeight) );
            }

            GUILayout.Space( 8 );


            xfur.FurDataProfiles[editFurProfile].renderingSamples = EditorGUILayout.IntSlider( new GUIContent( "Rendering Samples", "The amount of samples used to render the fur. More samples give better results, but may result in a reduced performance (especially when using ss" ), xfur.FurDataProfiles[editFurProfile].renderingSamples, 4, 128 );


            if ( xfur.FurDataProfiles[editFurProfile].furStrandsAsset || xfur.FurDataProfiles[editFurProfile].furStrandsTexture ) {
                xfur.FurDataProfiles[editFurProfile].furStrandsTiling = EditorGUILayout.FloatField( new GUIContent( "Fur Strands Tiling", "The tiling (UV size) to be applied to the fur strands" ), xfur.FurDataProfiles[editFurProfile].furStrandsTiling );
            }

            GUILayout.Space( 16 );

            xfur.FurDataProfiles[editFurProfile].doubleSided = EnableDisableToggle( new GUIContent( "Double Sided Fur" ), xfur.FurDataProfiles[editFurProfile].doubleSided );

            GUILayout.Space( 16 );

            CenteredLabel( "Fur Lighting" );

            GUILayout.Space( 16 );

            xfur.FurDataProfiles[editFurProfile].useProbes = EnableDisableToggle( new GUIContent( "Use Light Probes" ), xfur.FurDataProfiles[editFurProfile].useProbes, true );
            xfur.FurDataProfiles[editFurProfile].castShadows = EnableDisableToggle( new GUIContent( "Cast Shadows" ), xfur.FurDataProfiles[editFurProfile].castShadows, true );
            xfur.FurDataProfiles[editFurProfile].receiveShadows = EnableDisableToggle( new GUIContent( "Receive Shadows" ), xfur.FurDataProfiles[editFurProfile].receiveShadows, true );





            GUILayout.Space( 16 );

            CenteredLabel( "Additional Features" );

            GUILayout.Space( 16 );

            xfur.FurDataProfiles[editFurProfile].useCurlyFur = EnableDisableToggle( new GUIContent( "Curly Fur" ), xfur.FurDataProfiles[editFurProfile].useCurlyFur );

            xfur.FurDataProfiles[editFurProfile].emissiveFur = EnableDisableToggle( new GUIContent( "Emissive Fur" ), xfur.FurDataProfiles[editFurProfile].emissiveFur );

            GUILayout.Space( 32 );

        }







        void BrushSettingsDrawer() {

            string activeToolName = "Active Tool";

            switch ( brushData.activeTool ) {

                case 2:
                    activeToolName = "Fur Mask -";
                    break;

                case 3:
                    activeToolName = "Fur Length -";
                    break;


                case 4:
                    activeToolName = "Fur Thickness -";
                    break;


                case 5:
                    activeToolName = "Fur Shadowing -";
                    break;


                case 6:
                    activeToolName = "Grooming -";
                    break;

            }

            CenteredLabel( activeToolName + " Brush Settings" );

            GUILayout.Space( 16 );

            EditorGUILayout.HelpBox( "Please remember to export your work before closing this window. Any unexported data will be lost.", MessageType.Warning );

            GUILayout.Space( 16 );

            brushData.fineTuneBrush = EditorGUILayout.Toggle( "Fine Tune Brush Size", brushData.fineTuneBrush );
            GUILayout.Space( 4 );

            if ( brushData.activeTool > 1 && brushData.activeTool < 7 ) {
                brushData.invert = EnableDisableToggle( new GUIContent( "Inverted Effect" ), brushData.invert );
            }

            brushData.mirror = EnableDisableToggle( new GUIContent( "Symmetry Mode" ), brushData.mirror );

            GUILayout.Space( 10 );
            brushData.size = EditorGUILayout.Slider( "Size", brushData.size, brushData.minMaxSize.x, brushData.minMaxSize.y );

            if ( brushData.fineTuneBrush ) {
                brushData.minMaxSize = Vector2Field( new GUIContent( "Min/Max Size" ), brushData.minMaxSize );
                GUILayout.Space( 4 );
            }

            brushData.falloff = EditorGUILayout.Slider( "Falloff", brushData.falloff, 0.01f, 1.0f );
            brushData.opacity = EditorGUILayout.Slider( "Opacity", brushData.opacity, 0.01f, 1.0f );


            GUILayout.Space( 24 );

            var lstyle = new GUIStyle();
            lstyle.fontStyle = FontStyle.Italic;
            lstyle.fontSize = 10;
            lstyle.normal.textColor = new Color( 1, 1, 1, 0.7f );
            lstyle.alignment = TextAnchor.MiddleLeft;

            GUILayout.Label( "Hold Shift + Horizontal Left Click Drag for brush size.", lstyle, GUILayout.Height( 14 ) );
            GUILayout.Label( "Hold Shift + Vertical Left Click Drag for brush falloff.", lstyle, GUILayout.Height( 14 ) );
            GUILayout.Label( "Hold Shift + Horizontal Right Click Drag for brush opacity.", lstyle, GUILayout.Height( 14 ) );

            GUILayout.Space( 8 );

            GUILayout.Label( "Press F to center the Scene View around the XFur Studio Instance.", lstyle, GUILayout.Height( 14 ) );
            GUILayout.Label( "Press X to invert the brush mode, when available.", lstyle, GUILayout.Height( 14 ) );
            GUILayout.Label( "Press S to switch symmetry mode on and off.", lstyle, GUILayout.Height( 14 ) );


            GUILayout.Space( 12 );

            GUILayout.Label( "Press Shift+Z to Undo. Please beware that a Redo function is not implemented.", lstyle, GUILayout.Height( 14 ) );

            GUILayout.Space( 32 );

        }


        void CurrentToolDrawer() {


            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal( pidiSkin2.box, GUILayout.MaxWidth( 512 ) ); GUILayout.Space( 20 );
            GUILayout.BeginVertical();

            GUILayout.Space( 16 );

            switch ( brushData.activeTool ) {

                case 0:
                    MainFurSettings();
                    break;

                case 1:
                    FurProfileSettings();
                    break;

                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                    BrushSettingsDrawer();
                    break;

                case 7:
                    SaveLoadChanges();
                    break;
            }


            GUILayout.EndVertical();
            GUILayout.Space( 20 ); GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();

        }


        void ToolsBox() {

            var smStyle = new GUIStyle();
            smStyle.fontSize = 10;
            smStyle.normal.textColor = new Color( 1, 1, 1, 0.8f );

            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
            GUILayout.BeginVertical( pidiSkin2.box, GUILayout.MaxWidth( 400 ) );

            GUILayout.Space( 12 );

            CenteredLabel( "Tools" );

            GUILayout.Space( 12 );

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();


            GUILayout.BeginVertical( GUILayout.Width( 48 ) );
            if ( GUILayout.Button( genSettings, brushData.activeTool == 0 ? pidiSkin2.customStyles[6] : pidiSkin2.button, GUILayout.Width( 48 ), GUILayout.Height( 48 ) ) ) {
                brushData.activeTool = 0;
            }
            GUILayout.Space( 4 );
            CenteredLabel( "Settings", smStyle );
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical( GUILayout.Width( 48 ) );
            if ( GUILayout.Button( genProps, brushData.activeTool == 1 ? pidiSkin2.customStyles[6] : pidiSkin2.button, GUILayout.Width( 48 ), GUILayout.Height( 48 ) ) ) {
                brushData.activeTool = 1;
            }
            GUILayout.Space( 4 );
            CenteredLabel( "Properties", smStyle );
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical( GUILayout.Width( 48 ) );
            if ( GUILayout.Button( brushShave, brushData.activeTool == 2 ? pidiSkin2.customStyles[6] : pidiSkin2.button, GUILayout.Width( 48 ), GUILayout.Height( 48 ) ) ) {
                brushData.activeTool = 2;
            }
            GUILayout.Space( 4 );
            CenteredLabel( "Fur Mask", smStyle );
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical( GUILayout.Width( 48 ) );
            if ( GUILayout.Button( brushLen, brushData.activeTool == 3 ? pidiSkin2.customStyles[6] : pidiSkin2.button, GUILayout.Width( 48 ), GUILayout.Height( 48 ) ) ) {
                brushData.activeTool = 3;
            }
            GUILayout.Space( 4 );
            CenteredLabel( "Fur Length", smStyle );
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();

            GUILayout.Space( 12 );

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();


            GUILayout.BeginVertical( GUILayout.Width( 48 ) );
            if ( GUILayout.Button( brushThick, brushData.activeTool == 4 ? pidiSkin2.customStyles[6] : pidiSkin2.button, GUILayout.Width( 48 ), GUILayout.Height( 48 ) ) ) {
                brushData.activeTool = 4;
            }
            GUILayout.Space( 4 );
            CenteredLabel( "Thinness", smStyle );
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical( GUILayout.Width( 48 ) );
            if ( GUILayout.Button( brushOcc, brushData.activeTool == 5 ? pidiSkin2.customStyles[6] : pidiSkin2.button, GUILayout.Width( 48 ), GUILayout.Height( 48 ) ) ) {
                brushData.activeTool = 5;
            }
            GUILayout.Space( 4 );
            CenteredLabel( "Shadows", smStyle );
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical( GUILayout.Width( 48 ) );
            if ( GUILayout.Button( brushGroom, brushData.activeTool == 6 ? pidiSkin2.customStyles[6] : pidiSkin2.button, GUILayout.Width( 48 ), GUILayout.Height( 48 ) ) ) {
                brushData.activeTool = 6;
            }
            GUILayout.Space( 4 );
            CenteredLabel( "Grooming", smStyle );
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical( GUILayout.Width( 48 ) );
            if ( GUILayout.Button( exportData, brushData.activeTool == 7 ? pidiSkin2.customStyles[6] : pidiSkin2.button, GUILayout.Width( 48 ), GUILayout.Height( 48 ) ) ) {
                brushData.activeTool = 7;
            }
            GUILayout.Space( 4 );
            CenteredLabel( "Export/Load", smStyle );
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();

            GUILayout.Space( 12 );

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();

        }


        void OnGUI() {

            if ( !xfur ) {
                Close();
            }

            Repaint();

            scrollView = GUILayout.BeginScrollView( scrollView );

            EditorGUIUtility.labelWidth = 200;

            GUILayout.Space( 12 );

            AssetLogoAndVersion();

            GUILayout.Space( 24 );

            GUILayout.BeginHorizontal(); GUILayout.Space( 20 );
            GUILayout.BeginVertical();

            if ( xfur ) {

                ToolsBox();

                GUILayout.Space( 32 );

                CurrentToolDrawer();

                GUILayout.Space( 32 );
               
            }
            else {
                GUILayout.Space( 32 );
            }

            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();

            var lStyle = new GUIStyle( EditorStyles.label );
            lStyle.fontStyle = FontStyle.Italic;
            lStyle.fontSize = 8;

            GUILayout.Label( $"Copyright© 2017-{System.DateTime.Today.Year},   Jorge Pinal N.", lStyle );

            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();


            GUILayout.Space( 32 );


            GUILayout.EndVertical();
            GUILayout.Space( 20 ); GUILayout.EndHorizontal();


            GUILayout.EndScrollView();


        }





        #region PIDI 2020 EDITOR



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
            GUILayout.Label( $"v{xfur.Settings.version.x}.{xfur.Settings.version.y}.{xfur.Settings.version.z}", pidiSkin2.customStyles[2] );
            GUILayout.Space( 6 );
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        /// <summary>
        /// Draws a label centered in the Editor window
        /// </summary>
        /// <param name="label"></param>
        public void CenteredLabel( string label, GUIStyle style = null ) {

            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
            GUILayout.Label( label, style == null ? EditorStyles.boldLabel : style );
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
            currentValue = EditorGUILayout.IntField( currentValue );
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
            currentValue = EditorGUILayout.TextField( currentValue );
            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );

            return currentValue;
        }


        public Vector2 Vector2Field( GUIContent label, Vector2 currentValue ) {

            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            currentValue.x = EditorGUILayout.FloatField( currentValue.x );
            GUILayout.Space( 8 );
            currentValue.y = EditorGUILayout.FloatField( currentValue.y );
            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );

            return currentValue;

        }

        public Vector3 Vector3Field( GUIContent label, Vector3 currentValue ) {

            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            currentValue.x = EditorGUILayout.FloatField( currentValue.x );
            GUILayout.Space( 8 );
            currentValue.y = EditorGUILayout.FloatField( currentValue.y );
            GUILayout.Space( 8 );
            currentValue.z = EditorGUILayout.FloatField( currentValue.z );
            GUILayout.EndHorizontal();
            GUILayout.Space( 2 );

            return currentValue;

        }


        public Vector4 Vector4Field( GUIContent label, Vector4 currentValue ) {

            GUILayout.Space( 2 );
            GUILayout.BeginHorizontal();
            GUILayout.Label( label, pidiSkin2.label, GUILayout.Width( EditorGUIUtility.labelWidth ) );
            currentValue.x = EditorGUILayout.FloatField( currentValue.x );
            GUILayout.Space( 8 );
            currentValue.y = EditorGUILayout.FloatField( currentValue.y );
            GUILayout.Space( 8 );
            currentValue.z = EditorGUILayout.FloatField( currentValue.z );
            GUILayout.Space( 8 );
            currentValue.w = EditorGUILayout.FloatField( currentValue.w );
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



