using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

// This is the script responsible for handling the server side of the network, with connections etc

public struct PlayerInputData
{
    public int networkID;
    public Vector2 input;  
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class ServerBehaviour : SystemBase
{
    NetworkDriver m_Driver;
    NativeList<NetworkConnection> m_Connections;
    NativeList<int> m_NetworkIDs;
    
    private Dictionary<int, List<PlayerInputData>> everyTickInputBuffer;

    private int tickRate = 30;
    private int currentTick = 1;
    NativeList<Vector2> m_PlayerInputs;

    protected override void OnCreate()
    {
        m_Driver = NetworkDriver.Create();
        m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
        m_NetworkIDs = new NativeList<int>(16, Allocator.Persistent);
        m_PlayerInputs = new NativeList<Vector2>(16, Allocator.Persistent);
        
        everyTickInputBuffer = new Dictionary<int, List<PlayerInputData>>();
    
        var endpoint = NetworkEndpoint.AnyIpv4.WithPort(7777);
        if (m_Driver.Bind(endpoint) != 0)
        {
            Debug.LogError("Failed to bind to port 7777.");
            return;
        }
    
        m_Driver.Listen();
    }

    protected override void OnDestroy()
    {
        if (m_Driver.IsCreated)
        {
            m_Driver.Dispose();
            m_Connections.Dispose();
            m_NetworkIDs.Dispose();
            m_PlayerInputs.Dispose();
        }
    }

    protected override void OnUpdate()
    {
        m_Driver.ScheduleUpdate().Complete();
        
        //test
        if (Input.GetKeyDown(KeyCode.Space))
        {
            foreach (var connection in m_Connections)
            {
                // Collect player data
                CollectInitialPlayerData();

                // Send RPC with player data to all clients
                SendRPCtoStartGame();
            }
        }
    
        // Clean up connections.
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {
                m_Connections.RemoveAtSwapBack(i);
                i--;
            }
        }


        if (SceneManager.GetActiveScene().name != "Game") // We are not accepting new connections while in game
        {
            // Accept new connections.
            NetworkConnection c;
            while ((c = m_Driver.Accept()) != default)
            {
                m_Connections.Add(c);
                Debug.Log("Accepted a connection.");
            }
        }
        
    
        for (int i = 0; i < m_Connections.Length; i++)
        {
            DataStreamReader stream;
            NetworkEvent.Type cmd;
            while ((cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream)) != NetworkEvent.Type.Empty)
            {
                switch (cmd)
                {
                    case NetworkEvent.Type.Data:
                        HandleRpc(stream);
                        break;
                    case NetworkEvent.Type.Disconnect:
                        Debug.Log("Client disconnected from the server.");
                        m_Connections[i] = default;
                        break;
                }
            }
        }
    }
    
    void HandleRpc(DataStreamReader stream)
    {
        var id = (RpcDefinitions.RpcID) stream.ReadInt();
        switch (id)
        {
            case RpcDefinitions.RpcID.PlayerDataUpdate:
                var rpc = RpcUtils.DeserializeClientUpdatePlayerRPC(stream);
                SaveTheData(rpc);
                CheckIfAllDataReceivedAndSendToClients();
                break;
            default:
                Debug.LogWarning("Received unknown RPC ID.");
                break;
        }
    }
    
    private void CollectInitialPlayerData()
    {
        m_NetworkIDs.Clear();
        m_PlayerInputs.Clear();

        // Collect data from all players
        for (int i = 0; i < m_Connections.Length; i++)
        {
            // Example: Collect network IDs and positions of players
            m_NetworkIDs.Add(i + 1); // Example network ID
            m_PlayerInputs.Add(new Vector2(0, 0)); // Example input
        }
    }
    
    private void SendRPCtoStartGame()
    {
        for (int i = 0; i < m_Connections.Length; i++)
        {
            RpcUtils.SendRPCWithStartGameRequest(m_Driver, m_Connections[i], m_NetworkIDs, tickRate, i+1);
        }
    }
    
    private void SendRPCWithPlayersInputUpdate(NativeList<int> networkIDs, NativeList<Vector2> playerInputs)
    {
        for (int i = 0; i < m_Connections.Length; i++)
        {
            RpcUtils.SendRPCWithPlayersInput(m_Driver, m_Connections[i], networkIDs, playerInputs, tickRate);
        }
    }
    
    private void SaveTheData(RpcDefinitions.RpcPlayerDataUpdate rpc)
    {
        var inputData = new PlayerInputData
        {
            networkID = rpc.connectionID,
            input = rpc.playerInput
        };
        
        if (!everyTickInputBuffer.ContainsKey(rpc.currentTick))
        {
            everyTickInputBuffer[rpc.currentTick] = new List<PlayerInputData>();
        }
        
        // This tick already exists in the buffer. Check if the player already has inputs saved for this tick
        foreach (var oldInputData in everyTickInputBuffer[rpc.currentTick])
        {
            if (oldInputData.networkID == rpc.connectionID)
            {
                Debug.LogError("Already received input from network ID " + rpc.connectionID + " for tick " + rpc.currentTick);
                return; // Stop executing the function here, since we don't want to add the new inputData
            }
        }

        everyTickInputBuffer[rpc.currentTick].Add(inputData);
    }
    
    private void CheckIfAllDataReceivedAndSendToClients()
    {
        if (everyTickInputBuffer[currentTick].Count == m_Connections.Length)
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

            // Send the RPC to all connections
            SendRPCWithPlayersInputUpdate(networkIDs, inputs);
            
            // Clean up the temporary lists
            networkIDs.Dispose();
            inputs.Dispose();

            // Finally, remove this tick from the buffer, since we're done processing it
            everyTickInputBuffer.Remove(currentTick);
            currentTick++;
        }
        else if (everyTickInputBuffer[currentTick - 1].Count == m_Connections.Length)
        {
            Debug.LogError("Too many player inputs saved in one tick");
            return;
        }
        else
        {
            return;
        }
    }

}
