using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.Transforms;
using UnityEngine;
using DeterministicLockstep;
using Unity.Mathematics;

namespace PongGame
{
    /// <summary>
    /// System used to spawn the player prefab for the connections that are not spawned yet
    /// </summary>
    [UpdateInGroup(typeof(GameStateUpdateSystemGroup), OrderFirst = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class PongPlayerSpawnerSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PongPlayerSpawner>();
            RequireForUpdate<PongInputs>();
        }

        protected override void OnUpdate()
        {
            var prefab = SystemAPI.GetSingleton<PongPlayerSpawner>().Player;
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (ghostOwner, connectionEntity) in SystemAPI.Query<RefRW<GhostOwner>>()
                         .WithNone<PlayerSpawned>().WithEntityAccess())
            {
                Debug.Log($"Spawning player for connection {ghostOwner.ValueRO.networkId}");

                commandBuffer.AddComponent<PlayerSpawned>(connectionEntity);
                var player = commandBuffer.Instantiate(prefab);
                commandBuffer.AddComponent(connectionEntity, new CommandTarget() { targetEntity = player }); // is it necessery for the package? This is user implementation

                if (ghostOwner.ValueRO.networkId % 2 == 0)
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

            commandBuffer.Playback(EntityManager);
        }
    }
}