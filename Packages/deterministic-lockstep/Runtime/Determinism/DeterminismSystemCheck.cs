using System.Security.Cryptography;
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
        private DynamicBuffer<DeterministicComponent> listOfDeterministicTypes;
        private bool isQueryCreated;

        // private Dictionary<int, ulong> _everyTickHashBuffer;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DeterministicSettings>();
            state.RequireForUpdate<DeterministicTime>();
            state.RequireForUpdate<DeterministicComponent>();
            
            _resultsArray = new NativeList<ulong>(128, Allocator.Persistent); // probably need to refine this number
            isQueryCreated = false;
            // _everyTickHashBuffer = new Dictionary<int, ulong>();
        }
        
        
        
        public void OnUpdate(ref SystemState state)
        {
            var timeComponent = SystemAPI.GetSingletonRW<DeterministicTime>();
            var hashCalculationOption = SystemAPI.GetSingleton<DeterministicSettings>().hashCalculationOption;
            if (hashCalculationOption == DeterminismHashCalculationOption.None) {
                timeComponent.ValueRW.hashesForTheCurrentTick.Add(0); // Added for comparison
                return; // No determinism checks
            }
            
            listOfDeterministicTypes = SystemAPI.GetSingletonBuffer<DeterministicComponent>();

            if (true)
            {
                isQueryCreated = true;
                var componentTypes = new ComponentType[listOfDeterministicTypes.Length];
                for (int i = 0; i < listOfDeterministicTypes.Length; i++)
                {
                    componentTypes[i] = listOfDeterministicTypes[i].Type;
                }

                var query = new EntityQueryDesc
                {
                    Any = componentTypes
                };
                _mQuery = state.EntityManager.CreateEntityQuery(
                    query
                );
            }
            
            
            var list = new DynamicTypeList();
            DynamicTypeList.PopulateList(ref state, listOfDeterministicTypes, true, ref list);

            
            
            var resultsArrayCapacity = _mQuery.CalculateChunkCount();
            _resultsArray.Clear(); // Clear the array to avoid data from old frames
            var length = math.ceilpow2(resultsArrayCapacity);
            _resultsArray.Capacity = length;
            _resultsArray.Length = length; // refine this part at some point
            
           
            
            var job = new DeterminismCheckJob()
            {
                hashCalculationOption = hashCalculationOption,
                listOfDeterministicTypes = list,
                resultsNativeArray = _resultsArray.AsArray()
            };
            // Debug.Log(_mQuery.CalculateChunkCount()); //Weird because each chunk kind of represents one entity
            var handle = job.ScheduleParallel(_mQuery, state.Dependency);
            handle.Complete();
            
            // Combine the results
            ulong hash = 0;
            foreach (var result in _resultsArray)
                hash = TypeHash.CombineFNV1A64(hash, result);
            
            Debug.Log("Option: " + hashCalculationOption + " - Hash: " + hash);
            // // Save the results for the future
            // var currentTick = _everyTickHashBuffer.Count + 1;
            // _everyTickHashBuffer[currentTick] = hash;

            // Save Hash in the DeterministicTime component
            timeComponent.ValueRW.hashesForTheCurrentTick.Add(hash);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _resultsArray.Dispose();
        }
    }
}