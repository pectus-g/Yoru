/*

XFur Studio™, by Irreverent Software™
Copyright© 2018-2025, Jorge Pinal Negrete. All Rights Reserved.

*/

namespace XFurStudio.Core {

    using UnityEngine;

    public static class XFurStudioAPI {


        public enum PaintDataMode { FurMask, FurLength, FurThickness, FurOcclusion, FurColor, SnowFX, BloodFX, WaterFX, FurGrooming }

        public static Material FillerMaterial, PainterMaterial, UnwrapMaterial;


        public static void LoadPaintResources() {
            if ( !PainterMaterial ) {
                PainterMaterial = new Material( Shader.Find( "Hidden/XFur Studio 4/Designer/Painter" ) );
            }

            if ( !FillerMaterial ) {
                FillerMaterial = new Material( Shader.Find( "Hidden/XFur Studio 4/Designer/Filler" ) );
            }

            if ( !UnwrapMaterial ) {
                UnwrapMaterial = new Material( Shader.Find( "Hidden/XFur Studio 4/Designer/Auto Unwrap" ) );
            }


        }





        /// <summary>
        /// Allows dynamic grooming of the fur of an XFur Studio instance through code at runtime
        /// </summary>
        /// <param name="target">The target XFur Studio instance whose fur will be groomed</param>
        /// <param name="matIndex">The index of the material to be edited</param>
        /// <param name="brushCenter">The center of the brush sphere, in world coordinates</param>
        /// <param name="brushNormal">The normal / direction in which the brush is looking, in world coordinates</param>
        /// <param name="brushSize"> The size of the brush sphere in world units</param>
        /// <param name="brushOpacity">The opacity of the brush as a float between 0 and 1</param>
        /// <param name="brushHardness">The hardness of the brush as a float between 0 and 1</param>
        /// <param name="groomDirection">The direction in world coordinates in which the fur will be groomed</param>
        /// <param name="invert">If true, grooming data will be removed rather than applied</param>
        public static void Groom( XFurStudioInstance target, int matIndex, Vector3 brushCenter, Vector3 brushNormal, float brushSize, float brushOpacity, float brushHardness, Vector3 groomDirection, bool invert = false ) {

            LoadPaintResources();


            RenderTexture sourceTex = null;

            if ( target.FurDataProfiles[matIndex].furGroomingMap ) {
                if ( target.FurDataProfiles[matIndex].furGroomingMap.GetType() != typeof( RenderTexture ) ) {
                    sourceTex = RenderTexture.GetTemporary( target.FurDataProfiles[matIndex].furGroomingMap.width, target.FurDataProfiles[matIndex].furGroomingMap.height );
                    Graphics.Blit( target.FurDataProfiles[matIndex].furGroomingMap, (RenderTexture)sourceTex );
                    target.FurDataProfiles[matIndex].furGroomingMap = sourceTex;
                }
                else {
                    sourceTex = (RenderTexture)target.FurDataProfiles[matIndex].furGroomingMap;
                }
            }
            else {
                sourceTex = RenderTexture.GetTemporary( 1024, 1024, 24, RenderTextureFormat.ARGB32 );
                FillTexture( target, new Color( 0.5f, 0.5f, 0.5f, 0.0f ), sourceTex, new Color( 0.0f, 0.0f, 0.0f, 0.0f ), matIndex );
                //FillUVCracks( target, sourceTex, matIndex );
                target.FurDataProfiles[matIndex].furGroomingMap = sourceTex;
            }


            var mRoot = target.transform.root;
            var mRend = target.CurrentFurRenderer.renderer.transform;

            var tempPos = mRoot.position;
            var tempRot = mRoot.rotation;
            var tempScale = mRoot.localScale;

            mRoot.position = mRend.position;
            mRoot.rotation = mRend.rotation;
            mRoot.localScale = mRend.localScale;

            var targetMatrix = mRoot.localToWorldMatrix;
            var targetMesh = target.CurrentMesh;

            mRoot.position = tempPos;
            mRoot.rotation = tempRot;
            mRoot.localScale = tempScale;

            PainterMaterial.SetVector( "_BrushPosition", brushCenter );
            PainterMaterial.SetFloat( "_BrushOpacity", brushOpacity );
            PainterMaterial.SetFloat( "_BrushHardness", brushHardness );
            PainterMaterial.SetColor( "_BrushColor", new Vector4( groomDirection.x, groomDirection.y, groomDirection.z, 1 ) );
            PainterMaterial.SetFloat( "_BrushSize", brushSize );
            PainterMaterial.SetVector( "_BrushNormal", brushNormal );
            PainterMaterial.SetFloat( "_BrushMixMode", invert ? 1 : 0 );

            RenderTextureDescriptor rd = new RenderTextureDescriptor( sourceTex.width, sourceTex.height, RenderTextureFormat.ARGB32, 24 );

            var tempRT0 = RenderTexture.GetTemporary( rd );

            var tempRT1 = RenderTexture.GetTemporary( rd );

            Graphics.Blit( sourceTex, tempRT0 );
            Graphics.Blit( sourceTex, tempRT1 );

            PainterMaterial.SetTexture( "_InputMap", tempRT0 );

            var currentActive = RenderTexture.active;
            RenderTexture.active = tempRT1;
            PainterMaterial.SetPass( 1 );
            Graphics.DrawMeshNow( targetMesh, targetMatrix, matIndex );
            RenderTexture.active = currentActive;
            XFurStudioAPI.FillUVCracks( target, tempRT1, matIndex );

            Graphics.Blit( tempRT1, (RenderTexture)sourceTex );


            RenderTexture.ReleaseTemporary( tempRT0 );
            RenderTexture.ReleaseTemporary( tempRT1 );


        }






