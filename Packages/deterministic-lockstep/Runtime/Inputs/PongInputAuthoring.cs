using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    public struct PongInputs: IComponentData
    {
        public int verticalInput;

        public void SerializeInputs(ref DataStreamWriter writer)
        {
            writer.WriteInt(verticalInput);
        }

        public void
            DeserializeInputs(
                ref DataStreamReader reader) //question how user can know if the order will be correct? --> Same order as serialization
        {
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
                // AddComponent<PongInputs>(entity);
            }
        }
    }
}