/*

XFur Studio™, by Irreverent Software™
Copyright© 2018-2025, Jorge Pinal Negrete. All Rights Reserved.

*/


namespace XFurStudio.Core {

    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;


    [CreateAssetMenu( fileName = "New Fur Profile Asset", menuName = "XFur Studio 4/New Fur Profile Asset" )]
    public partial class FurProfileAsset : ScriptableObject {
        

#if UNITY_EDITOR
        public string Version { get { return $"v{FurProfileData.version.x}.{FurProfileData.version.y}.{FurProfileData.version.z}"; } }
#endif

        public FurProfileData FurProfileData;


        private void OnEnable() {
           
            if ( FurProfileData.version != new Vector3Int( 4, 0, 0 ) && FurProfileData.version.x < 3 && FurProfileData.furLength < 0.01f && !FurProfileData.furDataMap && !FurProfileData.furGroomingMap ) {
                FurProfileData = new FurProfileData( new Vector3Int( 4, 0, 0 ) );
            }
        }

        private void OnValidate() {
            
            if ( FurProfileData.version != new Vector3Int( 4, 0, 0 ) && FurProfileData.version.x < 3 && FurProfileData.furLength < 0.01f && !FurProfileData.furDataMap && !FurProfileData.furGroomingMap ) {
                FurProfileData = new FurProfileData( new Vector3Int( 4, 0, 0 ) );
            }
            else if ( FurProfileData.version != new Vector3Int( 4, 0, 0 ) ) {
                FurProfileData.Update( new Vector3Int( 4, 0, 0 ) );
            }

        }

    }

}