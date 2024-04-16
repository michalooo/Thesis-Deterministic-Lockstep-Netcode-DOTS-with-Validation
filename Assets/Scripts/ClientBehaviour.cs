using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine.SceneManagement;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ConnectionHandleSystemGroup))]
public partial class ClientBehaviour : SystemBase
{
    private const ushort KNetworkPort = 7979;
    private MenuHandler _menuHandler;
    
    private NetworkDriver _mDriver;
    private NetworkConnection _mConnection;
    private NetworkSettings _clientSimulatorParameters;
    private NetworkPipeline _simulatorPipeline;
    
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
        
        _clientSimulatorParameters = new NetworkSettings();
        _clientSimulatorParameters.WithSimulatorStageParameters(
            maxPacketCount: maxPacketCount,
            maxPacketSize: maxPacketSize,
            mode: mode,
            packetDelayMs: packetDelayMs,
            packetJitterMs: packetJitterMs,
            packetDropInterval: packetDropInterval,
            packetDropPercentage: packetDropPercentage,
            packetDuplicationPercentage: packetDuplicationPercentage);

        _mDriver = NetworkDriver.Create(_clientSimulatorParameters);
        _simulatorPipeline = _mDriver.CreatePipeline(typeof(SimulatorPipelineStage)); // reliable pipeline add
        
        var endpoint = NetworkEndpoint.Parse(_menuHandler.Address.text, ParsePortOrDefault(_menuHandler.Port.text));
        _mConnection = _mDriver.Connect(endpoint);
    }
    
    private ushort ParsePortOrDefault(string s)
    {
        if (ushort.TryParse(s, out var port)) return port;
        Debug.LogWarning($"Unable to parse port, using default port {KNetworkPort}");
        return KNetworkPort;

    }

    protected override void OnDestroy()
    {
        _mDriver.Dispose();
    }

    protected override void OnUpdate()
    {
        _mDriver.ScheduleUpdate().Complete();

        if (!_mConnection.IsCreated) return;

        NetworkEvent.Type cmd;
        while ((cmd = _mConnection.PopEvent(_mDriver, out var stream)) != NetworkEvent.Type.Empty)
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
                    _mConnection = default;
                    break;
                case NetworkEvent.Type.Empty:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        if (SceneManager.GetActiveScene().name == "Game" && Input.GetKey(KeyCode.C) && World.Name == "ClientWorld2") // Simulation of disconnection
        {
            _mConnection.Disconnect(_mDriver);
            _mConnection = default;
            SceneManager.LoadScene("Loading");
        }
    }

    private void HandleRpc(DataStreamReader stream)
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
            case RpcID.PlayersDesynchronized: // Stop simulation
                var rpcPlayerDesynchronizationInfo = new RpcPlayerDesynchronizationInfo();
                rpcPlayerDesynchronizationInfo.Deserialize(stream);
                var determinismSystemGroup = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<DeterministicSimulationSystemGroup>();
                determinismSystemGroup.Enabled = false;
                break;
            case RpcID.BroadcastPlayerInputToServer:
                Debug.LogError("This message should never be received by the client");
                break;
            default:
                Debug.LogError("Received RPC ID not proceeded by the client: " + id);
                break;
        }
    }
    
    private void StartGame(RpcStartDeterministicSimulation rpc)
    {
        if(SceneManager.GetActiveScene().name != "Game")
        {
            SceneManager.LoadScene("Game");

            foreach (var playerNetworkId in rpc.NetworkIDs)
            {
                var newEntity = EntityManager.CreateEntity();
                
                EntityManager.AddComponentData(newEntity, new PlayerInputDataToUse
                {
                    playerNetworkId = playerNetworkId,
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
                    tickRate = rpc.TickRate,
                    tickAheadValue = rpc.TickAhead,
                        
                    delayTime = 1f / rpc.TickRate,
                    currentSimulationTick = 0,
                    currentClientTickToSend = 0,
                    hashForTheTick = 0,
                    
                });
                EntityManager.AddComponentData(newEntity, new GhostOwner
                {
                    networkId = playerNetworkId
                });
                EntityManager.AddComponentData(newEntity, new NetworkConnectionReference
                {
                    driver = _mDriver,
                    simulatorPipeline = _simulatorPipeline,
                    connection = _mConnection
                });
                EntityManager.AddComponentData(newEntity, new GhostOwnerIsLocal());
                EntityManager.AddComponentData(newEntity, new StoredTicksAhead(true));
                if(playerNetworkId != rpc.NetworkID)  EntityManager.SetComponentEnabled<GhostOwnerIsLocal>(newEntity, false);
                EntityManager.SetComponentEnabled<PlayerInputDataToSend>(newEntity, false);
            }
        }
    }

    // This function will be called when the server sends an RPC with updated players data and will update the PlayerInputDataToUse components and set them to enabled
    void UpdatePlayersData(RpcPlayersDataUpdate rpc)
    {
        foreach (var storedTicksAhead in SystemAPI.Query<RefRW<StoredTicksAhead>>().WithAll<GhostOwnerIsLocal>())
        {
            bool foundEmptySlot = false;
            for (int i = 0; i < storedTicksAhead.ValueRW.entries.Length; i++)
            {
                if (storedTicksAhead.ValueRO.entries[i].tick == 0) // Check if the tick value is 0, assuming 0 indicates an empty slot
                {
                    storedTicksAhead.ValueRW.entries[i] = new InputsFromServerOnTheGivenTick { tick = rpc.Tick, data = rpc };
                    foundEmptySlot = true;
                    break; // Exit the loop after finding an empty slot
                    // Packages can be unreliable so it's better to give them random slot and later check the tick
                }
            }
    
            if (!foundEmptySlot)
            {
                Debug.LogError("No empty slots available to store the value of a future tick from the server. " + "The current capacity is: " + storedTicksAhead.ValueRO.entries.Length + " This error means an implementation problem");
            }
            // Always current tick is less or equal to the server tick and the difference between them can be max tickAhead
        }
    }
}