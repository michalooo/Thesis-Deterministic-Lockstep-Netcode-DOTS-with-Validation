using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Logging;
using Unity.Transforms;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// Job that will run on chunks to check and hash the components in them.
    /// </summary>

    [BurstCompile]
    public unsafe struct DeterminismCheckJob : IJobChunk
    {
        [ReadOnly]
        public DeterminismHashCalculationOption hashCalculationOption;
        
        [ReadOnly]
        public DynamicTypeList listOfDeterministicTypes;
        
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<ulong> resultsNativeArray;
        
        [ReadOnly]
        public EntityTypeHandle entityType;
        
        public NativeParallelMultiHashMap<Entity, KeyValuePair<TypeIndex, ulong>>.ParallelWriter logMap;
        
        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            NativeArray<Entity> entitiesArray = chunk.GetNativeArray(entityType);
            var dynamicTypeListPtr = listOfDeterministicTypes.GetData();
            var hash = (ulong) 0;
            
            switch (hashCalculationOption)
            {
                case DeterminismHashCalculationOption.WhitelistHashPerSystem or DeterminismHashCalculationOption.WhiteListHashPerTick:
                    if (chunk.Has<EnsureDeterministicBehaviour>())
                    {
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
                                var localHash = (ulong) 0;

                                // Extract the bytes for this entity
                                for (int byteIndex = startIndex; byteIndex < endIndex; byteIndex++)
                                {
                                    localHash = TypeHash.CombineFNV1A64(localHash, rawByteData[byteIndex]);
                                    hash = TypeHash.CombineFNV1A64(hash, rawByteData[byteIndex]);
                                }

                                // Optionally store the logging data
                                var log = new KeyValuePair<TypeIndex, ulong>(dynamicComponentTypeHandle.TypeIndex, localHash); // Example, adjust as needed
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
                            var localHash = (ulong) 0;

                            // Extract the bytes for this entity
                            for (int byteIndex = startIndex; byteIndex < endIndex; byteIndex++)
                            {
                                localHash = TypeHash.CombineFNV1A64(localHash, rawByteData[byteIndex]);
                                hash = TypeHash.CombineFNV1A64(hash, rawByteData[byteIndex]);
                            }

                            // Optionally store the logging data
                            var log = new KeyValuePair<TypeIndex, ulong>(dynamicComponentTypeHandle.TypeIndex, localHash); // Example, adjust as needed
                            logMap.Add(entity, log);
                        }
                    }

                    break;
            }
            

            

            resultsNativeArray[unfilteredChunkIndex] = hash; // check out if the array is large enough to support it 
        }
    }
}