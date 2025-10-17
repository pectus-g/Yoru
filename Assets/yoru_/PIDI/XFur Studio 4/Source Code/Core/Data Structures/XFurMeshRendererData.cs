/*

XFur Studio™, by Irreverent Software™
Copyright© 2018-2025, Jorge Pinal Negrete. All Rights Reserved.

*/

#define XFURSTUDIO3_UPGRADE

namespace XFurStudio.Core {


    using UnityEngine;


    public partial class XFurStudioInstance : MonoBehaviour {

        /// <summary>
        /// Contains important data to link and process a specified mesh/skinned mesh renderer so that it can render fur over its surface
        /// </summary>
        [System.Serializable]
        public struct XFurMeshRendererData {

            /// <summary>
            /// The renderer itself, as a reference
            /// </summary>
            public Renderer renderer;

            /// <summary>
            /// The list of materials used by this renderer
            /// </summary>
            public Material[] materials;

            /// <summary>
            /// Its original mesh
            /// </summary>
            public Mesh originalMesh;

            /// <summary>
            /// Whether the data referenced by this struct is out of date
            /// </summary>
            public bool requiresUpdate;

            /// <summary>
            /// Whether the different materials of this mesh/renderer should display fur or not.
            /// </summary>
            public bool[] isFurMaterial;

            /// <summary>
            /// Whether this renderer is used as part of Unity's LOD system
            /// </summary>
            public bool isLOD;

            /// <summary>
            /// The index of the fur profile attached to this particular renderer.
            /// </summary>
            public int[] furProfiles;


            /// <summary>
            /// Assigns a Renderer to be processed by this struct, extracting all the information needed.
            /// </summary>
            /// <param name="rend"></param>
            public void AssignRenderer( Renderer rend ) {

                isLOD = false;
                renderer = rend;

                if ( !rend ) {
                    materials = new Material[0];
                    originalMesh = null;
                    return;
                }

                materials = rend.sharedMaterials;

                var tempBools = new bool[materials.Length];

                if ( isFurMaterial != null ) {
                    for ( int i = 0; i < tempBools.Length; ++i ) {
                        if ( isFurMaterial.Length > i )
                            tempBools[i] = isFurMaterial[i];
                    }
                }

                isFurMaterial = tempBools;

                var tempInts = new int[materials.Length];

                if ( furProfiles != null ) {
                    for ( int i = 0; i < tempInts.Length; ++i ) {
                        if ( furProfiles.Length > i )
                            tempInts[i] = furProfiles[i];
                    }
                }
                else {
                    furProfiles = new int[rend.sharedMaterials.Length];
                }

                if ( !isLOD ) {
                    for ( int i = 0; i < furProfiles.Length; ++i ) {
                        furProfiles[i] = -1;
                    }
                }

                furProfiles = tempInts;

                if ( rend is SkinnedMeshRenderer ) {
                    originalMesh = ( (SkinnedMeshRenderer)rend ).sharedMesh;
                }
                else if ( rend is MeshRenderer ) {
                    originalMesh = rend.GetComponent<MeshFilter>().sharedMesh;
                }

            }


        }
    }

}