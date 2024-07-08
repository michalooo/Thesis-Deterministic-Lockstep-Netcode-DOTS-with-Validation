using DeterministicLockstep;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PongGame
{
    /// <summary>
    /// System responsible for updating all of players positions based on their PlayerInputDataToUse component.
    /// After updating those positions this component will be disabled signalling that those informations were applied
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
    public partial struct PlayerMovementSystem : ISystem
    {
        /// <summary>
        /// Query to get all of the players that are currently in the game
        /// </summary>
        private EntityQuery playerQuery;
        
        /// <summary>
        /// Interpolation speed for the player movement
        /// </summary>
        private const float interpolationSpeed = 0.2f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerInputDataToUse>(); 
            state.RequireForUpdate<PongInputs>();
            playerQuery = state.GetEntityQuery(typeof(GhostOwner), typeof(PlayerInputDataToUse), typeof(PlayerSpawned));
        }

        public void OnUpdate(ref SystemState state)
        {
            var ghostOwnerData = playerQuery.ToComponentDataArray<GhostOwner>(Allocator.Temp);
            var playerInputData = playerQuery.ToComponentDataArray<PlayerInputDataToUse>(Allocator.Temp);
            var connectionEntity = playerQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < playerInputData.Length; i++)
            {
                if (playerInputData[i].isPlayerDisconnected)
                {
                    Debug.Log("Destroying entity with ID: " + playerInputData[i].clientNetworkId);
                    state.EntityManager.DestroyEntity(ghostOwnerData[i].connectionCommandsTargetEntity);
                    state.EntityManager.DestroyEntity(connectionEntity[i]);
                }
                else
                {
                    var verticalInput = playerInputData[i].playerInputToApply.verticalInput;

                    var targetTransform = SystemAPI.GetComponentRW<LocalTransform>(ghostOwnerData[i].connectionCommandsTargetEntity);
                    var targetPosition = targetTransform.ValueRO.Position;

                    var newPositionY = targetPosition.y + (state.World.Time.DeltaTime * verticalInput);
                    
                    // Check if the new position is within the bounds
                    if (newPositionY < GameSettings.Instance.BottomScreenPosition)
                    {
                        newPositionY = GameSettings.Instance.BottomScreenPosition;
                    }
                    else if (newPositionY > GameSettings.Instance.TopScreenPosition)
                    {
                        newPositionY = GameSettings.Instance.TopScreenPosition;
                    }
                    
                    
                    // Interpolate from the current position to the new position
                    targetPosition.y = Mathf.Lerp(targetPosition.y, newPositionY, interpolationSpeed);
                    
                    state.EntityManager.SetComponentData(ghostOwnerData[i].connectionCommandsTargetEntity, new LocalTransform
                    {
                        Position = new float3(targetPosition.x, targetPosition.y, targetPosition.z),
                        Scale = 1f,
                        Rotation = quaternion.identity
                    });

                    state.EntityManager.SetComponentEnabled<PlayerInputDataToUse>(connectionEntity[i],
                        false);
                }
            }
        }
    }
}