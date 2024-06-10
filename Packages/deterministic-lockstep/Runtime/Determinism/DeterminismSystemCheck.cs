using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// System to check the determinism of the simulation. It will hash the necessary component of all entities with the DeterministicSimulation component.
    /// </summary>
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(PlayerInputSendSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct DeterminismCheckSystem : ISystem
    {
        private NativeList<ulong> _resultsArray;
        private EntityQuery _mQuery;

        // private Dictionary<int, ulong> _everyTickHashBuffer;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DeterministicSettings>();
            state.RequireForUpdate<DeterministicTime>();
            _resultsArray = new NativeList<ulong>(128, Allocator.Persistent); // probably need to refine this number
            // _everyTickHashBuffer = new Dictionary<int, ulong>();
            _mQuery = state.EntityManager.CreateEntityQuery(
                typeof(EnsureDeterministicBehaviour)
            );
        }
        
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<DeterministicSettings>().hashCalculationOption ==
                DeterminismHashCalculationOption.None) return; // No determinism checks
            
            
            var resultsArrayCapacity = _mQuery.CalculateChunkCount();
            _resultsArray.Clear(); // Clear the array to avoid data from old frames
            var length = math.ceilpow2(resultsArrayCapacity);
            _resultsArray.Capacity = length;
            _resultsArray.Length = length; // refine this part at some point

            var job = new DeterminismCheckJob()
            {
                transform = state.GetComponentTypeHandle<LocalTransform>(true),
                resultsNativeArray = _resultsArray.AsArray()
            };
            var handle = job.ScheduleParallel(_mQuery, state.Dependency);
            handle.Complete();

            // Combine the results
            ulong hash = 0;
            foreach (var result in _resultsArray)
                hash = TypeHash.CombineFNV1A64(hash, result);
            
            // // Save the results for the future
            // var currentTick = _everyTickHashBuffer.Count + 1;
            // _everyTickHashBuffer[currentTick] = hash;

            // Save Hash in the DeterministicTime component
            var timeComponent = SystemAPI.GetSingletonRW<DeterministicTime>();
            timeComponent.ValueRW.hashesForTheCurrentTick.Add(hash);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _resultsArray.Dispose();
        }
    }
}