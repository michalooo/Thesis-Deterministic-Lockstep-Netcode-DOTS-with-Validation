using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Transforms;

// Will it already work on DeterministicSimulation components or should I check like now
// We are adding results to this array and when the job is done we should combine it (parallel)
[BurstCompile]
public struct DeterminismCheckJob : IJobChunk
{
    [ReadOnly] public ComponentTypeHandle<LocalTransform> transform; // add more deterministic types into the job
    
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<ulong> ResultsNativeArray;
    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        if (!chunk.Has<DeterministicSimulation>()) return; // confirm if this object should be hashed
        ulong hash = 0;
        var componentTypes = chunk.Archetype.GetComponentTypes();
        hash = TypeHash.CombineFNV1A64(hash, (ulong) componentTypes.Length);
        
        var transforms = chunk.GetNativeArray(ref transform);
        var data = transforms.Reinterpret<byte>(transforms.Length * 4 * 4 * 2); // Unpacking the LocalTransform (float4x4) to avoid reinterpreting to different size
        
        foreach (var t in data)
            hash = TypeHash.CombineFNV1A64(hash, t);
        
        ResultsNativeArray[(int) chunk.SequenceNumber] = hash;
    }
}
