
namespace XFurStudio.Core {


    using UnityEngine;
    using Unity.Jobs;
    using Unity.Collections;
    using Unity.Burst;


    public struct XFurInstancingData {

        public Matrix4x4 objectToWorld;
        public Matrix4x4 prevObjectToWorld;

    }

#if XFUR_BURSTED

    [BurstCompile( CompileSynchronously = true )]
    public struct XFurMatrixProcessingJob : IJobParallelFor {

        public Matrix4x4 originalMatrix;
        public NativeArray<XFurInstancingData> matrices;


        public void Execute( int index ) {

            XFurInstancingData data = new XFurInstancingData();
            data.objectToWorld = originalMatrix;
            data.prevObjectToWorld = matrices[index].objectToWorld;
            matrices[index] = data;

        }
    }


#endif

}