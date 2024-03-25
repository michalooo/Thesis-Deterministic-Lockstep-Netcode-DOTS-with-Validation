using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Unity.Networking.Transport;
using UnityEngine.SceneManagement;

// This is the script responsible for handling the client side of the network, with connections to server etc

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class ClientBehaviour : SystemBase
{
    public NetworkDriver m_Driver;
    NetworkConnection m_Connection;
    
    protected override void OnCreate()
    {
        m_Driver = NetworkDriver.Create();
        
        var endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(7777);
        m_Connection = m_Driver.Connect(endpoint);
    }

    protected override void OnDestroy()
    {
        m_Driver.Dispose();
    }

    protected override void OnUpdate()
    {
        m_Driver.ScheduleUpdate().Complete();

        if (!m_Connection.IsCreated)
        {
            return;
        }

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        while ((cmd = m_Connection.PopEvent(m_Driver, out stream)) != NetworkEvent.Type.Empty)
        {
            switch (cmd)
            {
                case NetworkEvent.Type.Connect:
                    Debug.Log("Connected to server.");
                    break;
                case NetworkEvent.Type.Data:
                    HandleRpc(stream);
                    break;
                case NetworkEvent.Type.Disconnect:
                    Debug.Log("Disconnected from server.");
                    m_Connection = default;
                    break;
            }
        }
    }

    void HandleRpc(DataStreamReader stream)
    {
        var id = (RpcDefinitions.RpcID) stream.ReadInt();
        switch (id)
        {
            case RpcDefinitions.RpcID.BroadcastAllPlayersInputs:
                var rpcUpdatePLayers = RpcUtils.DeserializeServerUpdatePlayersRPC(stream);
                UpdatePlayersData(rpcUpdatePLayers);
                break;
            case RpcDefinitions.RpcID.StartDeterministicSimulation:
                Debug.Log("Starting game rpc");
                var rpcStartGame = RpcUtils.DeserializeServerStartGameRpc(stream);
                StartGame(rpcStartGame);
                break;
            default:
                Debug.LogWarning("Received unknown RPC ID.");
                break;
        }
    }
    
    // This function at the start of the game will spawn all players
    void StartGame(RpcDefinitions.RpcStartGameAndSpawnPlayers rpc)
    {
        if(SceneManager.GetActiveScene().name != "Game")
        {
            SceneManager.LoadScene("Game");
            
            for (int i = 0; i < rpc.networkIDs.Length; i++)
            {
                // Create a new entity
                Entity newEntity = EntityManager.CreateEntity();

                // Set the PlayerInputData component for the entity
                EntityManager.AddComponentData(newEntity, new PlayerInputDataToUse
                {
                    playerNetworkId = rpc.networkIDs[i],
                    horizontalInput = 0, 
                    verticalInput = 0,
                    initialPosition = rpc.initialPositions[i]
                });
                EntityManager.AddComponentData(newEntity, new PlayerInputDataToSend
                {
                    horizontalInput = 0, 
                    verticalInput = 0,
                });
                EntityManager.AddComponentData(newEntity, new TickRateInfo
                {
                    delayTime = 1f / rpc.tickrate,
                    tickRate = rpc.tickrate,
                    currentTick = 0
                });
                EntityManager.AddComponentData(newEntity, new GhostOwner
                {
                    networkId = rpc.networkIDs[i]
                });
                EntityManager.AddComponentData(newEntity, new NetworkConnectionReference
                {
                    Driver = m_Driver,
                    Connection = m_Connection
                });
                EntityManager.AddComponentData(newEntity, new GhostOwnerIsLocal());
                if(rpc.networkIDs[i] != rpc.connectionID)  EntityManager.SetComponentEnabled<GhostOwnerIsLocal>(newEntity, false);
                EntityManager.SetComponentEnabled<PlayerInputDataToSend>(newEntity, false);
            }
        }
    }

    // This function will be called when the server sends an RPC with updated players data and will update the PlayerInputDataToUse components and set them to enabled
    void UpdatePlayersData(RpcDefinitions.RpcPlayersDataUpdate rpc)
    {
        // Update player data based on received RPC
        NativeList<int> networkIDs = new NativeList<int>(16, Allocator.Temp);
        NativeList<Vector2> inputs = new NativeList<Vector2>(16, Allocator.Temp);
        networkIDs = rpc.networkIDs;
        inputs = rpc.inputs;
    
        // Update player cubes based on received data, I need a job that for each component of type Player will enable it and change input values there
        // Enable component on player which has info about current position of the player
        // Create a characterController script on player which will check if this component is enabled and then update the position of the player and disable that component
        var commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
        
        Entities
            .WithAll<PlayerInputDataToUse, PlayerInputDataToSend>()
            .WithEntityQueryOptions(EntityQueryOptions.IgnoreComponentEnabledState)
            .ForEach((Entity entity, ref PlayerInputDataToUse playerInputData) =>
            {
                for (int i = 0; i < networkIDs.Length; i++)
                {
                    if (playerInputData.playerNetworkId == networkIDs[i])
                    {
                        playerInputData.horizontalInput = (int)inputs[i].x;
                        playerInputData.verticalInput = (int)inputs[i].y;
                        commandBuffer.SetComponentEnabled<PlayerInputDataToUse>(entity, true);
                        commandBuffer.SetComponentEnabled<PlayerInputDataToSend>(entity, false);
                    }
                }
            }).Run();
        
        commandBuffer.Playback(EntityManager);
        
    }
}