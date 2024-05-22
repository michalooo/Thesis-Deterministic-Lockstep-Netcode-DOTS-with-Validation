using DeterministicLockstep;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace CapsulesGame
{
    /// <summary>
    /// System responsible for updating all of players positions based on their input component (updated by the server) if they are spawned and if UpdatePlayerPosition component is enabled. After updating those positions this component will be disabled
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
    public partial class PlayerUpdateSystem : SystemBase
    {
        private EntityQuery playerQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerInputDataToUse>(); // component from which data should be taken
            RequireForUpdate<CapsulesInputs>();
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
                if (playerInputData[i].isPlayerDisconnected)
                {
                    Debug.Log("Destroying entity with ID: " + playerInputData[i].playerNetworkId);
                    EntityManager.DestroyEntity(commandTargetData[i].connectionCommandsTargetEntity);
                    EntityManager.DestroyEntity(connectionEntity[i]);
                }
                else
                {
                    // var horizontalInput = playerInputData[i].inputToUse.horizontalInput;
                    var verticalInput = playerInputData[i].playerInputToApply.verticalInput;

                    var targetTransform = SystemAPI.GetComponent<LocalToWorld>(commandTargetData[i].connectionCommandsTargetEntity);
                    var targetPosition = targetTransform.Position;
                    // targetPosition.x += horizontalInput;
                    targetPosition.z += verticalInput;

                    EntityManager.SetComponentData(commandTargetData[i].connectionCommandsTargetEntity,
                        LocalTransform.FromPosition(targetPosition));

                    EntityManager.SetComponentEnabled<PlayerInputDataToUse>(connectionEntity[i],
                        false); //should it be required?
                }
            }
        }
    }
}