using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Logging;
using Unity.Mathematics;
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
            var logMap = new NativeParallelMultiHashMap<Entity, KeyValuePair<TypeIndex, ulong>>(_mQuery.CalculateEntityCount()*listOfDeterministicTypes.Length, Allocator.TempJob);
            
            var job = new DeterminismCheckJob()
            {
                hashCalculationOption = hashCalculationOption,
                listOfDeterministicTypes = list,
                resultsNativeArray = _resultsArray.AsArray(),
                entityType = SystemAPI.GetEntityTypeHandle(),
                logMap = logMap.AsParallelWriter()
            };
            
            var handle = job.ScheduleParallel(_mQuery, state.Dependency);
            handle.Complete();
            
            // Combine the results
            ulong hash = 0;
            foreach (var result in _resultsArray)
                hash = TypeHash.CombineFNV1A64(hash, result);
            
            // Debug.Log("Option: " + hashCalculationOption + " - Hash: " + hash);
            // // Save the results for the future
            // var currentTick = _everyTickHashBuffer.Count + 1;
            // _everyTickHashBuffer[currentTick] = hash;

            // Save Hash in the DeterministicTime component
            timeComponent.ValueRW.hashesForTheCurrentTick.Add(hash);
            
            var keys = logMap.GetKeyArray(Allocator.Temp);
            keys.Sort();
            var keyIndex = -1;
            var keyVersion = -1;
            foreach (var key in keys)
            {
                if (keyIndex != key.Index || keyVersion != key.Version)
                {
                    keyIndex = key.Index;
                    keyVersion = key.Version;
                    Log.Info($"          Entity({key.Index}:{key.Version})");
                    var values = logMap.GetValuesForKey(key);
                    foreach (var value in values)
                    {
                        Log.Info($"               [{value.Key}] - {value.Value}");
                    }
                }
            }
            
            
            logMap.Dispose();
            keys.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _resultsArray.Dispose();
        }
    }
}