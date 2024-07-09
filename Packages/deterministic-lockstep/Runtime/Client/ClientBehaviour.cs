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
    [UpdateBefore(typeof(DeterministicSimulationSystemGroup))]
    public partial class ClientBehaviour : SystemBase
    {
        /// <summary>
        /// Network driver used to handle connections
        /// </summary>
        private NetworkDriver networkDriver;
        
        /// <summary>
        /// Connection reference to the server
        /// </summary>
        private NetworkConnection connectionToTheServer;
        
        /// <summary>
        /// Pipeline used to handle reliable and sequenced messages
        /// </summary>
        private NetworkPipeline reliablePipeline;
        
        /// <summary>
        /// The time that the client should wait after the game finished before sending final hash and disconnecting.
        /// It allows for visually mark the end of the game.
        /// </summary>
        private const float TimeToWaitBeforeEndingGame = 5.0f;
        
        /// <summary>
        /// Counter of the time it passed after the game finished.
        /// </summary>
        private float timeWaitedAfterEndingTheGame = 0.0f;
        
        /// <summary>
        /// Tick that is nondeterministic and caused the desynchronization
        /// </summary>
        private ulong nondeterministicTick = 0;
        
        /// <summary>
        /// Bool used to indicate if player is ready to start the game after initial scene load.
        /// </summary>
        private bool isClientReady = false;

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
            networkDriver = NetworkDriver.Create();
            reliablePipeline =
                networkDriver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
        }

        protected override void OnDestroy()
        {
            networkDriver.Dispose();
        }

        /// <summary>
        /// Function which clears saved hashes for the current tick.
        /// It's used to prevent sending the same hash multiple times.
        /// </summary>
        private void ClearSavedHashes()
        {
            var deterministicTime = SystemAPI.GetSingleton<DeterministicSimulationTime>();
            deterministicTime.hashesForTheCurrentTick.Dispose();
            deterministicTime.hashesForTheCurrentTick = new NativeList<ulong>(Allocator.Persistent);
            SystemAPI.SetSingleton(deterministicTime);
        }

        protected override void OnUpdate()
        {
            if(SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.None) return;
            
            if (SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.Connect && !connectionToTheServer.IsCreated)
            {
               Connect();
            }

            if (SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.Disconnect &&
                connectionToTheServer.IsCreated)
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
                    nondeterministicTick = (ulong) SystemAPI.GetSingletonRW<DeterministicSettings>().ValueRO.nonDeterministicTickDuringReplay;
                }
                DeterministicLogger.Instance.LogClientNondeterministicTickInfoToTheFile(World.Name, nondeterministicTick);
                DeterministicLogger.Instance.LogSystemInfoToTheFile(World.Name);
                DeterministicLogger.Instance.LogClientSettingsToTheFile(World.Name, SystemAPI.GetSingleton<DeterministicSettings>());
            }

            if (SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode ==
                DeterministicClientWorkingMode.GameFinished &&
                connectionToTheServer.IsCreated)
            {
                if(timeWaitedAfterEndingTheGame >= TimeToWaitBeforeEndingGame)
                {
                    timeWaitedAfterEndingTheGame = -1.0f;
                 
                    var deterministicTime = SystemAPI.GetSingleton<DeterministicSimulationTime>();
                    var determinismCheckSystem = World.GetExistingSystem<StateHashForValidationSystem>();
                    
                    determinismCheckSystem.Update(World.Unmanaged);
                    
                    foreach (var (connectionReference, owner) in SystemAPI
                                 .Query<RefRO<NetworkConnectionReference>, RefRO<GhostOwner>>()
                                 .WithAll<GhostOwnerIsLocal>())
                    {
                        Debug.Log("Sending game ended RPC to server from player: " + owner.ValueRO.connectionNetworkId);
                        var rpc = new RpcEndGameHash()
                        {
                            FinalGameHash = deterministicTime.hashesForTheCurrentTick[0],
                            ClientNetworkID = owner.ValueRO.connectionNetworkId
                        };

                        rpc.Serialize(connectionReference.ValueRO.driverReference, connectionReference.ValueRO.connectionReference,
                            connectionReference.ValueRO.reliablePipelineReference);
                        
                        ClearSavedHashes();
                    }
                }
                else if(timeWaitedAfterEndingTheGame >= 0.0f)
                {
                    timeWaitedAfterEndingTheGame += SystemAPI.Time.DeltaTime;
                }
            }

            if (SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.ClientReady &&
                connectionToTheServer.IsCreated && !isClientReady)
            {
                var determinismCheckSystem = World.GetExistingSystem<StateHashForValidationSystem>();
                var deterministicTime = SystemAPI.GetSingleton<DeterministicSimulationTime>();
                    
                determinismCheckSystem.Update(World.Unmanaged);
                
                var clientReadyRPC = new RpcPlayerReady
                {
                    ClientNetworkID = SystemAPI.GetSingleton<DeterministicClientComponent>().clientNetworkId,
                    StartingHash = deterministicTime.hashesForTheCurrentTick[0] // Only one hashing is performed so we can take the first element
                };
                clientReadyRPC.Serialize(networkDriver, connectionToTheServer, reliablePipeline);
                ClearSavedHashes();
                isClientReady = true;
            }
            
            if (!connectionToTheServer.IsCreated) return;
            networkDriver.ScheduleUpdate().Complete();
            
            NetworkEvent.Type cmd;
            while ((cmd = connectionToTheServer.PopEvent(networkDriver, out var stream)) != NetworkEvent.Type.Empty)
            {
                switch (cmd)
                {
                    case NetworkEvent.Type.Connect:
                        break;
                    case NetworkEvent.Type.Data:
                        HandleRpc(stream);
                        break;
                    case NetworkEvent.Type.Disconnect:
                        SystemAPI.GetSingletonRW<DeterministicClientComponent>().ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.Disconnect;
                        connectionToTheServer = default;
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
            networkDriver.ScheduleUpdate().Complete();
                
            connectionToTheServer.Disconnect(networkDriver);
            connectionToTheServer = default;
            
            networkDriver.ScheduleUpdate().Complete();
            SystemAPI.GetSingletonRW<DeterministicClientComponent>().ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.None;
        }
        
        /// <summary>
        /// Function responsible for connecting the client to the server.
        /// </summary>
        private void Connect()
        {
            if (SystemAPI.TryGetSingleton(out DeterministicSettings deterministicSettings))
            {
                var endpoint = NetworkEndpoint.Parse(deterministicSettings._serverAddress.ToString(), (ushort) deterministicSettings._serverPort);
                connectionToTheServer = networkDriver.Connect(endpoint);
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
        private void HandleRpc(DataStreamReader stream)
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
                    nondeterministicTick = rpcPlayerDesynchronizationInfo.NonDeterministicTick;
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

        private void DestroyDisconnectedClients(RpcBroadcastTickDataToClients rpc)
        {
            var connectionEntities = GetEntityQuery(
                typeof(GhostOwner),
                ComponentType.Exclude<GhostOwnerIsLocal>()
            ).ToEntityArray(Allocator.TempJob); // We should never even consider to destroy local player
            
            if(rpc.NetworkIDsOfAllClients.Length >= connectionEntities.Length) return;
            
            foreach (var entity in connectionEntities)
            {
                var connectionReference = EntityManager.GetComponentData<GhostOwner>(entity);
                
                if (!rpc.NetworkIDsOfAllClients.Contains(connectionReference.connectionNetworkId))
                {
                    for(int i=0; i<rpc.NetworkIDsOfAllClients.Length; i++)
                    {
                        Debug.Log("rpc: " + rpc.NetworkIDsOfAllClients[i]);
                    }
                    Debug.LogError("Destroying connection: " + connectionReference.connectionNetworkId);
                    EntityManager.DestroyEntity(connectionReference.connectionCommandsTargetEntity);
                    EntityManager.DestroyEntity(entity);
                }
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

                EntityManager.AddComponentData(connectionEntity, new DeterministicEntityID { ID = DeterministicLogger.Instance.GetDeterministicEntityID(World.Name) });
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
                    driverReference = networkDriver,
                    reliablePipelineReference = reliablePipeline,
                    connectionReference = connectionToTheServer
                });
                EntityManager.AddComponentData(connectionEntity, new GhostOwnerIsLocal());
                if (playerNetworkId != rpc.ClientAssignedNetworkID)
                    EntityManager.SetComponentEnabled<GhostOwnerIsLocal>(connectionEntity, false);
            }

            var deterministicTime = SystemAPI.GetSingletonRW<DeterministicSimulationTime>();
            deterministicTime.ValueRW.GameTickRate = rpc.GameIntendedTickRate;
            deterministicTime.ValueRW.forcedInputLatencyDelay = rpc.TicksOfForcedInputLatency;
            deterministicTime.ValueRW.timeLeftToSendNextTick = 1f / rpc.GameIntendedTickRate;
            deterministicTime.ValueRW.currentSimulationTick = 0;
            deterministicTime.ValueRW.currentClientTickToSend = 0;
            deterministicTime.ValueRW.numTimesTickedThisFrame = 0;

            var client = SystemAPI.GetSingletonRW<DeterministicClientComponent>();
            client.ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.RunDeterministicSimulation;
            
            var settings = SystemAPI.GetSingletonRW<DeterministicSettings>();
            settings.ValueRW.simulationTickRate = rpc.GameIntendedTickRate;
            settings.ValueRW.ticksOfForcedInputLatency = rpc.TicksOfForcedInputLatency;
            settings.ValueRW.hashCalculationOption = (DeterminismHashCalculationOption) rpc.DeterminismHashCalculationOption;
            settings.ValueRW.isInGame = true;
            settings.ValueRW.randomSeed = rpc.SeedForPlayerRandomActions;
        }

        /// <summary>
        /// Function to update the players data from incoming RPC.
        /// It will update the buffer that contains all inputs from the server.
        /// </summary>
        /// <param name="rpc">RPC from the server with input data from each player for the given tick</param>
        private void UpdatePlayersData(RpcBroadcastTickDataToClients rpc)
        {
            var deterministicTime = SystemAPI.GetSingletonRW<DeterministicSimulationTime>();
            deterministicTime.ValueRW.storedIncomingTicksFromServer.Enqueue(rpc);
        }
    }
}