using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[UpdateAfter(typeof(SpawnPlayerSystem))]
[UpdateBefore(typeof(DeterministicSimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class CalculateTickSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<PlayerInputDataToSend>(); // delete
    }

    protected override void OnUpdate() // If I want to send my input I should also check if(lastServerTickRecieved < currentClientTickToSend - tickPeriod)
    {
        var deltaTime = SystemAPI.Time.DeltaTime;
        
        foreach (var (tickRateInfo, storedTicksAhead, connectionEntity) in SystemAPI.Query<RefRW<TickRateInfo>, RefRW<StoredTicksAhead>>().WithAll<GhostOwnerIsLocal, PlayerSpawned>().WithEntityAccess())
        {
            tickRateInfo.ValueRW.delayTime -= deltaTime;
            if (tickRateInfo.ValueRO.delayTime <= 0)
            {
                // 1) If current Tick to send is less or equal to tickAhead time then upgrade it and do nothing about the presentation update
                // 2) If current Tick is greater than tickAhead check if we can proceed with presentation step (if we have it in the array)
                //  if YES then proceed with both
                // if NO then disable deterministic simulation group and wait for the next tick
                // change and enable playerInputDataToUse or disable/enable DeterministicSimulation System Group

                if (tickRateInfo.ValueRO.currentClientTickToSend <= tickRateInfo.ValueRO.tickAheadValue)
                {
                    tickRateInfo.ValueRW.currentClientTickToSend++;
                    EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, true);
                }
                else
                {
                    Debug.Log("current simulation tick: " + tickRateInfo.ValueRO.currentSimulationTick);
                    Debug.Log("current tick to send: " + tickRateInfo.ValueRO.currentClientTickToSend);
                    for(int i=0; i<storedTicksAhead.ValueRO.entries.Length; i++)
                    {
                        Debug.Log("stored tick " + storedTicksAhead.ValueRO.entries[i].tick);
                        if(storedTicksAhead.ValueRO.entries[i].tick == tickRateInfo.ValueRO.currentSimulationTick + tickRateInfo.ValueRO.tickAheadValue) // Here the only problem would be if let's say 12 inputs arrived before the next one and our array is full
                        {
                            Debug.Log("found one");
                            tickRateInfo.ValueRW.currentClientTickToSend++;
                            tickRateInfo.ValueRW.currentSimulationTick++;
                            
                            UpdateComponentsData(storedTicksAhead.ValueRO.entries[i].data);
                            storedTicksAhead.ValueRW.entries[i].Dispose();
                            storedTicksAhead.ValueRW.entries[i] = new InputsFromServerOnTheGivenTick { tick = 0 };
                            EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, true);
                            EntityManager.SetComponentEnabled<PlayerInputDataToUse>(connectionEntity, true); // We are assuming that client input to Send will be always x ticks in from of the simulation one
                            
                            
                            break;
                        }
                    }
                }
                
                tickRateInfo.ValueRW.delayTime = 1f / tickRateInfo.ValueRO.tickRate;
                
                
                
                
                // check something with ahead tick???
                // we can progress if the difference between currentClientPresentationTick and clientInputTickToSend is less than the value 
                // we should increment currentClientPresentationTick first (if we can)
                // then we should compare the values
                // if we can proceed we can increase clientInputTickToSend
                
                // if the next tick from the server is ready to use then upgrade both this and the clientTickToSend
                
                
                // if current tick to send is within the approved range (simulation tick should be always smaller/equal to client tick to send) then upgrade client tick to send


                // if current tick to send is equal to the delay 

                // check if we need to change currentClientPresentationTick based on the array of ticks from server
                // if(currentClientPresentationTick + difference <= clientInputTickToSend) proceed
                
                // check if we have input from the server for the next tick
                // if we do, increase clientTick, enable Deterministic Simulation System Group (to update inputs and send new to the server)
                // if not then just disable deterministic group and reset delay time (so waiting one more tick)
                
            }
        }
    }
    
    void UpdateComponentsData(RpcPlayersDataUpdate rpc) // When do I want to refresh the screen? When input from the server arrives or together with the tick??
    {
        // Update player data based on received RPC
        NativeList<int> networkIDs = new NativeList<int>(16, Allocator.Temp);
        NativeList<Vector2> inputs = new NativeList<Vector2>(16, Allocator.Temp);
        networkIDs = rpc.NetworkIDs;
        inputs = rpc.Inputs;
        
        
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
                    playerInputData.ValueRW.horizontalInput = (int)inputs[i].x;
                    playerInputData.ValueRW.verticalInput = (int)inputs[i].y;
                    EntityManager.SetComponentEnabled<PlayerInputDataToUse>(connectionEntity, true);
                    EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, false);
                }
            }
            
            if (!idExists) //To show that the player disconnected
            {
                playerInputData.ValueRW.playerDisconnected = true;
                EntityManager.SetComponentEnabled<PlayerInputDataToUse>(connectionEntity, true);
                EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, false);
            }
        }
    }
}