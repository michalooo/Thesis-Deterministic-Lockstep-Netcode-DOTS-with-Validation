using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

// This is the script responsible for handling the server side of the network, with connections etc

public struct PlayerInputData
{
    public int networkID;
    public Vector2 input;  
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(ConnectionHandleSystemGroup))]
public partial class ServerBehaviour : SystemBase
{
    const ushort k_NetworkPort = 7979;
    private MenuHandler _menuHandler;
    
    NetworkDriver m_Driver;
    NetworkPipeline simulatorPipeline;
    NativeList<int> m_NetworkIDs;
    
    private Dictionary<int, List<PlayerInputData>> everyTickInputBuffer;
    private Dictionary<int, List<ulong>> everyTickHashBuffer;
    private NativeArray<NetworkConnection> connectedPlayers; // This is an array of all possible connection slots in the game and players that are already connected

    private int tickRate = 30;
    private int currentTick = 1;
    NativeList<Vector2> m_PlayerInputs;
    
    private bool desync = false;

    // singleton to define (clientserver tickrate)
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
        var settings = new NetworkSettings();
        settings.WithSimulatorStageParameters(
            maxPacketCount: maxPacketCount,
            maxPacketSize: maxPacketSize,
            mode: mode,
            packetDelayMs: packetDelayMs,
            packetJitterMs: packetJitterMs,
            packetDropInterval: packetDropInterval,
            packetDropPercentage: packetDropPercentage,
            packetDuplicationPercentage: packetDuplicationPercentage);
        
        m_Driver = NetworkDriver.Create(settings);
        simulatorPipeline = m_Driver.CreatePipeline(typeof(SimulatorPipelineStage));
        
        connectedPlayers = new NativeArray<NetworkConnection>(16, Allocator.Persistent);
        m_NetworkIDs = new NativeList<int>(16, Allocator.Persistent);
        m_PlayerInputs = new NativeList<Vector2>(16, Allocator.Persistent);
        
        everyTickInputBuffer = new Dictionary<int, List<PlayerInputData>>();
        everyTickHashBuffer = new Dictionary<int, List<ulong>>();
        _menuHandler = GameObject.Find("MenuManager").GetComponent<MenuHandler>();
    
        var port = ParsePortOrDefault(_menuHandler.Port.text);
        var endpoint = NetworkEndpoint.AnyIpv4.WithPort(port);
        
        if (m_Driver.Bind(endpoint) != 0)
        {
            Debug.LogError("Failed to bind to port 7979.");
            return;
        }
    
