using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

// This system is responsible for updating all of players positions based on their input component (updated by the server) if they are spawned and if
// UpdatePlayerPosition component is enabled. After updating those positions this component will be disabled
[UpdateAfter(typeof(SpawnPlayerSystem))]
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
        
        Entities.WithName("UpdatePlayersPositions")
            .WithAll<PlayerInputDataToUse, PlayerSpawned, CommandTarget>()
            .ForEach((Entity connection, ref PlayerInputDataToUse inputData, ref CommandTarget target) =>
            {
                Debug.Log($"Updating position for player of id: {inputData.playerNetworkId}");
                int horizontalInput = inputData.horizontalInput;
                int verticalInput = inputData.verticalInput;
                
                Debug.Log("horizontalInput: " + horizontalInput + " verticalInput: " + verticalInput);
                
                LocalToWorld targetTransform = SystemAPI.GetComponent<LocalToWorld>(target.targetEntity);
                float3 targetPosition = targetTransform.Position;
                targetPosition.x += horizontalInput;
                targetPosition.z += verticalInput;

                commandBuffer.SetComponent(target.targetEntity, LocalTransform.FromPosition(targetPosition));
                
                commandBuffer.SetComponentEnabled<PlayerInputDataToUse>(connection, false);
            }).Run();
        
        commandBuffer.Playback(EntityManager);
    }
}
