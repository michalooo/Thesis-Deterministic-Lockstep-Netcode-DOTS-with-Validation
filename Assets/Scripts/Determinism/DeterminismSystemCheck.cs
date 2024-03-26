using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

[UpdateInGroup(typeof(DeterminismSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class DeterminismCheckSystem : SystemBase
{
    NativeArray<ulong> resultsArray;
    private EntityQuery m_Query;
    
    private Dictionary<int, ulong> everyTickHashBuffer;
    
    protected override void OnCreate()
    {
        resultsArray = new NativeArray<ulong>(10000, Allocator.Persistent);
        everyTickHashBuffer = new Dictionary<int, ulong>();
    }
    
    protected override void OnUpdate()
    {
        m_Query = EntityManager.CreateEntityQuery(
            typeof(DeterministicSimulation)
        );
        
        var job = new DeterminismCheckJob()
        {
            transform = GetComponentTypeHandle<LocalTransform>(true),
            ResultsNativeArray = resultsArray
        };
        var handle = job.ScheduleParallel(m_Query, this.Dependency);
        handle.Complete();
        
        // Combine the results
        ulong hash = 0;
        foreach (var result in resultsArray)
            hash = TypeHash.CombineFNV1A64(hash, result);
        
        // Save the results for the future
        var currentTick = everyTickHashBuffer.Count + 1;
        everyTickHashBuffer[currentTick] = hash;
    }
    
    protected override void OnDestroy()
    {
        resultsArray.Dispose();
    }
}