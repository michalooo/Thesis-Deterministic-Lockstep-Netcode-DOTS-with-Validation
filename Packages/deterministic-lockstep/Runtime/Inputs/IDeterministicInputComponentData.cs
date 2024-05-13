using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeterministicLockstep
{
    // /// <summary>
    // /// A special component data interface used for storing player inputs.
    // /// </summary>
    // public interface IDeterministicInputComponentData: IComponentData
    // {
    //     void SerializeInputs(DataStreamWriter writer);
    //     void DeserializeInputs(DataStreamReader reader);
    // }
    //
    // public struct PlayerInput : IComponentData
    // {
    //     public ComponentType InputComponentType;
    // }

    public struct CapsulesInputs: IComponentData
    {
        public int networkID;
        public int tick;
        
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
    
    public struct PongInputs: IComponentData
    {
        public int networkID;
        public int tick;
        
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