        /// <summary>
        /// Allows dynamic data painting for the different fur property maps of an XFur Instance using a virtual world space based brush
        /// </summary>
        /// <param name="target"> The XFur Studio Instance that will be painted</param>
        /// <param name="painterMode">The data that will be painted in</param>
        /// <param name="matIndex">The Fur Material Index that will be affected</param>
        /// <param name="brushCenter">The center of the brush, as a world space point</param>
        /// <param name="brushNormal">The inverse of the direction in which the paint is being applied (used to avoid backface spill)</param>
        /// <param name="brushSize">The radius (in world space) of the brush</param>
        /// <param name="brushOpacity">The opacity of the brush stroke</param>
        /// <param name="brushHardness">The radial hardness of the brush</param>
        /// <param name="brushColor">The color of the brush. For fur data values and masks use either white or black </param>
        public static void Paint( XFurStudioInstance target, PaintDataMode painterMode, int matIndex, Vector3 brushCenter, Vector3 brushNormal, float brushSize, float brushOpacity, float brushHardness, Color brushColor ) {

            LoadPaintResources();

            RenderTexture sourceTex = null;
            Vector4 channelsMask = Vector4.zero;

            var rd = new RenderTextureDescriptor( 1024, 1024, RenderTextureFormat.ARGB32 );


            var mRoot = target.transform.root;
            var mRend = target.CurrentFurRenderer.renderer.transform;

            var tempPos = mRoot.position;
            var tempRot = mRoot.rotation;
            var tempScale = mRoot.localScale;

            mRoot.position = mRend.position;
            mRoot.rotation = mRend.rotation;
            mRoot.localScale = mRend.localScale;

            var targetMatrix = mRoot.localToWorldMatrix;
            var targetMesh = target.CurrentMesh;

            mRoot.position = tempPos;
            mRoot.rotation = tempRot;
            mRoot.localScale = tempScale;


            PainterMaterial.SetFloat( "_BrushMixMode", 0 );

            switch ( painterMode ) {
                case PaintDataMode.FurMask:
                    brushHardness = 1;
                    brushOpacity = 1;
                    channelsMask.x = 1;

                    if ( target.FurDataProfiles[matIndex].furDataMap ) {
                        if ( target.FurDataProfiles[matIndex].furDataMap.GetType() != typeof( RenderTexture ) ) {
                            sourceTex = RenderTexture.GetTemporary( target.FurDataProfiles[matIndex].furDataMap.width, target.FurDataProfiles[matIndex].furDataMap.height );
                            Graphics.Blit( target.FurDataProfiles[matIndex].furDataMap, sourceTex );
                            target.FurDataProfiles[matIndex].furDataMap = sourceTex;
                        }
                        else {
                            sourceTex = (RenderTexture)target.FurDataProfiles[matIndex].furDataMap;
                        }
                    }
                    else {
                        sourceTex = RenderTexture.GetTemporary( rd );
                        FillTexture( target, Color.white, sourceTex, matIndex );
                        target.FurDataProfiles[matIndex].furDataMap = sourceTex;
                    }

                    break;

                case PaintDataMode.FurLength:
                    channelsMask.y = 1;

                    if ( target.FurDataProfiles[matIndex].furDataMap ) {
                        if ( target.FurDataProfiles[matIndex].furDataMap.GetType() != typeof( RenderTexture ) ) {
                            sourceTex = RenderTexture.GetTemporary( target.FurDataProfiles[matIndex].furDataMap.width, target.FurDataProfiles[matIndex].furDataMap.height, 24 );
                            Graphics.Blit( target.FurDataProfiles[matIndex].furDataMap, (RenderTexture)sourceTex );
                            target.FurDataProfiles[matIndex].furDataMap = sourceTex;
                        }
                        else {
                            sourceTex = (RenderTexture)target.FurDataProfiles[matIndex].furDataMap;
                        }
                    }
                    else {
                        sourceTex = RenderTexture.GetTemporary( rd );
                        FillTexture( target, Color.white, sourceTex, matIndex );
                        target.FurDataProfiles[matIndex].furDataMap = sourceTex;
                    }

                    break;

                case PaintDataMode.FurThickness:
                    channelsMask.w = 1;

                    brushColor.a = brushColor.r;

                    if ( target.FurDataProfiles[matIndex].furDataMap ) {
                        if ( target.FurDataProfiles[matIndex].furDataMap.GetType() != typeof( RenderTexture ) ) {
                            sourceTex = RenderTexture.GetTemporary( target.FurDataProfiles[matIndex].furDataMap.width, target.FurDataProfiles[matIndex].furDataMap.height, 24 );
                            Graphics.Blit( target.FurDataProfiles[matIndex].furDataMap, (RenderTexture)sourceTex );
                            target.FurDataProfiles[matIndex].furDataMap = sourceTex;
                        }
                        else {
                            sourceTex = (RenderTexture)target.FurDataProfiles[matIndex].furDataMap;
                        }
                    }
                    else {
                        sourceTex = RenderTexture.GetTemporary( rd );
                        FillTexture( target, Color.white, sourceTex, matIndex );
                        target.FurDataProfiles[matIndex].furDataMap = sourceTex;
                    }

                    break;

                case PaintDataMode.FurOcclusion:
                    channelsMask.z = 1;

                    if ( target.FurDataProfiles[matIndex].furDataMap ) {
                        if ( target.FurDataProfiles[matIndex].furDataMap.GetType() != typeof( RenderTexture ) ) {
                            sourceTex = RenderTexture.GetTemporary( target.FurDataProfiles[matIndex].furDataMap.width, target.FurDataProfiles[matIndex].furDataMap.height, 24 );
                            Graphics.Blit( target.FurDataProfiles[matIndex].furDataMap, (RenderTexture)sourceTex );
                            target.FurDataProfiles[matIndex].furDataMap = sourceTex;
                        }
                        else {
                            sourceTex = (RenderTexture)target.FurDataProfiles[matIndex].furDataMap;
                        }
                    }
                    else {
                        sourceTex = RenderTexture.GetTemporary( rd );
                        FillTexture( target, Color.white, sourceTex, matIndex );
                        target.FurDataProfiles[matIndex].furDataMap = sourceTex;
                    }

                    break;

                case PaintDataMode.FurColor:
                    channelsMask.x = channelsMask.y = channelsMask.z = 1;

                    if ( target.FurDataProfiles[matIndex].colorMap ) {
                        if ( target.FurDataProfiles[matIndex].colorMap.GetType() != typeof( RenderTexture ) ) {
                            sourceTex = RenderTexture.GetTemporary( target.FurDataProfiles[matIndex].colorMap.width, target.FurDataProfiles[matIndex].colorMap.height, 24 );
                            Graphics.Blit( target.FurDataProfiles[matIndex].colorMap, (RenderTexture)sourceTex );
                            target.FurDataProfiles[matIndex].colorMap = sourceTex;
                        }
                        else {
                            sourceTex = (RenderTexture)target.FurDataProfiles[matIndex].colorMap;
                        }
                    }
                    else {
                        sourceTex = RenderTexture.GetTemporary( rd );
                        FillTexture( target, Color.white, sourceTex, matIndex );
                        target.FurDataProfiles[matIndex].colorMap = sourceTex;
                    }
                    break;


                case PaintDataMode.SnowFX:

                    channelsMask.x = 0;
                    channelsMask.y = 1;
                    channelsMask.z = 0;
                    channelsMask.w = 0;

                    PainterMaterial.SetFloat( "_BrushMixMode", brushColor.r > 0 ? 1 : 0 );

                    if ( target.VFXModule.IsEnabled ) {
                        sourceTex = target.VFXModule.VFXTexture[matIndex];
                    }
                    else {
                        return;
                    }

                    break;


                case PaintDataMode.BloodFX:

                    channelsMask.x = 1;
                    channelsMask.y = 0;
                    channelsMask.z = 0;
                    channelsMask.w = 0;

                    PainterMaterial.SetFloat( "_BrushMixMode", brushColor.r > 0 ? 1 : 0 );

                    if ( target.VFXModule.IsEnabled ) {
                        sourceTex = target.VFXModule.VFXTexture[matIndex];
                    }
                    else {
                        return;
                    }

                    break;

                case PaintDataMode.WaterFX:

                    channelsMask.x = 0;
                    channelsMask.y = 0;
                    channelsMask.z = 1;
                    channelsMask.w = 0;

                    PainterMaterial.SetFloat( "_BrushMixMode", brushColor.r > 0 ? 1 : 0 );

                    if ( target.VFXModule.IsEnabled ) {
                        sourceTex = target.VFXModule.VFXTexture[matIndex];
                    }
                    else {
                        return;
                    }

                    break;

            }



            PainterMaterial.SetVector( "_BrushPosition", brushCenter );
            PainterMaterial.SetFloat( "_BrushOpacity", brushOpacity );
            PainterMaterial.SetFloat( "_BrushHardness", brushHardness );
            PainterMaterial.SetColor( "_BrushColor", brushColor );
            PainterMaterial.SetFloat( "_BrushSize", brushSize );
            PainterMaterial.SetVector( "_BrushNormal", brushNormal );
            PainterMaterial.SetFloat( "_PaintR", channelsMask.x );
            PainterMaterial.SetFloat( "_PaintG", channelsMask.y );
            PainterMaterial.SetFloat( "_PaintB", channelsMask.z );
            PainterMaterial.SetFloat( "_PaintA", channelsMask.w );


            rd = new RenderTextureDescriptor( sourceTex.width, sourceTex.height, RenderTextureFormat.ARGB32, 24 );
            var tempRT0 = RenderTexture.GetTemporary( rd );

            var tempRT1 = RenderTexture.GetTemporary( rd );

            Graphics.Blit( sourceTex, tempRT0 );

            PainterMaterial.SetTexture( "_InputMap", tempRT0 );

            var currentActive = RenderTexture.active;
            RenderTexture.active = tempRT1;
            GL.Clear( true, true, new Color( 0, 0, 0, 0 ) );
            PainterMaterial.SetPass( 0 );
            Graphics.DrawMeshNow( targetMesh, targetMatrix, matIndex );
            RenderTexture.active = currentActive;
            XFurStudioAPI.FillUVCracks( target, tempRT1, matIndex );

            Graphics.Blit( tempRT1, (RenderTexture)sourceTex );


            RenderTexture.ReleaseTemporary( tempRT0 );
            RenderTexture.ReleaseTemporary( tempRT1 );

        }

