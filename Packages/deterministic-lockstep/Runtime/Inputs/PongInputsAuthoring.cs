using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    public struct PongInputs: IComponentData
    {
        public int horizontalInput;
        public int verticalInput;

        public void SerializeInputs(DataStreamWriter writer)
        {
            writer.WriteInt(horizontalInput);
            writer.WriteInt(verticalInput);
        }

        public void
            DeserializeInputs(
                DataStreamReader reader) //question how user can know if the order will be correct? --> Same order as serialization
        {
            horizontalInput = reader.ReadInt();
            verticalInput = reader.ReadInt();
        }
    }

    
    
    public class PongInputAuthoring : MonoBehaviour
    {
        class Baker : Baker<PongInputAuthoring>
        {
            public override void Bake(PongInputAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, default(PongInputs));
            }
        }
    }
}