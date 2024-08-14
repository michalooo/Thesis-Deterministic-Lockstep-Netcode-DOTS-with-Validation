using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Unity.Networking.Transport;

namespace DeterministicLockstep
{
    /// <summary>
    /// System that handles the client side of the game.
    /// It is responsible for handling connection with the server.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class ClientBehaviour : SystemBase
    {
        /// <summary>
        /// Network driver used to handle connections
        /// </summary>
        private NetworkDriver _networkDriver;
        
        /// <summary>
        /// Connection reference to the server
        /// </summary>
        private NetworkConnection _connectionToTheServer;
        
        /// <summary>
        /// Pipeline used to handle reliable and sequenced messages
        /// </summary>
        private NetworkPipeline _reliablePipeline;
        
        /// <summary>
        /// The time that the client should wait after the game finished before sending final hash and disconnecting.
        /// It allows for visually mark the end of the game.
        /// </summary>
        private const float TimeToWaitBeforeEndingGame = 5.0f;
        
        /// <summary>
        /// Counter of the time it passed after the game finished.
        /// </summary>
        private float _timeWaitedAfterEndingTheGame = 0.0f;
        
        /// <summary>
        /// Tick that is nondeterministic and caused the desynchronization
        /// </summary>
        private ulong _nondeterministicTick = 0;
        
        /// <summary>
        /// Bool used to indicate if player is ready to start the game after initial scene load.
        /// </summary>
        private bool _isClientReady = false;

        protected override void OnCreate()
        {
            RequireForUpdate<DeterministicSettings>();
            EntityManager.CreateSingleton(new DeterministicClientComponent()
            {
                deterministicClientWorkingMode = DeterministicClientWorkingMode.None,
                clientNetworkId = 0
            });
        }

        protected override void OnStartRunning()
        {
            _networkDriver = NetworkDriver.Create();
            _reliablePipeline =
                _networkDriver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
        }

        protected override void OnDestroy()
        {
            _networkDriver.Dispose();
        }

        /// <summary>
        /// Function which clears saved hashes for the current tick.
        /// It's used to prevent sending the same hash multiple times.
        /// </summary>
        private void ClearSavedHashes()
        {
            var deterministicTimeComponent = SystemAPI.GetSingleton<DeterministicSimulationTime>();
            deterministicTimeComponent.hashesForTheCurrentTick.Dispose();
            deterministicTimeComponent.hashesForTheCurrentTick = new NativeList<ulong>(Allocator.Persistent);
            SystemAPI.SetSingleton(deterministicTimeComponent);
        }

        protected override void OnUpdate()
        {
            if(SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.None) return;
            
            if (SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.Connect && !_connectionToTheServer.IsCreated)
            {
               Connect();
            }

            if (SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.Disconnect &&
                _connectionToTheServer.IsCreated)
            {
                Disconnect();
            }

            var determinismSystemGroup = World.DefaultGameObjectInjectionWorld
                .GetOrCreateSystemManaged<DeterministicSimulationSystemGroup>();
            if (SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode ==
                DeterministicClientWorkingMode.Desync && determinismSystemGroup.Enabled)
            {
                determinismSystemGroup.Enabled = false;
                if (SystemAPI.GetSingletonRW<DeterministicSettings>().ValueRO.isReplayFromFile)
                {
                    _nondeterministicTick = (ulong) SystemAPI.GetSingletonRW<DeterministicSettings>().ValueRO.targetNonDeterministicTickDuringReplay;
                }
                DeterministicLogger.Instance.LogClientNondeterministicTickInfoToTheFile(World.Name, _nondeterministicTick, SystemAPI.GetSingleton<DeterministicSettings>());
                DeterministicLogger.Instance.LogSystemInfoToTheFile(World.Name, SystemAPI.GetSingleton<DeterministicSettings>());
                DeterministicLogger.Instance.LogClientSettingsToTheFile(World.Name, SystemAPI.GetSingleton<DeterministicSettings>());
            }

            if (SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode ==
                DeterministicClientWorkingMode.GameFinished &&
                _connectionToTheServer.IsCreated)
            {
                if(_timeWaitedAfterEndingTheGame >= TimeToWaitBeforeEndingGame)
                {
                    _timeWaitedAfterEndingTheGame = -1.0f;
                 
                    var deterministicTimeComponent = SystemAPI.GetSingleton<DeterministicSimulationTime>();
                    var stateHashForValidationSystem = World.GetExistingSystem<StateHashForValidationSystem>();
                    
                    stateHashForValidationSystem.Update(World.Unmanaged);
                    
                    foreach (var (connectionReference, ghostOwner) in SystemAPI
                                 .Query<RefRO<NetworkConnectionReference>, RefRO<GhostOwner>>()
                                 .WithAll<GhostOwnerIsLocal>())
                    {
                        Debug.Log("Sending game ended RPC to server from player: " + ghostOwner.ValueRO.connectionNetworkId);
                        var rpcEndGameHash = new RpcEndGameHash()
                        {
                            FinalGameHash = deterministicTimeComponent.hashesForTheCurrentTick[0],
                            ClientNetworkID = ghostOwner.ValueRO.connectionNetworkId
                        };

                        rpcEndGameHash.Serialize(connectionReference.ValueRO.driverReference, connectionReference.ValueRO.connectionReference,
                            connectionReference.ValueRO.reliablePipelineReference);
                        
                        ClearSavedHashes();
                    }
                }
                else if(_timeWaitedAfterEndingTheGame >= 0.0f)
                {
                    _timeWaitedAfterEndingTheGame += SystemAPI.Time.DeltaTime;
                }
            }

            if (SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.ClientReady &&
                _connectionToTheServer.IsCreated && !_isClientReady)
            {
                var stateHashForValidationSystem = World.GetExistingSystem<StateHashForValidationSystem>();
                var deterministicSimulationTimeComponent = SystemAPI.GetSingleton<DeterministicSimulationTime>();
                    
                stateHashForValidationSystem.Update(World.Unmanaged);
                
                var rpcPlayerReady = new RpcPlayerReady
                {
                    ClientNetworkID = SystemAPI.GetSingleton<DeterministicClientComponent>().clientNetworkId,
                    StartingHash = deterministicSimulationTimeComponent.hashesForTheCurrentTick[0] // Only one hashing is performed so we can take the first element
                };
                rpcPlayerReady.Serialize(_networkDriver, _connectionToTheServer, _reliablePipeline);
                ClearSavedHashes();
                _isClientReady = true;
            }
            
            if (!_connectionToTheServer.IsCreated) return;
            _networkDriver.ScheduleUpdate().Complete();
            
            NetworkEvent.Type cmd;
            while ((cmd = _connectionToTheServer.PopEvent(_networkDriver, out var stream)) != NetworkEvent.Type.Empty)
            {
                switch (cmd)
                {
                    case NetworkEvent.Type.Connect:
                        break;
                    case NetworkEvent.Type.Data:
                        HandleIncomingRpcFromStream(stream);
                        break;
                    case NetworkEvent.Type.Disconnect:
                        SystemAPI.GetSingletonRW<DeterministicClientComponent>().ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.Disconnect;
                        _connectionToTheServer = default;
                        break;
                    case NetworkEvent.Type.Empty:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <summary>
        /// Function responsible for disconnecting the client from the server.
        /// </summary>
        private void Disconnect()
        {
            _networkDriver.ScheduleUpdate().Complete(); // Complete the update before disconnecting
                
            _connectionToTheServer.Disconnect(_networkDriver);
            _connectionToTheServer = default;
            
            _networkDriver.ScheduleUpdate().Complete();
            SystemAPI.GetSingletonRW<DeterministicClientComponent>().ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.None;
        }
        
        /// <summary>
        /// Function responsible for connecting the client to the server.
        /// </summary>
        private void Connect()
        {
            if (SystemAPI.TryGetSingleton(out DeterministicSettings deterministicSettings))
            {
                var endpoint = NetworkEndpoint.Parse(deterministicSettings.serverAddress.ToString(), (ushort) deterministicSettings.serverPort);
                _connectionToTheServer = _networkDriver.Connect(endpoint);
            }
            else
            {
                Debug.LogError("DeterministicSettings not found. Cannot connect to server.");
            }
        }

        /// <summary>
        /// Function used to handle incoming RPCs from server.
        /// </summary>
        /// <param name="stream">Stream from which the data arrived</param>
        private void HandleIncomingRpcFromStream(DataStreamReader stream)
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
                case RpcID.StartDeterministicGameSimulation:
                    var rpcStartDeterministicSimulation = new RpcStartDeterministicSimulation();
                    rpcStartDeterministicSimulation.Deserialize(ref stream);
                    StartGame(rpcStartDeterministicSimulation);
                    break;
                case RpcID.BroadcastTickDataToClients:
                    var rpcPlayersDataUpdate = new RpcBroadcastTickDataToClients();
                    rpcPlayersDataUpdate.Deserialize(ref stream);
                    DestroyDisconnectedClients(rpcPlayersDataUpdate);
                    UpdatePlayersData(rpcPlayersDataUpdate);
                    break;
                case RpcID.PlayerDesynchronized:
                    var rpcPlayerDesynchronizationInfo = new RpcPlayerDesynchronization();
                    rpcPlayerDesynchronizationInfo.Deserialize(ref stream);
                    SystemAPI.GetSingletonRW<DeterministicClientComponent>().ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.Desync;
                    _nondeterministicTick = rpcPlayerDesynchronizationInfo.NonDeterministicTick;
                    break;
                case RpcID.LoadGame:
                    var loadGameRPC = new RpcLoadGame();
                    loadGameRPC.Deserialize(ref stream);
                    SystemAPI.GetSingletonRW<DeterministicClientComponent>().ValueRW.clientNetworkId = loadGameRPC.ClientNetworkID;
                    SystemAPI.GetSingletonRW<DeterministicClientComponent>().ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.LoadingGame;
                    break;
                default:
                    Debug.LogError("Received RPC ID not proceeded by the client: " + id);
                    break;
            }
        }

        private void DestroyDisconnectedClients(RpcBroadcastTickDataToClients rpcBroadcastTickDataToClients)
        {
            var connectionEntities = GetEntityQuery(
                typeof(GhostOwner),
                ComponentType.Exclude<GhostOwnerIsLocal>()
            ).ToEntityArray(Allocator.TempJob); // We should never even consider to destroy local player
            
            if(rpcBroadcastTickDataToClients.NetworkIDsOfAllClients.Length >= connectionEntities.Length) return;
            
            foreach (var connectionEntity in connectionEntities)
            {
                var connectionReference = EntityManager.GetComponentData<GhostOwner>(connectionEntity);

                if (rpcBroadcastTickDataToClients.NetworkIDsOfAllClients.Contains(connectionReference.connectionNetworkId)) continue;
                EntityManager.DestroyEntity(connectionReference.connectionCommandsTargetEntity);
                EntityManager.DestroyEntity(connectionEntity);
            }

            connectionEntities.Dispose();
        }

        /// <summary>
        /// Function to start the game.
        /// It will load the game scene and create entities for each player connection with all necessary components.
        /// </summary>
        /// <param name="rpc">RPC from the server that contains parameters for game and request to start the game</param>
        private void StartGame(RpcStartDeterministicSimulation rpc)
        {
            foreach (var playerNetworkId in rpc.NetworkIDsOfAllClients)
            {
                var connectionEntity = EntityManager.CreateEntity();

                EntityManager.AddComponentData(connectionEntity, new DeterministicEntityID { id = DeterministicLogger.Instance.GetDeterministicEntityID(World.Name) });
                EntityManager.AddComponentData(connectionEntity, new PlayerInputDataToUse
                {
                    clientNetworkId = playerNetworkId,
                    playerInputToApply = new PongInputs(),
                    isPlayerDisconnected = false,
                });
                EntityManager.AddComponentData(connectionEntity, new GhostOwner
                {
                    connectionNetworkId = playerNetworkId
                });
                EntityManager.AddComponentData(connectionEntity, new NetworkConnectionReference
                {
                    driverReference = _networkDriver,
                    reliablePipelineReference = _reliablePipeline,
                    connectionReference = _connectionToTheServer
                });
                EntityManager.AddComponentData(connectionEntity, new GhostOwnerIsLocal());
                if (playerNetworkId != rpc.ClientAssignedNetworkID)
                    EntityManager.SetComponentEnabled<GhostOwnerIsLocal>(connectionEntity, false);
            }

            var deterministicSimulationTimeComponent = SystemAPI.GetSingletonRW<DeterministicSimulationTime>();
            deterministicSimulationTimeComponent.ValueRW.GameTickRate = rpc.GameIntendedTickRate;
            deterministicSimulationTimeComponent.ValueRW.forcedInputLatencyDelay = rpc.TicksOfForcedInputLatency;
            deterministicSimulationTimeComponent.ValueRW.timeLeftToSendNextTick = 1f / rpc.GameIntendedTickRate;
            deterministicSimulationTimeComponent.ValueRW.currentSimulationTick = 0;
            deterministicSimulationTimeComponent.ValueRW.currentClientTickToSend = 0;
            deterministicSimulationTimeComponent.ValueRW.numTimesTickedThisFrame = 0;

            var clientComponent = SystemAPI.GetSingletonRW<DeterministicClientComponent>();
            clientComponent.ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.RunDeterministicSimulation;
            
            var deterministicSettings = SystemAPI.GetSingletonRW<DeterministicSettings>();
            deterministicSettings.ValueRW.simulationTickRate = rpc.GameIntendedTickRate;
            deterministicSettings.ValueRW.ticksOfForcedInputLatency = rpc.TicksOfForcedInputLatency;
            deterministicSettings.ValueRW.hashCalculationOption = (DeterminismHashCalculationOption) rpc.DeterminismHashCalculationOption;
            deterministicSettings.ValueRW.isInGame = true;
            deterministicSettings.ValueRW.randomSeed = rpc.SeedForPlayerRandomActions;
        }

        /// <summary>
        /// Function to update the players data from incoming RPC.
        /// It will update the buffer that contains all inputs from the server.
        /// </summary>
        /// <param name="rpc">RPC from the server with input data from each player for the given tick</param>
        private void UpdatePlayersData(RpcBroadcastTickDataToClients rpc)
        {
            var deterministicSimulationTimeComponent = SystemAPI.GetSingletonRW<DeterministicSimulationTime>();
            deterministicSimulationTimeComponent.ValueRW.storedIncomingTicksFromServer.Enqueue(rpc);
        }
    }
}