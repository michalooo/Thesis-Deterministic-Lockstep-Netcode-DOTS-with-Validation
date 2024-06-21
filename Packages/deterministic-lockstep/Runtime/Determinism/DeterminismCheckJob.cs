using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// Job that will run on chunks to check and hash the components in them.
    /// </summary>

    [BurstCompile]
    public unsafe struct DeterminismCheckJob : IJobChunk
    {
        [ReadOnly]
        public DynamicTypeList listOfDeterministicTypes;
        
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<ulong> resultsNativeArray;
        
        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Debug.Assert(chunk.Has<EnsureDeterministicBehaviour>());
#endif
            var dynamicTypeListPtr = listOfDeterministicTypes.GetData();
            var hash = (ulong) listOfDeterministicTypes.Length;

            for (var i = 0; i < listOfDeterministicTypes.Length; i++)
            {
                var dynamicComponentTypeHandle = dynamicTypeListPtr[i];; 
                var typeInfo = TypeManager.GetTypeInfo(dynamicComponentTypeHandle.TypeIndex);
                var rawByteData = chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref dynamicComponentTypeHandle, typeInfo.TypeSize);
                
                foreach (var byteData in rawByteData)
                    hash = TypeHash.CombineFNV1A64(hash, byteData);
            }

            resultsNativeArray[unfilteredChunkIndex] = hash; // check out if the array is large enough to support it 
        }
    }
}