using DeterministicLockstep;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
public struct MyCapsulesCustomInputs : IDeterministicInputComponentData
{
    public int horizontalInput;
    public int verticalInput;

    public void SerializeInputs(DataStreamWriter writer)
    {
        writer.WriteInt(horizontalInput);
        writer.WriteInt(verticalInput);
    }

    public void DeserializeInputs(DataStreamReader reader)
    {
        horizontalInput = reader.ReadInt();
        verticalInput = reader.ReadInt();
    }
}

public class InputDefinitionAuthoring : MonoBehaviour
{
    class Baker : Baker<InputDefinitionAuthoring>
    {
        public override void Bake(InputDefinitionAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<MyCapsulesCustomInputs>(entity);
            
            var playerComponent = default(PlayerInputType);
            playerComponent.InputComponentType = ComponentType.ReadWrite<MyCapsulesCustomInputs>();
            AddComponent(entity, playerComponent);
            
            AddComponent<DynamicBuffer<InputBufferData<MyCapsulesCustomInputs>>>(entity);
        }
    }
}