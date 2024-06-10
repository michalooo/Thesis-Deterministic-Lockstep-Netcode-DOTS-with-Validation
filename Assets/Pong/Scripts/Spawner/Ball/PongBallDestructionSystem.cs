using DeterministicLockstep;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine;

namespace PongGame
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
    public partial struct PongBallDestructionSystem : ISystem
    {
        private EntityQuery ballsQuery;
        private NativeArray<LocalTransform> ballTransform;
        private NativeArray<Entity> ballEntities;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PongBallSpawner>();
        }
        
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            ballsQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Velocity>().Build();
            ballTransform = ballsQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            ballEntities = ballsQuery.ToEntityArray(Allocator.TempJob);
            
            var leftPointsQueue = new NativeQueue<int>(Allocator.TempJob);
            var rightPointsQueue = new NativeQueue<int>(Allocator.TempJob);
            
            Camera cam = Camera.main;
            float targetXPosition = Screen.width;
            Vector3 worldPosition = cam.ScreenToWorldPoint(new Vector3(targetXPosition, 0, cam.nearClipPlane));
           
            var ballDestructionJob = new BallDestructionJob
            {
                ECB = ecb,
                localTransform = ballTransform,
                Entities = ballEntities,
                worldPosition = worldPosition,
                leftCounter = leftPointsQueue.AsParallelWriter(),
                rightCounter = rightPointsQueue.AsParallelWriter()
            };
            
            JobHandle ballDestructionHandle = ballDestructionJob.Schedule(ballTransform.Length,1);
            ballDestructionHandle.Complete();
            
            UISingleton.Instance.AddRightScore(rightPointsQueue.Count);
            UISingleton.Instance.AddLeftScore(leftPointsQueue.Count);
            
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

        public Vector3 worldPosition;
        public NativeQueue<int>.ParallelWriter leftCounter;
        public NativeQueue<int>.ParallelWriter rightCounter;
    
        public void Execute(int index)
        {
            LocalTransform transform = localTransform[index];
            Entity entity = Entities[index];

            if (transform.Position.x < -worldPosition.x)
            {
                leftCounter.Enqueue(1);
                ECB.DestroyEntity(index, entity);
            }
            else if (transform.Position.x > worldPosition.x)
            {
                rightCounter.Enqueue(1);
                ECB.DestroyEntity(index, entity);
            }
        }
    }
}