        /// <summary>
        /// Fills a texture with the given color within the UV islands of the given XFur Studio Instance
        /// </summary>
        /// <param name="target"></param>
        /// <param name="color"></param>
        /// <param name="sourceTex"></param>
        /// <param name="backgroundColor"></param>
        /// <param name="matIndex"></param>
        public static void FillTexture( XFurStudioInstance target, Color color, RenderTexture sourceTex, Color backgroundColor, int matIndex = 0 ) {

            LoadPaintResources();

            var rd = new RenderTextureDescriptor( sourceTex.width, sourceTex.height, sourceTex.format, sourceTex.depth );

            var temp0 = RenderTexture.GetTemporary( rd );


            var mRoot = target.transform.root;
            var mRend = target.CurrentFurRenderer.renderer.transform;

            var tempPos = mRoot.position;
            var tempRot = mRoot.rotation;
            var tempScale = mRoot.localScale;

            mRoot.position = mRend.position;
            mRoot.rotation = mRend.rotation;
            mRoot.localScale = mRend.localScale;

            var targetMatrix = mRoot.localToWorldMatrix;
            var targetMesh = target.CurrentMesh;

            if ( !targetMesh ) {
                return;
            }

            mRoot.position = tempPos;
            mRoot.rotation = tempRot;
            mRoot.localScale = tempScale;



            var currentActive = RenderTexture.active;
            RenderTexture.active = temp0;
            GL.Clear( true, true, backgroundColor );
            UnwrapMaterial.SetVector( "_FillColor", color );
            UnwrapMaterial.SetPass( 0 );
            Graphics.DrawMeshNow( targetMesh, targetMatrix, matIndex );
            RenderTexture.active = currentActive;
            Graphics.Blit( temp0, (RenderTexture)sourceTex );
            RenderTexture.ReleaseTemporary( temp0 );

        }

