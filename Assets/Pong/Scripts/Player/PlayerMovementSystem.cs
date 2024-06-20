﻿using DeterministicLockstep;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PongGame
{
    /// <summary>
    /// System responsible for updating all of players positions based on their input component (updated by the server) if they are spawned and if UpdatePlayerPosition component is enabled. After updating those positions this component will be disabled
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
    // [UpdateAfter(typeof(PongBallDestructionSystem))]
    public partial struct PlayerMovementSystem : ISystem
    {
        private EntityQuery playerQuery;
        private const float minY = -6f;
        private const float maxY = 6f;
        private const float interpolationSpeed = 0.2f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerInputDataToUse>(); // component from which data should be taken
            state.RequireForUpdate<PongInputs>();
            playerQuery = state.GetEntityQuery(typeof(GhostOwner), typeof(PlayerInputDataToUse), typeof(PlayerSpawned));
        }

        public void OnUpdate(ref SystemState state)
        {
    // Get the component data from the entities
            var ghostOwnerData = playerQuery.ToComponentDataArray<GhostOwner>(Allocator.Temp);
            var playerInputData = playerQuery.ToComponentDataArray<PlayerInputDataToUse>(Allocator.Temp);
            var connectionEntity = playerQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < playerInputData.Length; i++)
            {
                if (playerInputData[i].isPlayerDisconnected)
                {
                    Debug.Log("Destroying entity with ID: " + playerInputData[i].playerNetworkId);
                    state.EntityManager.DestroyEntity(ghostOwnerData[i].connectionCommandsTargetEntity);
                    state.EntityManager.DestroyEntity(connectionEntity[i]);
                }
                else
                {
                    var verticalInput = playerInputData[i].playerInputToApply.verticalInput;

                    var targetTransform = SystemAPI.GetComponentRW<LocalTransform>(ghostOwnerData[i].connectionCommandsTargetEntity);
                    var targetPosition = targetTransform.ValueRO.Position;
                    
                    var newPositionY = targetPosition.y + (state.World.Time.DeltaTime * verticalInput);
                    
                    // TESTING DETERMINISM CHECKS
                    if (Input.GetKey(KeyCode.R) && state.World.Name == "ClientWorld")
                    {
                        newPositionY = targetPosition.y + (state.World.Time.DeltaTime * verticalInput * 2f);
                    }
                    // END OF TESTING DETERMINISM CHECKS
                    
                    // Check if the new position is within the bounds
                    if (newPositionY < minY)
                    {
                        newPositionY = minY;
                    }
                    else if (newPositionY > maxY)
                    {
                        newPositionY = maxY;
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
                        false); //should it be required?
                }
            }
        }
    }
}