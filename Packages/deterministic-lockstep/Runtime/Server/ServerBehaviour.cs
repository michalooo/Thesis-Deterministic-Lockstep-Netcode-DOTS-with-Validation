using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <summary>
        /// Network driver used to handle connections
        /// </summary>
        private NetworkDriver _mDriver;
        
        /// <summary>
        /// Pipeline used to handle reliable and sequenced messages
        /// </summary>
        private NetworkPipeline _reliableSimulationPipeline;
        
        /// <summary>
        /// List of network IDs assigned to players
        /// </summary>
        private NativeList<int> _mNetworkIDs;

        /// <summary>
        /// List of player inputs for each tick
        /// </summary>
        private Dictionary<ulong, NativeList<RpcBroadcastPlayerTickDataToServer>> _everyTickInputBuffer; // we are storing inputs for each tick but in reality we only need to store previousConfirmed, previousPredicted, currentPredicted and currentConfirmed (interpolation purposes)
        
        /// <summary>
        /// List of hashes for each tick
        /// </summary>
        private Dictionary<ulong, NativeList<ulong>> _everyTickHashBuffer; // here if we will point to indeterminism system we also don't need to store all of them (needs to be confirmed)

        /// <summary>
        /// Array of all possible connection slots in the game and players that are already connected
        /// </summary>
        private NativeArray<NetworkConnection> _connectedPlayers; 
        
        /// <summary>
        /// Specifies the last tick received
        /// </summary>
        private int _lastTickReceivedFromServer;

        protected override void OnCreate()
        {
            RequireForUpdate<DeterministicSettings>();
            EntityManager.CreateSingleton(new DeterministicServerComponent()
            {
                deterministicServerWorkingMode = DeterministicServerWorkingMode.None
            });
        }

        protected override void OnUpdate()
        {
            if (SystemAPI.GetSingleton<DeterministicServerComponent>().deterministicServerWorkingMode == DeterministicServerWorkingMode.ListenForConnections &&
                !_mDriver.IsCreated)
            {
                StartListening();
            }
            if(SystemAPI.GetSingleton<DeterministicServerComponent>().deterministicServerWorkingMode == DeterministicServerWorkingMode.RunDeterministicSimulation && !SystemAPI.GetSingleton<DeterministicSettings>().isInGame) StartGame();
            
            if(!_mDriver.IsCreated) return;
            
            
            
            
            _mDriver.ScheduleUpdate().Complete();
            
            if (!SystemAPI.GetSingleton<DeterministicSettings>().isInGame)
            {
                AcceptAndHandleConnections();
            }

            for (var i = 0; i < _connectedPlayers.Length; i++)
            {
                if (!_connectedPlayers[i].IsCreated) continue;
                NetworkEvent.Type cmd;
                while ((cmd = _mDriver.PopEventForConnection(_connectedPlayers[i], out var stream)) !=
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
                        case NetworkEvent.Type.Empty:
                            break;
                        case NetworkEvent.Type.Connect:
                            break; // this is handled in AcceptAndHandleConnections
                        default:
                            throw new ArgumentOutOfRangeException();
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
        private void StartListening()
        {
            _connectedPlayers = new NativeArray<NetworkConnection>(SystemAPI.GetSingleton<DeterministicSettings>().allowedConnectionsPerGame, Allocator.Persistent);
            _mNetworkIDs = new NativeList<int>(SystemAPI.GetSingleton<DeterministicSettings>().allowedConnectionsPerGame, Allocator.Persistent);

            _everyTickInputBuffer = new Dictionary<ulong, NativeList<RpcBroadcastPlayerTickDataToServer>>();
            _everyTickHashBuffer = new Dictionary<ulong, NativeList<ulong>>();
            
            _mDriver = NetworkDriver.Create();
            _reliableSimulationPipeline =
                _mDriver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            
            var endpoint = NetworkEndpoint.AnyIpv4.WithPort((ushort) SystemAPI.GetSingleton<DeterministicSettings>().serverPort);

            if (_mDriver.Bind(endpoint) != 0)
            {
                Debug.LogError("Failed to bind to port: " + SystemAPI.GetSingleton<DeterministicSettings>().serverPort);
                return;
            }

            _mDriver.Listen();
        }

        /// <summary>
        /// Function used to start the game and send RPC to clients to start the game. From this point no connection will be accepted.
        /// </summary>
        private void StartGame()
        {
            Debug.Log("Game started");
            if (SystemAPI.GetSingleton<DeterministicSettings>().isInGame) return;
            
            var settings = SystemAPI.GetSingleton<DeterministicSettings>();
            settings.isInGame = true;
            SystemAPI.SetSingleton(settings);
            
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
            var id = (RpcID)copyOfStream.ReadByte();
            if (!Enum.IsDefined(typeof(RpcID), id))
            {
                Debug.LogError("Received invalid RPC ID: " + id);
                return;
            }

            switch (id)
            {
                case RpcID.BroadcastPlayerTickDataToServer:
                    var rpc = new RpcBroadcastPlayerTickDataToServer();
                    rpc.Deserialize(ref stream);
                    SaveTheData(rpc, connection);
                    CheckIfAllDataReceivedAndSendToClients();
                    break;
                case RpcID.StartDeterministicGameSimulation:
                    Debug.LogError("StartDeterministicGameSimulation should never be received by the server");
                    break;
                case RpcID.BroadcastTickDataToClients:
                    Debug.LogError("BroadcastTickDataToClients should never be received by the server");
                    break;
                case RpcID.PlayersDesynchronizedMessage:
                    Debug.LogError("PlayersDesynchronizedMessage should never be received by the server");
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

            // Collect data from all players
            for (ushort i = 0; i < _connectedPlayers.Length; i++)
            {
                // Example: Collect network IDs and positions of players
                if (_connectedPlayers[i].IsCreated)
                {
                    _mNetworkIDs.Add(i); // set unique Network ID
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
                PlayersNetworkIDs = _mNetworkIDs,
                TickRate = SystemAPI.GetSingleton<DeterministicSettings>().simulationTickRate,
                TicksOfForcedInputLatency = SystemAPI.GetSingleton<DeterministicSettings>().ticksAhead
            };

            for (ushort i = 0; i < _connectedPlayers.Length; i++)
            {
                if (_connectedPlayers[i].IsCreated)
                {
                    rpc.ThisConnectionNetworkID = i;
                    rpc.Serialize(_mDriver, _connectedPlayers[i], _reliableSimulationPipeline);
                }
            }
        }

        /// <summary>
        /// Function used to send RPC to clients with all players inputs.
        /// </summary>
        /// <param name="networkIDs">List of client IDs</param>
        /// <param name="playerInputs">List of client inputs</param>
        private void SendRPCWithPlayersInputUpdate(NativeList<int> networkIDs, NativeList<PongInputs> playerInputs)
        {
            var rpc = new RpcBroadcastTickDataToClients
            {
                NetworkIDs = networkIDs,
                PlayersPongGameInputs = playerInputs,
                SimulationTick = _lastTickReceivedFromServer
            };

            foreach (var connectedPlayer in _connectedPlayers.Where(connectedPlayer => connectedPlayer.IsCreated))
            {
                rpc.Serialize(_mDriver, connectedPlayer, _reliableSimulationPipeline);
            }
        }

        /// <summary>
        /// Function used to send RPC to clients with information about desynchronization.
        /// </summary>
        private void SendRPCWithPlayersDesynchronizationInfo()
        {
            var rpc = new RpcPlayerDesynchronizationMessage { };

            foreach (var connection in _connectedPlayers.Where(connection => connection.IsCreated))
            {
                rpc.Serialize(_mDriver, connection, _reliableSimulationPipeline);
            }
        }

        /// <summary>
        /// Function used to save player inputs to the buffer when those arrive.
        /// </summary>
        /// <param name="rpc">RPC that arrived</param>
        /// <param name="connection">Connection from which it arrived</param>
        private void 
            SaveTheData(RpcBroadcastPlayerTickDataToServer rpc, NetworkConnection connection)
        {
            for (var i = 0; i < _connectedPlayers.Length; i++)
            {
                if (!_connectedPlayers[i].Equals(connection)) continue;
            
                if (!_everyTickInputBuffer.ContainsKey((ulong) rpc.FutureTick))
                {
                    _everyTickInputBuffer[(ulong) rpc.FutureTick] = new NativeList<RpcBroadcastPlayerTickDataToServer>(Allocator.Persistent);
                }
            
                if (!_everyTickHashBuffer.ContainsKey((ulong) rpc.FutureTick))
                {
                    _everyTickHashBuffer[(ulong) rpc.FutureTick] = new NativeList<ulong>(Allocator.Persistent);
                }
            
                // This tick already exists in the buffer. Check if the player already has inputs saved for this tick. No need to check for hash in that case because those should be send together and hash can be the same (if everything is correct) so we will get for example 3 same hashes
                foreach (var oldInputData in _everyTickInputBuffer[(ulong) rpc.FutureTick])
                {
                    if (oldInputData.PlayerNetworkID == i)
                    {
                        Debug.LogError("Already received input from network ID " + i + " for tick " + rpc.FutureTick);
                        return; // Stop executing the function here, since we don't want to add the new inputData
                    }
                }
            
                _everyTickInputBuffer[(ulong) rpc.FutureTick].Add(rpc);
                _everyTickHashBuffer[(ulong) rpc.FutureTick].Add(rpc.HashForFutureTick);
                _lastTickReceivedFromServer = rpc.FutureTick;
            }
        }

        /// <summary>
        /// Function used to get the number of active connections.
        /// </summary>
        /// <returns>Amount of active connections</returns>
        private int GetActiveConnectionCount()
        {
            return _connectedPlayers.Count(connectedPlayer => connectedPlayer.IsCreated);
        }
        
        /// <summary>
        /// Function used to accept new connections and assign them to the first available slot in the connectedPlayers array. If there are no available slots, the connection is disconnected.
        /// </summary>
        private void AcceptAndHandleConnections()
        {
            // Accept new connections
            NetworkConnection connection;
            while ((connection = _mDriver.Accept()) != default)
            {
                // Find the first available spot in connectedPlayers array
                var index = FindFreePlayerSlot();
                if (index != -1)
                {
                    // Assign the connection to the first available spot
                    _connectedPlayers[index] = connection; // Assign network ID based on the index
                    Debug.Log("Accepted a connection with network ID: " + index);
                }
                else
                {
                    Debug.LogWarning("Cannot accept more connections. Server is full.");
                    connection.Disconnect(_mDriver);
                }
            }
        }

        /// <summary>
        /// Function used to find the first free slot in the connectedPlayers array.
        /// </summary>
        /// <returns>Empty slot number or -1 otherwise</returns>
        private int FindFreePlayerSlot()
        {
            for (var i = 0; i < _connectedPlayers.Length; i++)
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
            if (_everyTickInputBuffer[(ulong) _lastTickReceivedFromServer].Length == GetActiveConnectionCount() &&
                _everyTickHashBuffer[(ulong) _lastTickReceivedFromServer].Length ==
                GetActiveConnectionCount()) // because of different order that we can received those inputs we are checking for last received input
            {
                // We've received a full set of data for this tick, so process it
                var networkIDs = new NativeList<int>(Allocator.Temp);
                var inputs = new NativeList<PongInputs>(Allocator.Temp);
            
                foreach (var inputData in _everyTickInputBuffer[(ulong) _lastTickReceivedFromServer])
                {
                    networkIDs.Add(inputData.PlayerNetworkID);
                    inputs.Add(inputData.PongGameInputs);
                }
            
                // check if every hash is the same. TODO will need some changes regarding determinism per system
                var firstHash = _everyTickHashBuffer[(ulong) _lastTickReceivedFromServer][0];
                for (var i = 1; i < _everyTickHashBuffer[(ulong) _lastTickReceivedFromServer].Length; i++)
                {
                    if (firstHash == _everyTickHashBuffer[(ulong) _lastTickReceivedFromServer][i]) continue;
            
                    // Hashes are not equal - handle this scenario
                    Debug.LogError("DESYNCHRONIZATION HAPPENED! HASHES ARE NOT EQUAL! " + "Ticks: " +
                                   _lastTickReceivedFromServer + " Hashes: " + firstHash + " and " +
                                   _everyTickHashBuffer[(ulong) _lastTickReceivedFromServer][i]);
                    desynchronized = true;
                    i = _everyTickHashBuffer[(ulong) _lastTickReceivedFromServer].Length;
                }
            
                if (!desynchronized)
                {
                    Debug.Log("All hashes are equal: " + firstHash + ". Number of hashes: " +
                              _everyTickHashBuffer[(ulong) _lastTickReceivedFromServer].Length + ". Tick: " + _lastTickReceivedFromServer);
            
                    // Send the RPC to all connections
                    SendRPCWithPlayersInputUpdate(networkIDs, inputs);
                }
                else
                {
                    SendRPCWithPlayersDesynchronizationInfo();
                }
            
                networkIDs.Dispose();
                inputs.Dispose();
            
                // Remove this tick from the buffer, since we're done processing it
                _everyTickInputBuffer.Remove((ulong) _lastTickReceivedFromServer);
                _everyTickHashBuffer.Remove((ulong) _lastTickReceivedFromServer);
                _lastTickReceivedFromServer++;
            }
            else if (_everyTickInputBuffer[(ulong) _lastTickReceivedFromServer].Length > GetActiveConnectionCount())
            {
                Debug.LogError("Too many player inputs saved in one tick");
            }
        }
    }
}