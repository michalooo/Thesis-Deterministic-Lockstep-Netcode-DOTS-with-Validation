using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace DeterministicLockstep
{
    /// <summary>
    /// Job that runs on chunks to hash components for determinism validation.
    /// </summary>
    [BurstCompile]
    public unsafe struct GameStateHashJob : IJobChunk
    {
        /// <summary>
        /// Which entities to include in hashing.
        /// </summary>
        [ReadOnly]
        public HashScope hashScope;
        
        /// <summary>
        /// List of component types to hash.
        /// </summary>
        [ReadOnly]
        public DynamicTypeList listOfDeterministicTypes;
        
        /// <summary>
        /// EntityTypeHandle for accessing entities in chunks.
        /// </summary>
        [ReadOnly]
        public EntityTypeHandle entityTypeHandle;
        
        /// <summary>
        /// Output map: Entity -> (ComponentType, Hash) pairs.
        /// </summary>
        public NativeParallelMultiHashMap<Entity, KeyValuePair<TypeIndex, ulong>>.ParallelWriter logHashMap;
        
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            // All entities must have DeterministicEntityID to be considered
            if (!chunk.Has<DeterministicEntityID>())
                return;
            
            // For whitelist mode, also require the whitelist tag
            if (hashScope == HashScope.WhitelistedEntitiesOnly)
            {
                if (!chunk.Has<CountEntityForWhitelistedDeterminismValidation>())
                    return;
            }
            
            var entitiesArray = chunk.GetNativeArray(entityTypeHandle);
            var dynamicTypeListPtr = listOfDeterministicTypes.GetData();
            
            for (var i = 0; i < chunk.Count; i++)
            {
                var entity = entitiesArray[i];
                            
                for (var j = 0; j < listOfDeterministicTypes.Length; j++)
                { 
                    if (!chunk.Has(dynamicTypeListPtr[j]))
                        continue;

                    var dynamicComponentTypeHandle = dynamicTypeListPtr[j];
                    var componentTypeInfo = TypeManager.GetTypeInfo(dynamicComponentTypeHandle.TypeIndex);
                    var rawComponentByteData = chunk.GetDynamicComponentDataArrayReinterpret<byte>(
                        ref dynamicComponentTypeHandle, componentTypeInfo.TypeSize);
                    
                    // Calculate byte range for this entity's component data
                    var startIndex = i * componentTypeInfo.TypeSize;
                    var endIndex = startIndex + componentTypeInfo.TypeSize;
                    
                    // Hash all bytes of the component
                    ulong componentHash = 0;
                    for (var byteIndex = startIndex; byteIndex < endIndex; byteIndex++)
                    {
                        componentHash = TypeHash.CombineFNV1A64(componentHash, rawComponentByteData[byteIndex]);
                    }
                                
                    var logEntry = new KeyValuePair<TypeIndex, ulong>(dynamicComponentTypeHandle.TypeIndex, componentHash);
                    logHashMap.Add(entity, logEntry);
                }
            }
        }
    }
}
