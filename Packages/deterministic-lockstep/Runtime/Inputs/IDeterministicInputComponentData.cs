using Unity.Collections;
using Unity.Entities;

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
    
    public struct PlayerInputType : IComponentData
    {
        public ComponentType InputComponentType;
    }
    
    public interface ICommandData : IBufferElementData
    { 
        int networkTick { get; set; }
    }
    
    [InternalBufferCapacity(16)]
    public struct InputBufferData<T> : IBufferElementData where T: unmanaged, IDeterministicInputComponentData
    {
        public int networkTick { get; set; }
        public T InternalInput;
    }
}