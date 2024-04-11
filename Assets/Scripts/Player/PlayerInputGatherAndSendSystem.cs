using Unity.Entities;
using UnityEngine;

// This system is responsible for gathering this player's input and storing it in the PlayerInputData component
[UpdateInGroup(typeof(DeterministicSimulationSystemGroup), OrderLast = true)]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class PlayerInputGatherAndSendSystem : SystemBase
{
    
    protected override void OnCreate()
    {
        RequireForUpdate<PlayerInputDataToSend>();
    }

    protected override void OnUpdate()
    {
        foreach (var (inputDataToSend, connectionReference, tickRateInfo, connectionEntity) in SystemAPI.Query<RefRW<PlayerInputDataToSend>, RefRO<NetworkConnectionReference>, RefRW<TickRateInfo>>().WithAll<PlayerSpawned, GhostOwnerIsLocal>().WithEntityAccess())
        {
            int horizontalInput;
            int verticalInput;

            if (World.Name == "ClientWorld2")
            {
                horizontalInput = Input.GetKey(KeyCode.A) ? -1 : Input.GetKey(KeyCode.D) ? 1 : 0;
                verticalInput = Input.GetKey(KeyCode.S) ? -1 : Input.GetKey(KeyCode.W) ? 1 : 0;
            }
            else if (World.Name != "ClientWorld3")
            {
                horizontalInput = Input.GetKey(KeyCode.LeftArrow) ? -1 : Input.GetKey(KeyCode.RightArrow) ? 1 : 0;
                verticalInput = Input.GetKey(KeyCode.DownArrow) ? -1 : Input.GetKey(KeyCode.UpArrow) ? 1 : 0;
            }
            else
            {
                Debug.LogError("Invalid world name!");
                return;
            }
            

            inputDataToSend.ValueRW.horizontalInput = horizontalInput;
            inputDataToSend.ValueRW.verticalInput = verticalInput;
            
            RpcBroadcastPlayerInputToServer rpc = new RpcBroadcastPlayerInputToServer
            {
                PlayerInput = new Vector2(inputDataToSend.ValueRO.horizontalInput, inputDataToSend.ValueRO.verticalInput),
                CurrentTick = tickRateInfo.ValueRO.currentClientTickToSend,
            };
            
            rpc.HashForCurrentTick = tickRateInfo.ValueRO.hashForTheTick; // setting hash
            rpc.Serialize(connectionReference.ValueRO.Driver, connectionReference.ValueRO.Connection, connectionReference.ValueRO.SimulatorPipeline);
            EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, false);
        }
    }
}
