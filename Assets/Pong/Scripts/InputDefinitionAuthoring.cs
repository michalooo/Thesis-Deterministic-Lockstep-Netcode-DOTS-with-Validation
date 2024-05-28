using DeterministicLockstep;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

// public struct MyCustomInputs : IDeterministicInputComponentData
// {
//     public int horizontalInput;
//     public int verticalInput;
//
//     public void SerializeInputs(DataStreamWriter writer)
//     {
//         writer.WriteInt(horizontalInput);
//         writer.WriteInt(verticalInput);
//     }
//
//     public void DeserializeInputs(DataStreamReader reader) //question how user can know if the order will be correct? --> Same order as serialization
//     {
//         horizontalInput = reader.ReadInt();
//         verticalInput = reader.ReadInt();
//     }
// }
//
// public class PlayerInputAuthoring : MonoBehaviour
// {
//     class Baker : Baker<PlayerInputAuthoring>
//     {
//         public override void Bake(PlayerInputAuthoring authoring)
//         {
//             var entity = GetEntity(TransformUsageFlags.Dynamic);
//             AddComponent(entity, default(MyCustomInputs));
//             
//             var playerComponent = default(PlayerInput);
//             playerComponent.InputComponentType = ComponentType.ReadWrite<MyCustomInputs>();
//             AddComponent(entity, playerComponent);
//         }
//     }
// }