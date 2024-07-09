using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
namespace DeterministicLockstep
{
    /// <summary>
    /// System group that contains deterministic simulation systems.
    /// All the systems that are affecting the game state should be added to this group.
    /// It's responsible for performing necessary determinism checks on those systems and running them in set frame rate.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class DeterministicSimulationSystemGroup : ComponentSystemGroup
    {
        /// <summary>
        /// DeltaTime used when simulation is catching up due to the time it took to process last frame.
        /// This is a fixed value which will be applied to the simulation until it finishes catching up.
        /// Default value is 1/60 of a second which reflects 60FPS pace.
        /// </summary>
        private static float localDeltaTime = 1.0f/60.0f;
        
        /// <summary>
        /// Value of how many ticks per frame the simulation can process when catching up.
        /// </summary>
        private const int MaxTicksPerFrame = 10;

        protected override void OnCreate()
        {
            base.OnCreate();
            RateManager = new DeterministicFixedStepRateManager(this);
            
            EntityManager.CreateSingletonBuffer<DeterministicComponent>();
            var deterministicComponentsBuffer = SystemAPI.GetSingletonBuffer<DeterministicComponent>();
            deterministicComponentsBuffer.Add(new DeterministicComponent
            {
                Type = ComponentType.ReadOnly<LocalTransform>(),
            });
            deterministicComponentsBuffer.Add(new DeterministicComponent
            {
                Type = ComponentType.ReadOnly<DeterministicSettings>(),
            });
            deterministicComponentsBuffer.Add(new DeterministicComponent
            {
                Type = ComponentType.ReadOnly<DeterministicEntityID>(),
            });
            
            EntityManager.CreateSingleton(new DeterministicSimulationTime
            {
                storedIncomingTicksFromServer = new NativeQueue<RpcBroadcastTickDataToClients>(Allocator.Persistent),
                hashesForTheCurrentTick = new NativeList<ulong>(Allocator.Persistent),
            });
            EntityManager.CreateSingleton<PongInputs>();
        }

        protected override void OnDestroy()
        {
            if (!SystemAPI.TryGetSingletonRW<DeterministicSimulationTime>(out var deterministicTime)) return;
            deterministicTime.ValueRW.storedIncomingTicksFromServer.Dispose();
            deterministicTime.ValueRW.hashesForTheCurrentTick.Dispose();
        }

        protected override void OnUpdate()
        {
            
            localDeltaTime = 1.0f/SystemAPI.GetSingleton<DeterministicSimulationTime>().GameTickRate;
            
            if (SystemAPI.GetSingleton<DeterministicSettings>().hashCalculationOption ==
                DeterminismHashCalculationOption.WhitelistHashPerSystem ||
                SystemAPI.GetSingleton<DeterministicSettings>().hashCalculationOption ==
                DeterminismHashCalculationOption.FullStateHashPerSystem)
            {
                while (RateManager.ShouldGroupUpdate(this))
                {
                    UpdateAllGroupSystems(this);
                }
            }
            else
            {
                base.OnUpdate();
            }
        }

        /// <summary>
        /// IRateManager implementation which allows for fixed step simulation.
        /// </summary>
        public struct DeterministicFixedStepRateManager : IRateManager
        {
            private EntityQuery deterministicTimeQuery;
            private EntityQuery deterministicSettingsQuery;
            private EntityQuery connectionQuery;
            private EntityQuery inputDataQuery;
            private EntityQuery deterministicClientQuery;
            private bool wasLogging;
            private NativeList<RpcBroadcastTickDataToClients> dataToReplayFromTheFile;
            public float Timestep { get; set; }

            public DeterministicFixedStepRateManager(ComponentSystemGroup group) : this()
            {
                deterministicTimeQuery = group.EntityManager.CreateEntityQuery(typeof(DeterministicSimulationTime));
                deterministicSettingsQuery = group.EntityManager.CreateEntityQuery(typeof(DeterministicSettings));
                deterministicClientQuery = group.EntityManager.CreateEntityQuery(typeof(DeterministicClientComponent));
                connectionQuery =
                    group.EntityManager.CreateEntityQuery(
                        typeof(GhostOwnerIsLocal));
                inputDataQuery = group.EntityManager.CreateEntityQuery(typeof(GhostOwner));
                wasLogging = false;
            }

            public bool ShouldGroupUpdate(ComponentSystemGroup group)
            {
                var deterministicClient = deterministicClientQuery.GetSingletonRW<DeterministicClientComponent>();
                if (deterministicClient.ValueRO.deterministicClientWorkingMode != DeterministicClientWorkingMode.RunDeterministicSimulation)
                    return false;
                
                var deltaTime = (double) group.World.Time.DeltaTime;
                var deterministicTime = deterministicTimeQuery.GetSingletonRW<DeterministicSimulationTime>();
                var deterministicSettings = deterministicSettingsQuery.GetSingletonRW<DeterministicSettings>();
                var localConnectionEntity = connectionQuery.ToEntityArray(Allocator.Temp);

                if (deterministicSettings.ValueRO.isReplayFromFile && !dataToReplayFromTheFile.IsCreated)
                {
                    dataToReplayFromTheFile = DeterministicLogger.Instance.ReadServerInputRecordingFromTheFile();
                    var deterministicSettingsFromTheFile = DeterministicLogger.Instance.ReadSettingsFromFile();
                    
                    deterministicSettings.ValueRW.ticksOfForcedInputLatency = deterministicSettingsFromTheFile.ticksOfForcedInputLatency;
                    deterministicSettings.ValueRW.allowedConnectionsPerGame = deterministicSettingsFromTheFile.allowedConnectionsPerGame;
                    deterministicSettings.ValueRW.simulationTickRate = deterministicSettingsFromTheFile.simulationTickRate;
                    deterministicSettings.ValueRW.isReplayFromFile = true;
                    deterministicSettings.ValueRW.randomSeed = deterministicSettingsFromTheFile.randomSeed;
                    deterministicSettings.ValueRW._serverAddress = deterministicSettingsFromTheFile._serverAddress;
                    deterministicSettings.ValueRW._serverPort = deterministicSettingsFromTheFile._serverPort;
                    
                    deterministicSettings.ValueRW.nonDeterministicTickDuringReplay = dataToReplayFromTheFile.Length;
                }
                
                if (deterministicTime.ValueRO.currentClientTickToSend <=
                    deterministicTime.ValueRO
                        .forcedInputLatencyDelay) // Sending first inputs to cover for forced input delay
                {
                    var inputSendSystem = group.World.GetExistingSystem<PlayerInputSendSystem>();

                    while (deterministicTime.ValueRO.currentClientTickToSend <=
                           deterministicTime.ValueRO
                               .forcedInputLatencyDelay)
                    {
                        inputSendSystem.Update(group.World.Unmanaged);
                        deterministicTime.ValueRW.currentClientTickToSend++;
                        deterministicTime.ValueRW.numTimesTickedThisFrame++;
                        group.World.PushTime(new TimeData(localDeltaTime, localDeltaTime));
                    }

                    return false;
                }
                
                var isTimeToSendNextTick = false;
                
                if (deterministicTime.ValueRO.numTimesTickedThisFrame >= MaxTicksPerFrame) // If we already ticked maximum times this frame
                {
                    deterministicTime.ValueRW.timeLeftToSendNextTick += localDeltaTime;
                }
                else if (deterministicTime.ValueRO.timeLeftToSendNextTick > deltaTime) // If we should wait because of the time left to send next tick
                {
                    deterministicTime.ValueRW.timeLeftToSendNextTick -= deltaTime;
                }
                else if (deterministicTime.ValueRO.timeLeftToSendNextTick <= deltaTime) // If simulation should run
                {
                    if (deterministicTime.ValueRO.timeLeftToSendNextTick >= 0) // If lets say deltaTime=16 and timeToWait=12. It means that we should tick and the time left to send next tick should be 4
                    {
                        isTimeToSendNextTick = true;
                        deterministicTime.ValueRW.timeLeftToSendNextTick -= deltaTime;
                    }
                    else // in this case time < 0 which means that for example deltaTime was 60 and time was 10 (so now we are -50) and simulation needs to catch up
                    {
                        if (deterministicTime.ValueRW.timeLeftToSendNextTick + deltaTime > 0) // if time=-14 and delta=16 it means that simulation should tick once and the result should be 2
                        {
                            deterministicTime.ValueRW.timeLeftToSendNextTick += deltaTime;
                        }
                        else // If timeToWait will still be on - after this tick
                        {
                            isTimeToSendNextTick = true;
                            deterministicTime.ValueRW.timeLeftToSendNextTick +=  deltaTime;
                        }
                    }
                }
                
                
                if (isTimeToSendNextTick)
                {
                    if(dataToReplayFromTheFile.IsCreated && dataToReplayFromTheFile.Length > 0)
                    {
                        var rpc = dataToReplayFromTheFile[0];
                        dataToReplayFromTheFile.RemoveAt(0);
                        
                        deterministicTime.ValueRW.currentSimulationTick++;
                        deterministicTime.ValueRW.currentClientTickToSend++;
                        
                        UpdateComponentsData(rpc, group);
                      
                        group.EntityManager.SetComponentEnabled<PlayerInputDataToUse>(localConnectionEntity[0],
                            true);
                        deterministicTime.ValueRW.numTimesTickedThisFrame++;
                        
                        group.World.PushTime(
                            new TimeData(localDeltaTime,
                                localDeltaTime));
                        if(dataToReplayFromTheFile.Length == 0)
                        {
                            deterministicClient.ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.Desync;
                        }
                        return true;
                    }
                    else
                    {
                        if (deterministicTime.ValueRO.storedIncomingTicksFromServer.Count > 0)
                        {
                            // If we found on we can increment both ticks (current presentation tick and tick we will send to server)
                            deterministicTime.ValueRW.currentSimulationTick++;
                            deterministicTime.ValueRW.currentClientTickToSend++;

                            // first update the component data before we will remove the info from the array to make space for more
                            UpdateComponentsData(deterministicTime.ValueRW.storedIncomingTicksFromServer.Dequeue(),
                                group); // it will remove it so no reason for dispose method for arrays?

                            group.EntityManager.SetComponentEnabled<PlayerInputDataToUse>(localConnectionEntity[0],
                                true);
                            
                            deterministicTime.ValueRW.numTimesTickedThisFrame++;
                            group.World.PushTime(
                                new TimeData(localDeltaTime,
                                    localDeltaTime));

                            return true;
                        }
                    }

                    

                    //check if we already pushed time this frame
                    for (int i = 0; i < deterministicTime.ValueRO.numTimesTickedThisFrame; i++)
                    {
                        group.World.PopTime();
                    }
                    
                    deterministicTime.ValueRW.timeLeftToSendNextTick =
                        1f / deterministicTime.ValueRO.GameTickRate; 
                    deterministicTime.ValueRW.numTimesTickedThisFrame = 0;
                    return false;
                }

                //check if we already pushed time this frame
                for (int i = 0; i < deterministicTime.ValueRO.numTimesTickedThisFrame; i++)
                {
                    group.World.PopTime();
                }

                deterministicTime.ValueRW.numTimesTickedThisFrame = 0;
                return false;
            }

            /// <summary>
            /// Function responsible for updating PlayerInputDataToUse components based on the given RPC.
            /// The actual use of this data and its reflection in the game needs to be implemented by the user based on those components values.
            /// </summary>
            /// <param name="rpc">RPC with data for update</param>
            private void
                UpdateComponentsData(RpcBroadcastTickDataToClients rpc,
                    ComponentSystemGroup group)
            {
                var networkIDs = rpc.NetworkIDsOfAllClients;
                var inputs = rpc.GameInputsFromAllClients;

                var connectionEntities = inputDataQuery.ToEntityArray(Allocator.Temp);

                foreach (var connectionEntity in connectionEntities)
                {
                    var idExists = false;
                    var playerInputData =
                        group.EntityManager.GetComponentData<PlayerInputDataToUse>(connectionEntity);

                    for (int j = 0; j < networkIDs.Length; j++)
                    {
                        if (playerInputData.clientNetworkId == networkIDs[j])
                        {
                            idExists = true;
                            playerInputData.playerInputToApply = inputs[j];
                        }
                    }

                    if (!idExists)
                    {
                        playerInputData.isPlayerDisconnected = true;
                    }

                    group.EntityManager.SetComponentData(connectionEntity, playerInputData);
                    group.EntityManager.SetComponentEnabled<PlayerInputDataToUse>(connectionEntity, true);
                }
            }
        }

        /// <summary>
        /// Function responsible for updating all systems in the group.
        /// This is required specifically for per system validation since hashing system needs to be inserted in between other systems in the group.
        /// </summary>
        /// <param name="group">ComponentSystemGroup on which manual update is performed</param>
        void UpdateAllGroupSystems(ComponentSystemGroup group)
        {
            var groupSystems = group.GetAllSystems();
            var hashSystem = group.World.GetExistingSystem<StateHashForValidationSystem>();
            var deterministicTimeQuery = group.EntityManager.CreateEntityQuery(typeof(DeterministicSimulationTime));
            var deterministicTime = deterministicTimeQuery.GetSingleton<DeterministicSimulationTime>();
        
            DeterministicLogger.Instance.AddToClientHashDictionary(World.Name, (ulong) deterministicTime.currentClientTickToSend, "     System index in DeterministicSystemGroup:" + 0);
            hashSystem.Update(World.Unmanaged); // This check can detect if a system from outside of this ComponentSystemGroup caused desync.
            
            for (int i = 0; i < groupSystems.Length; i++)
            {
                var system = groupSystems[i];
                try
                {
                    if (i == groupSystems.Length - 3) DeterministicLogger.Instance.AddToClientHashDictionary(World.Name, (ulong) deterministicTime.currentClientTickToSend, "     System index in DeterministicSystemGroup:" + (i+1)); // Hack to properly log all system numbers
                    system.Update(World.Unmanaged);
                    if (i < groupSystems.Length - 3) // -3 because we don't need to insert hashSystem in between last 2 systems which are hashSystem and InputSendSystem
                    {
                        DeterministicLogger.Instance.AddToClientHashDictionary(World.Name, (ulong) deterministicTime.currentClientTickToSend, "     System index in DeterministicSystemGroup:" + (i+1));
                        hashSystem.Update(World.Unmanaged);
                    }
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message);
                }
        
                if (World.QuitUpdate)
                    break;
            }
        }
    }

    /// <summary>
    /// System that is used to create system groups.
    /// </summary>
    public partial struct SystemGroupsDefinitions : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var world = World.DefaultGameObjectInjectionWorld;

            world.GetOrCreateSystem<DeterministicSimulationSystemGroup>();
        }
    }
}