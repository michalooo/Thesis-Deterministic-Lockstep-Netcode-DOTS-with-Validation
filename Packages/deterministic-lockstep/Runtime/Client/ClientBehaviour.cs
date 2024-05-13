using System;
using Unity.Entities;
using UnityEngine;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine.SceneManagement;

namespace DeterministicLockstep
{
    /// <summary>
    /// System that handles the client side of the game. It is responsible for handling connections.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(ConnectionHandleSystemGroup))]
    public partial class ClientBehaviour : SystemBase
    {
        private DeterministicSettings _settings;
        private const ushort KNetworkPort = 7979;

        private NetworkDriver _mDriver;
        private NetworkConnection _mConnection;
        private NetworkSettings _clientSimulatorParameters;
        private NetworkPipeline _reliableSimulatorPipeline;

        protected override void OnCreate()
        {
            RequireForUpdate<DeterministicSettings>();
        }

        protected override void OnStartRunning()
        {
            _settings = SystemAPI.GetSingleton<DeterministicSettings>();

            _clientSimulatorParameters = new NetworkSettings();
            _clientSimulatorParameters.WithSimulatorStageParameters(
                maxPacketCount: 1000,
                packetDelayMs: _settings.packetDelayMs,
                packetJitterMs: _settings.packetJitterMs,
                packetDropInterval: _settings.packetDropInterval,
                packetDropPercentage: _settings.packetDropPercentage,
                packetDuplicationPercentage: _settings.packetDuplicationPercentage);

            _mDriver = NetworkDriver.Create(_clientSimulatorParameters);
            _reliableSimulatorPipeline =
                _mDriver.CreatePipeline(typeof(ReliableSequencedPipelineStage), typeof(SimulatorPipelineStage));
        }

        protected override void OnDestroy()
        {
            _mDriver.Dispose();
        }

        protected override void OnUpdate()
        {
            HandleConnectionToDeterministicServer();
            HandleDisconnectionFromDeterministicServer();

            _mDriver.ScheduleUpdate().Complete();

            if (!_mConnection.IsCreated) return;

            NetworkEvent.Type cmd;
            while ((cmd = _mConnection.PopEvent(_mDriver, out var stream)) != NetworkEvent.Type.Empty)
            {
                switch (cmd)
                {
                    case NetworkEvent.Type.Connect:
                        Debug.Log(
                            $"[ConnectToServer] Called on '127.0.0:{KNetworkPort}'.");
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
        }

        private void HandleConnectionToDeterministicServer()
        {
            foreach (var (settings, settingsEntity) in SystemAPI.Query<RefRW<DeterministicSettings>>()
                         .WithAll<DeterministicClientConnect>().WithEntityAccess())
            {
                var endpoint = NetworkEndpoint.Parse("127.0.0.1", KNetworkPort); //change to chosen IP
                _mConnection = _mDriver.Connect(endpoint);
                SystemAPI.SetComponentEnabled<DeterministicClientConnect>(settingsEntity, false);
            }
        }

        private void HandleDisconnectionFromDeterministicServer()
        {
            foreach (var (settings, settingsEntity) in SystemAPI.Query<RefRW<DeterministicSettings>>()
                         .WithAll<DeterministicClientDisconnect>().WithEntityAccess())
            {
                _mConnection.Disconnect(_mDriver);
                _mConnection = default;
                SystemAPI.SetComponentEnabled<DeterministicClientDisconnect>(settingsEntity, false);
            }
        }

        /// <summary>
        /// Function used to handle incoming RPCs from server.
        /// </summary>
        /// <param name="stream">Stream from which the data arrived</param>
        private void HandleRpc(Unity.Collections.DataStreamReader stream)
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
                    var determinismSystemGroup = World.DefaultGameObjectInjectionWorld
                        .GetOrCreateSystemManaged<DeterministicSimulationSystemGroup>();
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

        /// <summary>
        /// Function to start the game. It will load the game scene and create entities for each player connection with all necessary components.
        /// </summary>
        /// <param name="rpc">RPC from the server that contains parameters for game and request to start the game</param>
        private void StartGame(RpcStartDeterministicSimulation rpc)
        {
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
                    reliableSimulatorPipeline = _reliableSimulatorPipeline,
                    connection = _mConnection
                });
                EntityManager.AddComponentData(newEntity, new GhostOwnerIsLocal());
                // EntityManager.AddComponentData(newEntity, new StoredTicksAhead(false));
                if (playerNetworkId != rpc.NetworkID)
                    EntityManager.SetComponentEnabled<GhostOwnerIsLocal>(newEntity, false);
                EntityManager.SetComponentEnabled<PlayerInputDataToSend>(newEntity, false);
            }
        }

        /// <summary>
        /// Function to update the players data from incoming RPC. It will update the buffer that contains all inputs from the server.
        /// </summary>
        /// <param name="rpc">RPC from the server with input data from each player for the given tick</param>
        void UpdatePlayersData(RpcPlayersDataUpdate rpc)
        {
            foreach (var storedTicksAhead in SystemAPI.Query<RefRW<StoredTicksAhead>>().WithAll<GhostOwnerIsLocal>())
            {
                storedTicksAhead.ValueRW.entries.Enqueue(rpc);
                // Are packages reliable with reliable pipeline so those will always arrive in order?
                // Always current tick is less or equal to the server tick
            }
        }
    }
}