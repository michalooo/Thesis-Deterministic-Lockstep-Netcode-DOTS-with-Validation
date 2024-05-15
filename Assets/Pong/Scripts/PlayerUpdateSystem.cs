using DeterministicLockstep;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace PongGame
{
    /// <summary>
    /// System responsible for updating all of players positions based on their input component (updated by the server) if they are spawned and if UpdatePlayerPosition component is enabled. After updating those positions this component will be disabled
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GameStateUpdateSystemGroup))]
    [UpdateAfter(typeof(PongBallSpawnerSystem))]
    public partial class PlayerUpdateSystem : SystemBase
    {
        private EntityQuery playerQuery;
        private const float minZ = -3.5f;
        private const float maxZ = 3.5f;

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerInputDataToUse>(); // component from which data should be taken
            RequireForUpdate<PongInputs>();
        }

        protected override void OnUpdate()
        {
            playerQuery = GetEntityQuery(typeof(CommandTarget), typeof(PlayerInputDataToUse), typeof(PlayerSpawned));

            // Get the component data from the entities
            var commandTargetData = playerQuery.ToComponentDataArray<CommandTarget>(Allocator.Temp);
            var playerInputData = playerQuery.ToComponentDataArray<PlayerInputDataToUse>(Allocator.Temp);
            var connectionEntity = playerQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < playerInputData.Length; i++)
            {
                if (playerInputData[i].playerDisconnected)
                {
                    Debug.Log("Destroying entity with ID: " + playerInputData[i].playerNetworkId);
                    EntityManager.DestroyEntity(commandTargetData[i].targetEntity);
                    EntityManager.DestroyEntity(connectionEntity[i]);
                }
                else
                {
                    var verticalInput = playerInputData[i].inputToUse.verticalInput;

                    var targetTransform = SystemAPI.GetComponent<LocalToWorld>(commandTargetData[i].targetEntity);
                    var targetPosition = targetTransform.Position;
                    
                    targetPosition.z += verticalInput;
                    
                    
                    // Check if the new position is within the bounds
                    if (targetPosition.z < minZ)
                    {
                        targetPosition.z = minZ;
                    }
                    else if (targetPosition.z > maxZ)
                    {
                        targetPosition.z = maxZ;
                    }

                    EntityManager.SetComponentData(commandTargetData[i].targetEntity,
                        LocalTransform.FromPosition(targetPosition));

                    EntityManager.SetComponentEnabled<PlayerInputDataToUse>(connectionEntity[i],
                        false); //should it be required?
                }
            }
        }
    }
}