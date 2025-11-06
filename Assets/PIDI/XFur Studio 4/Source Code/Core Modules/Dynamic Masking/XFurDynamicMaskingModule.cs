/*

XFur Studio™, by Irreverent Software™
Copyright© 2018-2025, Jorge Pinal Negrete. All Rights Reserved.

*/

namespace XFurStudio.Modules {

    using XFurStudio.Core;
    using UnityEngine;
    using System.Collections.Generic;

#if UNITY_EDITOR
    using UnityEditor;
#endif

    [System.Serializable]
    public class XFurDynamicMaskingModule : XFurStudioModule {

        protected override Vector3Int TargetVersion { get { return new Vector3Int(4,0,0); } }

        public List<Texture2D> _dynamicMasks = new List<Texture2D>();
        public static Material simpleMixMat;
        public Shader simpleMixShader;

        protected RenderTexture _dynamicMask;

        public override void Setup( XFurStudioInstance xfurOwner ) {
                        
            _internalName = "Dynamic Masking";
            Status = ModuleStatus.Stable;

            base.Setup( xfurOwner );

        }


        public override void Enable() {
            base.Enable();

            if ( !simpleMixShader ) {
                simpleMixShader = Shader.Find( "Hidden/XFur Studio 4/Designer/SimpleMix" );
                simpleMixMat = new Material( simpleMixShader );
            }


            if ( !simpleMixMat ) {
                simpleMixMat = new Material( simpleMixShader );
            }

        }

        public override void Load() {
                        
            if ( !simpleMixShader ) {
                simpleMixShader = Shader.Find( "Hidden/XFur Studio 4/Designer/SimpleMix" );
                simpleMixMat = new Material( simpleMixShader );
            }

            if ( !simpleMixMat ) {
                simpleMixMat = new Material( simpleMixShader );
            }

            for ( int i = 0; i < _dynamicMasks.Count; i++ ) {
                if ( !_dynamicMasks[i] ) {
                    _dynamicMasks[i] = Texture2D.whiteTexture;
                }
            }

        }


        public override void Disable() {
            base.Disable();

            if ( _dynamicMask ) {
                RenderTexture.ReleaseTemporary( _dynamicMask );
            }

        }

        public override void Unload() {

            if ( _dynamicMask ) {
                RenderTexture.ReleaseTemporary( _dynamicMask );
            }

        }


        public override void MainLoop() {

        }


        public override void MainRenderLoop( MaterialPropertyBlock block, int furProfileIndex ) {

            if ( Status == ModuleStatus.CriticalError || !_enabled || !simpleMixShader ) {
                return;
            }



            if ( !simpleMixMat ) {
                simpleMixMat = new Material( simpleMixShader );
            }


            if ( _dynamicMask ) {
                RenderTexture.ReleaseTemporary( _dynamicMask );
            }



            RenderTexture t00;
            RenderTexture t01;

            if ( _xfurInstance.FurDataProfiles[furProfileIndex].furDataMap ) {
                _dynamicMask = RenderTexture.GetTemporary( _xfurInstance.FurDataProfiles[furProfileIndex].furDataMap.width, _xfurInstance.FurDataProfiles[furProfileIndex].furDataMap.height );
                t00 = RenderTexture.GetTemporary( _dynamicMask.width, _dynamicMask.height );
                t01 = RenderTexture.GetTemporary( _dynamicMask.width, _dynamicMask.height );
                Graphics.Blit( _xfurInstance.FurDataProfiles[furProfileIndex].furDataMap, t00 );
            }
            else {
                _dynamicMask = RenderTexture.GetTemporary( 512, 512 );
                t00 = RenderTexture.GetTemporary( 512, 512 );
                t01 = RenderTexture.GetTemporary( 512, 512 );
                XFurStudioAPI.FillTexture( _xfurInstance, new Color(1,1,1,1), t00, Color.white, furProfileIndex );
            }



            if ( _dynamicMasks.Count < 1 )
                Graphics.Blit( t00, _dynamicMask );

            for ( int i = 0; i < _dynamicMasks.Count; i++ ) {

                if ( !_dynamicMasks[i] ) {
                    _dynamicMasks[i] = Texture2D.whiteTexture;
                }

                Graphics.Blit( _dynamicMasks[i], t01 );

                simpleMixMat.SetTexture( "_MaskA", t01 );

                Graphics.Blit( t00, _dynamicMask, simpleMixMat );

                Graphics.Blit( _dynamicMask, t00 );

            }

            RenderTexture.ReleaseTemporary( t00 );
            RenderTexture.ReleaseTemporary( t01 );

            block.SetTexture( XFurShaderProperties.xfurParamData, _dynamicMask );


        }

        public override void UnloadResources() {

        }


#if UNITY_2019_3_OR_NEWER

        [RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.SubsystemRegistration )]
        private static void UnloadStaticResources() {
            if ( simpleMixMat ) {
                Object.DestroyImmediate( simpleMixMat );
            }
            simpleMixMat = null;

        }

#endif

#if UNITY_EDITOR

        public override void UpdateModule() {

            _internalName = "Dynamic Masks";
            Status = ModuleStatus.Stable;
            _version = TargetVersion;

            if ( !simpleMixShader ) {
                simpleMixShader = Shader.Find( "Hidden/XFur Studio 4/Designer/SimpleMix" );

                if ( !simpleMixShader ) {
                    Status = ModuleStatus.CriticalError;
                    Debug.LogError( "Critical Error on the Dynamic Masks Module : The Simple Mix shader has not been found. Please re-import the asset in order to restore the missing files" );
                }

                simpleMixMat = new Material( simpleMixShader );
            }

        }


        public override void ModuleUI( SerializedProperty property ) {

            base.ModuleUI( property );

            GUILayout.Space( 12 );


            if ( BeginCenteredGroup( "Grayscale Masks", ref modFolds[0] ) ) {

                GUILayout.Space( 12 );

                for ( int i = 0; i < _dynamicMasks.Count; i++ ) {
                    GUILayout.BeginHorizontal();
                    _dynamicMasks[i] = (Texture2D)EditorGUILayout.ObjectField( $"Mask {i}", _dynamicMasks[i], typeof( Texture2D ), false, GUILayout.Height( EditorGUIUtility.singleLineHeight ) );
                    GUILayout.Space( 12 );
                    if ( GUILayout.Button( "X", GUILayout.Width( 20 ) ) ) {
                        _dynamicMasks.RemoveAt( i );
                        GUILayout.EndHorizontal();
                        break;
                    }
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space( 12 );

                if ( CenteredButton( "Add New Mask", 200 ) ) {
                    _dynamicMasks.Add( null );
                }

                GUILayout.Space( 12 );
            }
            EndCenteredGroup();

        }

#endif


    }


}