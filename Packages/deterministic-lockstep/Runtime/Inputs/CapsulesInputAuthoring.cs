using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    // Capsules inputs should be needed for serialization/deserialization, yet
    public struct CapsulesInputs: IComponentData
    {
        public int horizontalInput;
        public int verticalInput;
    
        public void SerializeInputs(ref DataStreamWriter writer)
        {
            writer.WriteInt(verticalInput);
            writer.WriteInt(horizontalInput);
        }
    
        public void
            DeserializeInputs(ref 
                DataStreamReader reader) 
        {
            verticalInput = reader.ReadInt();
            horizontalInput = reader.ReadInt();
        }
    }
    
    public class CapsulesInputAuthoring : MonoBehaviour
    {
        class Baker : Baker<CapsulesInputAuthoring>
        {
            public override void Bake(CapsulesInputAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, default(CapsulesInputs));
            }
        }
    }
}