        static void FillTexture( XFurStudioInstance target, Color color, RenderTexture sourceTex, int matIndex = 0 ) {

            LoadPaintResources();


            if ( !target.CurrentMesh ) {
                return;
            }


            var rd = new RenderTextureDescriptor( sourceTex.width, sourceTex.height, sourceTex.format, sourceTex.depth );
            var tempRT0 = RenderTexture.GetTemporary( rd );



            var mRoot = target.transform.root;
            var mRend = target.CurrentFurRenderer.renderer.transform;

            var tempPos = mRoot.position;
            var tempRot = mRoot.rotation;
            var tempScale = mRoot.localScale;

            mRoot.position = mRend.position;
            mRoot.rotation = mRend.rotation;
            mRoot.localScale = mRend.localScale;

            var targetMatrix = mRoot.localToWorldMatrix;
            var targetMesh = target.CurrentMesh;

            mRoot.position = tempPos;
            mRoot.rotation = tempRot;
            mRoot.localScale = tempScale;



            var currentActive = RenderTexture.active;
            RenderTexture.active = tempRT0;
            GL.Clear( true, true, color );
            UnwrapMaterial.SetColor( "_FillColor", color );
            UnwrapMaterial.SetPass( 0 );
            Graphics.DrawMeshNow( targetMesh, targetMatrix, matIndex );
            RenderTexture.active = currentActive;
            Graphics.Blit( tempRT0, sourceTex );
            RenderTexture.ReleaseTemporary( tempRT0 );

        }

