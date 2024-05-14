using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// System that gathers the player's input and sends it to the server
    /// </summary>
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup), OrderLast = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class PlayerInputSendSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PlayerInputDataToSend>();
        }

        protected override void OnUpdate()
        {
            foreach (var (connectionReference, tickRateInfo, owner, connectionEntity) in SystemAPI
                         .Query<RefRO<NetworkConnectionReference>, RefRW<TickRateInfo>, RefRO<GhostOwner>>()
                         .WithAll<PlayerSpawned, GhostOwnerIsLocal, PlayerInputDataToSend>().WithEntityAccess())
            {
                Debug.Log("Sending player input to server");
                if (!SystemAPI.TryGetSingleton<CapsulesInputs>(out var capsulesInputs))
                {
                    Debug.LogError("Inputs are not singleton");
                    return;
                }
                
                var rpc = new RpcBroadcastPlayerInputToServer
                {
                    CapsuleGameInputs = capsulesInputs,
                    PlayerNetworkID = owner.ValueRO.networkId,
                    CurrentTick = tickRateInfo.ValueRO.currentClientTickToSend,
                    HashForCurrentTick = tickRateInfo.ValueRO.hashForTheTick
                };

                rpc.Serialize(connectionReference.ValueRO.driver, connectionReference.ValueRO.connection,
                    connectionReference.ValueRO.reliableSimulatorPipeline);
                EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, false);
            }
        }
    }
}