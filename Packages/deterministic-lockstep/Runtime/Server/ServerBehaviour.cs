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
    [UpdateBefore(typeof(DeterministicSimulationSystemGroup))]
    public partial class ServerBehaviour : SystemBase
    {
        /// <summary>
        /// Network driver used to handle connections
        /// </summary>
        private NetworkDriver networkDriver;
        
        /// <summary>
        /// Pipeline used to handle reliable and sequenced messages
        /// </summary>
        private NetworkPipeline reliablePipeline;
        
        /// <summary>
        /// List of network IDs assigned to players
        /// </summary>
        private NativeList<int> clientsNetworkIDs;

        /// <summary>
        /// List of player inputs for each tick
        /// </summary>
        private Dictionary<ulong, NativeList<RpcBroadcastPlayerTickDataToServer>> bufferOfClientInputsForEachTick;

        /// <summary>
        /// NativeList of combined RPC send from every client with their input.
        /// This list is subsequently sent to every client.
        /// </summary>
        private NativeList<RpcBroadcastTickDataToClients> dataToSendToEveryClientWithEveryClientInputs;
        
        /// <summary>
        /// List of hashes from every client for each tick.
        /// It may be empty if game is using no hash calculation.
        /// It may contain one hash per tick per client if game is using per-tick hash calculation.
        /// It may contain multiple hashes per tick per client if game is using per-system hash calculation.
        /// </summary>
        private Dictionary<ulong, NativeList<NativeList<ulong>>> hashBufferForEveryTick;

        /// <summary>
        /// NativeList of final hashes for each client.
        /// Used to compare if all clients ended the game with the same state.
        /// </summary>
        private NativeList<RpcEndGameHash> endGameHashes;

        /// <summary>
        /// Array of all possible connection slots in the game containing clients that are already connected
        /// </summary>
        private NativeArray<NetworkConnection> connectedPlayers; 
        
        /// <summary>
        /// NativeArray containing starting game state hashes for each connected client.
        /// </summary>
        private NativeArray<ulong> clientsReady;
        
        /// <summary>
        /// Specifies the last tick received from all clients.
        /// To increase this value inputs from all clients need to arrive to the server.
        /// </summary>
        private int _lastTickReceivedFromClient;

        /// <summary>
        /// Bool value signaling that nondeterminism was detected.
        /// </summary>
        private bool nondeterminismDetected;

        protected override void OnCreate()
        {
            RequireForUpdate<DeterministicSettings>();
            EntityManager.CreateSingleton(new DeterministicServerComponent()
            {
                deterministicServerWorkingMode = DeterministicServerWorkingMode.None
            });
            nondeterminismDetected = false;
        }

        protected override void OnUpdate()
        {
            if (SystemAPI.GetSingleton<DeterministicServerComponent>().deterministicServerWorkingMode == DeterministicServerWorkingMode.ListenForConnections && !networkDriver.IsCreated)
            {
                StartListening();
            }
            
            if(!networkDriver.IsCreated) return;

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
            
            networkDriver.ScheduleUpdate().Complete();
            
            if (!SystemAPI.GetSingleton<DeterministicSettings>().isInGame)
            {
                AcceptAndHandleConnections();
            }

            for (var i = 0; i < connectedPlayers.Length; i++)
            {
                if (!connectedPlayers[i].IsCreated) continue;
                NetworkEvent.Type cmd;
                while ((cmd = networkDriver.PopEventForConnection(connectedPlayers[i], out var stream)) !=
                       NetworkEvent.Type.Empty)
                {
                    switch (cmd)
                    {
                        case NetworkEvent.Type.Data:
                            HandleRpc(stream, connectedPlayers[i]);
                            break;
                        case NetworkEvent.Type.Disconnect:
                            Debug.Log("Client disconnected from the server: " + i);
                            connectedPlayers[i] = default;
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
            networkDriver.ScheduleUpdate().Complete();

            for (int i = 0; i < connectedPlayers.Length; i++)
            {
                if (connectedPlayers[i].IsCreated)
                {
                    connectedPlayers[i].Disconnect(networkDriver);
                    connectedPlayers[i] = default;
                }
            }
        }
        
        protected override void OnDestroy()
        {
            if (!networkDriver.IsCreated) return;
            networkDriver.Dispose();
            connectedPlayers.Dispose();
            clientsNetworkIDs.Dispose();
            clientsReady.Dispose();
            bufferOfClientInputsForEachTick.Clear();
            dataToSendToEveryClientWithEveryClientInputs.Dispose();
            hashBufferForEveryTick.Clear();
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
            connectedPlayers = new NativeArray<NetworkConnection>(SystemAPI.GetSingleton<DeterministicSettings>().allowedConnectionsPerGame, Allocator.Persistent);
            clientsReady = new NativeArray<ulong>(SystemAPI.GetSingleton<DeterministicSettings>().allowedConnectionsPerGame, Allocator.Persistent);
            for (int i = 0; i < clientsReady.Length; i++)
            {
                clientsReady[i] = 1;
            }
            clientsNetworkIDs = new NativeList<int>(Allocator.Persistent);

            bufferOfClientInputsForEachTick = new Dictionary<ulong, NativeList<RpcBroadcastPlayerTickDataToServer>>();
            dataToSendToEveryClientWithEveryClientInputs = new NativeList<RpcBroadcastTickDataToClients>(Allocator.Persistent);
            hashBufferForEveryTick = new Dictionary<ulong, NativeList<NativeList<ulong>>>();
            endGameHashes = new NativeList<RpcEndGameHash>(Allocator.Persistent);
            
            networkDriver = NetworkDriver.Create();
            reliablePipeline =
                networkDriver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            
            var endpoint = NetworkEndpoint.AnyIpv4.WithPort((ushort) SystemAPI.GetSingleton<DeterministicSettings>()._serverPort);

            if (networkDriver.Bind(endpoint) != 0)
            {
                Debug.LogError("Failed to bind to port: " + SystemAPI.GetSingleton<DeterministicSettings>()._serverPort);
                return;
            }

            networkDriver.Listen();
        }

        /// <summary>
        /// Function used to start the game and send RPC to clients to start the game.
        /// After this function executes no connection will be accepted.
        /// </summary>
        private void StartGame()
        {
            if (SystemAPI.GetSingleton<DeterministicSettings>().isInGame) return;
            
            var settings = SystemAPI.GetSingletonRW<DeterministicSettings>();
            settings.ValueRW.isInGame = true;
            
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
                    var gameEndedRPC = new RpcEndGameHash();
                    gameEndedRPC.Deserialize(ref stream);
                    CheckEndGameHashes(gameEndedRPC);
                    break;
                default:
                    Debug.LogError("Received RPC ID not proceeded by the server: " + id);
                    break;
            }
        }
        
        /// <summary>
        /// Function used to check if all clients are ready to start the game.
        /// </summary>
        /// <param name="rpc">Last received rpc with client readiness message</param>
        private void CheckIfAllClientsReady(RpcPlayerReady rpc)
        {
            // mark that this specific client is ready
            clientsReady[rpc.ClientNetworkID] = rpc.StartingHash;
            
            // check if all clients are ready
            var hostHash = clientsReady[0];
            var desynchronized = false;
            if (hostHash == 1) return;
            
            for (int i = 0; i < clientsReady.Length; i++)
            {
                if (clientsReady[i] == 1 && connectedPlayers[i].IsCreated)
                {
                    return;
                }
                if(clientsReady[i] != hostHash && connectedPlayers[i].IsCreated)
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
            clientsNetworkIDs.Clear();
            
            for (ushort i = 0; i < connectedPlayers.Length; i++)
            {
                if (connectedPlayers[i].IsCreated)
                {
                    clientsNetworkIDs.Add(i); 
                }
            }
            
            
            var rpc = new RpcLoadGame();
            var playersIDs = new NativeList<int>(Allocator.Temp);
            for (ushort i = 0; i < connectedPlayers.Length; i++)
            {
                if (connectedPlayers[i].IsCreated)
                {
                    playersIDs.Add(i);
                }
            }
            
            for (ushort i = 0; i < connectedPlayers.Length; i++)
            {
                if (connectedPlayers[i].IsCreated)
                {
                    rpc.ClientNetworkID = i;
                    rpc.NetworkIDsOfAllClients = playersIDs;
                    rpc.Serialize(networkDriver, connectedPlayers[i], reliablePipeline);
                }
            }
            playersIDs.Dispose();
        }

        /// <summary>
        /// Function used to send RPC to clients to start the game.
        /// It contains all necessary information to start the game together with settings.
        /// </summary>
        private void SendRPCtoStartGame()
        {
            var rng = new Random();
            RpcStartDeterministicSimulation rpc = new RpcStartDeterministicSimulation
            {
                NetworkIDsOfAllClients = clientsNetworkIDs,
                GameIntendedTickRate = SystemAPI.GetSingleton<DeterministicSettings>().simulationTickRate,
                TicksOfForcedInputLatency = SystemAPI.GetSingleton<DeterministicSettings>().ticksOfForcedInputLatency,
                SeedForPlayerRandomActions = (uint)rng.Next(1, int.MaxValue),
                DeterminismHashCalculationOption = (int) SystemAPI.GetSingleton<DeterministicSettings>().hashCalculationOption
            };
            
            for (ushort i = 0; i < connectedPlayers.Length; i++)
            {
                if (connectedPlayers[i].IsCreated)
                {
                    rpc.ClientAssignedNetworkID = i;
                    rpc.Serialize(networkDriver, connectedPlayers[i], reliablePipeline);
                }
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
            NativeList<int> clonedNetworkIDs = new NativeList<int>(Allocator.TempJob);
            foreach (var id in networkIDs)
            {
                clonedNetworkIDs.Add(id);
            }
            
            NativeList<PongInputs> clonedPlayerInputs = new NativeList<PongInputs>(Allocator.TempJob);
            foreach (var input in playerInputs)
            {
                clonedPlayerInputs.Add(input);
            }
            
            var rpc = new RpcBroadcastTickDataToClients
            {
                NetworkIDsOfAllClients = clonedNetworkIDs,
                GameInputsFromAllClients = clonedPlayerInputs,
                SimulationTick = _lastTickReceivedFromClient
            };
            dataToSendToEveryClientWithEveryClientInputs.Add(rpc);
            
            
            foreach (var connectedPlayer in connectedPlayers.Where(connectedPlayer => connectedPlayer.IsCreated))
            {
                rpc.Serialize(networkDriver, connectedPlayer, reliablePipeline);
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
            dataToSendToEveryClientWithEveryClientInputs.Add(rpcWithPlayersDataToStore);
            
            var rpcWithPlayerDesynchronizationSignal = new RpcPlayerDesynchronization { NonDeterministicTick = (ulong) _lastTickReceivedFromClient};

            foreach (var connection in connectedPlayers.Where(connection => connection.IsCreated))
            {
                rpcWithPlayerDesynchronizationSignal.Serialize(networkDriver, connection, reliablePipeline);
            }
            DeterministicLogger.Instance.LogServerInputRecordingToTheFile(dataToSendToEveryClientWithEveryClientInputs);
        }
        
        /// <summary>
        /// Function used to send RPC to clients informing them that nondeterminism was detected and the game execution should be stopped.
        /// This version is used when confirming starting state and end state of the game when no inputs are needed to be saved.
        /// </summary>
        private void SendRPCSignallingNondeterminismDetection()
        {
            var rpcWithPlayerDesynchronizationSignal = new RpcPlayerDesynchronization { NonDeterministicTick = (ulong) _lastTickReceivedFromClient};

            foreach (var connection in connectedPlayers.Where(connection => connection.IsCreated))
            {
                rpcWithPlayerDesynchronizationSignal.Serialize(networkDriver, connection, reliablePipeline);
            }
            DeterministicLogger.Instance.LogServerInputRecordingToTheFile(dataToSendToEveryClientWithEveryClientInputs);
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
            string playerArrivingHashes = "";

            for (var i = 0;
                 i < rpc.HashesForTheTick.Length;
                 i++)
            {
                    // Append the current hash to the allHashes string
                    playerArrivingHashes +=
                        rpc.HashesForTheTick[i] +
                        ", ";
                
            }
            
            for (var i = 0; i < connectedPlayers.Length; i++)
            {
                if (!connectedPlayers[i].Equals(connection)) continue;
            
                if (!bufferOfClientInputsForEachTick.ContainsKey((ulong) rpc.TickToApplyInputsOn))
                {
                    bufferOfClientInputsForEachTick[(ulong) rpc.TickToApplyInputsOn] = new NativeList<RpcBroadcastPlayerTickDataToServer>(Allocator.Persistent);
                }
                
                if (!hashBufferForEveryTick.ContainsKey((ulong) rpc.TickToApplyInputsOn))
                {
                    hashBufferForEveryTick[(ulong) rpc.TickToApplyInputsOn] = new NativeList<NativeList<ulong>>(Allocator.Persistent);
                }
            
                // This tick already exists in the buffer. Check if the player already has inputs saved for this tick. No need to check for hash in that case because those should be send together and hash can be the same (if everything is correct) so we will get for example 3 same hashes
                foreach (var oldInputData in bufferOfClientInputsForEachTick[(ulong) rpc.TickToApplyInputsOn])
                {
                    if (oldInputData.ClientNetworkID == i)
                    {
                        return;
                    }
                }
            
                bufferOfClientInputsForEachTick[(ulong) rpc.TickToApplyInputsOn].Add(rpc);
                hashBufferForEveryTick[(ulong) rpc.TickToApplyInputsOn].Add(rpc.HashesForTheTick);
                _lastTickReceivedFromClient = rpc.TickToApplyInputsOn;
            }
        }

        /// <summary>
        /// Function used to get the number of active connections.
        /// </summary>
        /// <returns>Amount of active connections</returns>
        private int GetActiveConnectionCount()
        {
            return connectedPlayers.Count(connectedPlayer => connectedPlayer.IsCreated);
            
        }
        
        /// <summary>
        /// Function used to accept new connections and assign them to the first available slot in the connectedPlayers array.
        /// If there are no available slots, the connection is disconnected.
        /// </summary>
        private void AcceptAndHandleConnections()
        {
            NetworkConnection connection;
            while ((connection = networkDriver.Accept()) != default)
            {
                var index = FindFreePlayerSlot();
                if (index != -1)
                {
                    connectedPlayers[index] = connection;
                    Debug.Log("Accepted a connection with network ID: " + index);
                }
                else
                {
                    Debug.LogWarning("Cannot accept more connections. Server is full.");
                    connection.Disconnect(networkDriver);
                }
            }
        }

        /// <summary>
        /// Function used to find the first free slot in the connectedPlayers array.
        /// </summary>
        /// <returns>Empty slot number or -1 otherwise</returns>
        private int FindFreePlayerSlot()
        {
            for (var i = 0; i < connectedPlayers.Length; i++)
            {
                if (!connectedPlayers[i].IsCreated)
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
            if (nondeterminismDetected)
            {
                return;
            }
            
            if (bufferOfClientInputsForEachTick[(ulong) _lastTickReceivedFromClient].Length == GetActiveConnectionCount() &&
                hashBufferForEveryTick[(ulong) _lastTickReceivedFromClient].Length ==
                GetActiveConnectionCount())
            {
                var networkIDs = new NativeList<int>(Allocator.Temp);
                var inputs = new NativeList<PongInputs>(Allocator.Temp);
            
                foreach (var inputData in bufferOfClientInputsForEachTick[(ulong) _lastTickReceivedFromClient])
                {
                    if(connectedPlayers[inputData.ClientNetworkID].IsCreated)
                    {
                        networkIDs.Add(inputData.ClientNetworkID);
                        inputs.Add(inputData.PlayerGameInput);
                    }
                }
                
                // Get the number of hashes (assuming all players have the same number of hashes)
                var numHashesPerPlayer = hashBufferForEveryTick[(ulong)_lastTickReceivedFromClient][0].Length;

                // Iterate over each hash index
                for (var systemHash = 0; systemHash < numHashesPerPlayer; systemHash++)
                {
                    // Get the first player's hash at this index
                    var firstPlayerHash = hashBufferForEveryTick[(ulong)_lastTickReceivedFromClient][0][systemHash];

                    // Iterate over each player's hashes at this index
                    for (var player = 1; player < hashBufferForEveryTick[(ulong)_lastTickReceivedFromClient].Length; player++)
                    {
                        var currentPlayerHash = hashBufferForEveryTick[(ulong)_lastTickReceivedFromClient][player][systemHash];

                        // If the hashes are not equal, log an error and set desynchronized to true
                        if (firstPlayerHash != currentPlayerHash)
                        {
                            string allHashes = "";

                            for (var i = 0;
                                 i < hashBufferForEveryTick[(ulong)_lastTickReceivedFromClient].Length;
                                 i++)
                            {
                                // Iterate over each hash for the current player
                                for (var hashIndex = 0;
                                     hashIndex < hashBufferForEveryTick[(ulong)_lastTickReceivedFromClient][i]
                                         .Length;
                                     hashIndex++)
                                {
                                    // Append the current hash to the allHashes string
                                    allHashes +=
                                        hashBufferForEveryTick[(ulong)_lastTickReceivedFromClient][i][hashIndex] +
                                        ", ";
                                }
                            }
                            
                            if (!SystemAPI.GetSingletonRW<DeterministicSettings>().ValueRO.isReplayFromFile)
                            {
                                Debug.LogError("DESYNCHRONIZATION HAPPENED! HASHES ARE NOT EQUAL! " + "Ticks: " +
                                               _lastTickReceivedFromClient + " Hashes: " + firstPlayerHash + " and " +
                                               currentPlayerHash + " System number: " + systemHash + ". All hashes: " + allHashes);
                                nondeterminismDetected = true;
                            }
                            
                            break;
                        }
                    }
                    
                    if (nondeterminismDetected) break;
                }
                
                if (!nondeterminismDetected)
                {
                    string allHashes = "";

                    for (var i = 0;
                         i < hashBufferForEveryTick[(ulong)_lastTickReceivedFromClient].Length;
                         i++)
                    {
                        // Iterate over each hash for the current player
                        for (var hashIndex = 0;
                             hashIndex < hashBufferForEveryTick[(ulong)_lastTickReceivedFromClient][i]
                                 .Length;
                             hashIndex++)
                        {
                            // Append the current hash to the allHashes string
                            allHashes +=
                                hashBufferForEveryTick[(ulong)_lastTickReceivedFromClient][i][hashIndex] +
                                ", ";
                        }
                    }
                    
                    SendRPCWithPlayersInputUpdate(networkIDs, inputs);
                }
                else if(!SystemAPI.GetSingletonRW<DeterministicSettings>().ValueRO.isReplayFromFile)
                {
                    SendRPCSignallingNondeterminismDetection(networkIDs, inputs);
                }
            
                networkIDs.Dispose();
                inputs.Dispose();
                
                hashBufferForEveryTick.Remove((ulong) _lastTickReceivedFromClient);
                _lastTickReceivedFromClient++;
            }
            else if (hashBufferForEveryTick[(ulong) _lastTickReceivedFromClient].Length > GetActiveConnectionCount())
            {
                Debug.LogError("Too many player inputs saved in one tick");
            }
        }
        
        /// <summary>
        /// Function which compares hashes from all clients for the last game frame checking if all clients ended the game with the same state.
        /// </summary>
        /// <param name="rpc"> Last received rpc from client </param>
        private void CheckEndGameHashes(RpcEndGameHash rpc)
        {
            Debug.Log("Player: " + rpc.ClientNetworkID + " ended the game with hash: " + rpc.FinalGameHash);
            for (var i = 0; i < endGameHashes.Length; i++)
            {
                if (rpc.ClientNetworkID == endGameHashes[i].ClientNetworkID) return;
            }
            endGameHashes.Add(rpc);
            
            if (endGameHashes.Length == GetActiveConnectionCount())
            {
                var desynchronized = false;
                var hostHash = endGameHashes[0].FinalGameHash;
                for (var i = 1; i < endGameHashes.Length; i++)
                {
                    if(hostHash != endGameHashes[i].FinalGameHash)
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