        m_Driver.Listen();
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
        if (m_Driver.IsCreated)
        {
            m_Driver.Dispose();
            connectedPlayers.Dispose();
            m_NetworkIDs.Dispose();
            m_PlayerInputs.Dispose();
        }
    }
    
    private void AcceptAndHandleConnections()
    {
        // Accept new connections
        NetworkConnection c;
        while ((c = m_Driver.Accept()) != default)
        {
            // Find the first available spot in connectedPlayers array
            int index = FindFreePlayerSlot();
            if (index != -1)
            {
                // Assign the connection to the first available spot
                connectedPlayers[index] = c; // Assign network ID based on the index
                Debug.Log("Accepted a connection with network ID: " + index);
            }
            else
            {
                Debug.LogWarning("Cannot accept more connections. Server is full.");
                c.Disconnect(m_Driver);
            }
        }
    }
    
    private int FindFreePlayerSlot()
    {
        for (int i = 0; i < connectedPlayers.Length; i++)
        {
            if (!connectedPlayers[i].IsCreated)
            {
                return i;
            }
        }
        return -1; // No free slot found
    }

    protected override void OnUpdate()
    {
        m_Driver.ScheduleUpdate().Complete();

        if (SceneManager.GetActiveScene().name != "Game") // We are not accepting new connections while in game
        {
            AcceptAndHandleConnections();
        }
    
        for (int i = 0; i < connectedPlayers.Length; i++)
        {
            if (connectedPlayers[i].IsCreated)
            {
                DataStreamReader stream;
                NetworkEvent.Type cmd;
                while ((cmd = m_Driver.PopEventForConnection(connectedPlayers[i], out stream)) != NetworkEvent.Type.Empty)
                {
                    switch (cmd)
                    {
                        case NetworkEvent.Type.Data:
                            HandleRpc(stream, connectedPlayers[i]);
                            break;
                        case NetworkEvent.Type.Disconnect: // handling disconnect
                            Debug.Log("Client disconnected from the server.");
                            connectedPlayers[i] = default;
                            CheckIfAllDataReceivedAndSendToClients();
                            break;
                    }
                }
            }
        }
        
        //test
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (SceneManager.GetActiveScene().name == "Loading")
            {
                // Collect player data
                CollectInitialPlayerData();

                // Send RPC with player data to all clients
                SendRPCtoStartGame();
            }
        }
    }
    
    void HandleRpc(DataStreamReader stream, NetworkConnection connection)
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
            case RpcID.BroadcastPlayerInputToServer:
                var rpc = new RpcBroadcastPlayerInputToServer();
                rpc.Deserialize(stream);
                SaveTheData(rpc, connection);
                CheckIfAllDataReceivedAndSendToClients();
                break;
            default:
                Debug.LogError("Received RPC ID not proceeded by the server: " + id);
                break;
        }
    }
    
    private void CollectInitialPlayerData()
    {
        m_NetworkIDs.Clear();
        m_PlayerInputs.Clear();

        // Collect data from all players
        for (int i = 0; i < connectedPlayers.Length; i++)
        {
            // Example: Collect network IDs and positions of players
            if (connectedPlayers[i].IsCreated)
            {
                m_NetworkIDs.Add(i); // set unique Network ID
                m_PlayerInputs.Add(new Vector2(0, 0)); // Example input
            }
        }
    }
    
    private void SendRPCtoStartGame()
    {
        // register OnGameStart method where they can be used to spawn entities, separation between user code and package code
        
        RpcStartDeterministicSimulation rpc = new RpcStartDeterministicSimulation
        {
            NetworkIDs = m_NetworkIDs,
            Tickrate = tickRate
        };
        
        for (int i = 0; i < connectedPlayers.Length; i++)
        {
            if (connectedPlayers[i].IsCreated)
            {
                rpc.NetworkID = i;
                rpc.Serialize(m_Driver, connectedPlayers[i], simulatorPipeline);
            }
        }
    }
    
    private void SendRPCWithPlayersInputUpdate(NativeList<int> networkIDs, NativeList<Vector2> playerInputs)
    {
        RpcPlayersDataUpdate rpc = new RpcPlayersDataUpdate
        {
            NetworkIDs = networkIDs,
            Inputs = playerInputs,
            Tick = tickRate
        };
        
        for (int i = 0; i < connectedPlayers.Length; i++)
        {
            if (connectedPlayers[i].IsCreated)
            {
                rpc.Serialize(m_Driver, connectedPlayers[i], simulatorPipeline);
            }
        }
    }
    
    private void SendRPCWithPlayersDesyncronizationInfo()
    {
        RpcPlayerDesyncronizationInfo rpc = new RpcPlayerDesyncronizationInfo{};
        
        for (int i = 0; i < connectedPlayers.Length; i++)
        {
            if (connectedPlayers[i].IsCreated)
            {
                rpc.Serialize(m_Driver, connectedPlayers[i], simulatorPipeline);
            }
        }
    }
    
    private void SaveTheData(RpcBroadcastPlayerInputToServer rpc, NetworkConnection connection)
    {
        var inputData = new PlayerInputData();
        for(int i=0; i<connectedPlayers.Length; i++)
        {
            if (connectedPlayers[i].Equals(connection))
            {
                Debug.Log("Received input from network ID " + i + " for tick " + rpc.CurrentTick);
                inputData = new PlayerInputData
                {
                    networkID = i,
                    input = rpc.PlayerInput
                };
                
                if (!everyTickInputBuffer.ContainsKey(rpc.CurrentTick))
                {
                    everyTickInputBuffer[rpc.CurrentTick] = new List<PlayerInputData>();
                }
        
                if (!everyTickHashBuffer.ContainsKey(rpc.CurrentTick))
                {
                    everyTickHashBuffer[rpc.CurrentTick] = new List<ulong>();
                }
        
                // This tick already exists in the buffer. Check if the player already has inputs saved for this tick. No need to check for hash in that case because those should be send together and hash can be the same (if everything is correct) so we will get for example 3 same hashes
                foreach (var oldInputData in everyTickInputBuffer[rpc.CurrentTick])
                {
                    if (oldInputData.networkID == i)
                    {
                        Debug.LogError("Already received input from network ID " + i + " for tick " + rpc.CurrentTick);
                        return; // Stop executing the function here, since we don't want to add the new inputData
                    }
                }
        
                everyTickInputBuffer[rpc.CurrentTick].Add(inputData);
                everyTickHashBuffer[rpc.CurrentTick].Add(rpc.HashForCurrentTick);
            }
        }
    }

    private int GetActiveConnectionCount()
    {
        int count = 0;
        for (int i = 0; i < connectedPlayers.Length; i++)
        {
            if (connectedPlayers[i].IsCreated)
            {
                count++;
            }
        }
        return count;
    }

    
    private void CheckIfAllDataReceivedAndSendToClients()
    {
        if (everyTickInputBuffer[currentTick].Count == GetActiveConnectionCount() && everyTickHashBuffer[currentTick].Count == GetActiveConnectionCount())
        { 
            // We've received a full set of data for this tick, so process it
            // This means creating new NativeLists of network IDs and inputs and sending them with SendRPCWithPlayersInput
            var networkIDs = new NativeList<int>(Allocator.Temp);
            var inputs = new NativeList<Vector2>(Allocator.Temp);

            foreach (var inputData in everyTickInputBuffer[currentTick])
            {
                networkIDs.Add(inputData.networkID);
                inputs.Add(inputData.input);
            }
            
            // check if every hash is the same
            ulong firstHash = everyTickHashBuffer[currentTick][0];
            for (int i = 1; i < everyTickHashBuffer[currentTick].Count; i++)
            {
                if (firstHash != everyTickHashBuffer[currentTick][i])
                {
                    // Hashes are not equal - handle this scenario
                    Debug.LogError("DESCYNCRONIZATION HAPPEND! HASHES ARE NOT EQUAL! " + "Ticks: " + currentTick + " Hashes: " + firstHash + " and " + everyTickHashBuffer[currentTick][i]);
                    desync = true;
                    i = everyTickHashBuffer[currentTick].Count;
                }
            }
            if (!desync)
            {
                Debug.Log("All hashes are equal: " + firstHash + ". Number of hashes: " +
                          everyTickHashBuffer[currentTick].Count + ". Tick: " + currentTick);
                
                // Send the RPC to all connections
                SendRPCWithPlayersInputUpdate(networkIDs, inputs);
            }
            else{
                SendRPCWithPlayersDesyncronizationInfo();
            }
            
            // Clean up the temporary lists
            networkIDs.Dispose();
            inputs.Dispose();

            // Remove this tick from the buffer, since we're done processing it
            everyTickInputBuffer.Remove(currentTick);
            everyTickHashBuffer.Remove(currentTick);
            currentTick++;
        }
        else if(everyTickInputBuffer[currentTick].Count > GetActiveConnectionCount())
        {
            Debug.LogError("Too many player inputs saved in one tick");
        }
        // else if (everyTickInputBuffer[currentTick].Count == GetActiveConnectionCount())
        // {
        //     Debug.LogError("Too many player inputs saved in one tick");
        //     return;
        // }
        // else
        // {
        //     return;
        // }
    }

}