        /// <summary>
        /// Expands a given texture to attempt to remove the gaps between UV islands, also known as UV cracks
        /// </summary>
        /// <param name="target"></param>
        /// <param name="sourceTex"></param>
        /// <param name="matIndex"></param>
        public static void FillUVCracks( XFurStudioInstance target, RenderTexture sourceTex, int matIndex = 0 ) {

            LoadPaintResources();
            var rd = new RenderTextureDescriptor( sourceTex.width, sourceTex.height, sourceTex.format, sourceTex.depth );
            var tempRT0 = RenderTexture.GetTemporary( rd );
            tempRT0.wrapMode = sourceTex.wrapMode;
            var tempRT1 = RenderTexture.GetTemporary( rd );
            tempRT1.wrapMode = sourceTex.wrapMode;
            var uvLayout = RenderTexture.GetTemporary( rd );
            uvLayout.wrapMode = sourceTex.wrapMode;

            FillTexture( target, Color.green, uvLayout, Color.black, matIndex );

            FillTexture( target, Color.clear, tempRT0, Color.clear, matIndex );

            Graphics.Blit( sourceTex, tempRT0 );

            if ( FillerMaterial ) {
                FillerMaterial.SetTexture( "_UVLayout", uvLayout );
                Graphics.Blit( tempRT0, tempRT1, FillerMaterial );
            }
            Graphics.Blit( tempRT1, sourceTex );

            RenderTexture.ReleaseTemporary( tempRT0 );
            RenderTexture.ReleaseTemporary( tempRT1 );
            RenderTexture.ReleaseTemporary( uvLayout );

        }


    }




}
