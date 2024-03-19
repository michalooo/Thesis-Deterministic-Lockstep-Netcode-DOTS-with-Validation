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
}

struct TickRateInfo : IComponentData
{
    public float delayTime;
    public int tickRate;
    public int currentTick;
}

public struct NetworkConnectionReference : IComponentData
{
    public NetworkDriver Driver;
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



[UpdateAfter(typeof(ClientBehaviour))]
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
            
            Entities.WithName("SpawnPlayers").WithNone<PlayerSpawned, PlayerInputDataToSend>().WithAll<PlayerInputDataToUse>().ForEach(
                    (Entity connectionEntity, ref PlayerInputDataToUse inputData) =>
                    {
                        Debug.Log($"Spawning player for connection {inputData.playerNetworkId}");
                        
                        commandBuffer.AddComponent<PlayerSpawned>(connectionEntity);
                        var player = commandBuffer.Instantiate(prefab);
                        commandBuffer.AddComponent(connectionEntity, new CommandTarget(){targetEntity = player});
                        commandBuffer.SetComponent(player, LocalTransform.FromPosition(10 + Random.Range(-5f, 5f),1,10 + Random.Range(-3f, 3f)));
                        commandBuffer.SetComponentEnabled<PlayerInputDataToUse>(connectionEntity, false);
                        // Add the player to the linked entity group on the connection so it is destroyed
                        // automatically on disconnect (destroyed with connection entity destruction)
                        // commandBuffer.AddBuffer<LinkedEntityGroup>(connectionEntity);
                        // commandBuffer.AppendToBuffer(connectionEntity, new LinkedEntityGroup{Value = player});
                    }).Run();
            
            commandBuffer.Playback(EntityManager);
        }
    }