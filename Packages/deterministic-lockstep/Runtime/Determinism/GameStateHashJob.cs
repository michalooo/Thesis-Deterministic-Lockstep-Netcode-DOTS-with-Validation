using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// Job that will run on chunks to check and hash aproperiate components in them depending on validation option.
    /// </summary>
    [BurstCompile]
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
        /// NativeArray containing resulting hashes for each chunk
        /// </summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<ulong> resultsNativeArray;
        
        /// <summary>
        /// EntityTypeHandle used to get entities from the chunk
        /// </summary>
        [ReadOnly]
        public EntityTypeHandle entityType;
        
        /// <summary>
        /// HashMap used to organize the logging data on per entity basis.
        /// It contain info about the component type and its hash.
        /// </summary>
        public NativeParallelMultiHashMap<Entity, KeyValuePair<TypeIndex, ulong>>.ParallelWriter logMap;
        
        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            NativeArray<Entity> entitiesArray = chunk.GetNativeArray(entityType);
            var dynamicTypeListPtr = listOfDeterministicTypes.GetData();
            var hash = (ulong) 0; // This is used to calculate the final hash of the chunk
            
            switch (hashCalculationOption)
            {
                case DeterminismHashCalculationOption.WhitelistHashPerSystem or DeterminismHashCalculationOption.WhiteListHashPerTick:
                    if (chunk.Has<CountEntityForWhitelistedDeterminismValidation>()) // For those option we need to check if the chunk belongs to whitelisted entity
                    {
                        for (var i = 0; i < chunk.Count; i++)
                        {
                            Entity entity = entitiesArray[i];
                            
                            for (var j = 0; j < listOfDeterministicTypes.Length; j++) // For each entity listed for validation which is assigned to the entity
                            { 
                                if (!chunk.Has(dynamicTypeListPtr[j])) continue;

                                var dynamicComponentTypeHandle = dynamicTypeListPtr[j];
                                var typeInfo = TypeManager.GetTypeInfo(dynamicComponentTypeHandle.TypeIndex);
                                var rawByteData = chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref dynamicComponentTypeHandle, typeInfo.TypeSize);
                                
                                // Calculate the start index for the current entity's data slice
                                var startIndex = i * typeInfo.TypeSize;
                                // Calculate the end index
                                var endIndex = startIndex + typeInfo.TypeSize;
                                var localHash = (ulong) 0; // This is used to calculate the hash for the current component. In contrast to hash, this is local to every component and used for logging

                                // Extract the bytes for this entity and hash each of them. This allows to achieve bit-wise comparison of the data
                                for (var byteIndex = startIndex; byteIndex < endIndex; byteIndex++)
                                {
                                    localHash = TypeHash.CombineFNV1A64(localHash, rawByteData[byteIndex]);
                                    hash = TypeHash.CombineFNV1A64(hash, rawByteData[byteIndex]);
                                }
                                
                                var log = new KeyValuePair<TypeIndex, ulong>(dynamicComponentTypeHandle.TypeIndex, localHash);
                                logMap.Add(entity, log);
                            }
                        }
                    }

                    break;
                case DeterminismHashCalculationOption.FullStateHashPerSystem or DeterminismHashCalculationOption.FullStateHashPerTick:
                    for (var i = 0; i < chunk.Count; i++)
                    {
                        Entity entity = entitiesArray[i];
                            
                        for (var j = 0; j < listOfDeterministicTypes.Length; j++)
                        {
                            if (!chunk.Has(dynamicTypeListPtr[j])) continue;

                            var dynamicComponentTypeHandle = dynamicTypeListPtr[j];
                            var typeInfo = TypeManager.GetTypeInfo(dynamicComponentTypeHandle.TypeIndex);
                            var rawByteData = chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref dynamicComponentTypeHandle, typeInfo.TypeSize);
                                
                            // Calculate the start index for the current entity's data slice
                            int startIndex = i * typeInfo.TypeSize;
                            // Calculate the end index
                            int endIndex = startIndex + typeInfo.TypeSize;
                            var localHash = (ulong) 0; // This is used to calculate the hash for the current component. In contrast to hash, this is local to every component and used for logging

                            // Extract the bytes for this entity and hash each of them. This allows to achieve bit-wise comparison of the data
                            for (int byteIndex = startIndex; byteIndex < endIndex; byteIndex++)
                            {
                                localHash = TypeHash.CombineFNV1A64(localHash, rawByteData[byteIndex]);
                                hash = TypeHash.CombineFNV1A64(hash, rawByteData[byteIndex]);
                            }
                            
                            var log = new KeyValuePair<TypeIndex, ulong>(dynamicComponentTypeHandle.TypeIndex, localHash);
                            logMap.Add(entity, log);
                        }
                    }

                    break;
            }

            resultsNativeArray[unfilteredChunkIndex] = hash;
        }
    }
}