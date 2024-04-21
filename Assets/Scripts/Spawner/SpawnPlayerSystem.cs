using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.Transforms;
using UnityEngine;
using DeterministicLockstep;

/// <summary>
/// System used to spawn the player prefab for the connections that are not spawned yet
/// </summary>
[UpdateBefore(typeof(DeterministicSimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class SpawnPlayerSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<Spawner>();
        }

        protected override void OnUpdate()
        {
            var prefab = SystemAPI.GetSingleton<Spawner>().Player;
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (ghostOwner, connectionEntity) in SystemAPI.Query<RefRW<GhostOwner>>().WithNone<PlayerSpawned, PlayerInputDataToSend>().WithEntityAccess())
            {
                Debug.Log($"Spawning player for connection {ghostOwner.ValueRO.networkId}");
                        
                commandBuffer.AddComponent<PlayerSpawned>(connectionEntity);
                var player = commandBuffer.Instantiate(prefab);
                commandBuffer.AddComponent(connectionEntity, new CommandTarget(){targetEntity = player});
                
                // Fix the position problem (those should be different but are the same)
                commandBuffer.SetComponent(player, LocalTransform.FromPosition(new Vector3(5 + ghostOwner.ValueRO.networkId,1,5 + ghostOwner.ValueRO.networkId))); 
            }
            
            commandBuffer.Playback(EntityManager);
        }
    }
