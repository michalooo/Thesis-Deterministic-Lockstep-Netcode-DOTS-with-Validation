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
    public struct DeterminismCheckJob : IJobChunk
    {
        [ReadOnly]
        public ComponentTypeHandle<LocalTransform> transform; // add more deterministic types into the job later
        
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<ulong> resultsNativeArray;
        
        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Debug.Assert(chunk.Has<EnsureDeterministicBehaviour>()); // this will be used for whitelisting
#endif
            ulong hash = 0;

            var transforms =
                chunk.GetNativeArray(
                    ref transform); // LocalTransform can already be broken because of floats. Test on different devices to see if it's the case
            var data = transforms.Reinterpret<byte>(UnsafeUtility.SizeOf<LocalTransform>());

            foreach (var t in data)
                hash = TypeHash.CombineFNV1A64(hash, t);

            resultsNativeArray[unfilteredChunkIndex] = hash; // check out if the array is large enough to support it 
        }
    }
}