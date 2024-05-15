using DeterministicLockstep;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace PongGame
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GameStateUpdateSystemGroup))]
    [UpdateAfter(typeof(BallMovementSystem))]
    public partial class PongBallDestructionSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (transform, ballEntity) in SystemAPI.Query<LocalTransform>().WithEntityAccess())
            {
                if(transform.Position.x < -9f || transform.Position.x > 9f)
                    commandBuffer.DestroyEntity(ballEntity);
            }
            
            commandBuffer.Playback(EntityManager);
        }
    }
}