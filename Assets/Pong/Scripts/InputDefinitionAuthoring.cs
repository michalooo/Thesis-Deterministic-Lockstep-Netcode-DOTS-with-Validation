using DeterministicLockstep;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
public struct MyPongCustomInputs : IDeterministicInputComponentData
{
    public int verticalInput;

    public void SerializeInputs(DataStreamWriter writer)
    {
        writer.WriteInt(verticalInput);
    }

    public void DeserializeInputs(DataStreamReader reader)
    {
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
            AddComponent<MyPongCustomInputs>(entity);
            
            // playerComponent.InputComponentType = ComponentType.ReadWrite<MyPongCustomInputs>();
            // AddComponent(entity, playerComponent); // on create runtimr
            
            AddBuffer<InputBufferData<MyPongCustomInputs>>(entity);
        }
    }
}