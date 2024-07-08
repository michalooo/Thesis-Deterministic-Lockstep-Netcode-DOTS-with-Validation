using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// System that calculates the hash of the current state of the game for validation purposes.
    /// When run, it will add one hash to the DeterministicTime component.
    /// </summary>
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(PlayerInputSendSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct StateHashForValidationSystem : ISystem
    {
        /// <summary>
        /// NativeList of hashes from different jobs, that will be used to calculate the final hash.
        /// </summary>
        private NativeList<ulong> perJobHashArray;
        
        /// <summary>
        /// Query used to get all the chunks with components marked for validation
        /// </summary>
        private EntityQuery componentTypesQuery;
        
        /// <summary>
        /// Buffer of deterministic components that will be used to create the query
        /// </summary>
        private DynamicBuffer<DeterministicComponent> listOfDeterministicTypes;
        

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DeterministicSettings>();
            state.RequireForUpdate<DeterministicSimulationTime>();
            state.RequireForUpdate<DeterministicComponent>();

            perJobHashArray = new NativeList<ulong>(128, Allocator.Persistent);
            listOfDeterministicTypes = SystemAPI.GetSingletonBuffer<DeterministicComponent>();
            
            var componentTypes = new ComponentType[listOfDeterministicTypes.Length];
            for (int i = 0; i < listOfDeterministicTypes.Length; i++)
            {
                componentTypes[i] = listOfDeterministicTypes[i].Type;
            }

            var query = new EntityQueryDesc
            {
                Any = componentTypes
            };
            componentTypesQuery = state.EntityManager.CreateEntityQuery(
                query
            );
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeComponent = SystemAPI.GetSingletonRW<DeterministicSimulationTime>();
            var hashCalculationOption = SystemAPI.GetSingleton<DeterministicSettings>().hashCalculationOption;
            if (hashCalculationOption == DeterminismHashCalculationOption.None) {
                // No hash calculation will be performed. The hash is added to maintain consistency in checks.
                timeComponent.ValueRW.hashesForTheCurrentTick.Add(0); 
                return;
            }
            
            listOfDeterministicTypes = SystemAPI.GetSingletonBuffer<DeterministicComponent>();
            var dynamicListOfDeterministicTypes = new DynamicTypeList();
            DynamicTypeList.PopulateList(ref state, listOfDeterministicTypes, true, ref dynamicListOfDeterministicTypes);
            
            var resultsArrayCapacity = componentTypesQuery.CalculateChunkCount();
            
            var length = math.ceilpow2(resultsArrayCapacity);
            perJobHashArray.Length = length;
            perJobHashArray.Capacity = length;
            var determinismLogPerEntityTypeMap = new NativeParallelMultiHashMap<Entity, KeyValuePair<TypeIndex, ulong>>(componentTypesQuery.CalculateEntityCount()*listOfDeterministicTypes.Length, Allocator.TempJob);
            
            var hashingJob = new GameStateHashJob()
            {
                hashCalculationOption = hashCalculationOption,
                listOfDeterministicTypes = dynamicListOfDeterministicTypes,
                resultsNativeArray = perJobHashArray.AsArray(),
                entityType = SystemAPI.GetEntityTypeHandle(),
                logMap = determinismLogPerEntityTypeMap.AsParallelWriter()
            };
            
            var hashingJobHandle = hashingJob.ScheduleParallel(componentTypesQuery, state.Dependency);
            hashingJobHandle.Complete();
            
            ulong stateHash = 0;
            foreach (var perJobHash in perJobHashArray)
                stateHash = TypeHash.CombineFNV1A64(stateHash, perJobHash);
            timeComponent.ValueRW.hashesForTheCurrentTick.Add(stateHash);
            
            var keys = determinismLogPerEntityTypeMap.GetKeyArray(Allocator.Temp);
            keys.Sort();
            var keyIndex = -1;
            var keyVersion = -1;
            var hashedSimulationTick = SystemAPI.GetSingletonRW<DeterministicSimulationTime>().ValueRO.currentClientTickToSend;
            foreach (var key in keys)
            {
                if (keyIndex != key.Index || keyVersion != key.Version) // This is used because for local simulation we get duplicated entities
                {
                    keyIndex = key.Index;
                    keyVersion = key.Version;
                    
                    state.EntityManager.GetName(key, out FixedString64Bytes nameFs);
                    DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"          Entity({key.Index}:{key.Version}) - " + nameFs);
           
                    var values = determinismLogPerEntityTypeMap.GetValuesForKey(key);
                    foreach (var value in values)
                    {
                        DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"               Component [{value.Key}] - Hash value {value.Value}");
                        if (value.Key == TypeManager.GetTypeIndex<LocalTransform>())
                        {
                            LocalTransform localTransform = state.EntityManager.GetComponentData<LocalTransform>(key);
                            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"                    Position: {localTransform.Position}");
                            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"                    Rotation: {localTransform.Rotation}");
                            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"                    Scale: {localTransform.Scale}");
                        }
                        else if (value.Key == TypeManager.GetTypeIndex<DeterministicSettings>())
                        {
                            DeterministicSettings deterministicSettingsComponent = state.EntityManager.GetComponentData<DeterministicSettings>(key);
                            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"                    Simulation tick rate: {deterministicSettingsComponent.simulationTickRate}");
                            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"                    Hash calculation option: {deterministicSettingsComponent.hashCalculationOption}");
                            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"                    Server address: {deterministicSettingsComponent._serverAddress}");
                            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"                    Server port: {deterministicSettingsComponent._serverPort}");
                            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"                    Ticks of forced input latency: {deterministicSettingsComponent.ticksOfForcedInputLatency}");
                            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"                    Is simulation replaying for a file: {deterministicSettingsComponent.isReplayFromFile}");
                        }
                    }
                }
            }
            
            perJobHashArray.Clear(); 
            determinismLogPerEntityTypeMap.Dispose();
            keys.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            perJobHashArray.Dispose();
        }
    }
}