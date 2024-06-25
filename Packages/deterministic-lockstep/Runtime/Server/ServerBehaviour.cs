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
    /// System responsible for handling the server side of the network, with connections etc
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(ConnectionHandleSystemGroup))]
    public partial class ServerBehaviour : SystemBase
    {
        const int AmountOfPlayersPingMessages = 100;
        // /// <summary>
        // /// List of deterministic systems that are used by the client (only used to point which one is causing desynchronization)
        // /// </summary>
        // private NativeList<FixedString64Bytes> _clientDeterministicSystems;
        
        /// <summary>
        /// Network driver used to handle connections
        /// </summary>
        private NetworkDriver _mDriver;
        
        /// <summary>
        /// Pipeline used to handle reliable and sequenced messages
        /// </summary>
        private NetworkPipeline _reliablePipeline;
        
        /// <summary>
        /// Pipeline used as an empty pipeline
        /// </summary>
        private NetworkPipeline _emptyPipeline;
        
        /// <summary>
        /// List of network IDs assigned to players
        /// </summary>
        private NativeList<int> _mNetworkIDs;

        /// <summary>
        /// List of player inputs for each tick
        /// </summary>
        private Dictionary<ulong, NativeList<RpcBroadcastPlayerTickDataToServer>> _everyTickInputBuffer; // we are storing inputs for each tick but in reality we only need to store previousConfirmed, previousPredicted, currentPredicted and currentConfirmed (interpolation purposes)

        private NativeList<RpcBroadcastTickDataToClients> _serverDataToClients;
        
        /// <summary>
        /// List of hashes for each tick
        /// </summary>
        private Dictionary<ulong, NativeList<NativeList<ulong>>> _everyTickHashBuffer; // here if we will point to indeterminism system we also don't need to store all of them (needs to be confirmed)

        private NativeList<RpcGameEnded> endGameHashes;

        /// <summary>
        /// Array of all possible connection slots in the game and players that are already connected
        /// </summary>
        private NativeArray<NetworkConnection> _connectedPlayers; 
        private NativeArray<ulong> playersReady; //hash for each ready player
        private NativeArray<double> averagePingPerPlayer;
        private NativeArray<NativeList<double>> playersPings;
        
        /// <summary>
        /// Specifies the last tick received
        /// </summary>
        private int _lastTickReceivedFromClient;

        private bool _allPingsReceived;
        private bool desynchronized;

        protected override void OnCreate()
        {
            RequireForUpdate<DeterministicSettings>();
            EntityManager.CreateSingleton(new DeterministicServerComponent()
            {
                deterministicServerWorkingMode = DeterministicServerWorkingMode.None
            });
            _allPingsReceived = false;
            desynchronized = false;
        }

        protected override void OnUpdate()
        {
            if (SystemAPI.GetSingleton<DeterministicServerComponent>().deterministicServerWorkingMode == DeterministicServerWorkingMode.ListenForConnections && !_mDriver.IsCreated)
            {
                StartListening();
            }
            
            if(!_mDriver.IsCreated) return;

            if (SystemAPI.GetSingleton<DeterministicServerComponent>().deterministicServerWorkingMode ==
                DeterministicServerWorkingMode.RunDeterministicSimulation &&
                !SystemAPI.GetSingleton<DeterministicSettings>().isInGame)
            {
                // SendPingMessage();
                StartGame();
            }
            
            if (SystemAPI.GetSingleton<DeterministicServerComponent>().deterministicServerWorkingMode == DeterministicServerWorkingMode.Disconnect)
            {
                Disconnect();
            }
            
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
                            Debug.Log("Client disconnected from the server: " + i);
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
        
        // private void SendPingMessage()
        // {
        //     var rpc = new RpcPingPong
        //     {
        //         ServerTimeStampUTCtoday = DateTime.UtcNow.TimeOfDay
        //     };
        //     
        //     for (int i = 0; i < _connectedPlayers.Length; i++)
        //     {
        //         if (_connectedPlayers[i].IsCreated)
        //         {
        //             rpc.ClientNetworkID = i;
        //             rpc.Serialize(_mDriver, _connectedPlayers[i], _emptyPipeline);
        //         }
        //     }
        // }
        
        private void Disconnect()
        {
            _mDriver.ScheduleUpdate().Complete();

            for (int i = 0; i < _connectedPlayers.Length; i++)
            {
                if (_connectedPlayers[i].IsCreated)
                {
                    _connectedPlayers[i].Disconnect(_mDriver);
                    _connectedPlayers[i] = default(NetworkConnection);
                }
            }
        }
        
        protected override void OnDestroy()
        {
            if (!_mDriver.IsCreated) return;
            _mDriver.Dispose();
            _connectedPlayers.Dispose();
            _mNetworkIDs.Dispose();
            playersReady.Dispose();
            playersPings.Dispose();
            averagePingPerPlayer.Dispose();
            _everyTickInputBuffer.Clear();
            _serverDataToClients.Dispose();
            _everyTickHashBuffer.Clear();
            endGameHashes.Dispose();
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
            playersReady = new NativeArray<ulong>(SystemAPI.GetSingleton<DeterministicSettings>().allowedConnectionsPerGame, Allocator.Persistent);
            for (int i = 0; i < playersReady.Length; i++)
            {
                playersReady[i] = 1;
            }
            playersPings = new NativeArray<NativeList<double>>(SystemAPI.GetSingleton<DeterministicSettings>().allowedConnectionsPerGame, Allocator.Persistent);
            averagePingPerPlayer = new NativeArray<double>(SystemAPI.GetSingleton<DeterministicSettings>().allowedConnectionsPerGame, Allocator.Persistent);
            _mNetworkIDs = new NativeList<int>(Allocator.Persistent);

            _everyTickInputBuffer = new Dictionary<ulong, NativeList<RpcBroadcastPlayerTickDataToServer>>();
            _serverDataToClients = new NativeList<RpcBroadcastTickDataToClients>(Allocator.Persistent);
            _everyTickHashBuffer = new Dictionary<ulong, NativeList<NativeList<ulong>>>();
            endGameHashes = new NativeList<RpcGameEnded>(Allocator.Persistent);
            
            _mDriver = NetworkDriver.Create();
            _reliablePipeline =
                _mDriver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            _emptyPipeline = _mDriver.CreatePipeline(typeof(NullPipelineStage));
            
            var endpoint = NetworkEndpoint.AnyIpv4.WithPort((ushort) SystemAPI.GetSingleton<DeterministicSettings>()._serverPort);

            if (_mDriver.Bind(endpoint) != 0)
            {
                Debug.LogError("Failed to bind to port: " + SystemAPI.GetSingleton<DeterministicSettings>()._serverPort);
                return;
            }

            _mDriver.Listen();
        }

        /// <summary>
        /// Function used to start the game and send RPC to clients to start the game. From this point no connection will be accepted.
        /// </summary>
        private void StartGame()
        {
            if (SystemAPI.GetSingleton<DeterministicSettings>().isInGame) return;
            
            var settings = SystemAPI.GetSingletonRW<DeterministicSettings>();
            settings.ValueRW.isInGame = true;
            
            CollectInitialPlayerData();
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
                case RpcID.PlayerReady:
                    var clientReadyRPC = new RpcPlayerReady();
                    clientReadyRPC.Deserialize(ref stream);
                    CheckIfAllClientsReady(clientReadyRPC);
                    break;
                case RpcID.GameEnded:
                    var gameEndedRPC = new RpcGameEnded();
                    gameEndedRPC.Deserialize(ref stream);
                    CheckEndGameHashes(gameEndedRPC);
                    break;
                // case RpcID.PingPong:
                //     var pingPongRPC = new RpcPingPong();
                //     pingPongRPC.Deserialize(ref stream);
                //     AddClientPing(pingPongRPC);
                //     break;
                // case RpcID.PlayerConfiguration:
                //     var playerConfigRPC = new RpcPlayerConfiguration();
                //     playerConfigRPC.Deserialize(ref stream);
                //     _clientDeterministicSystems.Dispose();
                //     _clientDeterministicSystems = new NativeList<FixedString64Bytes>(playerConfigRPC.DeterministicSystemNamesDebug.Length, Allocator.Persistent);
                //     foreach (var systemName in playerConfigRPC.DeterministicSystemNamesDebug)
                //     {
                //         _clientDeterministicSystems.Add(systemName);
                //     }
                //     break;
                default:
                    Debug.LogError("Received RPC ID not proceeded by the server: " + id);
                    break;
            }
        }

        // private void AddClientPing(RpcPingPong rpc)
        // {
        //     if (!playersPings[rpc.ClientNetworkID].IsCreated)
        //     {
        //         playersPings[rpc.ClientNetworkID] = new NativeList<double>( Allocator.Persistent);
        //     }
        //     
        //     if(playersPings[rpc.ClientNetworkID].Length < AmountOfPlayersPingMessages)
        //     {
        //         var updatedOldServerTime = rpc.ServerTimeStampUTCtoday.TotalMilliseconds + SystemAPI.Time.DeltaTime *1000/2;
        //         var pingValue = 0d;
        //         if(DateTime.UtcNow.TimeOfDay.TotalMilliseconds - updatedOldServerTime < 0)
        //         {
        //             pingValue = 0d;
        //         }
        //         else
        //         {
        //             pingValue = DateTime.UtcNow.TimeOfDay.TotalMilliseconds - updatedOldServerTime;
        //         }
        //         
        //         
        //         playersPings[rpc.ClientNetworkID].Add(pingValue);
        //     }
        //
        //     CheckIfAllClientsPingsReceived(rpc);
        // }

        // private void CheckIfAllClientsPingsReceived(RpcPingPong rpc)
        // {
        //     if(_allPingsReceived) return;
        //     // Debug.Log("Checking --> PlayersPingListLength: " + playersPings.Length + " ClientToCheck:  " + rpc.ClientNetworkID + " How many pings: " + playersPings[rpc.ClientNetworkID].Length);
        //
        //     foreach (var player in playersPings)
        //     {
        //         if (player.IsCreated && player.Length < AmountOfPlayersPingMessages)
        //         {
        //             return;
        //         }
        //     }
        //     
        //     for(var i=0; i<playersPings.Length; i++)
        //     {
        //         if (!playersPings[i].IsCreated)
        //         {
        //             averagePingPerPlayer[i] = -1d;
        //         }
        //         else
        //         {
        //             var totalPing = 0d;
        //             for (var j = 0; j < playersPings[i].Length; j++)
        //             {
        //                 totalPing += playersPings[i][j];
        //             }
        //             averagePingPerPlayer[i] = totalPing / playersPings[i].Length;
        //         }
        //     }
        //
        //     // Debug.Log("All pings received.");
        //     _allPingsReceived = true;
        //     // for (int i = 0; i < averagePingPerPlayer.Length; i++)
        //     // {
        //     //     // Debug.Log("AveragePing of player " + i + " equals to " + averagePingPerPlayer[i]);
        //     // }
        //     StartGame();
        // }
        
        private void CheckIfAllClientsReady(RpcPlayerReady rpc)
        {
            // mark that this specific client is ready
            playersReady[rpc.PlayerNetworkID] = rpc.StartingHash;
            
            // check if all clients are ready
            var hostHash = playersReady[0];
            var desynchronized = false;
            if (hostHash == 1) return;
            
            for (int i = 0; i < playersReady.Length; i++)
            {
                // Debug.Log("Hash: " + i + " = " + playersReady[i]);
                if (playersReady[i] == 1 && _connectedPlayers[i].IsCreated)
                {
                    return;
                }
                if(playersReady[i] != hostHash && _connectedPlayers[i].IsCreated)
                {
                    desynchronized = true;
                }
            }

            if (desynchronized)
            {
                SendRPCWithPlayersDesynchronizationInfo();
            }
            else
            {
                SendRPCtoStartGame();
            }
            // SendRPCtoStartGame();
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
        
        private void SendRPCToLoadGame()
        {
            var rpc = new RpcLoadGame();
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
                if (_connectedPlayers[i].IsCreated)
                {
                    rpc.PlayerNetworkID = i;
                    rpc.PlayersNetworkIDs = playersIDs;
                    rpc.Serialize(_mDriver, _connectedPlayers[i], _emptyPipeline);
                }
            }
            playersIDs.Dispose();
        }

        /// <summary>
        /// Function used to send RPC to clients to start the game.
        /// </summary>
        private void SendRPCtoStartGame()
        {
            // register OnGameStart method where they can be used to spawn entities, separation between user code and package code
            var rng = new Random();
            // var serverTime = DateTime.UtcNow.TimeOfDay;
            RpcStartDeterministicSimulation rpc = new RpcStartDeterministicSimulation
            {
                // ServerTimestampUTC = serverTime,
                // PostponedStartInMiliseconds =  1000,
                PlayersNetworkIDs = _mNetworkIDs,
                TickRate = SystemAPI.GetSingleton<DeterministicSettings>().simulationTickRate,
                TicksOfForcedInputLatency = SystemAPI.GetSingleton<DeterministicSettings>().ticksAhead,
                SeedForPlayerRandomActions = (uint)rng.Next(1, int.MaxValue),
                DeterminismHashCalculationOption = (int) SystemAPI.GetSingleton<DeterministicSettings>().hashCalculationOption
            };
            
            for (ushort i = 0; i < _connectedPlayers.Length; i++)
            {
                if (_connectedPlayers[i].IsCreated)
                {
                    rpc.ThisConnectionNetworkID = i;
                    // rpc.PlayerAveragePing = averagePingPerPlayer[i];
                    rpc.Serialize(_mDriver, _connectedPlayers[i], _reliablePipeline);
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
            // Clone networkIDs
            NativeList<int> clonedNetworkIDs = new NativeList<int>(Allocator.Persistent);
            foreach (var id in networkIDs)
            {
                clonedNetworkIDs.Add(id);
            }

            // Clone playerInputs
            NativeList<PongInputs> clonedPlayerInputs = new NativeList<PongInputs>(Allocator.Persistent);
            foreach (var input in playerInputs)
            {
                clonedPlayerInputs.Add(input); // Ensure PongInputs is a struct for this to correctly copy by value
            }
            
            var rpc = new RpcBroadcastTickDataToClients
            {
                NetworkIDs = clonedNetworkIDs,
                PlayersPongGameInputs = clonedPlayerInputs,
                SimulationTick = _lastTickReceivedFromClient
            };
            _serverDataToClients.Add(rpc);
            
            
            foreach (var connectedPlayer in _connectedPlayers.Where(connectedPlayer => connectedPlayer.IsCreated))
            {
                rpc.Serialize(_mDriver, connectedPlayer, _reliablePipeline);
            }
        }

        /// <summary>
        /// Function used to send RPC to clients with information about desynchronization.
        /// </summary>
        private void SendRPCWithPlayersDesynchronizationInfo(NativeList<int> networkIDs, NativeList<PongInputs> playerInputs)
        {
            var rpcToLog = new RpcBroadcastTickDataToClients
            {
                NetworkIDs = networkIDs,
                PlayersPongGameInputs = playerInputs,
                SimulationTick = _lastTickReceivedFromClient
            };
            _serverDataToClients.Add(rpcToLog);
            
            Debug.LogError("Desynchronized");
            var rpc = new RpcPlayerDesynchronizationMessage { NonDeterministicTick = (ulong) _lastTickReceivedFromClient};

            foreach (var connection in _connectedPlayers.Where(connection => connection.IsCreated))
            {
                rpc.Serialize(_mDriver, connection, _emptyPipeline);
            }
            DeterministicLogger.Instance.LogInputsToFile(_serverDataToClients);
        }
        
        private void SendRPCWithPlayersDesynchronizationInfo()
        {
            Debug.LogError("Desynchronized");
            var rpc = new RpcPlayerDesynchronizationMessage { NonDeterministicTick = (ulong) _lastTickReceivedFromClient};

            foreach (var connection in _connectedPlayers.Where(connection => connection.IsCreated))
            {
                rpc.Serialize(_mDriver, connection, _emptyPipeline);
            }
            DeterministicLogger.Instance.LogInputsToFile(_serverDataToClients);
        }

        /// <summary>
        /// Function used to save player inputs to the buffer when those arrive.
        /// </summary>
        /// <param name="rpc">RPC that arrived</param>
        /// <param name="connection">Connection from which it arrived</param>
        private void 
            SaveTheData(RpcBroadcastPlayerTickDataToServer rpc, NetworkConnection connection)
        {
            string playerArrivingHashes = "";

            for (var i = 0;
                 i < rpc.HashesForFutureTick.Length;
                 i++)
            {
                    // Append the current hash to the allHashes string
                    playerArrivingHashes +=
                        rpc.HashesForFutureTick[i] +
                        ", ";
                
            }

            // Debug.Log("Data received: " + " Player network id --> " + rpc.PlayerNetworkID + " tick to apply --> " +
            //           rpc.FutureTick +
            //           " hash to apply --> " + playerArrivingHashes + " inputs to apply --> " +
            //           rpc.PongGameInputs.verticalInput + " client time when sending: " + rpc.ClientTimeStampUTC);
            for (var i = 0; i < _connectedPlayers.Length; i++)
            {
                if (!_connectedPlayers[i].Equals(connection)) continue;
            
                if (!_everyTickInputBuffer.ContainsKey((ulong) rpc.FutureTick))
                {
                    _everyTickInputBuffer[(ulong) rpc.FutureTick] = new NativeList<RpcBroadcastPlayerTickDataToServer>(Allocator.Persistent);
                }
            
                if (!_everyTickHashBuffer.ContainsKey((ulong) rpc.FutureTick))
                {
                    _everyTickHashBuffer[(ulong) rpc.FutureTick] = new NativeList<NativeList<ulong>>(Allocator.Persistent);
                }
            
                // This tick already exists in the buffer. Check if the player already has inputs saved for this tick. No need to check for hash in that case because those should be send together and hash can be the same (if everything is correct) so we will get for example 3 same hashes
                foreach (var oldInputData in _everyTickInputBuffer[(ulong) rpc.FutureTick])
                {
                    if (oldInputData.PlayerNetworkID == i)
                    {
                        // Debug.LogError("Already received input from network ID " + i + " for tick " + rpc.FutureTick);
                        return; // Stop executing the function here, since we don't want to add the new inputData
                    }
                }
            
                _everyTickInputBuffer[(ulong) rpc.FutureTick].Add(rpc);
                _everyTickHashBuffer[(ulong) rpc.FutureTick].Add(rpc.HashesForFutureTick);
                _lastTickReceivedFromClient = rpc.FutureTick;
            }
        }

        /// <summary>
        /// Function used to get the number of active connections.
        /// </summary>
        /// <returns>Amount of active connections</returns>
        private int GetActiveConnectionCount()
        {
            // Debug.Log(_connectedPlayers.Count(connectedPlayer => connectedPlayer.IsCreated));
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
            if (desynchronized)
            {
                return;
            }
            
            if (_everyTickInputBuffer[(ulong) _lastTickReceivedFromClient].Length == GetActiveConnectionCount() &&
                _everyTickHashBuffer[(ulong) _lastTickReceivedFromClient].Length ==
                GetActiveConnectionCount()) // because of different order that we can received those inputs we are checking for last received input
            {
                // We've received a full set of data for this tick, so process it
                var networkIDs = new NativeList<int>(Allocator.Temp);
                var inputs = new NativeList<PongInputs>(Allocator.Temp);
            
                foreach (var inputData in _everyTickInputBuffer[(ulong) _lastTickReceivedFromClient])
                {
                    if(_connectedPlayers[inputData.PlayerNetworkID].IsCreated)
                    {
                        networkIDs.Add(inputData.PlayerNetworkID);
                        inputs.Add(inputData.PongGameInputs);
                    }
                }
                
                // Get the number of hashes (assuming all players have the same number of hashes)
                var numHashesPerPlayer = _everyTickHashBuffer[(ulong)_lastTickReceivedFromClient][0].Length;

                // Iterate over each hash index
                for (var systemHash = 0; systemHash < numHashesPerPlayer; systemHash++)
                {
                    // Get the first player's hash at this index
                    var firstPlayerHash = _everyTickHashBuffer[(ulong)_lastTickReceivedFromClient][0][systemHash];

                    // Iterate over each player's hashes at this index
                    for (var player = 1; player < _everyTickHashBuffer[(ulong)_lastTickReceivedFromClient].Length; player++)
                    {
                        var currentPlayerHash = _everyTickHashBuffer[(ulong)_lastTickReceivedFromClient][player][systemHash];

                        // If the hashes are not equal, log an error and set desynchronized to true
                        if (firstPlayerHash != currentPlayerHash)
                        {
                            string allHashes = "";

                            for (var i = 0;
                                 i < _everyTickHashBuffer[(ulong)_lastTickReceivedFromClient].Length;
                                 i++)
                            {
                                // Iterate over each hash for the current player
                                for (var hashIndex = 0;
                                     hashIndex < _everyTickHashBuffer[(ulong)_lastTickReceivedFromClient][i]
                                         .Length;
                                     hashIndex++)
                                {
                                    // Append the current hash to the allHashes string
                                    allHashes +=
                                        _everyTickHashBuffer[(ulong)_lastTickReceivedFromClient][i][hashIndex] +
                                        ", ";
                                }
                            }
                            //
                            Debug.LogError("DESYNCHRONIZATION HAPPENED! HASHES ARE NOT EQUAL! " + "Ticks: " +
                                           _lastTickReceivedFromClient + " Hashes: " + firstPlayerHash + " and " +
                                           currentPlayerHash + " System number: " + systemHash + ". All hashes: " + allHashes);
                            desynchronized = true;
                            break;
                        }
                    }

                    // If a desynchronization was found, break out of the loop
                    if (desynchronized)
                    {
                        break;
                    }
                }
                
                if (!desynchronized)
                {
                    string allHashes = "";

                    for (var i = 0;
                         i < _everyTickHashBuffer[(ulong)_lastTickReceivedFromClient].Length;
                         i++)
                    {
                        // Iterate over each hash for the current player
                        for (var hashIndex = 0;
                             hashIndex < _everyTickHashBuffer[(ulong)_lastTickReceivedFromClient][i]
                                 .Length;
                             hashIndex++)
                        {
                            // Append the current hash to the allHashes string
                            allHashes +=
                                _everyTickHashBuffer[(ulong)_lastTickReceivedFromClient][i][hashIndex] +
                                ", ";
                        }
                    }
                    
                    // Debug.Log("All hashes are equal: " + allHashes + ". Number of players: " +
                    //           _everyTickHashBuffer[(ulong) _lastTickReceivedFromClient].Length + ". Tick: " + _lastTickReceivedFromClient + " Number of hashes per player: " + _everyTickHashBuffer[(ulong)_lastTickReceivedFromClient][0].Length);
            
                    // Send the RPC to all connections
                    SendRPCWithPlayersInputUpdate(networkIDs, inputs);
                }
                else
                {
                    SendRPCWithPlayersDesynchronizationInfo(networkIDs, inputs);
                }
            
                networkIDs.Dispose();
                inputs.Dispose();
            
                // Remove this tick from the buffer, since we're done processing it
                // _everyTickInputBuffer.Remove((ulong) _lastTickReceivedFromClient);
                _everyTickHashBuffer.Remove((ulong) _lastTickReceivedFromClient);
                _lastTickReceivedFromClient++;
            }
            else if (_everyTickInputBuffer[(ulong) _lastTickReceivedFromClient].Length > GetActiveConnectionCount())
            {
                Debug.LogError("Too many player inputs saved in one tick");
            }
        }
        
        private void CheckEndGameHashes(RpcGameEnded rpc)
        {
            // add new rpc
            Debug.Log("Player: " + rpc.PlayerNetworkID + " ended the game with hash: " + rpc.HashForGameEnd);
            for (var i = 0; i < endGameHashes.Length; i++)
            {
                if (rpc.PlayerNetworkID == endGameHashes[i].PlayerNetworkID) return;
            }
            endGameHashes.Add(rpc);
            
            // check if all received
            if (endGameHashes.Length == GetActiveConnectionCount())
            {
                // // desync or disconnect
                var desynchronized = false;
                var hostHash = endGameHashes[0].HashForGameEnd;
                for (var i = 1; i < endGameHashes.Length; i++)
                {
                    if(hostHash != endGameHashes[i].HashForGameEnd)
                    {
                        desynchronized = true;
                        break;
                    }
                }
                
                if (desynchronized)
                {
                    Debug.LogError("Desynchronized on game end");
                    SendRPCWithPlayersDesynchronizationInfo();
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