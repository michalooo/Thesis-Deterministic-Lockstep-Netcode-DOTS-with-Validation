using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace DeterministicLockstep
{
    /// <summary>
    /// Job that will run on chunks to check and hash aproperiate components in them depending on validation option.
    /// </summary>
    public unsafe struct GameStateHashJob : IJobChunk
    {
        /// <summary>
        /// Hash calculation option set for the game
        /// </summary>
        [ReadOnly]
        public DeterminismHashCalculationOption hashCalculationOption;
        
        /// <summary>
        /// List of deterministic types to check
        /// </summary>
        [ReadOnly]
        public DynamicTypeList listOfDeterministicTypes;
        
        /// <summary>
        /// EntityTypeHandle used to get entities from the chunk
        /// </summary>
        [ReadOnly]
        public EntityTypeHandle entityTypeHandle;
        
        /// <summary>
        /// HashMap used to organize the logging data on per entity basis.
        /// It contain info about the component type and its hash.
        /// </summary>
        public NativeParallelMultiHashMap<Entity, KeyValuePair<TypeIndex, ulong>>.ParallelWriter logHashMap;
        
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var entitiesArray = chunk.GetNativeArray(entityTypeHandle);
            
            var dynamicTypeListPtr = listOfDeterministicTypes.GetData();

            switch (hashCalculationOption)
            {
                case DeterminismHashCalculationOption.WhitelistHashPerSystem or DeterminismHashCalculationOption.WhiteListHashPerTick:
                    if (!chunk.Has<CountEntityForWhitelistedDeterminismValidation>() || !chunk.Has<DeterministicEntityID>()) return; // For those option we need to check if the chunk belongs to whitelisted entity

                    break;
                case DeterminismHashCalculationOption.FullStateHashPerSystem or DeterminismHashCalculationOption.FullStateHashPerTick:
                    if (!chunk.Has<DeterministicEntityID>()) return; // For those option we need to check if the chunk belongs to whitelisted entity

                    break;
            }
            
            for (var i = 0; i < chunk.Count; i++)
            {
                var entityInChunk = entitiesArray[i];
                            
                for (var j = 0; j < listOfDeterministicTypes.Length; j++) // For each entity listed for validation which is assigned to the entity
                { 
                    if (!chunk.Has(dynamicTypeListPtr[j])) continue;

                    var dynamicComponentTypeHandle = dynamicTypeListPtr[j];
                    var componentTypeInfo = TypeManager.GetTypeInfo(dynamicComponentTypeHandle.TypeIndex);
                    var rawComponentByteData = chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref dynamicComponentTypeHandle, componentTypeInfo.TypeSize);
                    
                    // Calculate the start and end index for the current entity's data slice
                    var startIndex = i * componentTypeInfo.TypeSize;
                    var endIndex = startIndex + componentTypeInfo.TypeSize;
                    
                    var componentHash = (ulong) 0; // This is used to calculate the hash for the current component. In contrast to hash, this is local to every component and used for logging

                    // Extract the bytes for this entity and hash each of them. This allows to achieve bit-wise comparison of the data
                    for (var byteIndex = startIndex; byteIndex < endIndex; byteIndex++)
                    {
                        componentHash = TypeHash.CombineFNV1A64(componentHash, rawComponentByteData[byteIndex]);
                    }
                                
                    var logForComponent = new KeyValuePair<TypeIndex, ulong>(dynamicComponentTypeHandle.TypeIndex, componentHash);
                    logHashMap.Add(entityInChunk, logForComponent);
                }
            }
        }
    }
}