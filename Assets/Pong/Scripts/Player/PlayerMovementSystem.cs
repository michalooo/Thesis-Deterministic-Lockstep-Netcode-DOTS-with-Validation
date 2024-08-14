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
    /// After updating those positions this component will be disabled signalling that those information were applied
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
    public partial struct PlayerMovementSystem : ISystem
    {
        private EntityQuery _playersQuery;
        private const float PlayerSpeedInterpolationSpeed = 0.2f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerInputDataToUse>(); 
            state.RequireForUpdate<PongInputs>();
            _playersQuery = state.GetEntityQuery(typeof(GhostOwner), typeof(PlayerInputDataToUse), typeof(PlayerSpawned));
        }

        public void OnUpdate(ref SystemState state)
        {
            var ghostOwnersData = _playersQuery.ToComponentDataArray<GhostOwner>(Allocator.Temp);
            var playersInputData = _playersQuery.ToComponentDataArray<PlayerInputDataToUse>(Allocator.Temp);
            var connectionEntities = _playersQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < playersInputData.Length; i++)
            {
                if (playersInputData[i].isPlayerDisconnected)
                {
                    state.EntityManager.DestroyEntity(ghostOwnersData[i].connectionCommandsTargetEntity);
                    state.EntityManager.DestroyEntity(connectionEntities[i]);
                }
                else
                {
                    var playerVerticalInput = playersInputData[i].playerInputToApply.verticalInput;

                    var playerCurrentTransform = SystemAPI.GetComponentRW<LocalTransform>(ghostOwnersData[i].connectionCommandsTargetEntity);
                    var playerCurrentPosition = playerCurrentTransform.ValueRO.Position;

                    var playerNewPositionY = playerCurrentPosition.y + (state.World.Time.DeltaTime * playerVerticalInput);
                    
                    // Check if the new position is within the bounds
                    if (playerNewPositionY < GameSettings.Instance.BottomScreenPosition)
                    {
                        playerNewPositionY = GameSettings.Instance.BottomScreenPosition;
                    }
                    else if (playerNewPositionY > GameSettings.Instance.TopScreenPosition)
                    {
                        playerNewPositionY = GameSettings.Instance.TopScreenPosition;
                    }
                    
                    
                    // Interpolate from the current position to the new position
                    playerCurrentPosition.y = Mathf.Lerp(playerCurrentPosition.y, playerNewPositionY, PlayerSpeedInterpolationSpeed);
                    
                    state.EntityManager.SetComponentData(ghostOwnersData[i].connectionCommandsTargetEntity, new LocalTransform
                    {
                        Position = new float3(playerCurrentPosition.x, playerCurrentPosition.y, playerCurrentPosition.z),
                        Scale = 1f,
                        Rotation = quaternion.identity
                    });

                    state.EntityManager.SetComponentEnabled<PlayerInputDataToUse>(connectionEntities[i],
                        false);
                }
            }
        }
    }
}