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

    public class XFurCustomModuleExample : XFurStudioModule {
        
        protected override Vector3Int TargetVersion { get { return new Vector3Int(4,0,0); } }

        public override void Setup( XFurStudioInstance xfurOwner ) {
            base.Setup( xfurOwner );

            _internalName = "Custom Module Example";

        }


        public override void Load() {

        }


        public override void MainLoop() {

        }

        public override void MainRenderLoop( MaterialPropertyBlock block, int furProfileIndex ) {


        }



        public override void Unload() {

        }


        public override void UnloadResources() {

        }

#if UNITY_EDITOR

        public override void ModuleUI() {

            EditorGUILayout.HelpBox( "This is the custom UI for this module. You can set up your own GUI in here", MessageType.Info );

        }

#endif


    }


}
