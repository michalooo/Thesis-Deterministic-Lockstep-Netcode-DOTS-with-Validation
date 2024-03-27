using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.Transforms;
using UnityEngine;

public struct PlayerSpawned : IComponentData { }

public struct PlayerInputDataToSend : IComponentData, IEnableableComponent
{
    public int horizontalInput;
    public int verticalInput;
}

public struct PlayerInputDataToUse : IComponentData, IEnableableComponent
{
    public int playerNetworkId;
    public int horizontalInput;
    public int verticalInput;
    public Vector3 initialPosition;
}

struct TickRateInfo : IComponentData
{
    public float delayTime;
    public int tickRate;
    public int currentTick;
    public ulong hashForTheTick;
}

public struct NetworkConnectionReference : IComponentData
{
    public NetworkDriver Driver;
    public NetworkPipeline SimulatorPipeline;
    public NetworkConnection Connection;
}

public struct GhostOwner : IComponentData
{
    public int networkId;
}

public struct CommandTarget : IComponentData
{
    public Entity targetEntity;
}

/// <summary>
/// An enableable tag component used to track if a ghost with an owner is owned by the local host or not.
/// This is enabled for all ghosts on the server and for ghosts where the ghost owner network id matches the connection id on the client.
/// </summary>
public struct GhostOwnerIsLocal : IComponentData, IEnableableComponent
{}

[UpdateInGroup(typeof(InputGatherSystemGroup))]
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
            
            foreach (var (inputDataToUse, connectionEntity) in SystemAPI.Query<RefRW<PlayerInputDataToUse>>().WithNone<PlayerSpawned, PlayerInputDataToSend>().WithEntityAccess())
            {
                Debug.Log($"Spawning player for connection {inputDataToUse.ValueRO.playerNetworkId}");
                        
                commandBuffer.AddComponent<PlayerSpawned>(connectionEntity);
                var player = commandBuffer.Instantiate(prefab);
                commandBuffer.AddComponent(connectionEntity, new CommandTarget(){targetEntity = player});
                commandBuffer.SetComponent(player, LocalTransform.FromPosition(inputDataToUse.ValueRO.initialPosition));
                commandBuffer.SetComponentEnabled<PlayerInputDataToUse>(connectionEntity, false);
                
                // Add the player to the linked entity group on the connection so it is destroyed
                // automatically on disconnect (destroyed with connection entity destruction)
                commandBuffer.AddBuffer<LinkedEntityGroup>(connectionEntity);
                commandBuffer.AppendToBuffer(connectionEntity, new LinkedEntityGroup{Value = player});
            }
            
            commandBuffer.Playback(EntityManager);
        }
    }
