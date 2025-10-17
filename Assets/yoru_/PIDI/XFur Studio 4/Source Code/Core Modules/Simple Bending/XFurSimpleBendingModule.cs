
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
    public class XFurSimpleBendingModule : XFurStudioModule {

        static readonly int _xfurBendingPower = Shader.PropertyToID( "_XFurBendingPower" );
        static readonly int _xfurBendingStrength = Shader.PropertyToID( "_XFurBendingStrength" );
        static readonly int _xfurBendingColliders = Shader.PropertyToID( "_XFurColliders" );

        public SphereCollider[] bendingSpheres = new SphereCollider[8];

        protected override Vector3Int TargetVersion { get { return new Vector3Int(4,0,0); } }

        Vector4[] _colliders = new Vector4[8];

        [Range(0.5f, 4)] public float bendingPower = 1;
        [Range(0,4)] public float bendingStrength = 1.0f;


#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.SubsystemRegistration )]
        public static void DestroyMaterial() {
           
        }
#endif
        public override void Setup( XFurStudioInstance xfurOwner ) {

            _internalName = "Simple Bending";
            Status = ModuleStatus.Stable;

            base.Setup( xfurOwner );
        
        }


        public override void Load() {

        }

        public override void MainLoop() {

        }


        public override void MainRenderLoop( MaterialPropertyBlock block, int furProfileIndex ) {

            for ( int i = 0; i < _colliders.Length; i++ ) {
                if ( bendingSpheres[i] && _enabled ) {
                    _colliders[i] = new Vector4( bendingSpheres[i].transform.position.x,
                        bendingSpheres[i].transform.position.y,
                        bendingSpheres[i].transform.position.z,
                        bendingSpheres[i].radius );
                }
                else {
                    _colliders[i] = new Vector4( 0, 0, -99999, 0 );
                }
            }


            block.SetFloat( _xfurBendingPower, bendingPower );
            block.SetFloat( _xfurBendingStrength, bendingStrength );
            block.SetVectorArray( _xfurBendingColliders, _colliders );

        }


#if UNITY_EDITOR

        private bool[] folds = new bool[0];

        public override void UpdateModule() {

            _internalName = "Simple Bending";
            Status = ModuleStatus.Stable;
            _version = TargetVersion;            

        }

        public override void ModuleUI( SerializedProperty property ) {
            base.ModuleUI( property );
            GUILayout.Space( 16 );

            bendingStrength = EditorGUILayout.Slider( "Bending Strength", bendingStrength, 0f, 4f );
            bendingPower = EditorGUILayout.Slider( "Bending Power", bendingPower, 0.5f, 4f );

            GUILayout.Space( 8 );

            for (int i = 0; i < 8; i++ ) {
                bendingSpheres[i] = (SphereCollider)EditorGUILayout.ObjectField( "Bending Sphere " + i, bendingSpheres[i], typeof(SphereCollider), true );
            }

            GUILayout.Space( 8 );

        }

#endif


        public override void Unload() {
            UnloadResources();
        }


        public override void UnloadResources() {

        }

    }


}