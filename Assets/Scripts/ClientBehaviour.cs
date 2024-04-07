using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine.SceneManagement;

// This is the script responsible for handling the client side of the network, with connections to server etc

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ConnectionHandleSystemGroup))]
public partial class ClientBehaviour : SystemBase
{
    const ushort k_NetworkPort = 7979;
    private MenuHandler _menuHandler;
    
    NetworkDriver m_Driver;
    NetworkConnection m_Connection;
    NetworkSettings clientSimulatorParameters;
    NetworkPipeline simulatorPipeline;
    
    [Tooltip("The maximum amount of packets the pipeline can keep track of. This used when a packet is delayed, the packet is stored in the pipeline processing buffer and can be later brought back.")]
    int maxPacketCount = 1000;
    [Tooltip("The maximum size of a packet which the simulator stores. If a packet exceeds this size it will bypass the simulator.")]
    int maxPacketSize = NetworkParameterConstants.MaxMessageSize;
    [Tooltip("Whether to apply simulation to received or sent packets (defaults to both).")]
    ApplyMode mode = ApplyMode.AllPackets;
    [Tooltip("Fixed delay in milliseconds to apply to all packets which pass through.")]
    int packetDelayMs = 0;
    [Tooltip("Variance of the delay that gets added to all packets that pass through. For example, setting this value to 5 will result in the delay being a random value within 5 milliseconds of the value set with PacketDelayMs.")]
    int packetJitterMs = 0;
    [Tooltip("Fixed interval to drop packets on. This is most suitable for tests where predictable behaviour is desired, as every X-th packet will be dropped. For example, if the value is 5 every fifth packet is dropped.")]
    int packetDropInterval = 0;
    [Tooltip("Percentage of packets that will be dropped.")]
    int packetDropPercentage = 0;
    [Tooltip("Percentage of packets that will be duplicated. Packets are duplicated at most once and will not be duplicated if they were first deemed to be dropped.")]
    int packetDuplicationPercentage = 0;
    
    protected override void OnCreate()
    {
        _menuHandler = GameObject.Find("MenuManager").GetComponent<MenuHandler>();
        
        clientSimulatorParameters = new NetworkSettings();
        clientSimulatorParameters.WithSimulatorStageParameters(
            maxPacketCount: maxPacketCount,
            maxPacketSize: maxPacketSize,
            mode: mode,
            packetDelayMs: packetDelayMs,
            packetJitterMs: packetJitterMs,
            packetDropInterval: packetDropInterval,
            packetDropPercentage: packetDropPercentage,
            packetDuplicationPercentage: packetDuplicationPercentage);

        m_Driver = NetworkDriver.Create(clientSimulatorParameters);
        simulatorPipeline = m_Driver.CreatePipeline(typeof(SimulatorPipelineStage));
        
        var endpoint = NetworkEndpoint.Parse(_menuHandler.Address.text, ParsePortOrDefault(_menuHandler.Port.text)); //NetworkEndpoint.LoopbackIpv4.WithPort(k_NetworkPort);
        m_Connection = m_Driver.Connect(endpoint);
    }
    
    private UInt16 ParsePortOrDefault(string s)
    {
        if (!UInt16.TryParse(s, out var port))
        {
            Debug.LogWarning($"Unable to parse port, using default port {k_NetworkPort}");
            return k_NetworkPort;
        }

        return port;
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
                    Debug.Log($"[ConnectToServer] Called on '{_menuHandler.Address.text}:{_menuHandler.Port.text}'.");
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

        if (SceneManager.GetActiveScene().name == "Game" && Input.GetKey(KeyCode.C) && World.Name == "ClientWorld2") // Simulation of disconnection
        {
            m_Connection.Disconnect(m_Driver);
            m_Connection = default;
            SceneManager.LoadScene("Loading");
        }
    }

