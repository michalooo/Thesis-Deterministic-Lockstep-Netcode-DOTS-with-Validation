using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Unity.Networking.Transport;

namespace DeterministicLockstep
{
    /// <summary>
    /// System that handles the client side of the game. It is responsible for handling connections.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(ConnectionHandleSystemGroup))]
    public partial class ClientBehaviour : SystemBase
    {
        private NetworkDriver _mDriver;
        private NetworkConnection _mConnection;
        private NetworkSettings _clientSimulatorParameters;
        private NetworkPipeline _reliablePipeline;
        private NetworkPipeline _emptyPipeline;
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
            _mDriver = NetworkDriver.Create();
            _reliablePipeline =
                _mDriver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            _emptyPipeline = _mDriver.CreatePipeline(typeof(NullPipelineStage));
        }

        protected override void OnDestroy()
        {
            _mDriver.Dispose();
        }

        protected override void OnUpdate()
        {
            if(SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.None) return;
            
            if (SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.Connect && !_mConnection.IsCreated)
            {
               Connect();
            }

            if (SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.Disconnect &&
                _mConnection.IsCreated)
            {
                Disconnect();
            }
            
            if (SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.ClientReady &&
                _mConnection.IsCreated && !_isClientReady)
            {
                var clientReadyRPC = new RpcPlayerReady
                {
                    PlayerNetworkID = SystemAPI.GetSingleton<DeterministicClientComponent>().clientNetworkId
                };
                clientReadyRPC.Serialize(_mDriver, _mConnection, _emptyPipeline);
                _isClientReady = true;
            }
            
            if (!_mConnection.IsCreated) return;
            _mDriver.ScheduleUpdate().Complete();
            
            NetworkEvent.Type cmd;
            while ((cmd = _mConnection.PopEvent(_mDriver, out var stream)) != NetworkEvent.Type.Empty)
            {
                switch (cmd)
                {
                    case NetworkEvent.Type.Connect:
                        break;
                    case NetworkEvent.Type.Data:
                        HandleRpc(stream);
                        break;
                    case NetworkEvent.Type.Disconnect:
                        Debug.Log("Disconnected from server.");
                        SystemAPI.GetSingletonRW<DeterministicClientComponent>().ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.Disconnect;
                        _mConnection = default;
                        break;
                    case NetworkEvent.Type.Empty:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private void Disconnect()
        {
            _mDriver.ScheduleUpdate().Complete();
                
            _mConnection.Disconnect(_mDriver);
            _mConnection = default;
            
            _mDriver.ScheduleUpdate().Complete();
        }
        
        private void Connect()
        {
            if (SystemAPI.TryGetSingleton(out DeterministicSettings deterministicSettings))
            {
                var endpoint = NetworkEndpoint.Parse(deterministicSettings._serverAddress.ToString(), (ushort) deterministicSettings._serverPort);
                _mConnection = _mDriver.Connect(endpoint);
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
            var id = (RpcID)copyOfStream.ReadByte(); // for the future check if its within a valid range (id as bytes)
            if (!Enum.IsDefined(typeof(RpcID), id))
            {
                Debug.LogError("Received invalid RPC ID: " + id);
                return;
            }

            Debug.Log(id);
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
                    var rpcPlayerDesynchronizationInfo = new RpcPlayerDesynchronizationMessage();
                    rpcPlayerDesynchronizationInfo.Deserialize(ref stream);
                    var determinismSystemGroup = World.DefaultGameObjectInjectionWorld
                        .GetOrCreateSystemManaged<DeterministicSimulationSystemGroup>();
                    determinismSystemGroup.Enabled = false;
                    break;
                case RpcID.LoadGame:
                    var loadGameRPC = new RpcLoadGame();
                    loadGameRPC.Deserialize(ref stream);
                    SystemAPI.GetSingletonRW<DeterministicClientComponent>().ValueRW.clientNetworkId = loadGameRPC.PlayerNetworkID;
                    SystemAPI.GetSingletonRW<DeterministicClientComponent>().ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.LoadingGame;
                    break;
                // case RpcID.PingPong:
                //     var pingPongRPC = new RpcPingPong();
                //     pingPongRPC.Deserialize(ref stream);
                //     
                //     var correctedServerTime = pingPongRPC.ServerTimeStampUTCtoday.TotalMilliseconds + SystemAPI.Time.DeltaTime*1000/2;
                //     pingPongRPC.ServerTimeStampUTCtoday = TimeSpan.FromMilliseconds(correctedServerTime);
                //     pingPongRPC.Serialize(_mDriver, _mConnection, _emptyPipeline);
                //     break;
                // case RpcID.PlayerConfiguration:
                //     Debug.LogError("PlayerConfiguration should never be received by the client");
                //     break;
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
            
            if(rpc.NetworkIDs.Length >= connectionEntities.Length) return;
            
            foreach (var entity in connectionEntities)
            {
                var connectionReference = EntityManager.GetComponentData<GhostOwner>(entity);
                
                if (!rpc.NetworkIDs.Contains(connectionReference.connectionNetworkId))
                {
                    for(int i=0; i<rpc.NetworkIDs.Length; i++)
                    {
                        Debug.Log("rpc: " + rpc.NetworkIDs[i]);
                    }
                    Debug.LogError("Destroying connection: " + connectionReference.connectionNetworkId);
                    EntityManager.DestroyEntity(connectionReference.connectionCommandsTargetEntity);
                    EntityManager.DestroyEntity(entity);
                }
            }

            connectionEntities.Dispose();
        }
        
        // private TimeSpan SyncDateTimeWithServer(RpcStartDeterministicSimulation rpc)
        // {
        //     // Get the current date without time (midnight)
        //     var currentTimestampUtc = DateTime.UtcNow.TimeOfDay;
        //     var pingValue = rpc.PlayerAveragePing;
        //     var serverTimeWhenSendingRPC = rpc.ServerTimestampUTC;
        //     
        //     // Debug.LogError("Timer setup --> currentLocalTimeStamp: " + currentTimestampUtc + " ping value: " + pingValue + " serverTimeWhenSendingRPC: " + serverTimeWhenSendingRPC);
        //
        //     // Add the milliseconds to the current date to get the remote time
        //     var currentServerTimestampUtc = TimeSpan.FromMilliseconds(serverTimeWhenSendingRPC.TotalMilliseconds + pingValue + SystemAPI.Time.DeltaTime*1000/2);
        //
        //     // Debug.LogError("Predicted server timestamp: " + currentServerTimestampUtc);
        //     return currentServerTimestampUtc;
        // }

        /// <summary>
        /// Function to start the game. It will load the game scene and create entities for each player connection with all necessary components.
        /// </summary>
        /// <param name="rpc">RPC from the server that contains parameters for game and request to start the game</param>
        private void StartGame(RpcStartDeterministicSimulation rpc)
        {
            // Debug.LogError("Starting game simulation...");
            // synchronize clock
            // var currentServerTimestampUtc = SyncDateTimeWithServer(rpc);
            // Debug.Log("Synchronized DateTime: " + syncedDateTime.TimeOfDay + " for player with ID: " + rpc.ThisConnectionNetworkID + " time to postpone: " + rpc.PostponedStartInMiliseconds);
            
            foreach (var playerNetworkId in rpc.PlayersNetworkIDs)
            {
                var newEntity = EntityManager.CreateEntity();

                EntityManager.AddComponentData(newEntity, new PlayerInputDataToUse
                {
                    playerNetworkId = playerNetworkId,
                    playerInputToApply = new PongInputs(),
                    isPlayerDisconnected = false,
                });
                EntityManager.AddComponentData(newEntity, new GhostOwner
                {
                    connectionNetworkId = playerNetworkId
                });
                EntityManager.AddComponentData(newEntity, new NetworkConnectionReference
                {
                    driverReference = _mDriver,
                    reliablePipelineReference = _reliablePipeline,
                    connectionReference = _mConnection
                });
                EntityManager.AddComponentData(newEntity, new GhostOwnerIsLocal());
                if (playerNetworkId != rpc.ThisConnectionNetworkID)
                    EntityManager.SetComponentEnabled<GhostOwnerIsLocal>(newEntity, false);
            }

            var deterministicTime = SystemAPI.GetSingletonRW<DeterministicTime>();
            deterministicTime.ValueRW.GameTickRate = rpc.TickRate;
            deterministicTime.ValueRW.forcedInputLatencyDelay = rpc.TicksOfForcedInputLatency;
            deterministicTime.ValueRW.timeLeftToSendNextTick = 1f / rpc.TickRate;
            deterministicTime.ValueRW.currentSimulationTick = 0;
            deterministicTime.ValueRW.currentClientTickToSend = 0;
            deterministicTime.ValueRW.hashesForTheCurrentTick = new NativeList<ulong>(Allocator.Persistent);
            deterministicTime.ValueRW.numTimesTickedThisFrame = 0;
            deterministicTime.ValueRW.realTime = 0;
            deterministicTime.ValueRW.deterministicLockstepElapsedTime = 0;
            // deterministicTime.ValueRW.serverTimestampUTC = currentServerTimestampUtc;
            // deterministicTime.ValueRW.timeToPostponeStartofSimulationInMiliseconds = rpc.PostponedStartInMiliseconds;
            // deterministicTime.ValueRW.localTimestampAtTheMomentOfSynchronizationUTC = DateTime.UtcNow.TimeOfDay;
            // deterministicTime.ValueRW.playerAveragePing = rpc.PlayerAveragePing;

            var client = SystemAPI.GetSingletonRW<DeterministicClientComponent>();
            client.ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.RunDeterministicSimulation;
            client.ValueRW.randomSeed = rpc.SeedForPlayerRandomActions;
            
            var settings = SystemAPI.GetSingletonRW<DeterministicSettings>();
            settings.ValueRW.simulationTickRate = rpc.TickRate;
            settings.ValueRW.ticksAhead = rpc.TicksOfForcedInputLatency;
            settings.ValueRW.hashCalculationOption = (DeterminismHashCalculationOption) rpc.DeterminismHashCalculationOption;
            settings.ValueRW.isInGame = true;
            
            // foreach (var connectionReference in SystemAPI
            //              .Query<RefRO<NetworkConnectionReference>>()
            //              .WithAll<GhostOwnerIsLocal>())
            // {
            //     Debug.Log("Sending player configuration to server");
            //     
                // var deterministicSystemNames = new NativeList<FixedString32Bytes>(Allocator.Temp);
                // var deterministicSystemGroup =
                //     World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged(
                //         typeof(DeterministicSimulationSystemGroup));
                

                // foreach (var system in deterministicSystemGroup.)
                // {
                //     
                // }
                
                // var playerConfigRPC = new RpcPlayerConfiguration
                // {
                //     DeterministicSystemNamesDebug = 
                // };
                //
                // configRPC.Serialize(connectionReference.ValueRO.driverReference, connectionReference.ValueRO.connectionReference,
                //     connectionReference.ValueRO.reliableSimulationPipelineReference);
            // }
        }

        /// <summary>
        /// Function to update the players data from incoming RPC. It will update the buffer that contains all inputs from the server.
        /// </summary>
        /// <param name="rpc">RPC from the server with input data from each player for the given tick</param>
        private void UpdatePlayersData(RpcBroadcastTickDataToClients rpc)
        {
            var deterministicTime = SystemAPI.GetSingletonRW<DeterministicTime>();
            deterministicTime.ValueRW.storedIncomingTicksFromServer.Enqueue(rpc);
        }
    }
}