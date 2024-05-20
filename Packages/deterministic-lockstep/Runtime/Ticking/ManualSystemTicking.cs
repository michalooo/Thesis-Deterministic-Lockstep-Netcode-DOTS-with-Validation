using System;
using Unity.Collections;
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
        protected override void OnCreate()
        {
            base.OnCreate();
            RateManager = new DeterministicFixedStepRateManager(this);
            EntityManager.CreateSingleton(new DeterministicTime()
            {
                storedIncomingTicksFromServer = new NativeQueue<RpcPlayersDataUpdate>(Allocator.Persistent),
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

        // protected override void OnUpdate()
        // {
        //     // if (RateManager.ShouldGroupUpdate(this))
        //     // {
        //     //     // CheckGroupCreated();
        //     //     // UpdateAllGroupSystems(); TODO implement those to allow for inserting deterministic check after each system
        //     //     base.OnUpdate();
        //     // }
        // }

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

                if (localConnectionEntity.Length == 0) return false; // before rpc with start game was received
                
                deterministicTime.ValueRW.timeLeftToSendNextTick -= deltaTime;
                if (deterministicTime.ValueRO.timeLeftToSendNextTick <= 0) // We are ready to try to send the next tick
                {
                    if (deterministicTime.ValueRO.currentClientTickToSend <=
                        deterministicTime.ValueRO
                            .AmountOfTicksSendingAhead) // If current Tick to send is less or equal to tickAhead then upgrade it and do nothing about the presentation update (it should mean we are processing those first ticks)
                    {
                        deterministicTime.ValueRW.currentClientTickToSend++;
                        group.EntityManager.SetComponentEnabled<PlayerInputDataToSend>(localConnectionEntity[0], true); // DO I need this component?

                        // If the tick to present wasn't found we are stopping to wait for inputs which just mean that PlayerInputDataToSend and PlayerInputDataToUse won't be enabled and used by other systems
                        deterministicTime.ValueRW.timeLeftToSendNextTick =
                            1f / deterministicTime.ValueRO.GameTickRate; // reset the time until next tick

                        
                        const float localDeltaTime = 1.0f / 60.0f;
                        deterministicTime.ValueRW.deterministicLockstepElapsedTime += deltaTime;
                        deterministicTime.ValueRW.numTimesTickedThisFrame++;
                        group.World.PushTime(
                            new TimeData(deterministicTime.ValueRO.deterministicLockstepElapsedTime,
                                localDeltaTime));
                        Debug.Log("tick: " + deterministicTime.ValueRO.currentSimulationTick + " tick to send: " + deterministicTime.ValueRO.currentClientTickToSend + " how many ticked: " + deterministicTime.ValueRO.numTimesTickedThisFrame + " how many ticks to send ahead: " + deterministicTime.ValueRO.AmountOfTicksSendingAhead + " How many stored ticks we have: " + deterministicTime.ValueRO.storedIncomingTicksFromServer.Count);
                        return true;
                    }
                    
                    if (deterministicTime.ValueRO.numTimesTickedThisFrame <
                        10) // restriction to prevent too expensive loop
                    {
                        var hasInputsForThisTick = deterministicTime.ValueRO.storedIncomingTicksFromServer.Count >=
                                                   deterministicTime.ValueRO.AmountOfTicksSendingAhead - 1;
                        
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

                                group.EntityManager.SetComponentEnabled<PlayerInputDataToSend>(localConnectionEntity[0],
                                    true);
                                group.EntityManager.SetComponentEnabled<PlayerInputDataToUse>(localConnectionEntity[0],
                                    true);

                                const float localDeltaTime = 1.0f / 60.0f;
                                deterministicTime.ValueRW.deterministicLockstepElapsedTime += deltaTime;
                                deterministicTime.ValueRW.numTimesTickedThisFrame++;
                                group.World.PushTime(
                                    new TimeData(deterministicTime.ValueRO.deterministicLockstepElapsedTime,
                                        localDeltaTime));
                                
                                Debug.Log("tick: " + deterministicTime.ValueRO.currentSimulationTick + " tick to send: " + deterministicTime.ValueRO.currentClientTickToSend + " how many ticked: " + deterministicTime.ValueRO.numTimesTickedThisFrame + " how many ticks to send ahead: " + deterministicTime.ValueRO.AmountOfTicksSendingAhead + " How many stored ticks we have: " + deterministicTime.ValueRO.storedIncomingTicksFromServer.Count);

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
                    
                    Debug.Log("tick: " + deterministicTime.ValueRO.currentSimulationTick + " tick to send: " + deterministicTime.ValueRO.currentClientTickToSend + " how many ticked: " + deterministicTime.ValueRO.numTimesTickedThisFrame + " how many ticks to send ahead: " + deterministicTime.ValueRO.AmountOfTicksSendingAhead + " How many stored ticks we have: " + deterministicTime.ValueRO.storedIncomingTicksFromServer.Count);

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
                UpdateComponentsData(RpcPlayersDataUpdate rpc,
                    ComponentSystemGroup group) // When do I want to refresh the screen? When input from the server arrives or together with the tick??
            {
                // NativeQueue<>
                var networkIDs = rpc.NetworkIDs;
                var inputs = rpc.PlayersCapsuleGameInputs;

                var connectionEntities = _inputDataQuery.ToEntityArray(Allocator.Temp);

                for (int i = 0; i < connectionEntities.Length; i++)
                {
                    var idExists = false;
                    var playerInputData =
                        group.EntityManager.GetComponentData<PlayerInputDataToUse>(connectionEntities[i]);

                    for (int j = 0; j < networkIDs.Length; j++)
                    {
                        if (playerInputData.playerNetworkId == networkIDs[j])
                        {
                            idExists = true;
                            playerInputData.inputToUse = inputs[j];
                        }
                    }

                    if (!idExists)
                    {
                        playerInputData.playerDisconnected = true;
                    }

                    group.EntityManager.SetComponentData(connectionEntities[i], playerInputData);
                    group.EntityManager.SetComponentEnabled<PlayerInputDataToUse>(connectionEntities[i], true);
                    group.EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntities[i], false);
                }
            }

            // private void CheckGroupCreated(ComponentSystemGroup group)
            // {
            //     if (!group.Created)
            //         throw new InvalidOperationException(
            //             $"Group of type {group.GetType()} has not been created, either the derived class forgot to call base.OnCreate(), or it has been destroyed");
            // }

            // void UpdateAllGroupSystems(ComponentSystemGroup group)
            // {
            //     if (group.m_systemSortDirty)
            //         SortSystems();
            //
            //     // Update all unmanaged and managed systems together, in the correct sort order.
            //     // The master update list contains indices for both managed and unmanaged systems.
            //     // Negative values indicate an index in the unmanaged system list.
            //     // Positive values indicate an index in the managed system list.
            //     var world = World.Unmanaged;
            //     ref var worldImpl = ref world.GetImpl();
            //
            //     // Cache the update list length before updating; any new systems added mid-loop will change the length and
            //     // should not be processed until the subsequent group update, to give SortSystems() a chance to run.
            //     int updateListLength = m_MasterUpdateList.Length;
            //     for (int i = 0; i < updateListLength; ++i)
            //     {
            //         try
            //         {
            //             var index = m_MasterUpdateList[i];
            //
            //             if (!index.IsManaged)
            //             {
            //                 // Update unmanaged (burstable) code.
            //                 var handle = m_UnmanagedSystemsToUpdate[index.Index];
            //                 worldImpl.UpdateSystem(handle);
            //             }
            //             else
            //             {
            //                 // Update managed code.
            //                 var sys = m_managedSystemsToUpdate[index.Index];
            //                 sys.Update();
            //             }
            //         }
            //         catch (Exception e)
            //         {
            //             Debug.LogException(e);
            //         }
            //
            //         if (World.QuitUpdate)
            //             break;
            //     }
            // }
        }
    }


    /// <summary>
    /// System group that is used for any game logic stuff (can be ticked when rolling back or catching up).
    /// </summary>
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup), OrderFirst = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class GameStateUpdateSystemGroup : ComponentSystemGroup
    {
    }

    public partial struct ManualSystemTicking : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var world = World.DefaultGameObjectInjectionWorld;

            world.GetOrCreateSystem<DeterministicSimulationSystemGroup>();
            world.GetOrCreateSystem<ConnectionHandleSystemGroup>();
            world.GetOrCreateSystem<GameStateUpdateSystemGroup>();
            world.GetOrCreateSystem<UserSystemGroup>();
        }
    }

    public struct DeterministicTime : IComponentData
    {
        /// <summary>
        /// Variable storing the elapsed time which is used to control system groups
        /// </summary>
        public double deterministicLockstepElapsedTime; //seconds same as ElapsedTime

        /// <summary>
        /// Variable storing the time that has passed since the last frame
        /// </summary>
        public float realTime;

        /// <summary>
        /// Variable storing information of how many ticks we already processed for the current frame
        /// </summary>
        public int numTimesTickedThisFrame;

        /// <summary>
        /// Set constant value of what's the tick rate of the game
        /// </summary>
        public int GameTickRate;

        /// <summary>
        /// Set constant value of how many ticks ahead client is sending its data
        /// </summary>
        public int AmountOfTicksSendingAhead;

        /// <summary>
        /// Variable that is used to calculate time before processing next tick
        /// </summary>
        public float timeLeftToSendNextTick;

        /// <summary>
        /// variable that takes count of which tick is being visually processed on the client
        /// </summary>
        public int currentSimulationTick;

        /// <summary>
        /// Variable that takes count of the current tick that we are sending to the server (future tick).
        /// </summary>
        public int currentClientTickToSend;

        /// <summary>
        /// Calculated hash for the current tick
        /// </summary>
        public ulong hashForTheCurrentTick; // maybe can be deleted

        /// <summary>
        /// Queue of RPCs that are received from the server with all clients inputs for a given tick.
        /// </summary>
        public NativeQueue<RpcPlayersDataUpdate> storedIncomingTicksFromServer; // be sure that there is no memory leak
    }
}