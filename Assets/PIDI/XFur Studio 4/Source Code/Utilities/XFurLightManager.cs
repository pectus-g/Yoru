/*

XFur Studio™, by Irreverent Software™
Copyright© 2018-2025, Jorge Pinal Negrete. All Rights Reserved.

*/

namespace XFurStudio.Utilities {

    using UnityEngine;

    [ExecuteAlways]
    public class XFurLightManager : MonoBehaviour {

        public Light mainLight;

        private void Update() {
            
            if ( mainLight ) {
                Shader.SetGlobalVector( "_XFurMainStandardLightDir", mainLight.transform.forward );
                Shader.SetGlobalColor( "_XFurMainStandardLightColor", mainLight.color * mainLight.intensity );
            }
            else {
                Shader.SetGlobalVector( "_XFurMainStandardLightDir", Vector3.zero );
                Shader.SetGlobalColor( "_XFurMainStandardLightColor", Color.black );
            }

        }


        private void OnDisable() {
            Shader.SetGlobalVector( "_XFurMainStandardLightDir", Vector3.forward );
            Shader.SetGlobalColor( "_XFurMainStandardLightColor", Color.black );
        }

    }
}