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
    public bool playerDisconnected;
}

struct TickRateInfo : IComponentData
{
    public int tickRate;
    public int tickAheadValue;
    
    public float delayTime;
    public int currentSimulationTick; // Received simulation tick from the server
    public int currentClientTickToSend; // We are sending input for the tick in the future
    public ulong hashForTheTick;
}

public struct InputsFromServerOnTheGivenTick
{
    public int tick;
    public RpcPlayersDataUpdate data;
    
    public void Dispose() // does this really work??
    {
        tick = 0;
        data.Inputs.Dispose();
        data.NetworkIDs.Dispose();
    }
}

// Define a component to store the fixed number of entries
public struct StoredTicksAhead : IComponentData
{
    private const int MaxEntries = 20; // Maximum number of entries. Worth checking with the incoming tickAhead from the server
    public NativeArray<InputsFromServerOnTheGivenTick> entries; 
    
    public StoredTicksAhead(bool shouldInitialize)
    {
        entries = new NativeArray<InputsFromServerOnTheGivenTick>(MaxEntries, Allocator.Persistent);
        if (shouldInitialize)
        {
            for (int i = 0; i < MaxEntries; i++)
            {
                entries[i] = new InputsFromServerOnTheGivenTick { tick = 0, data = new RpcPlayersDataUpdate(null, null, 0) }; // tick = 0 means that the entry can be used
            }
        }
    }
}

public struct NetworkConnectionReference : IComponentData
{
    public NetworkDriver driver;
    public NetworkPipeline simulatorPipeline;
    public NetworkConnection connection;
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
/// </summary>
public struct GhostOwnerIsLocal : IComponentData, IEnableableComponent
{} // added to different entites so it may cause desync if comparing amount of components

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
