using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// System responsible for handling the server side of the network, with connections etc
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(ConnectionHandleSystemGroup))]
    public partial class ServerBehaviour : SystemBase
    {
        private DeterministicSettings _settings;
        private Entity _settingsEntity;
        
        /// <summary>
        /// Network driver used to handle connections
        /// </summary>
        private NetworkDriver _mDriver;
        
        /// <summary>
        /// Pipeline used to handle reliable and sequenced messages
        /// </summary>
        private NetworkPipeline _reliableSimulatorPipeline;
        
        /// <summary>
        /// List of network IDs assigned to players
        /// </summary>
        private NativeList<int> _mNetworkIDs;

        /// <summary>
        /// List of player inputs for each tick
        /// </summary>
        private Dictionary<ulong, NativeList<CapsulesInputs>> _everyTickInputBuffer; // we are storing inputs for each tick but in reality we only need to store previousConfirmed, previousPredicted, currentPredicted and currentConfirmed (interpolation purposes)
        
        /// <summary>
        /// List of hashes for each tick
        /// </summary>
        private Dictionary<ulong, List<ulong>> _everyTickHashBuffer; // here if we will point to indeterminism system we also don't need to store all of them (needs to be confirmed)

        /// <summary>
        /// Array of all possible connection slots in the game and players that are already connected
        /// </summary>
        private NativeArray<NetworkConnection> _connectedPlayers; 
        
        /// <summary>
        /// Specifies the last tick received
        /// </summary>
        private int _lastTickReceived;

        protected override void OnCreate()
        {
            RequireForUpdate<DeterministicSettings>();
        }

        protected override void OnStartRunning()
        {
            _settings = SystemAPI.GetSingleton<DeterministicSettings>();
            _settingsEntity = SystemAPI.GetSingletonEntity<DeterministicSettings>();
        }

        protected override void OnUpdate()
        {
            if(SystemAPI.HasComponent<DeterministicServerListen>(_settingsEntity) && !_mDriver.IsCreated){
                StartListening();
            }
            else if(SystemAPI.HasComponent<DeterministicServerRunSimulation>(_settingsEntity) && !_settings.isInGame)
            {
                Debug.Log("excuse me");
                StartGame();
            }
            
            if(!_mDriver.IsCreated) return;
            _mDriver.ScheduleUpdate().Complete();
            
            Debug.Log("xd " + _settings.isInGame);
            if (!_settings.isInGame)
            {
                AcceptAndHandleConnections();
            }

            for (int i = 0; i < _connectedPlayers.Length; i++)
            {
                if (_connectedPlayers[i].IsCreated)
                {
                    DataStreamReader stream;
                    NetworkEvent.Type cmd;
                    while ((cmd = _mDriver.PopEventForConnection(_connectedPlayers[i], out stream)) !=
                           NetworkEvent.Type.Empty)
                    {
                        switch (cmd)
                        {
                            case NetworkEvent.Type.Data:
                                HandleRpc(stream, _connectedPlayers[i]);
                                break;
                            case NetworkEvent.Type.Disconnect:
                                Debug.Log("Client disconnected from the server.");
                                _connectedPlayers[i] = default;
                                CheckIfAllDataReceivedAndSendToClients();
                                break;
                        }
                    }
                }
            }
        }
        
        protected override void OnDestroy()
        {
            if (!_mDriver.IsCreated) return;
            _mDriver.Dispose();
            _connectedPlayers.Dispose();
            _mNetworkIDs.Dispose();
        }

        /// <summary>
        /// Function used to start the server and listen for incoming connections.
        /// </summary>
        /// <param name="port"> Port on which server will listen </param>
        /// <param name="numberOfAllowedConnections"> How many connections is allowed at maximum</param>
        /// <param name="settings"> Specific network settings for this port</param>
        public void StartListening()
        {
            _connectedPlayers = new NativeArray<NetworkConnection>(_settings.allowedConnectionsPerGame, Allocator.Persistent);
            _mNetworkIDs = new NativeList<int>(_settings.allowedConnectionsPerGame, Allocator.Persistent);

            _everyTickInputBuffer = new Dictionary<ulong, NativeList<CapsulesInputs>>();
            _everyTickHashBuffer = new Dictionary<ulong, List<ulong>>();
            
            _mDriver = NetworkDriver.Create();
            _reliableSimulatorPipeline =
                _mDriver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            
            var endpoint = NetworkEndpoint.AnyIpv4.WithPort((ushort) _settings.serverPort);

            if (_mDriver.Bind(endpoint) != 0)
            {
                Debug.LogError("Failed to bind to port: " + _settings.serverPort);
                return;
            }

            _mDriver.Listen();
        }

        /// <summary>
        /// Function used to start the game and send RPC to clients to start the game. From this point no connection will be accepted.
        /// </summary>
        private void StartGame()
        {
            if (_settings.isInGame) return;
            _settings.isInGame = true;
            
            CollectInitialPlayerData();
            SendRPCtoStartGame();
        }

        /// <summary>
        /// Function used to handle incoming RPCs from clients.
        /// </summary>
        /// <param name="stream">Stream from which the data arrived</param>
        /// <param name="connection">Client connection to check for RPC</param>
        private void HandleRpc(DataStreamReader stream, NetworkConnection connection)
        {
            var copyOfStream = stream;
            var id = (RpcID)copyOfStream.ReadByte(); // for the future check if its within a valid range (id as bytes)
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

        /// <summary>
        /// Function used to set initial player data (network IDs and positions).
        /// </summary>
        private void CollectInitialPlayerData()
        {
            _mNetworkIDs.Clear();
            // _mPlayerInputs.Clear();

            // Collect data from all players
            for (ushort i = 0; i < _connectedPlayers.Length; i++)
            {
                // Example: Collect network IDs and positions of players
                if (_connectedPlayers[i].IsCreated)
                {
                    _mNetworkIDs.Add(i); // set unique Network ID
                    // _mPlayerInputs.Add(new Vector2(0, 0)); // Example input
                }
            }
        }

        /// <summary>
        /// Function used to send RPC to clients to start the game.
        /// </summary>
        private void SendRPCtoStartGame()
        {
            // register OnGameStart method where they can be used to spawn entities, separation between user code and package code

            RpcStartDeterministicSimulation rpc = new RpcStartDeterministicSimulation
            {
                NetworkIDs = _mNetworkIDs,
                TickRate = _settings.simulationTickRate,
                TickAhead = _settings.ticksAhead
            };

            for (ushort i = 0; i < _connectedPlayers.Length; i++)
            {
                if (_connectedPlayers[i].IsCreated)
                {
                    rpc.NetworkID = i;
                    rpc.Serialize(_mDriver, _connectedPlayers[i], _reliableSimulatorPipeline);
                }
            }
        }

        /// <summary>
        /// Function used to send RPC to clients with all players inputs.
        /// </summary>
        /// <param name="networkIDs">List of client IDs</param>
        /// <param name="playerInputs">List of client inputs</param>
        private void SendRPCWithPlayersInputUpdate(NativeList<int> networkIDs, NativeList<CapsulesInputs> playerInputs)
        {
            
            var rpc = new RpcPlayersDataUpdate
            {
                NetworkIDs = networkIDs,
                PlayersCapsuleGameInputs = playerInputs,
                Tick = _lastTickReceived
            };

            foreach (var connectedPlayer in _connectedPlayers)
            {
                if (connectedPlayer.IsCreated)
                {
                    rpc.Serialize(_mDriver, connectedPlayer, _reliableSimulatorPipeline);
                }
            }
        }

        /// <summary>
        /// Function used to send RPC to clients with information about desynchronization.
        /// </summary>
        private void SendRPCWithPlayersDesynchronizationInfo()
        {
            var rpc = new RpcPlayerDesynchronizationInfo { };

            foreach (var t in _connectedPlayers)
            {
                if (t.IsCreated)
                {
                    rpc.Serialize(_mDriver, t, _reliableSimulatorPipeline);
                }
            }
        }

        /// <summary>
        /// Function used to save player inputs to the buffer when those arrive.
        /// </summary>
        /// <param name="rpc">RPC that arrived</param>
        /// <param name="connection">Connection from which it arrived</param>
        private void SaveTheData(RpcBroadcastPlayerInputToServer rpc, NetworkConnection connection)
        {
            for (ushort i = 0; i < _connectedPlayers.Length; i++)
            {
                if (!_connectedPlayers[i].Equals(connection)) continue;
            
                // var inputData = new IDeterministicInputComponentData
                // {
                //     networkID = i,
                //     inputComponentData = rpc.PlayerInput.inputComponentData
                // };
            
                if (!_everyTickInputBuffer.ContainsKey((ulong) rpc.CurrentTick))
                {
                    _everyTickInputBuffer[(ulong) rpc.CurrentTick] = new NativeList<CapsulesInputs>();
                }
            
                if (!_everyTickHashBuffer.ContainsKey((ulong) rpc.CurrentTick))
                {
                    _everyTickHashBuffer[(ulong) rpc.CurrentTick] = new List<ulong>();
                }
            
                // This tick already exists in the buffer. Check if the player already has inputs saved for this tick. No need to check for hash in that case because those should be send together and hash can be the same (if everything is correct) so we will get for example 3 same hashes
                foreach (var oldInputData in _everyTickInputBuffer[(ulong) rpc.CurrentTick])
                {
                    if (oldInputData.networkID == i)
                    {
                        Debug.LogError("Already received input from network ID " + i + " for tick " + rpc.CurrentTick);
                        return; // Stop executing the function here, since we don't want to add the new inputData
                    }
                }
            
                _everyTickInputBuffer[(ulong) rpc.CurrentTick].Add(rpc.CapsuleGameInputs);
                _everyTickHashBuffer[(ulong) rpc.CurrentTick].Add(rpc.HashForCurrentTick);
                _lastTickReceived = rpc.CurrentTick;
            }
        }

        /// <summary>
        /// Function used to get the number of active connections.
        /// </summary>
        /// <returns>Amount of active connections</returns>
        private int GetActiveConnectionCount()
        {
            var count = 0;
            foreach (var connectedPlayer in _connectedPlayers)
            {
                if (connectedPlayer.IsCreated)
                {
                    count++;
                }
            }

            return count;
        }
        
        /// <summary>
        /// Function used to accept new connections and assign them to the first available slot in the connectedPlayers array. If there are no available slots, the connection is disconnected.
        /// </summary>
        private void AcceptAndHandleConnections()
        {
            Debug.Log("elo");
            // Accept new connections
            NetworkConnection c;
            while ((c = _mDriver.Accept()) != default)
            {
                // Find the first available spot in connectedPlayers array
                int index = FindFreePlayerSlot();
                if (index != -1)
                {
                    // Assign the connection to the first available spot
                    _connectedPlayers[index] = c; // Assign network ID based on the index
                    Debug.Log("Accepted a connection with network ID: " + index);
                }
                else
                {
                    Debug.LogWarning("Cannot accept more connections. Server is full.");
                    c.Disconnect(_mDriver);
                }
            }
        }

        /// <summary>
        /// Function used to find the first free slot in the connectedPlayers array.
        /// </summary>
        /// <returns>Empty slot number or -1 otherwise</returns>
        private int FindFreePlayerSlot()
        {
            for (int i = 0; i < _connectedPlayers.Length; i++)
            {
                if (!_connectedPlayers[i].IsCreated)
                {
                    return i;
                }
            }

            return -1; // No free slot found
        }

        /// <summary>
        /// Function used to check if all data for the current tick has been received. If so it sends it to clients
        /// </summary>
        private void CheckIfAllDataReceivedAndSendToClients()
        {
            var desynchronized = false;
            if (_everyTickInputBuffer[(ulong) _lastTickReceived].Length == GetActiveConnectionCount() &&
                _everyTickHashBuffer[(ulong) _lastTickReceived].Count ==
                GetActiveConnectionCount()) // because of different order that we can received those inputs we are checking for last received input
            {
                // We've received a full set of data for this tick, so process it
                var networkIDs = new NativeList<int>(Allocator.Temp);
                var inputs = new NativeList<CapsulesInputs>(); //Allocator.Temp
            
                foreach (var inputData in _everyTickInputBuffer[(ulong) _lastTickReceived])
                {
                    networkIDs.Add(inputData.networkID);
                    inputs.Add(inputData);
                }
            
                // check if every hash is the same
                var firstHash = _everyTickHashBuffer[(ulong) _lastTickReceived][0];
                for (int i = 1; i < _everyTickHashBuffer[(ulong) _lastTickReceived].Count; i++)
                {
                    if (firstHash == _everyTickHashBuffer[(ulong) _lastTickReceived][i]) continue;
            
                    // Hashes are not equal - handle this scenario
                    Debug.LogError("DESYNCHRONIZATION HAPPENED! HASHES ARE NOT EQUAL! " + "Ticks: " +
                                   _lastTickReceived + " Hashes: " + firstHash + " and " +
                                   _everyTickHashBuffer[(ulong) _lastTickReceived][i]);
                    desynchronized = true;
                    i = _everyTickHashBuffer[(ulong) _lastTickReceived].Count;
                }
            
                if (!desynchronized)
                {
                    Debug.Log("All hashes are equal: " + firstHash + ". Number of hashes: " +
                              _everyTickHashBuffer[(ulong) _lastTickReceived].Count + ". Tick: " + _lastTickReceived);
            
                    // Send the RPC to all connections
                    SendRPCWithPlayersInputUpdate(networkIDs, inputs);
                }
                else
                {
                    SendRPCWithPlayersDesynchronizationInfo();
                }
            
                networkIDs.Dispose();
                // inputs.Dispose();
            
                // Remove this tick from the buffer, since we're done processing it
                _everyTickInputBuffer.Remove((ulong) _lastTickReceived);
                _everyTickHashBuffer.Remove((ulong) _lastTickReceived);
                _lastTickReceived++;
            }
            else if (_everyTickInputBuffer[(ulong) _lastTickReceived].Length > GetActiveConnectionCount())
            {
                Debug.LogError("Too many player inputs saved in one tick");
            }
        }
    }
}