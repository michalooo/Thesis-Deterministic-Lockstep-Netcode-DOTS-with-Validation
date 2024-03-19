using Unity.Entities;
using UnityEngine;

[UpdateAfter(typeof(PlayerInputGatherAndSendSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class PlayerSendSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<PlayerInputDataToSend>();
    }

    protected override void OnUpdate()
    {
        var deltaTime = SystemAPI.Time.DeltaTime;
        
        foreach (var (tickRateInfo, connectionEntity) in SystemAPI.Query<RefRW<TickRateInfo>>().WithAll<GhostOwnerIsLocal, PlayerSpawned>().WithDisabled<PlayerInputDataToSend>().WithEntityAccess())
        {
            tickRateInfo.ValueRW.delayTime -= deltaTime;
            if (tickRateInfo.ValueRO.tickRate != 0 && tickRateInfo.ValueRO.delayTime <= 0)
            {
                tickRateInfo.ValueRW.delayTime = 1 / tickRateInfo.ValueRO.tickRate;
                EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, true);
            }
        }
    }
}