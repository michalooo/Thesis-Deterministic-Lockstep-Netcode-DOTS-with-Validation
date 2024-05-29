using System;
using Unity.Collections;
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
        private NetworkDriver _mDriver;
        private NetworkConnection _mConnection;
        private NetworkSettings _clientSimulatorParameters;
        private NetworkPipeline _reliableSimulatorPipeline;

        protected override void OnCreate()
        {
            RequireForUpdate<DeterministicSettings>();
            EntityManager.CreateSingleton(new DeterministicClientComponent()
            {
                deterministicClientWorkingMode = DeterministicClientWorkingMode.None
            });
        }

        protected override void OnStartRunning()
        {
            var settings = SystemAPI.GetSingleton<DeterministicSettings>();

            _clientSimulatorParameters = new NetworkSettings();
            _clientSimulatorParameters.WithSimulatorStageParameters(
                maxPacketCount: 1000,
                packetDelayMs: settings.packetDelayMs,
                packetJitterMs: settings.packetJitterMs,
                packetDropInterval: settings.packetDropInterval,
                packetDropPercentage: settings.packetDropPercentage,
                packetDuplicationPercentage: settings.packetDuplicationPercentage);

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
            if(SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.None) return;
            
            if (SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.Connect && !_mConnection.IsCreated)
            {
                Debug.Log("connecting");
                if (SystemAPI.TryGetSingleton<DeterministicSettings>(out DeterministicSettings deterministicSettings))
                {
                    var endpoint = NetworkEndpoint.Parse(deterministicSettings._serverAddress.ToString(), (ushort) deterministicSettings._serverPort);
                    _mConnection = _mDriver.Connect(endpoint);
                }
                else
                {
                    var endpoint = NetworkEndpoint.Parse("127.0.0.1", 7979); 
                    _mConnection = _mDriver.Connect(endpoint);
                }
            }

            if (SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.Disconnect &&
                !_mConnection.IsCreated)
            {
                _mConnection.Disconnect(_mDriver);
                _mConnection = default;
            }

            if (!_mConnection.IsCreated) return;
            _mDriver.ScheduleUpdate().Complete();

            NetworkEvent.Type cmd;
            while ((cmd = _mConnection.PopEvent(_mDriver, out var stream)) != NetworkEvent.Type.Empty)
            {
                switch (cmd)
                {
                    case NetworkEvent.Type.Connect:
                        if (SystemAPI.TryGetSingleton<DeterministicSettings>(out DeterministicSettings deterministicSettings))
                        {
                            Debug.Log(
                                $"[ConnectToServer] Called on " + deterministicSettings._serverAddress + ":" + deterministicSettings._serverPort + ".");
                        }
                        else
                        {
                            Debug.Log(
                                $"[ConnectToServer] Called on '127.0.0:7979'.");
                        }
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

            switch (id)
            {
                case RpcID.BroadcastTickDataToClients:
                    var rpcPlayersDataUpdate = new RpcBroadcastTickDataToClients();
                    rpcPlayersDataUpdate.Deserialize(ref stream);
                    UpdatePlayersData(rpcPlayersDataUpdate);
                    break;
                case RpcID.StartDeterministicGameSimulation:
                    var rpcStartDeterministicSimulation = new RpcStartDeterministicSimulation();
                    rpcStartDeterministicSimulation.Deserialize(ref stream);
                    StartGame(rpcStartDeterministicSimulation);
                    break;
                case RpcID.PlayersDesynchronizedMessage: // Stop simulation
                    var rpcPlayerDesynchronizationInfo = new RpcPlayerDesynchronizationMessage();
                    rpcPlayerDesynchronizationInfo.Deserialize(ref stream);
                    var determinismSystemGroup = World.DefaultGameObjectInjectionWorld
                        .GetOrCreateSystemManaged<DeterministicSimulationSystemGroup>();
                    determinismSystemGroup.Enabled = false;
                    break;
                case RpcID.BroadcastPlayerTickDataToServer:
                    Debug.LogError("BroadcastPlayerTickDataToServer should never be received by the client");
                    break;
                case RpcID.TestClientPing:
                    var pingRPC = new RpcTestPing();
                    pingRPC.Deserialize(ref stream);
                    pingRPC.Serialize(_mDriver, _mConnection, _reliableSimulatorPipeline);
                    break;
                // case RpcID.PlayerConfiguration:
                //     Debug.LogError("PlayerConfiguration should never be received by the client");
                //     break;
                default:
                    Debug.LogError("Received RPC ID not proceeded by the client: " + id);
                    break;
            }
        }
        
        public static DateTime SyncDateTimeWithServer(double remoteMilliseconds)
        {
            // Get the current date without time (midnight)
            DateTime currentDate = DateTime.Today;

            // Add the milliseconds to the current date to get the remote time
            DateTime remoteDateTime = currentDate.AddMilliseconds(remoteMilliseconds);

            return remoteDateTime;
        }

        /// <summary>
        /// Function to start the game. It will load the game scene and create entities for each player connection with all necessary components.
        /// </summary>
        /// <param name="rpc">RPC from the server that contains parameters for game and request to start the game</param>
        private void StartGame(RpcStartDeterministicSimulation rpc)
        {
            // synchronize clock
            DateTime syncedDateTime = SyncDateTimeWithServer(rpc.TodaysMiliseconds + rpc.PingInMilliseconds);
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
                    reliableSimulationPipelineReference = _reliableSimulatorPipeline,
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
            deterministicTime.ValueRW.synchronizedDateTimeWithServer = syncedDateTime;
            deterministicTime.ValueRW.timeToPostponeStartofSimulation = rpc.PostponedStartInMiliseconds;
            deterministicTime.ValueRW.localTimeAtTheMomentOfSynchronization = DateTime.Now;

            var client = SystemAPI.GetSingleton<DeterministicClientComponent>();
            client.deterministicClientWorkingMode = DeterministicClientWorkingMode.PrepareGame;
            client.randomSeed = rpc.SeedForPlayerRandomActions;
            SystemAPI.SetSingleton(client);
            
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