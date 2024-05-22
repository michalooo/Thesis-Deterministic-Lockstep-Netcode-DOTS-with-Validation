using DeterministicLockstep;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace PongGame
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
    [BurstCompile]
    public partial struct BallMovementSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PongBallSpawner>();
        }

        private EntityQuery ballsQuery;
        private NativeArray<LocalTransform> ballTransform;
        private NativeArray<Velocity> ballVelocities;
        private NativeArray<Entity> ballEntities;
        
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            
            ballsQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Velocity>().Build();
            ballTransform = ballsQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            ballVelocities = ballsQuery.ToComponentDataArray<Velocity>(Allocator.TempJob);
            ballEntities = ballsQuery.ToEntityArray(Allocator.TempJob);
            
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            
            var ballMovementJob = new BallMovementJob
            {
                ECB = ecb,
                ballVelocities = ballVelocities,
                localTransform = ballTransform,
                Entities = ballEntities,
                deltaTime = deltaTime
            };
            
            JobHandle ballMovementHandle = ballMovementJob.Schedule(ballTransform.Length,1);
            ballMovementHandle.Complete();
            
            ballTransform.Dispose();
            ballVelocities.Dispose();
            ballEntities.Dispose();
        }
    }
    
    [BurstCompile]
    public struct BallMovementJob : IJobParallelFor
    {
        public EntityCommandBuffer.ParallelWriter ECB;
        
        public NativeArray<Entity> Entities;
        public NativeArray<LocalTransform> localTransform;
        public NativeArray<Velocity> ballVelocities;
        
        public float deltaTime;
    
        public void Execute(int index)
        {
            LocalTransform transform = localTransform[index];
            Velocity velocity = ballVelocities[index];
            Entity entity = Entities[index];
            
            var newPosition = transform.Position + velocity.value * deltaTime;
            var newTransform = new LocalTransform
            {
                Position = newPosition,
                Rotation = transform.Rotation,
                Scale = transform.Scale
            };
    
            ECB.SetComponent(index , entity, newTransform);
        }
    }
}