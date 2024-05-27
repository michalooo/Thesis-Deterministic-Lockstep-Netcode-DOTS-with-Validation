using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeterministicLockstep
{
    /// <summary>
    /// A special component data interface used for storing player inputs.
    /// </summary>
    public interface IDeterministicInputComponentData: IComponentData
    {
        void SerializeInputs(DataStreamWriter writer);
        void DeserializeInputs(DataStreamReader reader);
    }
    
  
    
    public struct InputBufferData<T> : IBufferElementData where T: unmanaged, IDeterministicInputComponentData
    {
        public T InternalInput;
    }
    
}