using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// Captures and restores ECS world state for determinism validation.
    /// Only captures entities with DeterministicEntityID component.
    /// </summary>
    public class WorldStateSnapshot : IDisposable
    {
        /// <summary>
        /// Captured entity data.
        /// </summary>
        public struct EntitySnapshot
        {
            public int deterministicId;
            public Dictionary<TypeIndex, byte[]> componentData;
        }
        
        private List<EntitySnapshot> _entitySnapshots = new List<EntitySnapshot>();
        private List<ComponentType> _componentTypes = new List<ComponentType>();
        private bool _isValid = false;
        private int _entityCount = 0;
        
        /// <summary>
        /// Whether this snapshot contains valid data.
        /// </summary>
        public bool IsValid => _isValid;
        
        /// <summary>
        /// Number of entities captured.
        /// </summary>
        public int EntityCount => _entityCount;

        /// <summary>
        /// Capture current world state.
        /// </summary>
        /// <param name="world">The world to capture.</param>
        /// <param name="componentTypesToCapture">Optional list of component types to capture. If null, uses DeterministicComponent buffer.</param>
        public void Capture(World world, List<ComponentType> componentTypesToCapture = null)
        {
            if (world == null || !world.IsCreated)
            {
                Debug.LogError("[WorldStateSnapshot] Cannot capture - world is null or destroyed.");
                return;
            }
            
            Clear();
            
            var entityManager = world.EntityManager;
            
            // Get component types to capture
            if (componentTypesToCapture != null)
            {
                _componentTypes.AddRange(componentTypesToCapture);
            }
            else
            {
                // Use DeterministicComponent buffer
                var bufferQuery = entityManager.CreateEntityQuery(typeof(DeterministicComponent));
                if (!bufferQuery.IsEmpty)
                {
                    var bufferEntity = bufferQuery.GetSingletonEntity();
                    var buffer = entityManager.GetBuffer<DeterministicComponent>(bufferEntity);
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        _componentTypes.Add(buffer[i].type);
                    }
                }
            }
            
            // Query entities with DeterministicEntityID
            var query = entityManager.CreateEntityQuery(typeof(DeterministicEntityID));
            var entities = query.ToEntityArray(Allocator.Temp);
            
            foreach (var entity in entities)
            {
                var snapshot = new EntitySnapshot
                {
                    deterministicId = entityManager.GetComponentData<DeterministicEntityID>(entity).id,
                    componentData = new Dictionary<TypeIndex, byte[]>()
                };
                
                // Capture each component type
                foreach (var componentType in _componentTypes)
                {
                    if (!entityManager.HasComponent(entity, componentType))
                        continue;
                        
                    var typeIndex = componentType.TypeIndex;
                    var typeInfo = TypeManager.GetTypeInfo(typeIndex);
                    
                    if (typeInfo.TypeSize <= 0)
                        continue;
                    
                    // Get raw component data using ComponentType
                    unsafe
                    {
                        var ptr = entityManager.GetComponentDataRawRO(entity, componentType);
                        var data = new byte[typeInfo.TypeSize];
                        fixed (byte* dest = data)
                        {
                            UnsafeUtility.MemCpy(dest, ptr, typeInfo.TypeSize);
                        }
                        snapshot.componentData[typeIndex] = data;
                    }
                }
                
                _entitySnapshots.Add(snapshot);
            }
            
            entities.Dispose();
            _entityCount = _entitySnapshots.Count;
            _isValid = true;
            
            Debug.Log($"[WorldStateSnapshot] Captured {_entityCount} entities with {_componentTypes.Count} component types.");
        }

        /// <summary>
        /// Restore captured state to world.
        /// Note: This only restores component data on existing entities, it does not create/destroy entities.
        /// </summary>
        /// <param name="world">The world to restore to.</param>
        public void Restore(World world)
        {
            if (!_isValid)
            {
                Debug.LogError("[WorldStateSnapshot] Cannot restore - snapshot is not valid.");
                return;
            }
            
            if (world == null || !world.IsCreated)
            {
                Debug.LogError("[WorldStateSnapshot] Cannot restore - world is null or destroyed.");
                return;
            }
            
            var entityManager = world.EntityManager;
            var query = entityManager.CreateEntityQuery(typeof(DeterministicEntityID));
            var entities = query.ToEntityArray(Allocator.Temp);
            
            // Build lookup from deterministic ID to entity
            var idToEntity = new Dictionary<int, Entity>();
            foreach (var entity in entities)
            {
                var id = entityManager.GetComponentData<DeterministicEntityID>(entity).id;
                idToEntity[id] = entity;
            }
            
            int restoredCount = 0;
            
            foreach (var snapshot in _entitySnapshots)
            {
                if (!idToEntity.TryGetValue(snapshot.deterministicId, out var entity))
                {
                    Debug.LogWarning($"[WorldStateSnapshot] Entity with ID {snapshot.deterministicId} not found during restore.");
                    continue;
                }
                
                // Restore each component
                foreach (var kvp in snapshot.componentData)
                {
                    var typeIndex = kvp.Key;
                    var data = kvp.Value;
                    var componentType = ComponentType.FromTypeIndex(typeIndex);
                    
                    if (!entityManager.HasComponent(entity, componentType))
                        continue;
                    
                    unsafe
                    {
                        var ptr = entityManager.GetComponentDataRawRW(entity, componentType);
                        fixed (byte* src = data)
                        {
                            UnsafeUtility.MemCpy(ptr, src, data.Length);
                        }
                    }
                }
                
                restoredCount++;
            }
            
            entities.Dispose();
            Debug.Log($"[WorldStateSnapshot] Restored {restoredCount}/{_entitySnapshots.Count} entities.");
        }

        /// <summary>
        /// Compute hash of the snapshot.
        /// </summary>
        public ulong ComputeHash()
        {
            if (!_isValid)
                return 0;
                
            ulong hash = 0;
            
            // Sort by deterministic ID for consistent hashing
            _entitySnapshots.Sort((a, b) => a.deterministicId.CompareTo(b.deterministicId));
            
            foreach (var snapshot in _entitySnapshots)
            {
                hash = TypeHash.CombineFNV1A64(hash, (ulong)snapshot.deterministicId);
                
                // Sort component types for consistent ordering
                var sortedTypes = new List<TypeIndex>(snapshot.componentData.Keys);
                sortedTypes.Sort((a, b) => a.Value.CompareTo(b.Value));
                
                foreach (var typeIndex in sortedTypes)
                {
                    var data = snapshot.componentData[typeIndex];
                    foreach (var b in data)
                    {
                        hash = TypeHash.CombineFNV1A64(hash, b);
                    }
                }
            }
            
            return hash;
        }

        /// <summary>
        /// Clear captured data.
        /// </summary>
        public void Clear()
        {
            _entitySnapshots.Clear();
            _componentTypes.Clear();
            _isValid = false;
            _entityCount = 0;
        }

        public void Dispose()
        {
            Clear();
        }
    }
    
    /// <summary>
    /// Unsafe utility for memory operations.
    /// </summary>
    public static unsafe class UnsafeUtility
    {
        public static void MemCpy(void* dest, void* src, int size)
        {
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.MemCpy(dest, src, size);
        }
    }
}

