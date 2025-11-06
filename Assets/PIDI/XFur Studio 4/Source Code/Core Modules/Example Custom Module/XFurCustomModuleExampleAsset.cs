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


    [CreateAssetMenu(menuName = "XFur Studio 4/Examples/Custom Module Example")]
    public class XFurCustomModuleExampleAsset : XFurStudioModuleAsset {

        public override XFurStudioModule Module { get { return _customModule; } }

        [SerializeField] protected XFurCustomModuleExample _customModule = new XFurCustomModuleExample();


#if UNITY_EDITOR

        public override void ModuleUI() {

            Undo.RecordObject( this, GetInstanceID().ToString() );

            _customModule.ModuleUI();



        }

#endif


    }

}
