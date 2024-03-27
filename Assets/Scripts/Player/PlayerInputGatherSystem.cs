using Unity.Entities;
using UnityEngine;

// This system is responsible for gathering this player's input and storing it in the PlayerInputData component
[UpdateInGroup(typeof(InputGatherSystemGroup))]
[UpdateAfter(typeof(SpawnPlayerSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class PlayerInputGatherAndSendSystem : SystemBase
{
    
    protected override void OnCreate()
    {
        RequireForUpdate<PlayerInputDataToSend>();
    }

    protected override void OnUpdate()
    {
        foreach (var (inputDataToSend, connectionReference, tickRateInfo, owner, connectionEntity) in SystemAPI.Query<RefRW<PlayerInputDataToSend>, RefRO<NetworkConnectionReference>, RefRW<TickRateInfo>, RefRO<GhostOwner>>().WithAll<PlayerSpawned>().WithEntityAccess())
        {
            int horizontalInput = Input.GetKey("left") ? -1 : Input.GetKey("right") ? 1 : 0;
            int verticalInput = Input.GetKey("down") ? -1 : Input.GetKey("up") ? 1 : 0;

            inputDataToSend.ValueRW.horizontalInput = horizontalInput;
            inputDataToSend.ValueRW.verticalInput = verticalInput;
            
            RpcBroadcastPlayerInputToServer rpc = new RpcBroadcastPlayerInputToServer
            {
                playerInput = new Vector2(inputDataToSend.ValueRO.horizontalInput, inputDataToSend.ValueRO.verticalInput),
                currentTick = tickRateInfo.ValueRO.currentTick,
                connectionID = tickRateInfo.ValueRO.currentTick
            };
            
            rpc.Serialize(connectionReference.ValueRO.Driver, connectionReference.ValueRO.Connection, owner.ValueRO.networkId, connectionReference.ValueRO.SimulatorPipeline);
            tickRateInfo.ValueRW.currentTick++;
            EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, false);
        }
    }
}
