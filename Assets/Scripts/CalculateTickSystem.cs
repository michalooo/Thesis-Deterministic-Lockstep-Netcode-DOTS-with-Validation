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
        RequireForUpdate<PlayerSpawned>(); // Start to tick directly after players are spawned
    }

    protected override void OnUpdate()
    {
        var deltaTime = SystemAPI.Time.DeltaTime;
        
        foreach (var (tickRateInfo, storedTicksAhead, connectionEntity) in SystemAPI.Query<RefRW<TickRateInfo>, RefRW<StoredTicksAhead>>().WithAll<GhostOwnerIsLocal, PlayerSpawned>().WithEntityAccess())
        {
            tickRateInfo.ValueRW.delayTime -= deltaTime;
            if (tickRateInfo.ValueRO.delayTime <= 0) // We are ready to try to send the next tick
            {
                if (tickRateInfo.ValueRO.currentClientTickToSend <= tickRateInfo.ValueRO.tickAheadValue) // If current Tick to send is less or equal to tickAhead then upgrade it and do nothing about the presentation update (it should mean we are processing those first ticks)
                {
                    tickRateInfo.ValueRW.currentClientTickToSend++;
                    EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, true);
                }
                else // otherwise we can try to proceed with presentation step
                {
                    for(int i=0; i<storedTicksAhead.ValueRO.entries.Length; i++) // Let's see if the tick we would like to present is already in the array
                    {
                        if(storedTicksAhead.ValueRO.entries[i].tick == tickRateInfo.ValueRO.currentSimulationTick + tickRateInfo.ValueRO.tickAheadValue) // Here the only problem would be if let's say 12 inputs arrived before the next one and our array is full
                        { 
                            // If we found on we can increment both ticks (current presentation tick and tick we will send to server)
                            tickRateInfo.ValueRW.currentClientTickToSend++;
                            tickRateInfo.ValueRW.currentSimulationTick++;
                            
                            UpdateComponentsData(storedTicksAhead.ValueRO.entries[i].data); // first update the component data before we will remove the info from the array to make space for more
                            storedTicksAhead.ValueRW.entries[i].Dispose();
                            storedTicksAhead.ValueRW.entries[i] = new InputsFromServerOnTheGivenTick { tick = 0 };
                            EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, true);
                            EntityManager.SetComponentEnabled<PlayerInputDataToUse>(connectionEntity, true); // We are assuming that client input to Send will be always x ticks in front of the simulation one (because we are upgrading them both)
                            break;
                        }
                    }
                }
                // If the tick to present wasn't found we are stopping to wait for inputs which just mean that PlayerInputDataToSend and PlayerInputDataToUse won't be enabled and used by other systems
                tickRateInfo.ValueRW.delayTime = 1f / tickRateInfo.ValueRO.tickRate; // reset the time until next tick
            }
        }
    }
    
    
    /// <param name="rpc"></param>
    // Update player data based on received RPC
    private void UpdateComponentsData(RpcPlayersDataUpdate rpc) // When do I want to refresh the screen? When input from the server arrives or together with the tick??
    {
        // NativeQueue<>
        var networkIDs = rpc.NetworkIDs;
        var inputs = rpc.Inputs;
        
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
                    
                }
            }
            
            if (!idExists) // To show that the player disconnected
            {
                playerInputData.ValueRW.playerDisconnected = true;
            }
            
            EntityManager.SetComponentEnabled<PlayerInputDataToUse>(connectionEntity, true);
            EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, false);
        }
    }
}