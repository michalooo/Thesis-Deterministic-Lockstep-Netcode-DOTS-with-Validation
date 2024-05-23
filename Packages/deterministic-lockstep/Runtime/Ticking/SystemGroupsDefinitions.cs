using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// System group that contains all of user defined systems which are not affecting state of the game.
    /// </summary>
    [UpdateAfter(typeof(ConnectionHandleSystemGroup))]
    public partial class UserSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// System group that contains connection handle systems.
    /// </summary>
    public partial class ConnectionHandleSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// System group that contains deterministic simulation systems. Systems that are using it are PlayerUpdateSystem, DeterminismSystemCheck, and PlayerInputGatherAndSendSystem.
    /// </summary>
    [UpdateAfter(typeof(UserSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class DeterministicSimulationSystemGroup : ComponentSystemGroup
    {
        private const float LocalDeltaTime = 1.0f / 60.0f;
        private const int MaxTicksPerFrame = 10;

        protected override void OnCreate()
        {
            base.OnCreate();
            RateManager = new DeterministicFixedStepRateManager(this);
            EntityManager.CreateSingleton(new DeterministicTime()
            {
                storedIncomingTicksFromServer = new NativeQueue<RpcBroadcastTickDataToClients>(Allocator.Persistent),
            });
            EntityManager.CreateSingleton<PongInputs>();
        }

        protected override void OnDestroy()
        {
            if (!SystemAPI.TryGetSingletonRW<DeterministicTime>(out var value))
            {
                value.ValueRW.storedIncomingTicksFromServer.Dispose();
            }
        }

        protected override void OnUpdate()
        {
            if (SystemAPI.GetSingleton<DeterministicSettings>().hashCalculationOption !=
                DeterminismHashCalculationOption.PerSystem)
            {
                base.OnUpdate();
            }
            else
            {
                while (RateManager.ShouldGroupUpdate(this))
                {
                    UpdateAllGroupSystems(this);
                }
            }
        }

        public struct DeterministicFixedStepRateManager : IRateManager
        {
            private EntityQuery _deterministicTimeQuery;
            private EntityQuery _connectionQuery;
            private EntityQuery _inputDataQuery;

            public DeterministicFixedStepRateManager(ComponentSystemGroup group) : this()
            {
                _deterministicTimeQuery = group.EntityManager.CreateEntityQuery(typeof(DeterministicTime));
                _connectionQuery =
                    group.EntityManager.CreateEntityQuery(
                        typeof(GhostOwnerIsLocal)); // This component will only be created when RPC to start game was send
                _inputDataQuery = group.EntityManager.CreateEntityQuery(typeof(GhostOwner));
            }

            public bool ShouldGroupUpdate(ComponentSystemGroup group)
            {
                var deltaTime = group.World.Time.DeltaTime;
                var deterministicTime = _deterministicTimeQuery.GetSingletonRW<DeterministicTime>();
                var localConnectionEntity = _connectionQuery.ToEntityArray(Allocator.Temp);

                if (localConnectionEntity.Length == 0)
                {
                    return false; // before rpc with start game was received
                }

                deterministicTime.ValueRW.timeLeftToSendNextTick -= deltaTime;
                if (deterministicTime.ValueRO.timeLeftToSendNextTick <= 0) // We are ready to try to send the next tick
                {
                    if (deterministicTime.ValueRO.currentClientTickToSend <=
                        deterministicTime.ValueRO
                            .forcedInputLatencyDelay) // If current Tick to send is less or equal to tickAhead then upgrade it and do nothing about the presentation update (it should mean we are processing those first ticks)
                    {
                        deterministicTime.ValueRW.currentClientTickToSend++;

                        // If the tick to present wasn't found we are stopping to wait for inputs which just mean that PlayerInputDataToSend and PlayerInputDataToUse won't be enabled and used by other systems
                        deterministicTime.ValueRW.timeLeftToSendNextTick =
                            1f / deterministicTime.ValueRO.GameTickRate; // reset the time until next tick


                        deterministicTime.ValueRW.deterministicLockstepElapsedTime += deltaTime;
                        deterministicTime.ValueRW.numTimesTickedThisFrame++;
                        group.World.PushTime(
                            new TimeData(deterministicTime.ValueRO.deterministicLockstepElapsedTime,
                                LocalDeltaTime));
                        return true;
                    }

                    if (deterministicTime.ValueRO.numTimesTickedThisFrame <
                        MaxTicksPerFrame) // restriction to prevent too expensive loop
                    {
                        var hasInputsForThisTick = deterministicTime.ValueRO.storedIncomingTicksFromServer.Count >=
                                                   deterministicTime.ValueRO.forcedInputLatencyDelay - 1;

                        if (hasInputsForThisTick)
                        {
                            if (deterministicTime.ValueRO.deterministicLockstepElapsedTime <
                                group.World.Time
                                    .ElapsedTime) // if it has inputs for the frame. automatically handles question of how many ticks for now
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
                        }
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