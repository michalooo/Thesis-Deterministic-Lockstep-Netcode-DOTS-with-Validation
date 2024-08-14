using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using UnityEngine;
using Random = System.Random;

namespace DeterministicLockstep
{
    /// <summary>
    /// System responsible for handling the server side of the netcode model.
    /// It listens for incoming connections and handles incoming client RPCs.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class ServerBehaviour : SystemBase
    {
        /// <summary>
        /// Network driver used to handle connections
        /// </summary>
        private NetworkDriver _networkDriver;
        
        /// <summary>
        /// Pipeline used to handle reliable and sequenced messages
        /// </summary>
        private NetworkPipeline _reliablePipeline;
        
        /// <summary>
        /// List of network IDs assigned to players
        /// </summary>
        private NativeList<int> _clientsNetworkIDs;

        /// <summary>
        /// List of player inputs for each tick
        /// </summary>
        private Dictionary<ulong, NativeList<RpcBroadcastPlayerTickDataToServer>> _bufferOfClientInputsForEachTick;

        /// <summary>
        /// NativeList of combined RPC send from every client with their input.
        /// This list is subsequently sent to every client.
        /// </summary>
        private NativeList<RpcBroadcastTickDataToClients> _dataToSendToEveryClientWithEveryClientInputs;
        
        /// <summary>
        /// List of hashes from every client for each tick.
        /// It may be empty if game is using no hash calculation.
        /// It may contain one hash per tick per client if game is using per-tick hash calculation.
        /// It may contain multiple hashes per tick per client if game is using per-system hash calculation.
        /// </summary>
        private Dictionary<ulong, NativeList<NativeList<ulong>>> _hashBufferForEveryTick;

        /// <summary>
        /// NativeList of final hashes for each client.
        /// Used to compare if all clients ended the game with the same state.
        /// </summary>
        private NativeList<RpcEndGameHash> _endGameHashes;

        /// <summary>
        /// Array of all possible connection slots in the game containing clients that are already connected
        /// </summary>
        private NativeArray<NetworkConnection> _connectedPlayers; 
        
        /// <summary>
        /// NativeArray containing starting game state hashes for each connected client.
        /// </summary>
        private NativeArray<ulong> _clientsReady;
        
        /// <summary>
        /// Specifies the last tick received from all clients.
        /// To increase this value inputs from all clients need to arrive to the server.
        /// </summary>
        private int _lastTickReceivedFromClient;

        /// <summary>
        /// Bool value signaling that nondeterminism was detected.
        /// </summary>
        private bool _nondeterminismDetected;

        protected override void OnCreate()
        {
            RequireForUpdate<DeterministicSettings>();
            EntityManager.CreateSingleton(new DeterministicServerComponent()
            {
                deterministicServerWorkingMode = DeterministicServerWorkingMode.None
            });
            _nondeterminismDetected = false;
        }

        protected override void OnUpdate()
        {
            if (SystemAPI.GetSingleton<DeterministicServerComponent>().deterministicServerWorkingMode == DeterministicServerWorkingMode.ListenForConnections && !_networkDriver.IsCreated)
            {
                StartListening();
            }
            
            if(!_networkDriver.IsCreated) return;

            if (SystemAPI.GetSingleton<DeterministicServerComponent>().deterministicServerWorkingMode ==
                DeterministicServerWorkingMode.RunDeterministicSimulation &&
                !SystemAPI.GetSingleton<DeterministicSettings>().isInGame)
            {
                StartGame();
            }
            
            if (SystemAPI.GetSingleton<DeterministicServerComponent>().deterministicServerWorkingMode == DeterministicServerWorkingMode.Disconnect)
            {
                Disconnect();
            }
            
            _networkDriver.ScheduleUpdate().Complete();
            
            if (!SystemAPI.GetSingleton<DeterministicSettings>().isInGame)
            {
                AcceptAndHandleConnections();
            }

            for (var i = 0; i < _connectedPlayers.Length; i++)
            {
                if (!_connectedPlayers[i].IsCreated) continue;
                NetworkEvent.Type networkEvent;
                while ((networkEvent = _networkDriver.PopEventForConnection(_connectedPlayers[i], out var stream)) !=
                       NetworkEvent.Type.Empty)
                {
                    switch (networkEvent)
                    {
                        case NetworkEvent.Type.Data:
                            HandleRpc(stream, _connectedPlayers[i]);
                            break;
                        case NetworkEvent.Type.Disconnect:
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
        
        private void Disconnect()
        {
            _networkDriver.ScheduleUpdate().Complete();

            for (int i = 0; i < _connectedPlayers.Length; i++)
            {
                if (!_connectedPlayers[i].IsCreated) continue;
                _connectedPlayers[i].Disconnect(_networkDriver);
                _connectedPlayers[i] = default;
            }
        }
        
        protected override void OnDestroy()
        {
            if (!_networkDriver.IsCreated) return;
            _networkDriver.Dispose();
            _connectedPlayers.Dispose();
            _clientsNetworkIDs.Dispose();
            _clientsReady.Dispose();
            _bufferOfClientInputsForEachTick.Clear();
            _dataToSendToEveryClientWithEveryClientInputs.Dispose();
            _hashBufferForEveryTick.Clear();
            _endGameHashes.Dispose();
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
            _clientsReady = new NativeArray<ulong>(SystemAPI.GetSingleton<DeterministicSettings>().allowedConnectionsPerGame, Allocator.Persistent);
            for (int i = 0; i < _clientsReady.Length; i++)
            {
                _clientsReady[i] = 1;
            }
            _clientsNetworkIDs = new NativeList<int>(Allocator.Persistent);

            _bufferOfClientInputsForEachTick = new Dictionary<ulong, NativeList<RpcBroadcastPlayerTickDataToServer>>();
            _dataToSendToEveryClientWithEveryClientInputs = new NativeList<RpcBroadcastTickDataToClients>(Allocator.Persistent);
            _hashBufferForEveryTick = new Dictionary<ulong, NativeList<NativeList<ulong>>>();
            _endGameHashes = new NativeList<RpcEndGameHash>(Allocator.Persistent);
            
            _networkDriver = NetworkDriver.Create();
            _reliablePipeline =
                _networkDriver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            
            var endpoint = NetworkEndpoint.AnyIpv4.WithPort((ushort) SystemAPI.GetSingleton<DeterministicSettings>().serverPort);

            if (_networkDriver.Bind(endpoint) != 0)
            {
                Debug.LogError("Failed to bind to port: " + SystemAPI.GetSingleton<DeterministicSettings>().serverPort);
                return;
            }

            _networkDriver.Listen();
        }

        /// <summary>
        /// Function used to start the game and send RPC to clients to start the game.
        /// After this function executes no connection will be accepted.
        /// </summary>
        private void StartGame()
        {
            if (SystemAPI.GetSingleton<DeterministicSettings>().isInGame) return;
            
            var deterministicSettings = SystemAPI.GetSingletonRW<DeterministicSettings>();
            deterministicSettings.ValueRW.isInGame = true;
            
            SendRPCToLoadGame();
        }

        /// <summary>
        /// Function used to handle incoming RPCs from clients.
        /// </summary>
        /// <param name="stream">Stream from which the data arrived</param>
        /// <param name="connection">Client connection to check for RPC</param>
        private void HandleRpc(DataStreamReader stream, NetworkConnection connection)
        {
            var copyOfStream = stream;
            var rpcID = (RpcID)copyOfStream.ReadByte();
            if (!Enum.IsDefined(typeof(RpcID), rpcID))
            {
                Debug.LogError("Received invalid RPC ID: " + rpcID);
                return;
            }

            switch (rpcID)
            {
                case RpcID.BroadcastPlayerTickDataToServer:
                    var rpc = new RpcBroadcastPlayerTickDataToServer();
                    rpc.Deserialize(ref stream);
                    SaveTheData(rpc, connection);
                    CheckIfAllDataReceivedAndSendToClients();
                    break;
                case RpcID.PlayerReady:
                    var clientReadyRPC = new RpcPlayerReady();
                    clientReadyRPC.Deserialize(ref stream);
                    CheckIfAllClientsReady(clientReadyRPC);
                    break;
                case RpcID.GameEnded:
                    var gameEndedRPC = new RpcEndGameHash();
                    gameEndedRPC.Deserialize(ref stream);
                    CheckEndGameHashes(gameEndedRPC);
                    break;
                case RpcID.StartDeterministicGameSimulation:
                    Debug.LogError("Received RPC with ID: " + rpcID + " should not be received by the server.");
                    break;
                case RpcID.BroadcastTickDataToClients:
                    Debug.LogError("Received RPC with ID: " + rpcID + " should not be received by the server.");
                    break;
                case RpcID.PlayerDesynchronized:
                    Debug.LogError("Received RPC with ID: " + rpcID + " should not be received by the server.");
                    break;
                case RpcID.LoadGame:
                    Debug.LogError("Received RPC with ID: " + rpcID + " should not be received by the server.");
                    break;
                default:
                    Debug.LogError("Received RPC ID not proceeded by the server: " + rpcID);
                    break;
            }
        }
        
        /// <summary>
        /// Function used to check if all clients are ready to start the game.
        /// </summary>
        /// <param name="rpc">Last received rpc with client readiness message</param>
        private void CheckIfAllClientsReady(RpcPlayerReady rpcPlayerReady)
        {
            // mark that this specific client is ready. This value is a starting hash
            _clientsReady[rpcPlayerReady.ClientNetworkID] = rpcPlayerReady.StartingHash;
            
            // check if all clients are ready
            var hostStartingHash = _clientsReady[0];
            var desynchronized = false;
            if (hostStartingHash == 1) return;
            
            for (var i = 0; i < _clientsReady.Length; i++)
            {
                if (_clientsReady[i] == 1 && _connectedPlayers[i].IsCreated)
                {
                    return;
                }
                if(_clientsReady[i] != hostStartingHash && _connectedPlayers[i].IsCreated)
                {
                    desynchronized = true;
                }
            }

            if (desynchronized)
            {
                SendRPCSignallingNondeterminismDetection();
            }
            else
            {
                SendRPCtoStartGame();
            }
        }
        
        private void SendRPCToLoadGame()
        {
            _clientsNetworkIDs.Clear();
            
            for (ushort i = 0; i < _connectedPlayers.Length; i++)
            {
                if (_connectedPlayers[i].IsCreated)
                {
                    _clientsNetworkIDs.Add(i); 
                }
            }
            
            
            var rpcLoadGame = new RpcLoadGame();
            var playersIDs = new NativeList<int>(Allocator.Temp);
            for (ushort i = 0; i < _connectedPlayers.Length; i++)
            {
                if (_connectedPlayers[i].IsCreated)
                {
                    playersIDs.Add(i);
                }
            }
            
            for (ushort i = 0; i < _connectedPlayers.Length; i++)
            {
                if (!_connectedPlayers[i].IsCreated) continue;
                rpcLoadGame.ClientNetworkID = i;
                rpcLoadGame.NetworkIDsOfAllClients = playersIDs;
                rpcLoadGame.Serialize(_networkDriver, _connectedPlayers[i], _reliablePipeline);
            }
            playersIDs.Dispose();
        }

        /// <summary>
        /// Function used to send RPC to clients to start the game.
        /// It contains all necessary information to start the game together with settings.
        /// </summary>
        private void SendRPCtoStartGame()
        {
            var random = new Random();
            RpcStartDeterministicSimulation rpcStartDeterministicSimulation = new RpcStartDeterministicSimulation
            {
                NetworkIDsOfAllClients = _clientsNetworkIDs,
                GameIntendedTickRate = SystemAPI.GetSingleton<DeterministicSettings>().simulationTickRate,
                TicksOfForcedInputLatency = SystemAPI.GetSingleton<DeterministicSettings>().ticksOfForcedInputLatency,
                SeedForPlayerRandomActions = (uint)random.Next(1, int.MaxValue),
                // SeedForPlayerRandomActions = 550619823, // hardcoded seed which results in nondeterministic sin/cos values
                DeterminismHashCalculationOption = (int) SystemAPI.GetSingleton<DeterministicSettings>().hashCalculationOption
            };
            
            for (ushort i = 0; i < _connectedPlayers.Length; i++)
            {
                if (!_connectedPlayers[i].IsCreated) continue;
                rpcStartDeterministicSimulation.ClientAssignedNetworkID = i;
                rpcStartDeterministicSimulation.Serialize(_networkDriver, _connectedPlayers[i], _reliablePipeline);
            }
        }

        /// <summary>
        /// Function used to send RPC to clients with all players inputs.
        /// It sends grouped inputs from all clients for each tick.
        /// </summary>
        /// <param name="networkIDs">List of client IDs</param>
        /// <param name="playerInputs">List of client inputs</param>
        private void SendRPCWithPlayersInputUpdate(NativeList<int> networkIDs, NativeList<PongInputs> playerInputs)
        {
            var clonedNetworkIDsNativeList = new NativeList<int>(Allocator.TempJob);
            foreach (var networkID in networkIDs)
            {
                clonedNetworkIDsNativeList.Add(networkID);
            }
            
            var clonedPlayerInputsNativeList = new NativeList<PongInputs>(Allocator.TempJob);
            foreach (var input in playerInputs)
            {
                clonedPlayerInputsNativeList.Add(input);
            }
            
            var rpc = new RpcBroadcastTickDataToClients
            {
                NetworkIDsOfAllClients = clonedNetworkIDsNativeList,
                GameInputsFromAllClients = clonedPlayerInputsNativeList,
                SimulationTick = _lastTickReceivedFromClient
            };
            _dataToSendToEveryClientWithEveryClientInputs.Add(rpc);
            
            
            foreach (var connectedPlayer in _connectedPlayers.Where(connectedPlayer => connectedPlayer.IsCreated))
            {
                rpc.Serialize(_networkDriver, connectedPlayer, _reliablePipeline);
            }
        }
        
        /// <summary>
        /// Function used to send RPC to clients informing them that nondeterminism was detected and the game execution should be stopped.
        /// Parameters are required in case that we need to first save inputs and then stop the game, otherwise ServerInputRecording will miss one entry.
        /// </summary>
        private void SendRPCSignallingNondeterminismDetection(NativeList<int> networkIDs, NativeList<PongInputs> playerInputs)
        {
            var rpcWithPlayersDataToStore = new RpcBroadcastTickDataToClients
            {
                NetworkIDsOfAllClients = networkIDs,
                GameInputsFromAllClients = playerInputs,
                SimulationTick = _lastTickReceivedFromClient
            };
            _dataToSendToEveryClientWithEveryClientInputs.Add(rpcWithPlayersDataToStore);
            
            var rpcWithPlayerDesynchronizationSignal = new RpcPlayerDesynchronization { NonDeterministicTick = (ulong) _lastTickReceivedFromClient};

            foreach (var connection in _connectedPlayers.Where(connection => connection.IsCreated))
            {
                rpcWithPlayerDesynchronizationSignal.Serialize(_networkDriver, connection, _reliablePipeline);
            }
            DeterministicLogger.Instance.LogServerInputRecordingToTheFile(_dataToSendToEveryClientWithEveryClientInputs, SystemAPI.GetSingleton<DeterministicSettings>());
        }
        
        /// <summary>
        /// Function used to send RPC to clients informing them that nondeterminism was detected and the game execution should be stopped.
        /// This version is used when confirming starting state and end state of the game when no inputs are needed to be saved.
        /// </summary>
        private void SendRPCSignallingNondeterminismDetection()
        {
            var rpcWithPlayerDesynchronizationSignal = new RpcPlayerDesynchronization { NonDeterministicTick = (ulong) _lastTickReceivedFromClient};

            foreach (var connection in _connectedPlayers.Where(connection => connection.IsCreated))
            {
                rpcWithPlayerDesynchronizationSignal.Serialize(_networkDriver, connection, _reliablePipeline);
            }
            DeterministicLogger.Instance.LogServerInputRecordingToTheFile(_dataToSendToEveryClientWithEveryClientInputs, SystemAPI.GetSingleton<DeterministicSettings>());
        }

        /// <summary>
        /// Function used to save player inputs to the buffer when those arrive.
        /// This function also checks if all inputs arrived and if so it sends them to clients as combined packet.
        /// </summary>
        /// <param name="rpc">RPC that arrived</param>
        /// <param name="connection">Connection from which it arrived</param>
        private void 
            SaveTheData(RpcBroadcastPlayerTickDataToServer rpc, NetworkConnection connection)
        {
            for (var i = 0; i < _connectedPlayers.Length; i++)
            {
                if (!_connectedPlayers[i].Equals(connection)) continue;
            
                if (!_bufferOfClientInputsForEachTick.ContainsKey((ulong) rpc.TickToApplyInputsOn))
                {
                    _bufferOfClientInputsForEachTick[(ulong) rpc.TickToApplyInputsOn] = new NativeList<RpcBroadcastPlayerTickDataToServer>(Allocator.Persistent);
                }
                
                if (!_hashBufferForEveryTick.ContainsKey((ulong) rpc.TickToApplyInputsOn))
                {
                    _hashBufferForEveryTick[(ulong) rpc.TickToApplyInputsOn] = new NativeList<NativeList<ulong>>(Allocator.Persistent);
                }
            
                // This tick already exists in the buffer. Check if the player already has inputs saved for this tick. No need to check for hash in that case because those should be send together and hash can be the same (if everything is correct) so we will get for example 3 same hashes
                foreach (var oldInputData in _bufferOfClientInputsForEachTick[(ulong) rpc.TickToApplyInputsOn])
                {
                    if (oldInputData.ClientNetworkID == i)
                    {
                        return;
                    }
                }
            
                _bufferOfClientInputsForEachTick[(ulong) rpc.TickToApplyInputsOn].Add(rpc);
                _hashBufferForEveryTick[(ulong) rpc.TickToApplyInputsOn].Add(rpc.HashesForTheTick);
                _lastTickReceivedFromClient = rpc.TickToApplyInputsOn;
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
        /// Function used to accept new connections and assign them to the first available slot in the connectedPlayers array.
        /// If there are no available slots, the connection is disconnected.
        /// </summary>
        private void AcceptAndHandleConnections()
        {
            NetworkConnection connection;
            while ((connection = _networkDriver.Accept()) != default)
            {
                var slotIndex = FindFreePlayerSlot();
                if (slotIndex != -1)
                {
                    _connectedPlayers[slotIndex] = connection;
                }
                else
                {
                    Debug.LogWarning("Cannot accept more connections. Server is full.");
                    connection.Disconnect(_networkDriver);
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

            return -1;
        }

        /// <summary>
        /// Function used to check if all data for the current tick has been received.
        /// If so it sends it back to clients as one packet combining all inputs from clients.
        /// </summary>
        private void CheckIfAllDataReceivedAndSendToClients()
        {
            if (_nondeterminismDetected) return;
            
            if (_bufferOfClientInputsForEachTick[(ulong) _lastTickReceivedFromClient].Length == GetActiveConnectionCount() &&
                _hashBufferForEveryTick[(ulong) _lastTickReceivedFromClient].Length ==
                GetActiveConnectionCount())
            {
                var networkIDs = new NativeList<int>(Allocator.Temp);
                var playerInputs = new NativeList<PongInputs>(Allocator.Temp);
            
                foreach (var inputDataForEachTick in _bufferOfClientInputsForEachTick[(ulong) _lastTickReceivedFromClient])
                {
                    if (!_connectedPlayers[inputDataForEachTick.ClientNetworkID].IsCreated) continue;
                    networkIDs.Add(inputDataForEachTick.ClientNetworkID);
                    playerInputs.Add(inputDataForEachTick.PlayerGameInput);
                }
                
                // Get the number of hashes (assuming all players have the same number of hashes)
                var numHashesPerPlayer = _hashBufferForEveryTick[(ulong)_lastTickReceivedFromClient][0].Length;

                // Iterate over each hash index
                for (var systemHash = 0; systemHash < numHashesPerPlayer; systemHash++)
                {
                    // Get the first player's hash at this index
                    var firstPlayerHash = _hashBufferForEveryTick[(ulong)_lastTickReceivedFromClient][0][systemHash];

                    // Iterate over each player's hashes at this index
                    for (var player = 1; player < _hashBufferForEveryTick[(ulong)_lastTickReceivedFromClient].Length; player++)
                    {
                        var currentPlayerHash = _hashBufferForEveryTick[(ulong)_lastTickReceivedFromClient][player][systemHash];

                        // If the hashes are not equal, log an error and set desynchronized to true
                        if (firstPlayerHash != currentPlayerHash)
                        {
                            if (!SystemAPI.GetSingletonRW<DeterministicSettings>().ValueRO.isReplayFromFile)
                            {
                                Debug.LogError("DESYNCHRONIZATION HAPPENED! HASHES ARE NOT EQUAL! " + "Ticks: " +
                                               _lastTickReceivedFromClient + " Hashes: " + firstPlayerHash + " and " +
                                               currentPlayerHash + " System number: " + systemHash);
                                _nondeterminismDetected = true;
                            }
                            
                            break;
                        }
                    }
                    
                    if (_nondeterminismDetected) break;
                }
                
                if (!_nondeterminismDetected)
                {
                    SendRPCWithPlayersInputUpdate(networkIDs, playerInputs);
                }
                else if(!SystemAPI.GetSingletonRW<DeterministicSettings>().ValueRO.isReplayFromFile)
                {
                    SendRPCSignallingNondeterminismDetection(networkIDs, playerInputs);
                }
            
                networkIDs.Dispose();
                playerInputs.Dispose();
                
                _hashBufferForEveryTick.Remove((ulong) _lastTickReceivedFromClient);
                _lastTickReceivedFromClient++;
            }
            else if (_hashBufferForEveryTick[(ulong) _lastTickReceivedFromClient].Length > GetActiveConnectionCount())
            {
                Debug.LogError("Too many player inputs saved in one tick");
            }
        }
        
        /// <summary>
        /// Function which compares hashes from all clients for the last game frame checking if all clients ended the game with the same state.
        /// </summary>
        /// <param name="rpc"> Last received rpc from client </param>
        private void CheckEndGameHashes(RpcEndGameHash rpcEndGameHash)
        {
            foreach (var endGameHash in _endGameHashes)
            {
                if (rpcEndGameHash.ClientNetworkID == endGameHash.ClientNetworkID) return;
            }
            _endGameHashes.Add(rpcEndGameHash);
            
            if (_endGameHashes.Length == GetActiveConnectionCount())
            {
                var desynchronized = false;
                var endGameHostHash = _endGameHashes[0].FinalGameHash;
                for (var i = 1; i < _endGameHashes.Length; i++)
                {
                    if(endGameHostHash != _endGameHashes[i].FinalGameHash)
                    {
                        desynchronized = true;
                        break;
                    }
                }
                
                if (desynchronized)
                {
                    Debug.LogError("Desynchronized on game end");
                    SendRPCSignallingNondeterminismDetection();
                }
                else
                {
                    Debug.Log("Game ended successfully");
                    Disconnect();
                }
            }
        }
    }
}