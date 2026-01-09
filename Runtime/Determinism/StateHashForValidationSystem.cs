using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace DeterministicLockstep
{
    /// <summary>
    /// System that calculates the hash of the current game state for validation purposes.
    /// When run, it adds a hash to the DeterministicSimulationTime component.
    /// </summary>
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup), OrderLast = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial struct StateHashForValidationSystem : ISystem
    {
        private NativeList<ulong> _perJobHashArray;
        private EntityQuery _componentTypesQuery;
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DeterministicSettings>();
            state.RequireForUpdate<DeterministicSimulationTime>();
            state.RequireForUpdate<DeterministicComponent>();
            _initialized = false;
        }
        
        private void EnsureInitialized(ref SystemState state)
        {
            if (_initialized)
                return;
                
            _perJobHashArray = new NativeList<ulong>(128, Allocator.Persistent);
            
            var listOfDeterministicTypes = SystemAPI.GetSingletonBuffer<DeterministicComponent>();
            var componentTypes = new ComponentType[listOfDeterministicTypes.Length];
            for (var i = 0; i < listOfDeterministicTypes.Length; i++)
            {
                componentTypes[i] = listOfDeterministicTypes[i].type;
            }

            var query = new EntityQueryDesc
            {
                Any = componentTypes
            };
            _componentTypesQuery = state.EntityManager.CreateEntityQuery(query);
            _initialized = true;
        }

        public void OnUpdate(ref SystemState state)
        {
            EnsureInitialized(ref state);
            
            var simTime = SystemAPI.GetSingletonRW<DeterministicSimulationTime>();
            var settings = SystemAPI.GetSingleton<DeterministicSettings>();
            
            var listOfDeterministicTypes = SystemAPI.GetSingletonBuffer<DeterministicComponent>();
            var dynamicListOfDeterministicTypes = new DynamicTypeList();
            var typeIndexList = new TypeIndexList();
            DynamicTypeList.PopulateList(ref state, listOfDeterministicTypes, true, ref dynamicListOfDeterministicTypes, ref typeIndexList);
            
            var entityCount = _componentTypesQuery.CalculateEntityCount();
            var determinismLogPerEntityTypeMap = new NativeParallelMultiHashMap<Entity, KeyValuePair<TypeIndex, ulong>>(
                entityCount * listOfDeterministicTypes.Length, Allocator.TempJob);
            
            var hashingJob = new GameStateHashJob
            {
                hashScope = settings.hashScope,
                listOfDeterministicTypes = dynamicListOfDeterministicTypes,
                typeIndexList = typeIndexList,
                entityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                logHashMap = determinismLogPerEntityTypeMap.AsParallelWriter(),
            };
            
            var hashingJobHandle = hashingJob.ScheduleParallel(_componentTypesQuery, state.Dependency);
            hashingJobHandle.Complete();
            
            var currentTick = simTime.ValueRO.currentTick;
            ulong stateHash = 0;
 
            var logKeys = determinismLogPerEntityTypeMap.GetKeyArray(Allocator.Temp);
            logKeys.Sort(new EntityComparer { manager = state.EntityManager });
           
            var entityKeyIndex = -1;
            var entityKeyVersion = -1;
            
            foreach (var key in logKeys)
            {
                // Skip duplicates (can happen in local simulation)
                if (entityKeyIndex == key.Index && entityKeyVersion == key.Version)
                    continue;
                    
                entityKeyIndex = key.Index;
                entityKeyVersion = key.Version;
                
                state.EntityManager.GetName(key, out FixedString64Bytes nameFs);
                DeterministicLogger.Instance?.AddToHashLog(state.World.Name, currentTick, 
                    $"  Entity({key.Index}:{key.Version}) - {nameFs}");
       
                var values = determinismLogPerEntityTypeMap.GetValuesForKey(key);
                foreach (var value in values)
                {
                    DeterministicLogger.Instance?.AddToHashLog(state.World.Name, currentTick, 
                        $"    Component [{value.Key}] - Hash: {value.Value:X16}");
                    stateHash = TypeHash.CombineFNV1A64(stateHash, value.Value);
                    
                    // Log component details for debugging
                    LogComponentDetails(ref state, key, value.Key, currentTick);
                }
            }
            
            DeterministicLogger.Instance?.AddToHashLog(state.World.Name, currentTick, 
                $"Tick {currentTick} State Hash: {stateHash:X16}");
            simTime.ValueRW.hashesForCurrentTick.Add(stateHash);

            _perJobHashArray.Clear();
            determinismLogPerEntityTypeMap.Dispose();
            logKeys.Dispose();
        }
        
        private void LogComponentDetails(ref SystemState state, Entity entity, TypeIndex typeIndex, int tick)
        {
            if (DeterministicLogger.Instance == null)
                return;
                
            if (typeIndex == TypeManager.GetTypeIndex<LocalTransform>())
            {
                var localTransform = state.EntityManager.GetComponentData<LocalTransform>(entity);
                DeterministicLogger.Instance.AddToHashLog(state.World.Name, tick, 
                    $"      Position: {localTransform.Position}");
                DeterministicLogger.Instance.AddToHashLog(state.World.Name, tick, 
                    $"      Rotation: {localTransform.Rotation}");
                DeterministicLogger.Instance.AddToHashLog(state.World.Name, tick, 
                    $"      Scale: {localTransform.Scale}");
            }
            else if (typeIndex == TypeManager.GetTypeIndex<DeterministicEntityID>())
            {
                var entityID = state.EntityManager.GetComponentData<DeterministicEntityID>(entity);
                DeterministicLogger.Instance.AddToHashLog(state.World.Name, tick, 
                    $"      ID: {entityID.id}");
            }
        }
        
        public void OnDestroy(ref SystemState state)
        {
            if (_perJobHashArray.IsCreated)
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
