using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using System.Runtime.InteropServices;

namespace DeterministicLockstep
{
    /// <summary>
    /// Fixed-size array of TypeIndex values to accompany DynamicTypeList.
    /// Needed because DynamicComponentTypeHandle no longer exposes TypeIndex in newer Unity Entities versions.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct TypeIndexList
    {
        #if NETCODE_COMPONENTS_256
        public const int MaxCapacity = 256;
        #else
        public const int MaxCapacity = 128;
        #endif
        
        private fixed int typeIndices[MaxCapacity];
        public int Length;
        
        public TypeIndex GetTypeIndex(int index)
        {
            return new TypeIndex { Value = typeIndices[index] };
        }
        
        public void SetTypeIndex(int index, TypeIndex typeIndex)
        {
            typeIndices[index] = typeIndex.Value;
        }
    }
    
    /// <summary>
    /// This struct stores all component types we're checking when performing determinism validation.
    /// It only exists because of an IJob limitation where <see cref="DynamicComponentTypeHandle"/>'s MUST be defined as fields.
    /// I.e. Collections containing <see cref="DynamicComponentTypeHandle"/>'s are not valid.
    /// This struct was directly copied from N4E code.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DynamicTypeList
    {
        #if NETCODE_COMPONENTS_256
        public const int MaxCapacity = 256;
        #else
        public const int MaxCapacity = 128;
        #endif

        public static unsafe void PopulateList(ref SystemState system, DynamicBuffer<DeterministicComponent> deterministicComponentsCollection, bool readOnly, ref DynamicTypeList list, ref TypeIndexList typeIndexList)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<DynamicComponentTypeHandle32>() != Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<DynamicComponentTypeHandle>()*32)
                throw new System.Exception("Invalid type size, this will cause undefined behavior");
#endif
            var listLength = deterministicComponentsCollection.Length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (listLength > MaxCapacity)
                throw new System.Exception($"Invalid number of components used for ghost serialization: {listLength}, max is {MaxCapacity}. The maximum limit can be increased up to 256 by defining NETCODE_COMPONENTS_256.");
#endif
            DynamicComponentTypeHandle* GhostChunkComponentTypesPtr = list.GetData();
            list.Length = listLength;
            typeIndexList.Length = listLength;
            for (int i = 0; i < list.Length; ++i)
            {
                var compType = deterministicComponentsCollection[i].type;
                if (readOnly)
                    compType.AccessModeType = ComponentType.AccessMode.ReadOnly;
                GhostChunkComponentTypesPtr[i] = system.GetDynamicComponentTypeHandle(compType);
                typeIndexList.SetTypeIndex(i, compType.TypeIndex);
            }
        }

        public static unsafe void PopulateListFromArray(ref SystemState system, NativeArray<ComponentType> componentTypes,  bool readOnly, ref DynamicTypeList list, ref TypeIndexList typeIndexList)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<DynamicComponentTypeHandle32>() != Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<DynamicComponentTypeHandle>()*32)
                throw new System.Exception("Invalid type size, this will cause undefined behavior");
#endif

            DynamicComponentTypeHandle* componentTypesPtr = list.GetData();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (componentTypes.Length > MaxCapacity)
                throw new System.Exception($"Invalid number of components used for ghost serialization: {componentTypes.Length}, max is {MaxCapacity}. The maximum limit can be increased up to 256 by defining NETCODE_COMPONENTS_256.");
#endif
            list.Length = componentTypes.Length;
            typeIndexList.Length = componentTypes.Length;
            for (int i = 0; i < list.Length; ++i)
            {
                var compType = componentTypes[i];
                if (readOnly)
                    compType.AccessModeType = ComponentType.AccessMode.ReadOnly;
                componentTypesPtr[i] = system.GetDynamicComponentTypeHandle(compType);
                typeIndexList.SetTypeIndex(i, compType.TypeIndex);
            }
        }

        private DynamicComponentTypeHandle32 dynamicType000;
#pragma warning disable 0169
        private DynamicComponentTypeHandle32 dynamicType032;
        private DynamicComponentTypeHandle32 dynamicType064;
        private DynamicComponentTypeHandle32 dynamicType096;
        #if NETCODE_COMPONENTS_256
        private DynamicComponentTypeHandle32 dynamicType128;
        private DynamicComponentTypeHandle32 dynamicType160;
        private DynamicComponentTypeHandle32 dynamicType192;
        private DynamicComponentTypeHandle32 dynamicType224;
        #endif
#pragma warning restore 0169
        public int Length { get; set; }

        public unsafe DynamicComponentTypeHandle* GetData()
        {
            fixed (DynamicComponentTypeHandle* ptr = &dynamicType000.dynamicType00)
            {
                return ptr;
            }
        }
    }

    /// <summary>
    /// This struct only exists because of an IJob limitation where <see cref="DynamicComponentTypeHandle"/>'s MUST be defined as fields.
    /// I.e. Collections containing <see cref="DynamicComponentTypeHandle"/>'s are not valid.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct DynamicComponentTypeHandle32
    {
        public DynamicComponentTypeHandle dynamicType00;
        public DynamicComponentTypeHandle dynamicType01;
        public DynamicComponentTypeHandle dynamicType02;
        public DynamicComponentTypeHandle dynamicType03;
        public DynamicComponentTypeHandle dynamicType04;
        public DynamicComponentTypeHandle dynamicType05;
        public DynamicComponentTypeHandle dynamicType06;
        public DynamicComponentTypeHandle dynamicType07;
        public DynamicComponentTypeHandle dynamicType08;
        public DynamicComponentTypeHandle dynamicType09;
        public DynamicComponentTypeHandle dynamicType10;
        public DynamicComponentTypeHandle dynamicType11;
        public DynamicComponentTypeHandle dynamicType12;
        public DynamicComponentTypeHandle dynamicType13;
        public DynamicComponentTypeHandle dynamicType14;
        public DynamicComponentTypeHandle dynamicType15;
        public DynamicComponentTypeHandle dynamicType16;
        public DynamicComponentTypeHandle dynamicType17;
        public DynamicComponentTypeHandle dynamicType18;
        public DynamicComponentTypeHandle dynamicType19;
        public DynamicComponentTypeHandle dynamicType20;
        public DynamicComponentTypeHandle dynamicType21;
        public DynamicComponentTypeHandle dynamicType22;
        public DynamicComponentTypeHandle dynamicType23;
        public DynamicComponentTypeHandle dynamicType24;
        public DynamicComponentTypeHandle dynamicType25;
        public DynamicComponentTypeHandle dynamicType26;
        public DynamicComponentTypeHandle dynamicType27;
        public DynamicComponentTypeHandle dynamicType28;
        public DynamicComponentTypeHandle dynamicType29;
        public DynamicComponentTypeHandle dynamicType30;
        public DynamicComponentTypeHandle dynamicType31;
    }
}
