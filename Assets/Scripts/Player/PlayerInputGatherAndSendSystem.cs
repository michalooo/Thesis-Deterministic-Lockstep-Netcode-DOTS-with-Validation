using Unity.Entities;
using UnityEngine;

/// <summary>
/// System that gathers the player's input and sends it to the server
/// </summary>
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

            if (World.Name == "ClientWorld2") // for local testing purposes
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
            
            var rpc = new RpcBroadcastPlayerInputToServer
            {
                PlayerInput = new Vector2(inputDataToSend.ValueRO.horizontalInput, inputDataToSend.ValueRO.verticalInput),
                CurrentTick = tickRateInfo.ValueRO.currentClientTickToSend,
                HashForCurrentTick = tickRateInfo.ValueRO.hashForTheTick 
            };

            rpc.Serialize(connectionReference.ValueRO.driver, connectionReference.ValueRO.connection, connectionReference.ValueRO.reliableSimulatorPipeline);
            EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, false);
        }
    }
}
