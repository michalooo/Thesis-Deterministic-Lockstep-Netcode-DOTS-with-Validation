using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace DeterministicLockstep
{
    /// <summary>
    /// System that calculates the hash of the current state of the game for validation purposes.
    /// When run, it will add one hash to the DeterministicTime component.
    /// Works in any world (single-player, multiplayer client, or default world).
    /// </summary>
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(PlayerInputSendSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.Default)]
    public partial struct StateHashForValidationSystem : ISystem
    {
        /// <summary>
        /// NativeList of hashes from different jobs, that will be used to calculate the final hash.
        /// </summary>
        private NativeList<ulong> _perJobHashArray;
        
        /// <summary>
        /// Query used to get all the chunks with components marked for validation
        /// </summary>
        private EntityQuery _componentTypesQuery;
        
        /// <summary>
        /// Buffer of deterministic components that will be used to create the query
        /// </summary>
        private DynamicBuffer<DeterministicComponent> _listOfDeterministicTypes;
        

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DeterministicSettings>();
            state.RequireForUpdate<DeterministicSimulationTime>();
            state.RequireForUpdate<DeterministicComponent>();
            
            _listOfDeterministicTypes = SystemAPI.GetSingletonBuffer<DeterministicComponent>();
            _perJobHashArray = new NativeList<ulong>(128, Allocator.Persistent);
            
            var componentTypes = new ComponentType[_listOfDeterministicTypes.Length];
            for (var i = 0; i < _listOfDeterministicTypes.Length; i++)
            {
                componentTypes[i] = _listOfDeterministicTypes[i].type;
            }

            var query = new EntityQueryDesc
            {
                Any = componentTypes
            };
            _componentTypesQuery = state.EntityManager.CreateEntityQuery(
                query
            );
        }

        public void OnUpdate(ref SystemState state)
        {
            var deterministicSimulationTimeComponent = SystemAPI.GetSingletonRW<DeterministicSimulationTime>();
            var hashCalculationOption = SystemAPI.GetSingleton<DeterministicSettings>().hashCalculationOption;
            if (hashCalculationOption == DeterminismHashCalculationOption.None) {
                // No hash calculation will be performed. The hash is added to maintain consistency in checks.
                deterministicSimulationTimeComponent.ValueRW.hashesForTheCurrentTick.Add(0); 
                return;
            }
            
            _listOfDeterministicTypes = SystemAPI.GetSingletonBuffer<DeterministicComponent>();
            var dynamicListOfDeterministicTypes = new DynamicTypeList();
            DynamicTypeList.PopulateList(ref state, _listOfDeterministicTypes, true, ref dynamicListOfDeterministicTypes);
            
            var determinismLogPerEntityTypeMap = new NativeParallelMultiHashMap<Entity, KeyValuePair<TypeIndex, ulong>>(_componentTypesQuery.CalculateEntityCount()*_listOfDeterministicTypes.Length, Allocator.TempJob);
            
            var hashingJob = new GameStateHashJob()
            {
                hashCalculationOption = hashCalculationOption,
                listOfDeterministicTypes = dynamicListOfDeterministicTypes,
                entityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                logHashMap = determinismLogPerEntityTypeMap.AsParallelWriter(),
            };
            
            var hashingJobHandle = hashingJob.ScheduleParallel(_componentTypesQuery, state.Dependency);
            hashingJobHandle.Complete();
            
            var hashedSimulationTick = SystemAPI.GetSingletonRW<DeterministicSimulationTime>().ValueRO.currentClientTickToSend;
            
            ulong stateHash = 0;
 
            var logKeys = determinismLogPerEntityTypeMap.GetKeyArray(Allocator.Temp);
            logKeys.Sort(new EntityComparer { manager = state.EntityManager });
           
            var entityKeyIndex = -1;
            var entityKeyVersion = -1;
            foreach (var key in logKeys)
            {
                if (entityKeyIndex != key.Index || entityKeyVersion != key.Version) // This is used because for local simulation we get duplicated entities
                {
                    entityKeyIndex = key.Index;
                    entityKeyVersion = key.Version;
                    
                    state.EntityManager.GetName(key, out FixedString64Bytes nameFs);
                    DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"          Entity({key.Index}:{key.Version}) - " + nameFs);
           
                    var values = determinismLogPerEntityTypeMap.GetValuesForKey(key);
                    foreach (var value in values)
                    {
                        DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"               Component [{value.Key}] - Hash value {value.Value}");
                        stateHash = TypeHash.CombineFNV1A64(stateHash, value.Value);
                        
                        // TODO: remove this hack and instead achieve it via code generation
                        if (value.Key == TypeManager.GetTypeIndex<LocalTransform>())
                        {
                            var localTransform = state.EntityManager.GetComponentData<LocalTransform>(key);
                            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"                    Position: {localTransform.Position}");
                            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"                    Rotation: {localTransform.Rotation}");
                            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"                    Scale: {localTransform.Scale}");
                        }
                        else if (value.Key == TypeManager.GetTypeIndex<DeterministicEntityID>())
                        {
                            var deterministicEntityID = state.EntityManager.GetComponentData<DeterministicEntityID>(key);
                            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"                    Deterministic entity ID: {deterministicEntityID.id}");
                        }
                        else if (value.Key == TypeManager.GetTypeIndex<DeterministicSettings>())
                        {
                            var deterministicSettingsComponent = state.EntityManager.GetComponentData<DeterministicSettings>(key);
                            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"                    Simulation tick rate: {deterministicSettingsComponent.simulationTickRate}");
                            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"                    Hash calculation option: {deterministicSettingsComponent.hashCalculationOption}");
                            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"                    Server address: {deterministicSettingsComponent.serverAddress}");
                            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"                    Server port: {deterministicSettingsComponent.serverPort}");
                            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"                    Ticks of forced input latency: {deterministicSettingsComponent.ticksOfForcedInputLatency}");
                            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"                    Is simulation replaying for a file: {deterministicSettingsComponent.isReplayFromFile}");
                        }
                    }
                }
            }
            DeterministicLogger.Instance.AddToClientHashDictionary(state.World.Name, (ulong) hashedSimulationTick, $"State hash: {stateHash} ");
            deterministicSimulationTimeComponent.ValueRW.hashesForTheCurrentTick.Add(stateHash);

            _perJobHashArray.Clear();
            determinismLogPerEntityTypeMap.Dispose();
            logKeys.Dispose();
        }
        
        public void OnDestroy(ref SystemState state)
        {
            _perJobHashArray.Dispose();
        }
    }
    
    struct EntityComparer : IComparer<Entity>
    {
        public EntityManager manager;

        public int Compare(Entity entity1, Entity entity2)
        {
            var value1 = manager.GetComponentData<DeterministicEntityID>(entity1).id;
            var value2 = manager.GetComponentData<DeterministicEntityID>(entity2).id;
            return value1.CompareTo(value2);
        }
    }
}