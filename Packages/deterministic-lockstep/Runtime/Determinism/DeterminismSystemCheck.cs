using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DeterministicLockstep
{
    /// <summary>
    /// System to check the determinism of the simulation. It will hash the necessary component of all entities with the DeterministicSimulation component.
    /// </summary>
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
    [UpdateAfter(typeof(GameStateUpdateSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class DeterminismCheckSystem : SystemBase
    {
        private NativeList<ulong> _resultsArray;
        private EntityQuery _mQuery;

        private Dictionary<int, ulong> _everyTickHashBuffer;

        protected override void OnCreate()
        {
            _resultsArray = new NativeList<ulong>(128, Allocator.Persistent);
            _everyTickHashBuffer = new Dictionary<int, ulong>();
        }

        protected override void OnUpdate()
        {
            _mQuery = EntityManager.CreateEntityQuery(
                typeof(DeterministicSimulation)
            );

            var resultsArrayCapacity = _mQuery.CalculateChunkCount();
            _resultsArray.Clear(); // Clear the array to avoid data from old frames
            var length = math.ceilpow2(resultsArrayCapacity);
            _resultsArray.Capacity = length;
            _resultsArray.Length = length; // refine this part at some point

            var job = new DeterminismCheckJob()
            {
                transform = GetComponentTypeHandle<LocalTransform>(true),
                ResultsNativeArray = _resultsArray.AsArray()
            };
            var handle = job.ScheduleParallel(_mQuery, this.Dependency);
            handle.Complete();

            // Combine the results
            ulong hash = 0;
            foreach (var result in _resultsArray)
                hash = TypeHash.CombineFNV1A64(hash, result);

            // Save the results for the future
            var currentTick = _everyTickHashBuffer.Count + 1;
            _everyTickHashBuffer[currentTick] = hash;

            // Save Hash in the tickRateInfo component
            foreach (var tickRateInfo in SystemAPI.Query<RefRW<TickRateInfo>>().WithAll<GhostOwnerIsLocal>())
            {
                tickRateInfo.ValueRW.hashForTheTick = hash;
            }
        }

        protected override void OnDestroy()
        {
            _resultsArray.Dispose();
        }
    }
}