    void HandleRpc(DataStreamReader stream)
    {
        var copyOfStream = stream;
        var id = (RpcID) copyOfStream.ReadByte(); // for the future check if its within a valid range (id as bytes)
        if (!Enum.IsDefined(typeof(RpcID), id))
        {
            Debug.LogError("Received invalid RPC ID: " + id);
            return;
        }
        
        switch (id)
        { 
            case RpcID.BroadcastAllPlayersInputsToClients:
                var rpcPlayersDataUpdate = new RpcPlayersDataUpdate();
                rpcPlayersDataUpdate.Deserialize(stream);
                UpdatePlayersData(rpcPlayersDataUpdate);
                break;
            case RpcID.StartDeterministicSimulation:
                var rpcStartDeterministicSimulation = new RpcStartDeterministicSimulation();
                rpcStartDeterministicSimulation.Deserialize(stream);
                StartGame(rpcStartDeterministicSimulation);
                break;
            case RpcID.PlayersDesyncronized:
                var rpcPlayerDesyncronizationInfo = new RpcPlayerDesyncronizationInfo();
                rpcPlayerDesyncronizationInfo.Deserialize(stream);
                var determinismSystemGroup = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<DeterministicSimulationSystemGroup>();
                determinismSystemGroup.Enabled = false;
                break;
            default:
                Debug.LogError("Received RPC ID not proceeded by the client: " + id);
                break;
        }
    }
    
    // This function at the start of the game will spawn all players
    void StartGame(RpcStartDeterministicSimulation rpc)
    {
        if(SceneManager.GetActiveScene().name != "Game")
        {
            SceneManager.LoadScene("Game");
            
            for (int i = 0; i < rpc.NetworkIDs.Length; i++)
            {
                // Create a new entity
                Entity newEntity = EntityManager.CreateEntity();

                // Set the PlayerInputData component for the entity
                EntityManager.AddComponentData(newEntity, new PlayerInputDataToUse
                {
                    playerNetworkId = rpc.NetworkIDs[i],
                    horizontalInput = 0, 
                    verticalInput = 0,
                    playerDisconnected = false,
                });
                EntityManager.AddComponentData(newEntity, new PlayerInputDataToSend
                {
                    horizontalInput = 0, 
                    verticalInput = 0,
                });
                EntityManager.AddComponentData(newEntity, new TickRateInfo
                {
                    delayTime = 1f / rpc.Tickrate,
                    tickRate = rpc.Tickrate,
                    currentTick = 1,
                    hashForTheTick = 0
                });
                EntityManager.AddComponentData(newEntity, new GhostOwner
                {
                    networkId = rpc.NetworkIDs[i]
                });
                EntityManager.AddComponentData(newEntity, new NetworkConnectionReference
                {
                    Driver = m_Driver,
                    SimulatorPipeline = simulatorPipeline,
                    Connection = m_Connection
                });
                EntityManager.AddComponentData(newEntity, new GhostOwnerIsLocal());
                if(rpc.NetworkIDs[i] != rpc.NetworkID)  EntityManager.SetComponentEnabled<GhostOwnerIsLocal>(newEntity, false);
                EntityManager.SetComponentEnabled<PlayerInputDataToSend>(newEntity, false);
            }
        }
    }

    // This function will be called when the server sends an RPC with updated players data and will update the PlayerInputDataToUse components and set them to enabled
    void UpdatePlayersData(RpcPlayersDataUpdate rpc)
    {
        // Update player data based on received RPC
        NativeList<int> networkIDs = new NativeList<int>(16, Allocator.Temp);
        NativeList<Vector2> inputs = new NativeList<Vector2>(16, Allocator.Temp);
        networkIDs = rpc.NetworkIDs;
        inputs = rpc.Inputs;
    
        // Update player cubes based on received data, I need a job that for each component of type Player will enable it and change input values there
        // Enable component on player which has info about current position of the player
        // Create a characterController script on player which will check if this component is enabled and then update the position of the player and disable that component

        foreach (var (playerInputData, connectionEntity) in SystemAPI
                     .Query<RefRW<PlayerInputDataToUse>>()
                     .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                     .WithAll<PlayerInputDataToSend>().WithEntityAccess())
        {
            var idExists = false;
            for (int i = 0; i < networkIDs.Length; i++)
            {
                if (playerInputData.ValueRO.playerNetworkId == networkIDs[i])
                {
                    idExists = true;
                    playerInputData.ValueRW.horizontalInput = (int)inputs[i].x;
                    playerInputData.ValueRW.verticalInput = (int)inputs[i].y;
                    EntityManager.SetComponentEnabled<PlayerInputDataToUse>(connectionEntity, true);
                    EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, false);
                }
            }
            
            if (!idExists) //To show that the player disconnected
            {
                playerInputData.ValueRW.playerDisconnected = true;
                EntityManager.SetComponentEnabled<PlayerInputDataToUse>(connectionEntity, true);
                EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, false);
            }
        }
    }
}