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
            foreach (var (connectionReference, tickRateInfo, connectionEntity) in SystemAPI
                         .Query<RefRO<NetworkConnectionReference>, RefRW<TickRateInfo>>()
                         .WithAll<PlayerSpawned, GhostOwnerIsLocal>().WithEntityAccess())
            {
                var rpc = new RpcBroadcastPlayerInputToServer
                {
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