using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(DeterministicSimulationSystemGroup), OrderLast = true)]
[UpdateBefore(typeof(PlayerInputGatherAndSendSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class CalculateTickSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<PlayerInputDataToSend>();
    }

    protected override void OnUpdate()
    {
        var deltaTime = SystemAPI.Time.DeltaTime;
        
        foreach (var (tickRateInfo, connectionEntity) in SystemAPI.Query<RefRW<TickRateInfo>>().WithAll<GhostOwnerIsLocal, PlayerSpawned>().WithEntityAccess())
        {
            tickRateInfo.ValueRW.delayTime -= deltaTime;
            if (tickRateInfo.ValueRO.delayTime <= 0)
            {
                tickRateInfo.ValueRW.delayTime = 1f / tickRateInfo.ValueRO.tickRate;
                EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, true);
            }
        }
    }
}