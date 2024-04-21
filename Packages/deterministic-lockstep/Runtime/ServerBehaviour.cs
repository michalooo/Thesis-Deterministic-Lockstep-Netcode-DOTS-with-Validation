using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeterministicLockstep
{
    // This is the script responsible for handling the server side of the network, with connections etc

    /// <summary>
    /// Struct used to store player single input data to be send to server. It contains information about player networkID and inputs (currently horizontal+vertical).
    /// </summary>
    public struct PlayerInputData
    {
        public int networkID;
        public Vector2 input;
    }

    /// <summary>
    /// System responsible for handling the server side of the network, with connections etc
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(ConnectionHandleSystemGroup))]
    public partial class ServerBehaviour : SystemBase
    {
        private const ushort NetworkPort = 7979;

        private NetworkDriver _mDriver;
        private NetworkPipeline _reliableSimulatorPipeline;
        private NativeList<int> _mNetworkIDs;

        private Dictionary<int, List<PlayerInputData>> _everyTickInputBuffer;
        private Dictionary<int, List<ulong>> _everyTickHashBuffer;

        private NativeArray<NetworkConnection>
            _connectedPlayers; // This is an array of all possible connection slots in the game and players that are already connected

        private const int TickRate = 30;
        private int _tickAhead;
        private int _lastTickReceived;

        private NativeList<Vector2> _mPlayerInputs;

        // singleton to define (client-server tickRate)
        [Tooltip(
            "The maximum amount of packets the pipeline can keep track of. This used when a packet is delayed, the packet is stored in the pipeline processing buffer and can be later brought back.")]
        int maxPacketCount = 1000;

        [Tooltip(
            "The maximum size of a packet which the simulator stores. If a packet exceeds this size it will bypass the simulator.")]
        int maxPacketSize = NetworkParameterConstants.MaxMessageSize;

        [Tooltip("Whether to apply simulation to received or sent packets (defaults to both).")]
        ApplyMode mode = ApplyMode.AllPackets;

        [Tooltip("Fixed delay in milliseconds to apply to all packets which pass through.")]
        int packetDelayMs = 0;

        [Tooltip(
            "Variance of the delay that gets added to all packets that pass through. For example, setting this value to 5 will result in the delay being a random value within 5 milliseconds of the value set with PacketDelayMs.")]
        int packetJitterMs = 0;

        [Tooltip(
            "Fixed interval to drop packets on. This is most suitable for tests where predictable behaviour is desired, as every X-th packet will be dropped. For example, if the value is 5 every fifth packet is dropped.")]
        int packetDropInterval = 0;

        [Tooltip("Percentage of packets that will be dropped.")]
        int packetDropPercentage = 0;

        [Tooltip(
            "Percentage of packets that will be duplicated. Packets are duplicated at most once and will not be duplicated if they were first deemed to be dropped.")]
        int packetDuplicationPercentage = 0;

        protected override void OnCreate()
        {
            _tickAhead = Mathf.CeilToInt(0.15f * TickRate);

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

            _mDriver = NetworkDriver.Create(settings);
            _reliableSimulatorPipeline =
                _mDriver.CreatePipeline(typeof(ReliableSequencedPipelineStage), typeof(SimulatorPipelineStage));

            _connectedPlayers = new NativeArray<NetworkConnection>(16, Allocator.Persistent);
            _mNetworkIDs = new NativeList<int>(16, Allocator.Persistent);
            _mPlayerInputs = new NativeList<Vector2>(16, Allocator.Persistent);

            _everyTickInputBuffer = new Dictionary<int, List<PlayerInputData>>();
            _everyTickHashBuffer = new Dictionary<int, List<ulong>>();

            var endpoint = NetworkEndpoint.AnyIpv4.WithPort(NetworkPort);

            if (_mDriver.Bind(endpoint) != 0)
            {
                Debug.LogError("Failed to bind to port 7979.");
                return;
            }

            _mDriver.Listen();
        }

        protected override void OnDestroy()
        {
            if (!_mDriver.IsCreated) return;
            _mDriver.Dispose();
            _connectedPlayers.Dispose();
            _mNetworkIDs.Dispose();
            _mPlayerInputs.Dispose();
        }

        /// <summary>
        /// Function used to accept new connections and assign them to the first available slot in the connectedPlayers array. If there are no available slots, the connection is disconnected.
        /// </summary>
        private void AcceptAndHandleConnections()
        {
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

        protected override void OnUpdate()
        {
            _mDriver.ScheduleUpdate().Complete();

            if (SceneManager.GetActiveScene().name != "Game") // We are not accepting new connections while in game
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

            // Start the game
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (SceneManager.GetActiveScene().name == "Loading")
                {
                    CollectInitialPlayerData();
                    SendRPCtoStartGame();
                }
            }
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
            _mPlayerInputs.Clear();

            // Collect data from all players
            for (int i = 0; i < _connectedPlayers.Length; i++)
            {
                // Example: Collect network IDs and positions of players
                if (_connectedPlayers[i].IsCreated)
                {
                    _mNetworkIDs.Add(i); // set unique Network ID
                    _mPlayerInputs.Add(new Vector2(0, 0)); // Example input
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
                TickRate = TickRate,
                TickAhead = _tickAhead
            };

            for (int i = 0; i < _connectedPlayers.Length; i++)
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
        private void SendRPCWithPlayersInputUpdate(NativeList<int> networkIDs, NativeList<Vector2> playerInputs)
        {
            var rpc = new RpcPlayersDataUpdate
            {
                NetworkIDs = networkIDs,
                Inputs = playerInputs,
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
            for (var i = 0; i < _connectedPlayers.Length; i++)
            {
                if (!_connectedPlayers[i].Equals(connection)) continue;

                var inputData = new PlayerInputData
                {
                    networkID = i,
                    input = rpc.PlayerInput
                };

                if (!_everyTickInputBuffer.ContainsKey(rpc.CurrentTick))
                {
                    _everyTickInputBuffer[rpc.CurrentTick] = new List<PlayerInputData>();
                }

                if (!_everyTickHashBuffer.ContainsKey(rpc.CurrentTick))
                {
                    _everyTickHashBuffer[rpc.CurrentTick] = new List<ulong>();
                }

                // This tick already exists in the buffer. Check if the player already has inputs saved for this tick. No need to check for hash in that case because those should be send together and hash can be the same (if everything is correct) so we will get for example 3 same hashes
                foreach (var oldInputData in _everyTickInputBuffer[rpc.CurrentTick])
                {
                    if (oldInputData.networkID == i)
                    {
                        Debug.LogError("Already received input from network ID " + i + " for tick " + rpc.CurrentTick);
                        return; // Stop executing the function here, since we don't want to add the new inputData
                    }
                }

                _everyTickInputBuffer[rpc.CurrentTick].Add(inputData);
                _everyTickHashBuffer[rpc.CurrentTick].Add(rpc.HashForCurrentTick);
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
        /// Function used to check if all data for the current tick has been received. If so it sends it to clients
        /// </summary>
        private void CheckIfAllDataReceivedAndSendToClients()
        {
            var desynchronized = false;
            if (_everyTickInputBuffer[_lastTickReceived].Count == GetActiveConnectionCount() &&
                _everyTickHashBuffer[_lastTickReceived].Count ==
                GetActiveConnectionCount()) // because of different order that we can received those inputs we are checking for last received input
            {
                // We've received a full set of data for this tick, so process it
                var networkIDs = new NativeList<int>(Allocator.Temp);
                var inputs = new NativeList<Vector2>(Allocator.Temp);

                foreach (var inputData in _everyTickInputBuffer[_lastTickReceived])
                {
                    networkIDs.Add(inputData.networkID);
                    inputs.Add(inputData.input);
                }

                // check if every hash is the same
                var firstHash = _everyTickHashBuffer[_lastTickReceived][0];
                for (int i = 1; i < _everyTickHashBuffer[_lastTickReceived].Count; i++)
                {
                    if (firstHash == _everyTickHashBuffer[_lastTickReceived][i]) continue;

                    // Hashes are not equal - handle this scenario
                    Debug.LogError("DESYNCHRONIZATION HAPPENED! HASHES ARE NOT EQUAL! " + "Ticks: " +
                                   _lastTickReceived + " Hashes: " + firstHash + " and " +
                                   _everyTickHashBuffer[_lastTickReceived][i]);
                    desynchronized = true;
                    i = _everyTickHashBuffer[_lastTickReceived].Count;
                }

                if (!desynchronized)
                {
                    Debug.Log("All hashes are equal: " + firstHash + ". Number of hashes: " +
                              _everyTickHashBuffer[_lastTickReceived].Count + ". Tick: " + _lastTickReceived);

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
                _everyTickInputBuffer.Remove(_lastTickReceived);
                _everyTickHashBuffer.Remove(_lastTickReceived);
                _lastTickReceived++;
            }
            else if (_everyTickInputBuffer[_lastTickReceived].Count > GetActiveConnectionCount())
            {
                Debug.LogError("Too many player inputs saved in one tick");
            }
        }
    }
}