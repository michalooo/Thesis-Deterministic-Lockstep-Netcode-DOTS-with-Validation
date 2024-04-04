using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

// This system is responsible for updating all of players positions based on their input component (updated by the server) if they are spawned and if
// UpdatePlayerPosition component is enabled. After updating those positions this component will be disabled
[UpdateInGroup(typeof(MovementSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class PlayerUpdateSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<PlayerInputDataToUse>();
    }

    protected override void OnUpdate()
    {
        var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (commandTarget, playerInputDataToUse, connectionEntity) in SystemAPI.Query<RefRO<CommandTarget>, RefRO<PlayerInputDataToUse>>().WithAll<PlayerSpawned>().WithEntityAccess())
        {
            if (playerInputDataToUse.ValueRO.playerDisconnected)
            {
                Debug.Log("Destroying entity with ID: " + playerInputDataToUse.ValueRO.playerNetworkId);
                commandBuffer.DestroyEntity(commandTarget.ValueRO.targetEntity);
                commandBuffer.DestroyEntity(connectionEntity);
            }
            else
            {
                int horizontalInput = playerInputDataToUse.ValueRO.horizontalInput;
                int verticalInput = playerInputDataToUse.ValueRO.verticalInput;
                
                LocalToWorld targetTransform = SystemAPI.GetComponent<LocalToWorld>(commandTarget.ValueRO.targetEntity);
                float3 targetPosition = targetTransform.Position;
                targetPosition.x += horizontalInput;
                targetPosition.z += verticalInput;

                commandBuffer.SetComponent(commandTarget.ValueRO.targetEntity, LocalTransform.FromPosition(targetPosition));
                
                commandBuffer.SetComponentEnabled<PlayerInputDataToUse>(connectionEntity, false);
            }
        }
        
        commandBuffer.Playback(EntityManager);
    }
}
