using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

[UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
[UpdateAfter(typeof(MovementSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class DeterminismCheckSystem : SystemBase
{
    NativeList<ulong> resultsArray;
    private EntityQuery m_Query;
    
    private Dictionary<int, ulong> everyTickHashBuffer;
    
    protected override void OnCreate()
    {
        resultsArray = new NativeList<ulong>(128, Allocator.Persistent);
        everyTickHashBuffer = new Dictionary<int, ulong>();
    }
    
    protected override void OnUpdate()
    {
        m_Query = EntityManager.CreateEntityQuery(
            typeof(DeterministicSimulation)
        );

        var resultsArrayCapacity = m_Query.CalculateChunkCount();
        if(resultsArray.Capacity < resultsArrayCapacity)
            resultsArray.Capacity = resultsArrayCapacity;

        var job = new DeterminismCheckJob()
        {
            transform = GetComponentTypeHandle<LocalTransform>(true),
            ResultsNativeArray = resultsArray.AsArray()
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
        
        // Save Hash in the tickRateInfo component
        foreach (var tickRateInfo in SystemAPI.Query<RefRW<TickRateInfo>>().WithAll<GhostOwnerIsLocal>())
        {
            tickRateInfo.ValueRW.hashForTheTick = hash;
        }
    }
    
    protected override void OnDestroy()
    {
        resultsArray.Dispose();
    }
}