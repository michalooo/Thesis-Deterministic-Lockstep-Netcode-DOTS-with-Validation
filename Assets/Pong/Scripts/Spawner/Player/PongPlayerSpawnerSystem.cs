using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using DeterministicLockstep;
using Unity.Burst;
using Unity.Mathematics;

namespace PongGame
{
    /// <summary>
    /// System used to spawn the player prefab for the connections that are not spawned yet
    /// </summary>
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    public partial struct PongPlayerSpawnerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PongPlayerSpawner>();
            state.RequireForUpdate<PongInputs>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var prefab = SystemAPI.GetSingleton<PongPlayerSpawner>().Player;
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (ghostOwner, connectionEntity) in SystemAPI.Query<RefRW<GhostOwner>>()
                         .WithNone<PlayerSpawned>().WithEntityAccess())
            {
                // Debug.Log($"Spawning player for connection {ghostOwner.ValueRO.connectionNetworkId}");

                commandBuffer.AddComponent<PlayerSpawned>(connectionEntity);
                var player = commandBuffer.Instantiate(prefab);
                commandBuffer.AddComponent(connectionEntity, new CommandTarget() { connectionCommandsTargetEntity = player }); // is it necessary for the package? This is user implementation

                if (ghostOwner.ValueRO.connectionNetworkId % 2 == 0)
                {
                    // Fix the position problem (those should be different but are the same)
                    commandBuffer.SetComponent(player,
                        LocalTransform.FromPosition(new float3(-8, 0, 0)));
                }
                else
                {
                    // Fix the position problem (those should be different but are the same)
                    commandBuffer.SetComponent(player,
                        LocalTransform.FromPosition(new float3(8, 0, 0)));
                }
            }

            commandBuffer.Playback(state.EntityManager);
        }
    }
}