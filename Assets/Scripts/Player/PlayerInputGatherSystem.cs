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
        foreach (var (inputDataToSend, connectionReference, tickRateInfo, connectionEntity) in SystemAPI.Query<RefRW<PlayerInputDataToSend>, RefRO<NetworkConnectionReference>, RefRW<TickRateInfo>>().WithAll<PlayerSpawned>().WithEntityAccess())
        {
            int horizontalInput = Input.GetKey("left") ? -1 : Input.GetKey("right") ? 1 : 0;
            int verticalInput = Input.GetKey("down") ? -1 : Input.GetKey("up") ? 1 : 0;

            inputDataToSend.ValueRW.horizontalInput = horizontalInput;
            inputDataToSend.ValueRW.verticalInput = verticalInput;
            
            RpcBroadcastPlayerInputToServer rpc = new RpcBroadcastPlayerInputToServer
            {
                PlayerInput = new Vector2(inputDataToSend.ValueRO.horizontalInput, inputDataToSend.ValueRO.verticalInput),
                CurrentTick = tickRateInfo.ValueRO.currentTick,
            };
            
            rpc.HashForCurrentTick = tickRateInfo.ValueRO.hashForTheTick; // setting hash
            rpc.Serialize(connectionReference.ValueRO.Driver, connectionReference.ValueRO.Connection, connectionReference.ValueRO.SimulatorPipeline);
            tickRateInfo.ValueRW.currentTick++;
            EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, false);
        }
    }
}
