using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// System group that contains all of user defined systems which are not affecting state of the game.
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(ConnectionHandleSystemGroup))]
    public partial class UserSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// System group that contains connection handle systems.
    /// </summary>
    [BurstCompile]
    public partial class ConnectionHandleSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// System group that contains deterministic simulation systems. Systems that are using it are PlayerUpdateSystem, DeterminismSystemCheck, and PlayerInputGatherAndSendSystem.
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(UserSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class DeterministicSimulationSystemGroup : ComponentSystemGroup
    {
        private static float LocalDeltaTime = 1.0f/60.0f;
        private const int MaxTicksPerFrame = 10;

        protected override void OnCreate()
        {
            base.OnCreate();
            RateManager = new DeterministicFixedStepRateManager(this);
            
            EntityManager.CreateSingletonBuffer<DeterministicComponent>();
            var client = SystemAPI.GetSingletonBuffer<DeterministicComponent>();
            client.Add(new DeterministicComponent
            {
                Type = ComponentType.ReadOnly<LocalTransform>(),
            });
            client.Add(new DeterministicComponent
            {
                Type = ComponentType.ReadOnly<DeterministicSettings>(),
            });
            
            EntityManager.CreateSingleton(new DeterministicTime()
            {
                storedIncomingTicksFromServer = new NativeQueue<RpcBroadcastTickDataToClients>(Allocator.Persistent),
                hashesForTheCurrentTick = new NativeList<ulong>(Allocator.Persistent),
            });
            EntityManager.CreateSingleton<PongInputs>();
        }

        protected override void OnDestroy()
        {
            if (!SystemAPI.TryGetSingletonRW<DeterministicTime>(out var deterministicTime))
            {
                deterministicTime.ValueRW.storedIncomingTicksFromServer.Dispose();
                deterministicTime.ValueRW.hashesForTheCurrentTick.Dispose();
            }
        }

        protected override void OnUpdate()
        {
            LocalDeltaTime = 1.0f/(float)SystemAPI.GetSingleton<DeterministicTime>().GameTickRate;
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

        public struct DeterministicFixedStepRateManager : IRateManager
        {
            private EntityQuery _deterministicTimeQuery;
            private EntityQuery _connectionQuery;
            private EntityQuery _inputDataQuery;
            private EntityQuery _deterministicClientQuery;
            private bool wasLogging;

            public DeterministicFixedStepRateManager(ComponentSystemGroup group) : this()
            {
                _deterministicTimeQuery = group.EntityManager.CreateEntityQuery(typeof(DeterministicTime));
                _deterministicClientQuery = group.EntityManager.CreateEntityQuery(typeof(DeterministicClientComponent));
                _connectionQuery =
                    group.EntityManager.CreateEntityQuery(
                        typeof(GhostOwnerIsLocal)); // This component will only be created when RPC to start game was send
                _inputDataQuery = group.EntityManager.CreateEntityQuery(typeof(GhostOwner));
                wasLogging = false;
            }

            public bool ShouldGroupUpdate(ComponentSystemGroup group)
            {
                var deterministicClient = _deterministicClientQuery.GetSingleton<DeterministicClientComponent>();
                if (deterministicClient.deterministicClientWorkingMode != DeterministicClientWorkingMode.RunDeterministicSimulation)
                    return false;
                
                
                var deltaTime = (double) group.World.Time.DeltaTime;
                var deterministicTime = _deterministicTimeQuery.GetSingletonRW<DeterministicTime>();
                var localConnectionEntity = _connectionQuery.ToEntityArray(Allocator.Temp);
                
                // var currentLocalTime = DateTime.UtcNow.TimeOfDay;
                // var howMuchTimePassedSinceReceivingRPCAboutGameStart = currentLocalTime.TotalMilliseconds -
                //                             deterministicTime.ValueRO.localTimestampAtTheMomentOfSynchronizationUTC.TotalMilliseconds;
                // var predictedCurrentServerTime = deterministicTime.ValueRO.serverTimestampUTC.TotalMilliseconds + howMuchTimePassedSinceReceivingRPCAboutGameStart;
                // var predictedServerTimeWhenSimulationShouldStart = deterministicTime.ValueRO.serverTimestampUTC.TotalMilliseconds - deterministicTime.ValueRO.playerAveragePing*10 + deterministicTime.ValueRO.timeToPostponeStartofSimulationInMiliseconds;
                
                // need to wait for the delay to start at the same moment as other clients
                // if (predictedCurrentServerTime < predictedServerTimeWhenSimulationShouldStart)
                // {
                //     // Debug.LogError("Before: " + elapsedLocalMilliseconds + " " + deterministicTime.ValueRO.timeToPostponeStartofSimulationInMiliseconds);
                //     return false;
                // }
                // else if (!wasLogging)
                // {
                //     wasLogging = true;
                //     // Debug.LogError("Server time when sending on client: " + deterministicTime.ValueRO.serverTimestampUTC);
                //     // Debug.LogError("Time to wait on client: " + deterministicTime.ValueRO.timeToPostponeStartofSimulationInMiliseconds);
                //     // Debug.LogError("Predicted server time when game should start: " + TimeSpan.FromMilliseconds(deterministicTime.ValueRO.serverTimestampUTC.TotalMilliseconds + deterministicTime.ValueRO.timeToPostponeStartofSimulationInMiliseconds));
                //     // Debug.LogError("After: " + elapsedLocalMilliseconds + " " + deterministicTime.ValueRO.timeToPostponeStartofSimulationInMiliseconds);
                // }
                
                // Debug.LogError("Delta time: " + deltaTime + ", how many times ticked this frame: " + deterministicTime.ValueRO.numTimesTickedThisFrame + ", time left to send next tick: " + deterministicTime.ValueRO.timeLeftToSendNextTick + ", tick: " +deterministicTime.ValueRO.currentClientTickToSend);
                
                
                var isTimeToSendNextTick = false;
                if (deterministicTime.ValueRO.numTimesTickedThisFrame >=
                    MaxTicksPerFrame) // If we already ticked maximum times this frame
                {
                    isTimeToSendNextTick = false;
                    deterministicTime.ValueRW.timeLeftToSendNextTick += LocalDeltaTime;
                }
                else if (deterministicTime.ValueRO.timeLeftToSendNextTick > deltaTime) // If we should wait
                {
                    isTimeToSendNextTick = false;
                    deterministicTime.ValueRW.timeLeftToSendNextTick -= deltaTime;
                }
                else if (deterministicTime.ValueRO.timeLeftToSendNextTick <= deltaTime) // either lower on + or on -
                {
                    if (deterministicTime.ValueRO.timeLeftToSendNextTick >= 0) // If lets say deltaTime=16 and time=12
                    {
                        isTimeToSendNextTick = true;
                        deterministicTime.ValueRW.timeLeftToSendNextTick -= deltaTime;
                    }
                    else // in this case time < 0 which means that for example deltaTime was 60 and time was 10 (so now we are -50)
                    {
                        if (deterministicTime.ValueRW.timeLeftToSendNextTick + deltaTime > 0) // if time=-14 and delta=16 it means that the result should be 2
                        {
                            isTimeToSendNextTick = false;
                            deterministicTime.ValueRW.timeLeftToSendNextTick += deltaTime;
                        }
                        else // tick otherwise
                        {
                            isTimeToSendNextTick = true;
                            deterministicTime.ValueRW.timeLeftToSendNextTick +=  deltaTime;
                        }
                    }
                }

                // Debug.LogError("Should group update: " + isTimeToSendNextTick + ", tick: " +
                // deterministicTime.ValueRO.currentClientTickToSend);
                
                //-----------------------------------
                
                
                if (isTimeToSendNextTick) // We should  try to send the next tick
                {
                    if (deterministicTime.ValueRO.currentClientTickToSend <=
                        deterministicTime.ValueRO
                            .forcedInputLatencyDelay) // If current Tick to send is less or equal to tickAhead then upgrade it and do nothing about the presentation update (it should mean we are processing those first ticks)
                    {
                        deterministicTime.ValueRW.currentClientTickToSend++;

                        deterministicTime.ValueRW.deterministicLockstepElapsedTime += deltaTime;
                        deterministicTime.ValueRW.numTimesTickedThisFrame++;
                        group.World.PushTime(
                            new TimeData(deterministicTime.ValueRO.deterministicLockstepElapsedTime,
                                LocalDeltaTime));

                        // Debug.LogError("Current tick to send: " + deterministicTime.ValueRO.currentClientTickToSend +
                        //                " forced input latency delay: " +
                        //                deterministicTime.ValueRO.forcedInputLatencyDelay);
                        // Debug.Log("New time: " + deterministicTime.ValueRO.timeLeftToSendNextTick);
                        return true;
                    }
                    
                    
                    var hasInputsForThisTick = deterministicTime.ValueRO.storedIncomingTicksFromServer.Count > 0;

                    if (hasInputsForThisTick)
                    {
                        // If we found on we can increment both ticks (current presentation tick and tick we will send to server)
                        deterministicTime.ValueRW.currentSimulationTick++;
                        deterministicTime.ValueRW.currentClientTickToSend++;

                        // first update the component data before we will remove the info from the array to make space for more
                        UpdateComponentsData(deterministicTime.ValueRW.storedIncomingTicksFromServer.Dequeue(),
                            group); // it will remove it so no reason for dispose method for arrays?

                        group.EntityManager.SetComponentEnabled<PlayerInputDataToUse>(localConnectionEntity[0],
                            true);

                        deterministicTime.ValueRW.deterministicLockstepElapsedTime += deltaTime;
                        deterministicTime.ValueRW.numTimesTickedThisFrame++;
                        group.World.PushTime(
                            new TimeData(deterministicTime.ValueRO.deterministicLockstepElapsedTime,
                                LocalDeltaTime));

                        return true;
                    }

                    //check if we already pushed time this frame
                    for (int i = 0; i < deterministicTime.ValueRO.numTimesTickedThisFrame; i++)
                    {
                        group.World.PopTime();
                    }

                    // If the tick to present wasn't found we are stopping to wait for inputs which just mean that PlayerInputDataToSend and PlayerInputDataToUse won't be enabled and used by other systems
                    deterministicTime.ValueRW.timeLeftToSendNextTick =
                        1f / deterministicTime.ValueRO.GameTickRate; // reset the time until next tick
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

            public float Timestep { get; set; }

            /// <summary>
            /// Function responsible for updating the player components based on the given RPC. Needs to be implemented by the user
            /// </summary>
            /// <param name="rpc">RPC with data for update</param>
            private void
                UpdateComponentsData(RpcBroadcastTickDataToClients rpc,
                    ComponentSystemGroup group) // When do I want to refresh the screen? When input from the server arrives or together with the tick??
            {
                var networkIDs = rpc.NetworkIDs;
                var inputs = rpc.PlayersPongGameInputs;

                var connectionEntities = _inputDataQuery.ToEntityArray(Allocator.Temp);

                foreach (var connectionEntity in connectionEntities)
                {
                    var idExists = false;
                    var playerInputData =
                        group.EntityManager.GetComponentData<PlayerInputDataToUse>(connectionEntity);

                    for (int j = 0; j < networkIDs.Length; j++)
                    {
                        if (playerInputData.playerNetworkId == networkIDs[j])
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

        void UpdateAllGroupSystems(ComponentSystemGroup group)
        {
            // assumption that we are talking only about unmanaged systems
            var systems = group.GetAllSystems();
            var determinismCheckSystem = group.World.GetExistingSystem<DeterminismCheckSystem>();
        
            for (int i = 0; i < systems.Length; i++)
            {
                var system = systems[i];
                try
                {
                    if (i <= systems.Length - 4)
                    {
                        system.Update(World.Unmanaged);
                        determinismCheckSystem.Update(World.Unmanaged);
                    }
                    else
                    {
                        system.Update(World.Unmanaged);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
        
                if (World.QuitUpdate)
                    break;
            }
        }
    }

    public partial struct SystemGroupsDefinitions : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var world = World.DefaultGameObjectInjectionWorld;

            world.GetOrCreateSystem<DeterministicSimulationSystemGroup>();
            world.GetOrCreateSystem<ConnectionHandleSystemGroup>();
            world.GetOrCreateSystem<UserSystemGroup>();
        }
    }
}