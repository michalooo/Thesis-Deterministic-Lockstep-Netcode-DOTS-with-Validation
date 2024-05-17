using Unity.Core;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// System responsible for calculating the next tick to send to the server and deciding if and how many ticks we should process.
    /// </summary>
    [UpdateBefore(typeof(DeterministicSimulationSystemGroup))]
    [UpdateAfter(typeof(UserSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class CalculateTickSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PlayerSpawned>(); // Start to tick directly after players are spawned
        }

        protected override void OnUpdate()
        {
            var deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (tickRateInfo, storedTicksAhead, connectionEntity) in SystemAPI
                         .Query<RefRW<TickRateInfo>, RefRW<StoredTicksAhead>>()
                         .WithAll<GhostOwnerIsLocal, PlayerSpawned>().WithEntityAccess())
            {
                tickRateInfo.ValueRW.delayTime -= deltaTime;
                if (tickRateInfo.ValueRO.delayTime <= 0) // We are ready to try to send the next tick
                {
                    var determinismSystemGroup = World.DefaultGameObjectInjectionWorld
                        .GetOrCreateSystemManaged<DeterministicSimulationSystemGroup>();

                    if (tickRateInfo.ValueRO.currentClientTickToSend <=
                        tickRateInfo.ValueRO
                            .tickAheadValue) // If current Tick to send is less or equal to tickAhead then upgrade it and do nothing about the presentation update (it should mean we are processing those first ticks)
                    {
                        tickRateInfo.ValueRW.currentClientTickToSend++;
                        EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, true);
                        determinismSystemGroup.Update(); // maybe worth checking if it's ok that this system will still try to run after this
                    }
                    else if
                        (storedTicksAhead.ValueRO.entries.Count >
                         0) // otherwise we can try to proceed with simulation steps (different than presentation steps)
                    {
                        // Do we need to check if the tick we want to process next is in the queue or it's reliable and in order?
                        // #TODO implement some restriction regarding how many ticks should we process in one frame
                        // while (storedTicksAhead.ValueRO.entries.Count >
                        //        tickRateInfo.ValueRO.tickAheadValue) // We are trying to catch up with the server
                        // {
                        //     // If we found on we can increment both ticks (current presentation tick and tick we will send to server)
                        //     tickRateInfo.ValueRW.currentClientTickToSend++;
                        //     tickRateInfo.ValueRW.currentSimulationTick++;
                        //
                        //     // first update the component data before we will remove the info from the array to make space for more
                        //     UpdateComponentsData(storedTicksAhead.ValueRW.entries
                        //         .Dequeue()); // it will remove it so no reason for dispose method for arrays?
                        //
                        //     EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, true);
                        //     EntityManager.SetComponentEnabled<PlayerInputDataToUse>(connectionEntity, true);
                        //
                        //     //TODO move this logic to IRate
                        //     // World.PushTime(new TimeData(elapsedTime: 0f,
                        //     //     deltaTime: 1f / tickRateInfo.ValueRO.tickRate));
                        //     determinismSystemGroup.Update();
                        //     // World.PopTime();
                        // }
                    }

                    // If the tick to present wasn't found we are stopping to wait for inputs which just mean that PlayerInputDataToSend and PlayerInputDataToUse won't be enabled and used by other systems
                    tickRateInfo.ValueRW.delayTime =
                        1f / tickRateInfo.ValueRO.tickRate; // reset the time until next tick
                }
            }
        }


        /// <summary>
        /// Function responsible for updating the player components based on the given RPC. Needs to be implemented by the user
        /// </summary>
        /// <param name="rpc">RPC with data for update</param>
        private void
            UpdateComponentsData(
                RpcPlayersDataUpdate rpc) // When do I want to refresh the screen? When input from the server arrives or together with the tick??
        {
            // NativeQueue<>
            var networkIDs = rpc.NetworkIDs;
            var inputs = rpc.PlayersCapsuleGameInputs;
        
            foreach (var (playerInputData, connectionEntity) in SystemAPI
                         .Query<RefRW<PlayerInputDataToUse>>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                         .WithAll<PlayerInputDataToSend>().WithEntityAccess())
            {
                var idExists = false;
                for (int i = 0; i < networkIDs.Length; i++)
                {
                    if (playerInputData.ValueRO.playerNetworkId == networkIDs[i])
                    {
                        idExists = true;
                        playerInputData.ValueRW.inputToUse = inputs[i];
                    }
                }
        
                if (!idExists)
                {
                    playerInputData.ValueRW.playerDisconnected = true;
                }
        
                EntityManager.SetComponentEnabled<PlayerInputDataToUse>(connectionEntity, true);
                EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, false);
            }
        }
    }
}