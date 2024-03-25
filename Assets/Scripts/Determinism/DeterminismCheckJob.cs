using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

// czy to już się wywoła na DeterministicSimulation components czy muszę sprawdzić
// We are adding results to this array and when the job is done we should combine it (parallel)
[BurstCompile]
struct DeterminismCheckJob : IJobChunk
{
    [ReadOnly] public ComponentTypeHandle<LocalTransform> transform; // add more deterministic types into the job
    public NativeArray<ulong> ResultsNativeArray;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        if (!chunk.Has<DeterministicSimulation>()) return; // confirm if this object should be hashed
        ulong hash = 0;
        var componentTypes = chunk.Archetype.GetComponentTypes();
        hash = TypeHash.CombineFNV1A64(hash, (ulong) componentTypes.Length);
        
        var data = chunk.GetNativeArray(ref transform).Reinterpret<byte>();
        foreach (var t in data)
            hash = TypeHash.CombineFNV1A64(hash, t);
        
        ResultsNativeArray[unfilteredChunkIndex] = hash;
    }
}
