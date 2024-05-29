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
    public partial struct PongBallDestructionSystem : ISystem
    {
        private EntityQuery ballsQuery;
        private NativeArray<LocalTransform> ballTransform;
        private NativeArray<Entity> ballEntities;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PongBallSpawner>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            ballsQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Velocity>().Build();
            ballTransform = ballsQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            ballEntities = ballsQuery.ToEntityArray(Allocator.TempJob);
           
            var ballDestructionJob = new BallDestructionJob
            {
                ECB = ecb,
                localTransform = ballTransform,
                Entities = ballEntities,
            };
            
            JobHandle ballDestructionHandle = ballDestructionJob.Schedule(ballTransform.Length,1);
            ballDestructionHandle.Complete();
            
            ballTransform.Dispose();
            ballEntities.Dispose();
        }
    }
    
    [BurstCompile]
    public struct BallDestructionJob : IJobParallelFor
    {
        public EntityCommandBuffer.ParallelWriter ECB;
        
        public NativeArray<Entity> Entities;
        public NativeArray<LocalTransform> localTransform;
    
        public void Execute(int index)
        {
            LocalTransform transform = localTransform[index];
            Entity entity = Entities[index];
            
            if(transform.Position.x < -9f || transform.Position.x > 9f)
                ECB.DestroyEntity(index, entity);
        }
    }
}