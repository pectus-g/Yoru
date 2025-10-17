/*

XFur Studio™, by Irreverent Software™
Copyright© 2018-2025, Jorge Pinal Negrete. All Rights Reserved.

*/


namespace XFurStudio.Core {

    using UnityEngine;
    using XFurStudio.Modules;

    public abstract class XFurStudioModuleAsset : ScriptableObject {

        [field:SerializeField][field:HideInInspector] public bool isRuntimeReady { get; protected set; }

        public abstract XFurStudioModule Module { get; }

        public XFurStudioModuleAsset CreateRuntime() {

            var instance = Instantiate( this );
            instance.isRuntimeReady = true;
            instance.name = this.name+"_Runtime";
            return instance;

        }

#if UNITY_EDITOR

        public virtual void ModuleUI() {

        }

#endif


    }


}